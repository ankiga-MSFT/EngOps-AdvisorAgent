using System.Text.Json;
using AdvisorAgent.Core.ContextResolution;
using AdvisorAgent.Core.Conversation;
using AdvisorAgent.Core.Models;
using AdvisorAgent.Core.Skills;
using AdvisorAgent.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Functions.Orchestration;

/// <summary>
/// Durable Functions activity wrappers for each pipeline step.
/// Each activity delegates to Core services — no business logic here.
/// </summary>
public sealed class AdvisorActivities
{
    private readonly IAgentOrchestrationService _orchestration;
    private readonly IAzureContextResolver _contextResolver;
    private readonly IConversationStore _conversationStore;
    private readonly Dictionary<string, object> _toolInstances;
    private readonly ILogger<AdvisorActivities> _logger;

    public AdvisorActivities(
        IAgentOrchestrationService orchestration,
        IAzureContextResolver contextResolver,
        IConversationStore conversationStore,
        Dictionary<string, object> toolInstances,
        ILogger<AdvisorActivities> logger)
    {
        _orchestration = orchestration;
        _contextResolver = contextResolver;
        _conversationStore = conversationStore;
        _toolInstances = toolInstances;
        _logger = logger;
    }

    // ── Conversation History Activities ──────────────────

    [Function(nameof(LoadConversationHistoryActivity))]
    public async Task<List<ConversationTurn>> LoadConversationHistoryActivity(
        [ActivityTrigger] LoadConversationHistoryInput input)
    {
        _logger.LogInformation("[Activity:LoadHistory] Loading history — UserId: {UserId}, SessionId: {SessionId}, Count: {Count}",
            input.UserId, input.SessionId, input.Count);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var turns = await _conversationStore.GetRecentTurnsAsync(input.UserId, input.SessionId, input.Count);
        sw.Stop();
        _logger.LogInformation("[Activity:LoadHistory] Loaded {TurnCount} turns in {ElapsedMs}ms", turns.Count, sw.ElapsedMilliseconds);
        return turns;
    }

    [Function(nameof(SaveConversationTurnActivity))]
    public async Task SaveConversationTurnActivity(
        [ActivityTrigger] SaveConversationTurnInput input)
    {
        _logger.LogInformation("[Activity:SaveTurn] Saving turn — UserId: {UserId}, SessionId: {SessionId}, RequestId: {RequestId}",
            input.UserId, input.SessionId, input.RequestId);
        var turn = new ConversationTurn
        {
            Prompt = input.Prompt,
            Response = input.Response,
            RequestId = input.RequestId,
            Timestamp = DateTimeOffset.UtcNow
        };
        await _conversationStore.AppendTurnAsync(input.UserId, input.SessionId, turn);
        _logger.LogInformation("[Activity:SaveTurn] Saved successfully");
    }

    // ── Pipeline Activities ────────────────────────────────

    [Function(nameof(ResolveContextActivity))]
    public async Task<AzureContext> ResolveContextActivity(
        [ActivityTrigger] ResolveContextInput input)
    {
        _logger.LogInformation("[Activity:ResolveContext] Starting — Prompt: {Prompt}, HistoryTurns: {Turns}",
            Truncate(input.Prompt), input.ConversationHistory?.Count ?? 0);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var history = MapHistory(input.ConversationHistory);
        var result = await _contextResolver.ResolveAsync(input.Prompt, null, history);
        sw.Stop();
        _logger.LogInformation("[Activity:ResolveContext] Completed in {ElapsedMs}ms — Context: {Context}",
            sw.ElapsedMilliseconds, result.ToContextSummary());
        return result;
    }

    [Function(nameof(ClassifyIntentActivity))]
    public async Task<UserIntent> ClassifyIntentActivity(
        [ActivityTrigger] ClassifyIntentInput input)
    {
        _logger.LogInformation("[Activity:ClassifyIntent] Starting — Prompt: {Prompt}, AzureContext: {Context}, HistoryTurns: {Turns}",
            Truncate(input.Prompt), Truncate(input.AzureContextSummary), input.ConversationHistory?.Count ?? 0);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var history = MapHistory(input.ConversationHistory);
        var result = await _orchestration.ClassifyIntentAsync(input.Prompt, input.AzureContextSummary, history);
        sw.Stop();
        _logger.LogInformation("[Activity:ClassifyIntent] Completed in {ElapsedMs}ms — Intent: {Intent}, Reasoning: {Reasoning}",
            sw.ElapsedMilliseconds, result.Intent, result.Reasoning);
        return result;
    }

    [Function(nameof(AnswerDirectlyActivity))]
    public async Task<string> AnswerDirectlyActivity(
        [ActivityTrigger] ClassifyIntentInput input)
    {
        _logger.LogInformation("[Activity:AnswerDirectly] Starting — Prompt: {Prompt}, HistoryTurns: {Turns}",
            Truncate(input.Prompt), input.ConversationHistory?.Count ?? 0);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var history = MapHistory(input.ConversationHistory);
        var result = await _orchestration.AnswerDirectlyAsync(input.Prompt, input.AzureContextSummary, history);
        sw.Stop();
        _logger.LogInformation("[Activity:AnswerDirectly] Completed in {ElapsedMs}ms — ResponseLength: {Length} chars",
            sw.ElapsedMilliseconds, result.Length);
        return result;
    }

    [Function(nameof(DecomposeTasksActivity))]
    public async Task<List<TaskPlanItem>> DecomposeTasksActivity(
        [ActivityTrigger] DecomposeTasksInput input)
    {
        _logger.LogInformation("[Activity:DecomposeTasks] Starting — Prompt: {Prompt}, HistoryTurns: {Turns}",
            Truncate(input.Prompt), input.ConversationHistory?.Count ?? 0);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var history = MapHistory(input.ConversationHistory);
        var result = await _orchestration.DecomposeTasksAsync(input.Prompt, input.AzureContextSummary, history);
        sw.Stop();
        _logger.LogInformation("[Activity:DecomposeTasks] Completed in {ElapsedMs}ms — TaskCount: {Count}", sw.ElapsedMilliseconds, result.Count);
        for (int i = 0; i < result.Count; i++)
        {
            var t = result[i];
            _logger.LogInformation("[Activity:DecomposeTasks]   Task[{Index}]: \"{Task}\" → Skill: {Skill}, DependsOn: [{Deps}]",
                i, t.Task, t.SkillName, string.Join(", ", t.DependsOn));
        }
        return result;
    }

    [Function(nameof(GetSkillDefinitionsActivity))]
    public Task<List<AgentSkillDefinition>> GetSkillDefinitionsActivity(
        [ActivityTrigger] List<string> skillNames)
    {
        _logger.LogInformation("[Activity:GetSkillDefinitions] Requested skills: [{Skills}]", string.Join(", ", skillNames));
        var filtered = _orchestration.GetSkillDefinitions(skillNames);
        _logger.LogInformation("[Activity:GetSkillDefinitions] Resolved {Count}/{Total} skills: [{Resolved}]",
            filtered.Count, skillNames.Count, string.Join(", ", filtered.Select(s => s.SkillName)));
        return Task.FromResult(filtered);
    }

    [Function(nameof(GenerateSkillPromptActivity))]
    public async Task<string> GenerateSkillPromptActivity(
        [ActivityTrigger] GenerateSkillPromptInput input)
    {
        _logger.LogInformation("[Activity:GenerateSkillPrompt] Starting — Task: \"{Task}\", Skill: {Skill}, HasUpstream: {HasUpstream}, HistoryTurns: {Turns}",
            input.TaskLabel, input.SkillDescription, !string.IsNullOrWhiteSpace(input.UpstreamOutputs), input.ConversationHistory?.Count ?? 0);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var history = MapHistory(input.ConversationHistory);
        var result = await _orchestration.GenerateSkillPromptAsync(
            input.TaskLabel, input.SkillDescription, input.ExpectedInput,
            input.AzureContextSummary, input.UpstreamOutputs, input.OriginalPrompt, history);
        sw.Stop();
        _logger.LogInformation("[Activity:GenerateSkillPrompt] Completed in {ElapsedMs}ms — GeneratedPrompt: {Prompt}",
            sw.ElapsedMilliseconds, Truncate(result, 300));
        return result;
    }

    [Function(nameof(ExecuteSkillActivity))]
    public async Task<SkillExecutionResult> ExecuteSkillActivity(
        [ActivityTrigger] ExecuteSkillInput input)
    {
        _logger.LogInformation("[Activity:ExecuteSkill] Starting — Skill: {SkillName}, Prompt: {Prompt}",
            input.SkillName, Truncate(input.Prompt, 300));
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var skillDefs = _orchestration.GetSkillDefinitions([input.SkillName]);
            var skillDef = skillDefs.FirstOrDefault();

            if (skillDef == null)
            {
                _logger.LogWarning("[Activity:ExecuteSkill] Unknown skill: {SkillName}", input.SkillName);
                return new SkillExecutionResult
                {
                    IsSuccess = false,
                    Response = $"Unknown skill: {input.SkillName}"
                };
            }

            _logger.LogInformation("[Activity:ExecuteSkill] Skill {SkillName} — Tools: [{Tools}], Model: {Model}, Timeout: {Timeout}s",
                input.SkillName,
                string.Join(", ", skillDef.Tools.Select(t => t.Name)),
                skillDef.ModelName,
                skillDef.Timeout);

            var response = await _orchestration.ExecuteSkillAsync(skillDef, input.Prompt, input.AccessToken);
            sw.Stop();

            _logger.LogInformation("[Activity:ExecuteSkill] Completed — Skill: {SkillName}, Success: {Success}, Duration: {ElapsedMs}ms, ResponseLength: {Length} chars",
                input.SkillName, response.IsSuccess, sw.ElapsedMilliseconds, response.Response.Length);

            return new SkillExecutionResult
            {
                IsSuccess = response.IsSuccess,
                Response = response.Response
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[Activity:ExecuteSkill] FAILED — Skill: {SkillName}, Duration: {ElapsedMs}ms, Error: {Error}",
                input.SkillName, sw.ElapsedMilliseconds, ex.Message);
            return new SkillExecutionResult
            {
                IsSuccess = false,
                Response = $"Skill execution failed: {ex.Message}"
            };
        }
    }

    // ── Subscription Discovery Activity ────────────────────

    [Function(nameof(FetchSubscriptionsActivity))]
    public async Task<List<SubscriptionSummary>> FetchSubscriptionsActivity(
        [ActivityTrigger] FetchSubscriptionsInput input)
    {
        _logger.LogInformation("[Activity:FetchSubscriptions] Fetching accessible subscriptions...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (!_toolInstances.TryGetValue("SubscriptionTools", out var toolObj))
        {
            _logger.LogError("[Activity:FetchSubscriptions] SubscriptionTools not registered");
            return [];
        }

        // Set access token via reflection (same pattern as ExecuteSkillAsync)
        var setTokenMethod = toolObj.GetType().GetMethod("SetAccessToken");
        setTokenMethod?.Invoke(toolObj, [input.AccessToken]);

        var listMethod = toolObj.GetType().GetMethod("ListSubscriptions");
        if (listMethod is null)
        {
            _logger.LogError("[Activity:FetchSubscriptions] ListSubscriptions method not found");
            return [];
        }

        var rawJson = await (Task<string>)listMethod.Invoke(toolObj, [])!;
        sw.Stop();

        // Parse the ARM response to extract compact subscription summaries
        var results = new List<SubscriptionSummary>();
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("value", out var valueArr))
            {
                foreach (var sub in valueArr.EnumerateArray())
                {
                    var subId = sub.GetProperty("subscriptionId").GetString() ?? "";
                    var name = sub.GetProperty("displayName").GetString() ?? subId;
                    var state = sub.GetProperty("state").GetString() ?? "Unknown";
                    if (state.Equals("Enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new SubscriptionSummary { SubscriptionId = subId, DisplayName = name });
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[Activity:FetchSubscriptions] Failed to parse subscriptions response");
        }

        _logger.LogInformation("[Activity:FetchSubscriptions] Found {Count} enabled subscriptions in {ElapsedMs}ms",
            results.Count, sw.ElapsedMilliseconds);
        return results;
    }

    // ── Status publishing activities (log-only, real-time via SetCustomStatus) ──

    [Function(nameof(PublishStatusActivity))]
    public void PublishStatusActivity(
        [ActivityTrigger] PublishStatusInput input)
    {
        _logger.LogInformation("[Pipeline] Step: {Step} | State: {State} | {Message}", input.StepName, input.StepState, input.Message ?? "—");
    }

    [Function(nameof(PublishCompletedActivity))]
    public void PublishCompletedActivity(
        [ActivityTrigger] PublishCompletedInput input)
    {
        _logger.LogInformation("[Pipeline] COMPLETED — Session: {SessionId}, Success: {IsSuccess}, ResponseLength: {Length} chars",
            input.SessionId, input.Response.IsSuccess, input.Response.Response?.Length ?? 0);
    }

    private static string Truncate(string? value, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(value)) return "(empty)";
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }

    /// <summary>
    /// Maps ConversationTurnDto (serializable DTO) to ConversationTurn (domain model).
    /// </summary>
    private static List<ConversationTurn>? MapHistory(List<ConversationTurnDto>? dtos)
    {
        if (dtos is null or { Count: 0 }) return null;
        return dtos.Select(d => new ConversationTurn
        {
            Prompt = d.Prompt,
            Response = d.Response
        }).ToList();
    }
}
