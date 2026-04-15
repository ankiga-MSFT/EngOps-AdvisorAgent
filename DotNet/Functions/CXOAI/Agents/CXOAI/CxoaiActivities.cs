using Azure.AI.OpenAI;
using Azure.Identity;
using CXOAI.SkillFramework;
using CXOAI.Functions.Models;
using CXOAI.StatusNotifier;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CXOAI.Functions.Agents.CXOAI;

/// <summary>
/// All Durable Functions activities. Each [Function] method is a thin wrapper
/// that delegates to <see cref="IOrchestratorStepService"/> for the actual business logic.
/// This ensures the console app and function app share the same implementation.
/// </summary>
public class CxoaiActivities
{
    private readonly IOrchestratorStepService _stepService;
    private readonly IUserAuthContext _authContext;
    private readonly ServiceHubContext? _hubContext;
    private readonly ILogger<CxoaiActivities> _logger;

    public CxoaiActivities(
        IOrchestratorStepService stepService,
        IUserAuthContext authContext,
        ILogger<CxoaiActivities> logger,
        ServiceHubContext? hubContext = null)
    {
        _stepService = stepService;
        _authContext = authContext;
        _logger = logger;
        _hubContext = hubContext;
    }

    // ── SignalR status publishing ─────────────────────────────────────

    [Function(nameof(PublishStatusActivity))]
    public async Task PublishStatusActivity(
        [ActivityTrigger] PublishStatusInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["StepName"] = "PublishStatus" }))
        {
            if (_hubContext is null)
            {
                _logger.LogDebug("SignalR not configured, skipping status publish");
                return;
            }

            try
            {
                await _hubContext.Clients.Group(input.SessionId)
                    .SendAsync("ReceiveStatus", input.Status);

                _logger.LogInformation("Published status for session '{SessionId}', step '{Step}'",
                    input.SessionId, input.Status.CurrentStep);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish status for session '{SessionId}'", input.SessionId);
            }
        }
    }

    [Function(nameof(NotifyUserInputNeededActivity))]
    public async Task<bool> NotifyUserInputNeededActivity(
        [ActivityTrigger] UserInputNotification input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["StepName"] = "NotifyUserInput", ["SkillName"] = input.SkillName }))
        {
            if (_hubContext is null)
            {
                _logger.LogWarning("SignalR not configured — cannot notify client for user input");
                return false;
            }

            try
            {
                await _hubContext.Clients.Group(input.SessionId)
                    .SendAsync("ReceiveUserInputRequest", input.SkillName, input.TaskId, input.Prompt, input.SessionId, input.InstanceId, input.SkillResult);

                _logger.LogInformation("Notified client for user input — instance '{InstanceId}'", input.InstanceId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify client for user input");
                return false;
            }
        }
    }

    // ── Publish completed result via SignalR ─────────────────────────

    [Function(nameof(PublishCompletedActivity))]
    public async Task PublishCompletedActivity(
        [ActivityTrigger] PublishCompletedInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["StepName"] = "PublishCompleted" }))
        {
            if (_hubContext is null)
            {
                _logger.LogDebug("SignalR not configured, skipping completed publish");
                return;
            }

            try
            {
                await _hubContext.Clients.Group(input.SessionId)
                    .SendAsync("ReceiveCompleted", new
                    {
                        sessionId = input.SessionId,
                        result = input.Result
                    });

                _logger.LogInformation("Published completed result (success={IsSuccess})", input.Result.IsSuccess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish completed result");
            }
        }
    }

    // ── Step 1: Enhance Prompt ───────────────────────────────────────

    [Function(nameof(EnhancePromptActivity))]
    public async Task<EnhancePromptResult> EnhancePromptActivity(
        [ActivityTrigger] OrchestratorInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["StepName"] = "EnhancePrompt" }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation($"EnhancePromptActivity started for userId={input.UserId}");
                var result = await _stepService.EnhancePromptAsync(input.UserId, input.SessionId, input.Prompt, input.UserContext);
                _logger.LogInformation($"EnhancePromptActivity completed in {sw.ElapsedMilliseconds}ms");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"EnhancePromptActivity failed after {sw.ElapsedMilliseconds}ms");
                throw;
            }
        }
    }

    // ── Step 1a: Check History ────────────────────────────────────────

    [Function(nameof(CheckHistoryActivity))]
    public async Task<HistoryAnswerResult> CheckHistoryActivity(
        [ActivityTrigger] CheckHistoryInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["StepName"] = "CheckHistory" }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("CheckHistoryActivity started");
                var sessionSummary = await _stepService.GetSessionSummaryAsync(input.UserId, input.SessionId);
                if (string.IsNullOrWhiteSpace(sessionSummary))
                {
                    _logger.LogInformation("No session summary found, skipping history check ({ElapsedMs}ms)", sw.ElapsedMilliseconds);
                    return new HistoryAnswerResult { CanAnswer = false, HasRelevantContext = false };
                }
                var result = await _stepService.TryAnswerFromHistoryAsync(input.Prompt, sessionSummary, input.UIContextEntityName);
                _logger.LogInformation("CheckHistoryActivity completed in {ElapsedMs}ms, canAnswer={CanAnswer}, hasContext={HasContext}",
                    sw.ElapsedMilliseconds, result.CanAnswer, result.HasRelevantContext);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckHistoryActivity failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
                throw;
            }
        }
    }

    // ── Step 2: Classify Intent ──────────────────────────────────────

    [Function(nameof(ClassifyIntentActivity))]
    public async Task<UserIntent> ClassifyIntentActivity(
        [ActivityTrigger] ClassifyIntentInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["StepName"] = "ClassifyIntent" }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("ClassifyIntentActivity started");
                var intent = await _stepService.ClassifyIntentAsync(input.Prompt, input.GeneralKnowledge, input.UIContextEntityName);
                _logger.LogInformation("ClassifyIntentActivity completed in {ElapsedMs}ms, intent={Intent}", sw.ElapsedMilliseconds, intent.Intent);
                return intent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClassifyIntentActivity failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
                throw;
            }
        }
    }

    // ── Step 2a: Answer From Knowledge ───────────────────────────────

    [Function(nameof(AnswerFromKnowledgeActivity))]
    public async Task<string> AnswerFromKnowledgeActivity(
        [ActivityTrigger] KnowledgeAnswerInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["StepName"] = "AnswerFromKnowledge" }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("AnswerFromKnowledgeActivity started");
                var answer = await _stepService.AnswerFromKnowledgeAsync(input.Prompt, input.GeneralKnowledge);
                _logger.LogInformation("AnswerFromKnowledgeActivity completed in {ElapsedMs}ms, answerLength={Length}", sw.ElapsedMilliseconds, answer.Length);
                return answer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AnswerFromKnowledgeActivity failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
                throw;
            }
        }
    }

    // ── Step 2b: Decompose Tasks (Task Planner) ────────────────────

    [Function(nameof(DecomposeTasksActivity))]
    public async Task<List<TaskPlanItem>> DecomposeTasksActivity(
        [ActivityTrigger] DecomposeTasksInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["StepName"] = "DecomposeTasks" }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("DecomposeTasksActivity started");
                var plan = await _stepService.DecomposeTasksAsync(input.EnhancedPrompt, input.OriginalPrompt);
                _logger.LogInformation("DecomposeTasksActivity completed in {ElapsedMs}ms, taskCount={Count}", sw.ElapsedMilliseconds, plan.Count);
                return plan;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DecomposeTasksActivity failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
                throw;
            }
        }
    }

    // ── Step 2c: Resolve skills by name ──────────────────────────────

    [Function(nameof(GetSkillsByNameActivity))]
    public async Task<List<AgentSkill>> GetSkillsByNameActivity(
        [ActivityTrigger] SkillsByNameInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["StepName"] = "GetSkillsByName" }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("GetSkillsByNameActivity started, loading {Count} skill config(s)", input.SkillNames.Count);
                var skills = await _stepService.GetSkillsByNameAsync(input.SkillNames);
                _logger.LogInformation("GetSkillsByNameActivity completed in {ElapsedMs}ms, resolved {Count} skill(s)", sw.ElapsedMilliseconds, skills.Count);
                return skills;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSkillsByNameActivity failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
                throw;
            }
        }
    }

    // ── Step 6a: Generate skill prompt at execution time ─────────────

    [Function(nameof(GenerateSkillPromptActivity))]
    public async Task<string> GenerateSkillPromptActivity(
        [ActivityTrigger] GenerateSkillPromptInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["StepName"] = "GenerateSkillPrompt", ["SkillName"] = input.Task.SkillName }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("GenerateSkillPromptActivity started for skill '{Skill}', task '{Task}'",
                    input.Task.SkillName, input.Task.Task);
                var prompt = await _stepService.GenerateSkillPromptAsync(
                    input.Task, input.SkillDescription, input.ExpectedSkillInput,
                    input.DomainKnowledge, input.UIContext, input.UpstreamOutputs,
                    input.OriginalUserPrompt);
                _logger.LogInformation("GenerateSkillPromptActivity completed in {ElapsedMs}ms, promptLength={Length}",
                    sw.ElapsedMilliseconds, prompt.Length);
                return prompt;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateSkillPromptActivity failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
                throw;
            }
        }
    }

    // ── Step 7: Execute Single Skill ─────────────────────────────────

    [Function(nameof(ExecuteSkillActivity))]
    public async Task<SkillExecutionResult> ExecuteSkillActivity(
        [ActivityTrigger] ExecuteSkillInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["StepName"] = "ExecuteSkill", ["SkillName"] = input.SkillName }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("ExecuteSkillActivity started for skill '{Skill}'", input.SkillName);

                // Populate the scoped auth context so all tools in this invocation can access the token
                _authContext.AccessToken = input.AccessToken;

                // Set session context on tools so they can send SignalR notifications
                _stepService.SetToolSession(input.SessionId);

                // Inject continuation payload from previous round so tools can detect resume mode
                _stepService.SetToolContinuationPayload(input.PayloadJson);

                var resolvedTools = _stepService.ResolveTools(input.SkillInfo);
                var result = await _stepService.ExecuteSkillAsync(input.SkillInfo, input.Prompt, resolvedTools);

                _logger.LogInformation("ExecuteSkillActivity completed in {ElapsedMs}ms, skill='{Skill}', success={IsSuccess}, needsInput={NeedsInput}, responseLength={Length}",
                    sw.ElapsedMilliseconds, input.SkillName, result.IsSuccess, result.NeedsInputForUser, result.Response.Length);

                return new SkillExecutionResult
                {
                    IsSuccess = result.IsSuccess,
                    NeedsUserInput = result.NeedsInputForUser,
                    Response = result.Response,
                    IsReport = result.IsReport,
                    UserPrompt = result.NeedsInputForUser ? result.Response : null,
                    IsUIComponent = result.IsUIComponent,
                    UIComponent = result.UIComponent,
                    PayloadJson = result.Payload?.ToString(Newtonsoft.Json.Formatting.None)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExecuteSkillActivity failed after {ElapsedMs}ms for skill '{Skill}'", sw.ElapsedMilliseconds, input.SkillName);
                throw;
            }
        }
    }

    // ── Step 8: Summarize ────────────────────────────────────────────

    [Function(nameof(SummarizeActivity))]
    public async Task SummarizeActivity(
        [ActivityTrigger] SummarizeInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["RequestId"] = input.RequestId ?? "N/A", ["StepName"] = "Summarize" }))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("SummarizeActivity started, contentLength={Length}", input.ConversationContent.Length);
                await _stepService.SummarizeAndStoreAsync(input.UserId, input.SessionId, input.ConversationContent, input.FreshSkillOutputs, input.RequestId);
                _logger.LogInformation("SummarizeActivity completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SummarizeActivity failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
                throw;
            }
        }
    }

    [Function(nameof(GetSessionSummaryActivity))]
    public async Task<string?> GetSessionSummaryActivity(
        [ActivityTrigger] GetSessionSummaryInput input)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = input.SessionId, ["StepName"] = "GetSessionSummary" }))
        {
            _logger.LogInformation("GetSessionSummaryActivity started for userId={UserId}", input.UserId);
            return await _stepService.GetSessionSummaryAsync(input.UserId, input.SessionId);
        }
    }
}
