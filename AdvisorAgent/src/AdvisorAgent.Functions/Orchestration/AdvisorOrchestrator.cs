using System.Text;
using AdvisorAgent.Core.Models;
using AdvisorAgent.Core.Skills;
using AdvisorAgent.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Functions.Orchestration;

/// <summary>
/// Durable Functions orchestrator for the Advisor Agent pipeline.
/// Pipeline: ResolveContext → ClassifyIntent → (Direct answer OR Decompose → Execute skills) → Return.
/// </summary>
public static class AdvisorOrchestrator
{
    [Function(nameof(AdvisorOrchestratorMain))]
    public static async Task<AdvisorAgentResponse> AdvisorOrchestratorMain(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<AdvisorOrchestratorInput>()
            ?? throw new ArgumentNullException("input", "Orchestrator input is required");

        var logger = context.CreateReplaySafeLogger(nameof(AdvisorOrchestratorMain));

        input.RequestId ??= context.NewGuid().ToString("N");

        try
        {
            logger.LogInformation("╔══════════════════════════════════════════════════════════════╗");
            logger.LogInformation("║  ADVISOR ORCHESTRATION STARTED                              ║");
            logger.LogInformation("║  InstanceId: {InstanceId}", context.InstanceId);
            logger.LogInformation("║  SessionId:  {SessionId}", input.SessionId);
            logger.LogInformation("║  Prompt:     {Prompt}", input.Prompt?.Length > 100 ? input.Prompt[..100] + "…" : input.Prompt);
            logger.LogInformation("╚══════════════════════════════════════════════════════════════╝");

            // Step 0: Load conversation history for multi-turn context
            logger.LogInformation("━━━ Step 0: LoadConversationHistory ━━━");
            await PublishStatus(context, input, "LoadConversationHistory", "Running", "Loading conversation context...");

            var conversationTurns = await context.CallActivityAsync<List<ConversationTurn>>(
                nameof(AdvisorActivities.LoadConversationHistoryActivity),
                new LoadConversationHistoryInput
                {
                    UserId = input.UserId,
                    SessionId = input.SessionId,
                    Count = 5
                });

            // Map domain turns to serializable DTOs for passing to activities
            var historyDtos = conversationTurns.Select(t => new ConversationTurnDto
            {
                Prompt = t.Prompt,
                Response = t.Response
            }).ToList();

            await PublishStatus(context, input, "LoadConversationHistory", "Completed",
                conversationTurns.Count > 0 ? $"Loaded {conversationTurns.Count} previous turns." : "First message in session.");
            logger.LogInformation("━━━ Step 0 Done: {Count} conversation turns loaded ━━━", conversationTurns.Count);

            // Step 1: Resolve Azure Context
            logger.LogInformation("━━━ Step 1/5: ResolveAzureContext ━━━");
            await PublishStatus(context, input, "ResolveAzureContext", "Running", "Extracting Azure resource scope from prompt...");

            var azureContext = await context.CallActivityAsync<AzureContext>(
                nameof(AdvisorActivities.ResolveContextActivity),
                new ResolveContextInput { Prompt = input.Prompt, ConversationHistory = historyDtos });

            var contextSummary = azureContext.ToContextSummary();

            await PublishStatus(context, input, "ResolveAzureContext", "Completed", contextSummary);
            logger.LogInformation("━━━ Step 1 Done: Context = {Context} ━━━", contextSummary);

            // Step 1.5: Subscription gate — if no subscriptions in context, ask user to select
            var resolvedSubs = azureContext.GetAllSubscriptionIds();
            if (resolvedSubs.Count == 0)
            {
                logger.LogInformation("━━━ Step 1.5: SubscriptionGate — No subscriptions in context ━━━");
                await PublishStatus(context, input, "SubscriptionDiscovery", "Running", "No subscription scope found — fetching your subscriptions...");

                var subscriptions = await context.CallActivityAsync<List<SubscriptionSummary>>(
                    nameof(AdvisorActivities.FetchSubscriptionsActivity),
                    new FetchSubscriptionsInput { AccessToken = input.AccessToken });

                if (subscriptions.Count == 0)
                {
                    var noSubsResponse = "I couldn't find any Azure subscriptions you have access to. " +
                        "Please ensure your account has active subscriptions and the access token is valid.";
                    await SaveTurn(context, input, noSubsResponse);
                    var response = AdvisorAgentResponse.Success(noSubsResponse);
                    response.NeedsUserInput = true;
                    await PublishCompleted(context, input, response);
                    return response;
                }

                // Return structured subscription list for UI to render a rich picker card
                var selectionResponse = $"I found {subscriptions.Count} Azure subscription(s) you have access to. Please select the ones you'd like me to analyze (max 10).";
                await SaveTurn(context, input, selectionResponse);

                await PublishStatus(context, input, "SubscriptionDiscovery", "Completed",
                    $"Found {subscriptions.Count} subscriptions — waiting for user selection.");

                var gateResponse = AdvisorAgentResponse.Success(selectionResponse);
                gateResponse.NeedsUserInput = true;
                gateResponse.UiAction = "subscriptionPicker";
                gateResponse.UiData = subscriptions;
                await PublishCompleted(context, input, gateResponse);
                return gateResponse;
            }

            if (resolvedSubs.Count > 10)
            {
                logger.LogWarning("━━━ Step 1.5: SubscriptionGate — {Count} subscriptions in context, capping to first 10 ━━━", resolvedSubs.Count);
                var capped = resolvedSubs.Take(10).ToList();
                azureContext.SubscriptionIds = capped;
                azureContext.SubscriptionId = capped[0];
                contextSummary = azureContext.ToContextSummary();
                logger.LogInformation("━━━ Step 1.5 Done: Capped to 10 subscriptions ━━━");
            }

            // Step 2: Classify Intent
            logger.LogInformation("━━━ Step 2/5: ClassifyIntent ━━━");
            await PublishStatus(context, input, "ClassifyIntent", "Running", "Analyzing user intent...");

            var intent = await context.CallActivityAsync<UserIntent>(
                nameof(AdvisorActivities.ClassifyIntentActivity),
                new ClassifyIntentInput { Prompt = input.Prompt, AzureContextSummary = contextSummary, ConversationHistory = historyDtos });

            await PublishStatus(context, input, "ClassifyIntent", "Completed", $"Intent: {intent.Intent} — {intent.Reasoning}");
            logger.LogInformation("━━━ Step 2 Done: Intent = {Intent}, Reasoning = {Reasoning} ━━━", intent.Intent, intent.Reasoning);

            // Step 3: Handle based on intent
            if (intent.Intent == UserIntentType.Informational)
            {
                logger.LogInformation("━━━ Step 3/5: AnswerDirectly (Informational) ━━━");
                await PublishStatus(context, input, "AnswerDirectly", "Running", "Generating direct answer...");

                var answer = await context.CallActivityAsync<string>(
                    nameof(AdvisorActivities.AnswerDirectlyActivity),
                    new ClassifyIntentInput { Prompt = input.Prompt, AzureContextSummary = contextSummary, ConversationHistory = historyDtos });

                var response = AdvisorAgentResponse.Success(answer);

                await PublishStatus(context, input, "AnswerDirectly", "Completed");
                logger.LogInformation("━━━ Informational path complete — ResponseLength: {Length} chars ━━━", answer.Length);

                // Save conversation turn
                await SaveTurn(context, input, answer);

                await PublishCompleted(context, input, response);
                return response;
            }

            if (intent.Intent == UserIntentType.Unknown)
            {
                logger.LogWarning("Intent classified as Unknown — aborting pipeline");
                var unknownResponse = "I couldn't understand your request. Please try rephrasing — for example: " +
                    "'Help me find retiring resources' or 'How can I optimize costs for my workload?'";
                var response = AdvisorAgentResponse.Failure(unknownResponse);

                // Save even failed turns so the agent has context on next attempt
                await SaveTurn(context, input, unknownResponse);

                await PublishCompleted(context, input, response);
                return response;
            }

            // Step 4: Decompose into task plan
            logger.LogInformation("━━━ Step 4/5: DecomposeTasks ━━━");
            await PublishStatus(context, input, "DecomposeTasks", "Running", "Breaking down request into tasks...");

            var taskPlan = await context.CallActivityAsync<List<TaskPlanItem>>(
                nameof(AdvisorActivities.DecomposeTasksActivity),
                new DecomposeTasksInput { Prompt = input.Prompt, AzureContextSummary = contextSummary, ConversationHistory = historyDtos });

            if (taskPlan.Count == 0)
            {
                logger.LogWarning("Task decomposition returned 0 tasks — aborting pipeline");
                var response = AdvisorAgentResponse.Failure("Unable to decompose your request into actionable tasks. Please try a more specific question.");
                await PublishCompleted(context, input, response);
                return response;
            }

            // Load skill definitions and validate plan
            var skillNames = taskPlan.Select(t => t.SkillName).Distinct().ToList();
            var skillDefs = await context.CallActivityAsync<List<AgentSkillDefinition>>(
                nameof(AdvisorActivities.GetSkillDefinitionsActivity), skillNames);

            var knownSkills = new HashSet<string>(skillDefs.Select(s => s.SkillName));
            taskPlan = PlanValidator.RemoveUnknownSkills(taskPlan, knownSkills);

            List<int> executionOrder;
            try
            {
                executionOrder = PlanValidator.TopologicalSort(taskPlan);
            }
            catch (InvalidOperationException)
            {
                logger.LogWarning("Task plan has a cycle — falling back to sequential execution");
                executionOrder = Enumerable.Range(0, taskPlan.Count).ToList();
            }

            await PublishStatus(context, input, "DecomposeTasks", "Completed", $"{taskPlan.Count} tasks planned across {knownSkills.Count} skills.");
            logger.LogInformation("━━━ Step 4 Done: {Count} tasks, Execution order: [{Order}] ━━━",
                taskPlan.Count, string.Join(" → ", executionOrder));
            for (int i = 0; i < taskPlan.Count; i++)
            {
                var t = taskPlan[i];
                logger.LogInformation("  Task[{Index}]: \"{Task}\" → Skill: {Skill}, DependsOn: [{Deps}]",
                    i, t.Task, t.SkillName, string.Join(", ", t.DependsOn));
            }

            // Step 5: Execute skills (inlined for granular progress reporting)
            logger.LogInformation("━━━ Step 5/5: SkillExecution ━━━");

            // Build skill definition lookup for tool-level progress messages
            var skillDefMap = skillDefs.ToDictionary(s => s.SkillName);

            var outputs = new Dictionary<int, string>();
            var allResponses = new StringBuilder();

            foreach (int taskIndex in executionOrder)
            {
                var task = taskPlan[taskIndex];
                var skillStepName = $"ExecuteSkill:{task.SkillName}";

                // Publish: preparing skill prompt
                var toolNames = skillDefMap.TryGetValue(task.SkillName, out var def)
                    ? string.Join(", ", def.Tools.Select(t => t.Name.Split('-', 2).Last()))
                    : "";
                var toolHint = string.IsNullOrEmpty(toolNames) ? "" : $" (tools: {toolNames})";
                await PublishStatus(context, input, skillStepName, "Running",
                    $"Preparing {task.SkillName} — {task.Task}...{toolHint}");
                logger.LogInformation("━━━ Step 5: Task[{Index}] \"{Task}\" → {Skill} ━━━", taskIndex, task.Task, task.SkillName);

                // Collect upstream outputs from dependencies
                var upstreamOutputs = new StringBuilder();
                foreach (int dep in task.DependsOn)
                {
                    if (outputs.TryGetValue(dep, out var depOutput))
                    {
                        upstreamOutputs.AppendLine($"[{taskPlan[dep].Task}]: {depOutput}");
                    }
                }

                // Generate skill-specific prompt
                var skillPrompt = await context.CallActivityAsync<string>(
                    nameof(AdvisorActivities.GenerateSkillPromptActivity),
                    new GenerateSkillPromptInput
                    {
                        TaskLabel = task.Task,
                        SkillDescription = task.SkillName,
                        ExpectedInput = string.Empty,
                        AzureContextSummary = contextSummary,
                        UpstreamOutputs = upstreamOutputs.ToString(),
                        OriginalPrompt = input.Prompt,
                        ConversationHistory = historyDtos
                    });

                // Publish: executing skill (now invoking ARM APIs)
                await PublishStatus(context, input, skillStepName, "Running",
                    $"Executing {task.SkillName} — invoking Azure APIs...{toolHint}");

                // Execute skill
                var result = await context.CallActivityAsync<SkillExecutionResult>(
                    nameof(AdvisorActivities.ExecuteSkillActivity),
                    new ExecuteSkillInput
                    {
                        SkillName = task.SkillName,
                        Prompt = skillPrompt,
                        SessionId = input.SessionId,
                        AccessToken = input.AccessToken
                    });

                outputs[taskIndex] = result.Response;

                if (allResponses.Length > 0) allResponses.AppendLine("\n---\n");
                allResponses.AppendLine(result.Response);

                // Publish: skill completed
                await PublishStatus(context, input, skillStepName, "Completed",
                    $"{task.SkillName} completed.");
                logger.LogInformation("━━━ Step 5: Task[{Index}] ({Skill}): {Status}, {Length} chars ━━━",
                    taskIndex, task.SkillName, result.IsSuccess ? "SUCCESS" : "FAILED", result.Response.Length);
            }

            var finalResponse = AdvisorAgentResponse.Success(allResponses.ToString());

            logger.LogInformation("╔══════════════════════════════════════════════════════════════╗");
            logger.LogInformation("║  ADVISOR ORCHESTRATION COMPLETED                            ║");
            logger.LogInformation("║  InstanceId: {InstanceId}", context.InstanceId);
            logger.LogInformation("║  Success:    {Success}", finalResponse.IsSuccess);
            logger.LogInformation("║  ResponseLength: {Length} chars", finalResponse.Response?.Length ?? 0);
            logger.LogInformation("╚══════════════════════════════════════════════════════════════╝");

            // Save conversation turn
            await SaveTurn(context, input, finalResponse.Response ?? string.Empty);

            await PublishCompleted(context, input, finalResponse);
            return finalResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Orchestrator failed for session {SessionId}", input.SessionId);
            var error = AdvisorAgentResponse.Failure($"An unexpected error occurred: {ex.Message}");
            await PublishCompleted(context, input, error);
            return error;
        }
    }

    // ── Status publishing helpers ─────────────────────────

    private static Task PublishStatus(
        TaskOrchestrationContext context,
        AdvisorOrchestratorInput input,
        string stepName,
        string state,
        string? message = null)
    {
        // Update custom status for polling-based real-time updates
        context.SetCustomStatus(new OrchestrationProgress
        {
            Steps = GetOrCreateSteps(context, stepName, state, message),
            IsCompleted = false
        });

        return context.CallActivityAsync(
            nameof(AdvisorActivities.PublishStatusActivity),
            new PublishStatusInput
            {
                SessionId = input.SessionId,
                UserId = input.UserId,
                StepName = stepName,
                StepState = state,
                Message = message
            });
    }

    private static Task PublishCompleted(
        TaskOrchestrationContext context,
        AdvisorOrchestratorInput input,
        AdvisorAgentResponse response)
    {
        context.SetCustomStatus(new OrchestrationProgress
        {
            Steps = _progressSteps.GetValueOrDefault(context.InstanceId, []),
            IsCompleted = true
        });

        return context.CallActivityAsync(
            nameof(AdvisorActivities.PublishCompletedActivity),
            new PublishCompletedInput
            {
                SessionId = input.SessionId,
                UserId = input.UserId,
                Response = response
            });
    }

    // Track steps per orchestration instance for custom status
    private static readonly Dictionary<string, List<StepProgress>> _progressSteps = new();

    private static List<StepProgress> GetOrCreateSteps(
        TaskOrchestrationContext context, string stepName, string state, string? message)
    {
        if (!_progressSteps.ContainsKey(context.InstanceId))
            _progressSteps[context.InstanceId] = [];

        var steps = _progressSteps[context.InstanceId];
        var existing = steps.FirstOrDefault(s => s.StepName == stepName);
        if (existing != null)
        {
            existing.State = state;
            if (message != null) existing.Message = message;
        }
        else
        {
            steps.Add(new StepProgress { StepName = stepName, State = state, Message = message });
        }
        return steps;
    }

    /// <summary>
    /// Saves a conversation turn (prompt + response) to the conversation store via an activity.
    /// </summary>
    private static Task SaveTurn(TaskOrchestrationContext context, AdvisorOrchestratorInput input, string response)
    {
        return context.CallActivityAsync(
            nameof(AdvisorActivities.SaveConversationTurnActivity),
            new SaveConversationTurnInput
            {
                UserId = input.UserId,
                SessionId = input.SessionId,
                Prompt = input.Prompt,
                Response = response,
                RequestId = input.RequestId ?? string.Empty
            });
    }
}
