using CXOAI.Memory;
using CXOAI.StatusNotifier;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace CXOAI.SkillFramework
{
    // ══════════════════════════════════════════════════════════════════════════
    // AZURE DURABLE FUNCTIONS ARCHITECTURE
    // ══════════════════════════════════════════════════════════════════════════
    //
    // Trigger: HTTP → starts OrchestratorMain
    //
    // ┌─────────────────────────────────────────────────────────────────────┐
    // │  OrchestratorMain  [OrchestrationTrigger]                          │
    // │                                                                     │
    // │  1. EnhancePromptActivity        → EnhancePromptResult             │
    // │     ├── Builds prompt with UserQuery + UserPreference              │
    // │     ├── Fetches systemKnowledge (for skill pipeline)               │
    // │     └── Fetches generalKnowledge (for informational answers)       │
    // │                                                                     │
    // │  2. ClassifyIntentActivity        → UserIntent                     │
    // │     ├── Informational → AnswerFromKnowledgeActivity → return       │
    // │     ├── Unknown       → return error message                       │
    // │     └── DataAction    → continue                                   │
    // │                                                                     │
    // │  2a. CheckHistoryActivity         → HistoryAnswerResult            │
    // │      └── If answered → return; else carry context forward          │
    // │                                                                     │
    // │  ── DataAction path ───────────────────────────────────────────     │
    // │                                                                     │
    // │  3. DecomposeTasksActivity       → List<TaskPlanItem>              │
    // │     └── Task Planner: 1 LLM call replaces old skill selection,     │
    // │         DAG creation, and prompt generation steps.                  │
    // │         Each TaskPlanItem has: Group, SkillName, DependsOn,        │
    // │         PromptToSend.                                               │
    // │                                                                     │
    // │  4. GetSkillsByNameActivity      → List<AgentSkill>                │
    // │     └── Config lookup (no LLM) for unique skills in the plan       │
    // │                                                                     │
    // │  5. TopologicalSort (in-orchestrator, deterministic, no I/O)       │
    // │     └── Sorts task indices based on DependsOn                      │
    // │                                                                     │
    // │  6. CallSubOrchestrator → SkillExecutionSubOrchestrator            │
    // └─────────────────────────────────────────────────────────────────────┘
    //
    // ┌─────────────────────────────────────────────────────────────────────┐
    // │  SkillExecutionSubOrchestrator  [OrchestrationTrigger]             │
    // │                                                                     │
    // │  Input: SkillExecutionInput { Skills, TaskPlan, Order }            │
    // │  State: memory = Dictionary<taskIndex, CXOAgentResponse>           │
    // │                                                                     │
    // │  foreach (taskIndex in Order):                                     │
    // │  ┌─────────────────────────────────────────────────────────────┐   │
    // │  │  ExecuteSkillActivity                                       │   │
    // │  │  Input:  ExecuteSkillInput { SkillInfo, Prompt,             │   │
    // │  │          DependencyOutputs, UserResponse? }                 │   │
    // │  │                                                             │   │
    // │  │  Memory key = task index (not skill name) so multiple       │   │
    // │  │  tasks using the same skill don't overwrite each other.     │   │
    // │  └─────────────────────────────────────────────────────────────┘   │
    // │                                                                     │
    // │  if (output.NeedsUserInput):                                       │
    // │  ┌─────────────────────────────────────────────────────────────┐   │
    // │  │  User-input suspend/resume loop                             │   │
    // │  │                                                             │   │
    // │  │  1. NotifyUIActivity → push question to SignalR/queue       │   │
    // │  │  2. WaitForExternalEvent<string>("UserInputReceived")       │   │
    // │  │     └── Orchestrator suspends, Azure stores state           │   │
    // │  │     └── UI calls /api/instances/{id}/raiseEvent             │   │
    // │  │  3. Re-run ExecuteSkillActivity with UserResponse           │   │
    // │  │  4. Repeat until NeedsUserInput == false                    │   │
    // │  └─────────────────────────────────────────────────────────────┘   │
    // │                                                                     │
    // │  Assemble GroupResult[] from memory, merge into CXOAgentResponse   │
    // └─────────────────────────────────────────────────────────────────────┘
    //
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Console-only orchestrator that drives the full pipeline sequentially.
    /// All business logic is delegated to <see cref="IOrchestratorStepService"/>;
    /// this class manages flow control, status notifications, state caching,
    /// and the console-based user-input loop.
    /// </summary>
    public class SkillOrchestrator
    {
        private readonly IOrchestratorStepService _stepService;
        private readonly IStatusNotifier _notifier;
        private readonly ILogger<SkillOrchestrator> _logger;
        private readonly OrchestratorState _state = new();
        private const int MaxUserInputRounds = 5;

        public SkillOrchestrator(
            IOrchestratorStepService stepService,
            ILoggerFactory loggerFactory,
            IStatusNotifier? notifier = null)
        {
            _stepService = stepService;
            _logger = loggerFactory.CreateLogger<SkillOrchestrator>();
            _notifier = notifier ?? new ConsoleStatusNotifier(_logger);
        }

        // ── Orchestrator entry point ─────────────────────────────────────
        public async Task<CXOAgentResponse> RunAsync(string userId, string inputPrompt, UserContext? userContext = null, string? sessionId = null, string? requestId = null)
        {
            var st = Stopwatch.StartNew();
            sessionId ??= Guid.NewGuid().ToString("N")[..8];
            requestId ??= Guid.NewGuid().ToString("N");

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["SessionId"] = sessionId,
                ["RequestId"] = requestId
            }))
            {
            var status = new OrchestratorStatus
            {
                UserId = userId,
                OriginalPrompt = inputPrompt,
                SessionId = sessionId
            };

            _logger.LogInformation("RunAsync started");

            // Fresh state for this orchestration run
            _state.Clear();

            try
            {

            // ── Step 1: Enhance prompt ───────────────────────────────────
            if (!_state.TryGet<string>("EnhancedPrompt", out var enhancedPrompt))
            {
                status.BeginStep("EnhancePrompt", "Building prompt with user preferences and knowledge graph");
                await _notifier.PublishStatusAsync(status);
                var enhanceResult = await _stepService.EnhancePromptAsync(userId, sessionId, inputPrompt, userContext);
                enhancedPrompt = enhanceResult.EnhancedPrompt;
                _state.Set("EnhancedPrompt", enhancedPrompt);
                _state.Set("GeneralKnowledge", enhanceResult.GeneralKnowledge);
                status.CompleteStep("EnhancePrompt", "Prompt enhanced");
                await _notifier.PublishStatusAsync(status);
            }

            // ── Step 1b: Classify intent ─────────────────────────────────
            // Runs BEFORE history check so definitional queries ("what is csat?")
            // go straight to AnswerFromKnowledge and never hit the history store.
            if (!_state.TryGet<UserIntent>("UserIntent", out var intent))
            {
                status.BeginStep("ClassifyIntent", "Determining if query is informational or data action");
                await _notifier.PublishStatusAsync(status);
                var generalKnowledge = _state.TryGet<string>("GeneralKnowledge", out var gk) ? gk! : string.Empty;
                intent = await _stepService.ClassifyIntentAsync(inputPrompt, generalKnowledge, userContext?.EntityName);
                _state.Set("UserIntent", intent);
                status.CompleteStep("ClassifyIntent", $"{intent!.Intent}: {intent.Reasoning}");
                await _notifier.PublishStatusAsync(status);
            }

            // ── Step 1c: Short-circuit for informational queries ─────────
            if (intent.Intent == UserIntentType.Informational)
            {
                status.BeginStep("AnswerFromKnowledge", "Generating answer from domain knowledge");
                await _notifier.PublishStatusAsync(status);
                var gkForAnswer = _state.TryGet<string>("GeneralKnowledge", out var gka) ? gka! : string.Empty;
                var answer = await _stepService.AnswerFromKnowledgeAsync(inputPrompt, gkForAnswer);
                await _stepService.SummarizeAndStoreAsync(userId, sessionId, $"[UserPrompt] {inputPrompt}\n[SkillOutput:AnswerFromKnowledge] {answer}", requestId: requestId);
                status.CompleteStep("AnswerFromKnowledge", "Knowledge answer generated");
                await _notifier.PublishStatusAsync(status);
                return new CXOAgentResponse
                {
                    IsSuccess = true,
                    NeedsInputForUser = false,
                    Response = answer,
                    IsUIComponent = false,
                    UIComponent = string.Empty,
                };
            }

            // ── Step 1d: Short-circuit for unknown/nonsensical queries ───
            if (intent.Intent == UserIntentType.Unknown)
            {
                var unknownMsg = $"{OrchestratorMessages.UnknownIntent} {intent.Reasoning}";
                await _stepService.SummarizeAndStoreAsync(userId, sessionId, $"[UserPrompt] {inputPrompt}\n[Response] {unknownMsg}", requestId: requestId);
                status.BeginStep("UnknownIntent", "Query not recognized");
                status.CompleteStep("UnknownIntent", intent.Reasoning);
                await _notifier.PublishStatusAsync(status);
                 return new CXOAgentResponse
                {
                    IsSuccess = true,
                    NeedsInputForUser = false,
                    Response = unknownMsg,
                    IsUIComponent = false,
                    UIComponent = string.Empty,
                }; 
            }

            // ── Step 2: Try to answer from conversation history ──────────
            // Only reached for DataAction queries — definitional and unknown
            // queries have already been handled above.
            HistoryAnswerResult historyResult = null;
            var sessionSummary = await _stepService.GetSessionSummaryAsync(userId, sessionId);
            if (!string.IsNullOrWhiteSpace(sessionSummary))
            {
                status.BeginStep("CheckHistory", "Checking if question can be answered from conversation history");
                await _notifier.PublishStatusAsync(status);

                 historyResult = await _stepService.TryAnswerFromHistoryAsync(inputPrompt, sessionSummary, userContext?.EntityName);
                if (historyResult.CanAnswer && !string.IsNullOrWhiteSpace(historyResult.Answer))
                {
                    await _stepService.SummarizeAndStoreAsync(userId, sessionId, $"[UserPrompt] {inputPrompt}\n[SkillOutput:HistoryAnswer] {historyResult.Answer}", requestId: requestId);
                    status.CompleteStep("CheckHistory", "Answered from conversation history");
                    await _notifier.PublishStatusAsync(status);
                    return new CXOAgentResponse
                    {
                        IsSuccess = true,
                        NeedsInputForUser = false,
                        Response = historyResult.Answer,
                        IsUIComponent = false,
                        UIComponent = string.Empty,
                    };
                }

                if (historyResult.HasRelevantContext && !string.IsNullOrWhiteSpace(historyResult.RelevantContext))
                {
                    status.CompleteStep("CheckHistory", "Found relevant context from history, continuing pipeline with context");
                    _logger.LogInformation("CheckHistory: Carrying forward history context ({ContextLength} chars)", historyResult.RelevantContext.Length);
                }
                else
                {
                    status.CompleteStep("CheckHistory", "Not answerable from history, continuing pipeline");
                }
                await _notifier.PublishStatusAsync(status);
            }

            // ── Step 3: Decompose into task plan ────────────────────────
            if (!_state.TryGet<List<TaskPlanItem>>("TaskPlan", out var taskPlan))
            {
                status.BeginStep("DecomposeTasks", "Breaking prompt into tasks with skills and dependencies");
                await _notifier.PublishStatusAsync(status);
                taskPlan = await _stepService.DecomposeTasksAsync(enhancedPrompt!, inputPrompt);
                _state.Set("TaskPlan", taskPlan);
                status.CompleteStep("DecomposeTasks", $"{taskPlan!.Count} task(s) in {taskPlan.Select(t => t.Group).Distinct().Count()} group(s)");
                await _notifier.PublishStatusAsync(status);
            }
            ////sridhar remove below line
            //return new CXOAgentResponse
            //{
            //    IsSuccess = true,
            //    NeedsInputForUser = false,
            //    Response = $"Decomposed into {taskPlan!.Count} task(s) across {taskPlan.Select(t => t.Group).Distinct().Count()} group(s).",
            //    IsUIComponent = false,
            //    UIComponent = string.Empty,
            //};
            // Guard: if no tasks, the planner couldn't decompose
            if (taskPlan == null || taskPlan.Count == 0)
            {
                var noTasksMsg = OrchestratorMessages.NoTasksGenerated;
                await _stepService.SummarizeAndStoreAsync(userId, sessionId, $"[UserPrompt] {inputPrompt}\n[Response] {noTasksMsg}");
                status.CompleteStep("DecomposeTasks", "No tasks generated");
                await _notifier.PublishStatusAsync(status);
                return new CXOAgentResponse
                {
                    IsSuccess = true,
                    NeedsInputForUser = false,
                    Response = noTasksMsg,
                    IsUIComponent = false,
                    UIComponent = string.Empty,
                };
            }

            // ── Step 4: Resolve skill configs for all unique skills in the plan
            var uniqueSkillNames = taskPlan.Select(t => t.SkillName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!_state.TryGet<List<AgentSkill>>("ResolvedSkills", out var resolvedSkills))
            {
                status.BeginStep("ResolveSkills", $"Loading configs for: [{string.Join(", ", uniqueSkillNames)}]");
                await _notifier.PublishStatusAsync(status);
                resolvedSkills = await _stepService.GetSkillsByNameAsync(uniqueSkillNames);
                _state.Set("ResolvedSkills", resolvedSkills);
                status.CompleteStep("ResolveSkills", $"Loaded {resolvedSkills!.Count} skill config(s)");
                await _notifier.PublishStatusAsync(status);
            }

            var skillLookup = resolvedSkills.ToDictionary(s => s.SkillName, StringComparer.OrdinalIgnoreCase);

            // ── Validate & safely remove unknown skills with re-indexing ──
            PlanValidator.RemoveUnknownSkillsAndReindex(taskPlan, 
                new HashSet<string>(skillLookup.Keys, StringComparer.OrdinalIgnoreCase), _logger);

            // Fix sibling dependency violations (consumer skills chained instead of siblings)
            PlanValidator.FixSiblingDependencies(taskPlan, _logger);

            // ── Step 5: Build DAG from task plan (index-based) and topological sort
            var dagForSort = PlanValidator.ToDag(taskPlan);
            var executionOrder = TopologicalSort.Sort(dagForSort, _logger);

            // ── Step 6: Execute tasks in topological order ───────────────
            _stepService.SetToolSession(sessionId);
            status.BeginStep("ExecuteTasks", $"Running {taskPlan.Count} task(s)");
            foreach (var taskIdStr in executionOrder)
                status.BeginSkill($"Task_{taskIdStr}");
            await _notifier.PublishStatusAsync(status);

            
            // Memory keyed by task index (string)
            var memory = new Dictionary<string, CXOAgentResponse>(StringComparer.OrdinalIgnoreCase);
            var sessionMessages = new List<string>();
            sessionMessages.Add($"[UserPrompt] {inputPrompt}");

            // Two-layer user-input deduplication:
            // Layer 1: Q&A context injection — appended to skill prompts so the LLM already has answers
            // Layer 2: Param-key cache — fallback interceptor if the LLM ignores injected context
            var answeredQAPairs = new StringBuilder();
            var userInputCache = new UserInputCache();

            foreach (var taskIdStr in executionOrder)
            {
                var taskIdx = int.Parse(taskIdStr);
                var task = taskPlan[taskIdx];

                if (memory.ContainsKey(taskIdStr))
                {
                    _logger.LogWarning("Task {TaskId} '{TaskLabel}' already has output. Skipping.", taskIdStr, task.Task);
                    continue;
                }

                if (!skillLookup.TryGetValue(task.SkillName, out var skillInfo))
                {
                    _logger.LogWarning("Skill '{SkillName}' for task {TaskId} not found in resolved skills. Skipping.", task.SkillName, taskIdStr);
                    continue;
                }

                status.BeginSkill($"Task_{taskIdStr}", task.Task);
                await _notifier.PublishStatusAsync(status);

                var resolvedTools = _stepService.ResolveTools(skillInfo);

                // Build upstream outputs from completed dependencies
                var upstreamOutputs = new StringBuilder();
                foreach (var depIdx in task.DependsOn)
                {
                    if (memory.TryGetValue(depIdx.ToString(), out var depResult))
                    {
                        var depTask = taskPlan[depIdx];
                        upstreamOutputs.AppendLine($"### {depTask.Task} ({depTask.SkillName})");
                        upstreamOutputs.AppendLine(depResult.Response);
                        upstreamOutputs.AppendLine();
                    }
                }

                // Build UI context string
                var uiContextStr = userContext is not null
                    ? $"## UI Context:\nEntity Name: {userContext.EntityName}\nEntity ID: {userContext.EntityId}\nEntity Type: {userContext.EntityType}"
                      + (userContext.GlobalLevelFilters?.Any(f => f.SelectedValues?.Count > 0) == true
                          ? "\nGlobalFilter: [\n" + string.Join(",\n", userContext.GlobalLevelFilters!
                              .Where(f => f.SelectedValues?.Count > 0)
                              .Select(f => $"  {{ UI_Filter_Name: {f.UIFilterName}, FilterClause: {f.FilterClause}, SelectedValues: {string.Join(", ", f.SelectedValues!)} }}"))
                          + "\n]"
                          : "")
                    : "No UI context available";

                // Get domain knowledge and skill config for prompt generation
                var domainKnowledge = _state.TryGet<string>("GeneralKnowledge", out var gk) ? gk : "";
                var expectedSkillInput = skillInfo.ExpectedSkillInput ?? "";

                // Generate the skill prompt at execution time — all context is now available
                status.BeginSkill($"Task_{taskIdStr}", "Generating skill prompt...");
                await _notifier.PublishStatusAsync(status);

                var basePrompt = await _stepService.GenerateSkillPromptAsync(
                    task, skillInfo.Description ?? "", expectedSkillInput,
                    domainKnowledge, uiContextStr, upstreamOutputs.ToString(), inputPrompt);

                // Log the generated prompt for observability
                _logger.LogInformation("TASK_PROMPT idx={Idx} Skill={Skill} Task={TaskLabel} Prompt={Prompt}",
                    taskIdStr, task.SkillName, task.Task, basePrompt);

                // Inject history context for root tasks with no upstream data
                if (historyResult!=null! && historyResult.HasRelevantContext
                    && upstreamOutputs.Length == 0
                    && task.DependsOn.Count == 0)
                {
                    var fullSessionSummary = await _stepService.GetSessionSummaryAsync(userId, sessionId);
                    basePrompt = $"{basePrompt}\n\n## Data from previous session\n{fullSessionSummary}";
                }



                // Layer 1: Inject previously answered Q&A pairs so the skill has answers upfront
                if (answeredQAPairs.Length > 0)
                {
                    basePrompt = $"{basePrompt}\n------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n## Previous User Responses\n{answeredQAPairs}";
                }

                status.BeginSkill($"Task_{taskIdStr}", "Executing agent with tools...");
                await _notifier.PublishStatusAsync(status);

                var result = await _stepService.ExecuteSkillAsync(skillInfo, basePrompt, resolvedTools);

                var inputRound = 0;
                var conversationHistory = new StringBuilder(); 
                while (result.NeedsInputForUser)
                {
                    inputRound++;
                    if (inputRound > MaxUserInputRounds)
                    {
                        result = new CXOAgentResponse
                        {
                            NeedsInputForUser = false,
                            Response = $"Task '{task.Task}' exceeded maximum user input rounds ({MaxUserInputRounds}). Last question: {result.Response}"
                        };
                        break;
                    }

                    // Accumulate the skill's question in conversation history
                    conversationHistory.AppendLine($"## Skill Question (round {inputRound})");
                    conversationHistory.AppendLine(result.Response);
                    conversationHistory.AppendLine();

                    // Layer 2: Check param-key cache before prompting the user
                    var cachedAnswer = userInputCache.TryGetAnswer(result.Response);
                    string userInput;
                    if (cachedAnswer != null)
                    {
                        userInput = cachedAnswer;
                        _logger.LogInformation(
                            "UserInputCache HIT for Task {TaskId}, reusing cached answer. Question: {Question}",
                            taskIdStr, result.Response);
                    }
                    else
                    {
                        status.SkillNeedsInput($"Task_{taskIdStr}", result.Response, inputRound);
                        await _notifier.PublishStatusAsync(status);

                        userInput = await _notifier.WaitForUserInputAsync($"Task_{taskIdStr}", result.Response);

                        // Store in both layers for future tasks
                        userInputCache.Store(result.Response, userInput);
                        answeredQAPairs.AppendLine($"Question: {result.Response}");
                        answeredQAPairs.AppendLine($"UserResponse: {userInput}");
                        answeredQAPairs.AppendLine();
                    }

                    // Accumulate the user's answer in conversation history
                    conversationHistory.AppendLine($"## User Response (round {inputRound})");
                    conversationHistory.AppendLine(userInput);
                    conversationHistory.AppendLine();


                    status.BeginSkill($"Task_{taskIdStr}", $"Re-executing with user input (round {inputRound})...");
                    await _notifier.PublishStatusAsync(status);

                    // Send basePrompt + FULL conversation history so the skill has all context
                    var followUp = $"{basePrompt}\n\n## Previous Interaction\n{conversationHistory}";
                    result = await _stepService.ExecuteSkillAsync(skillInfo, followUp, resolvedTools);
                }

                if (!result.IsSuccess)
                {
                    _logger.LogError("Task {TaskId} '{TaskLabel}' execution failed. Response: {Response}", taskIdStr, task.Task, result.Response);
                    status.FailStep($"Task_{taskIdStr}", $"Execution failed: {result.Response}");
                    await _notifier.PublishStatusAsync(status);
                    break;
                }

                memory[taskIdStr] = result;
                sessionMessages.Add($"[TaskOutput:{taskIdStr}:{task.SkillName}:{task.Task}] {result.Response}");

                status.CompleteSkill($"Task_{taskIdStr}", $"Done ({result.Response.Length} chars)");
                await _notifier.PublishStatusAsync(status);
            }

            // ── Assemble per-task results (output tasks only) ────────────
            var outputIndices = PlanValidator.GetOutputTaskIndices(taskPlan);
            var groupResults = new List<GroupResult>();
            var responseBuilder = new StringBuilder();

            foreach (var taskIdStr in executionOrder)
            {
                var taskIdx = int.Parse(taskIdStr);

                // Skip intermediate data-fetching tasks
                if (!outputIndices.Contains(taskIdx))
                    continue;

                if (!memory.TryGetValue(taskIdStr, out var taskResult))
                    continue;

                var task = taskPlan[taskIdx];

                var gr = new GroupResult
                {
                    Group = taskIdx,
                    Label = task.Task,
                    IsSuccess = taskResult.IsSuccess,
                    Response = taskResult.Response ?? string.Empty,
                    IsReport = taskResult.IsReport,
                    NeedsInputForUser = taskResult.NeedsInputForUser,
                    Payload = taskResult.Payload,
                    IsUIComponent = taskResult.IsUIComponent,
                    UIComponent = taskResult.UIComponent
                };


                

                groupResults.Add(gr);
                responseBuilder.AppendLine(gr.Response);
            }

            var mergedResponse = responseBuilder.ToString().TrimEnd();
            if (string.IsNullOrWhiteSpace(mergedResponse))
                mergedResponse = "No results were produced.";

            // Aggregate flags from all task results
            var uiGroup = groupResults.FirstOrDefault(g => g.IsUIComponent);

            if (groupResults.Any(g => g.IsSuccess))
            {
                status.CompleteStep("ExecuteTasks", "All tasks complete");
                await _notifier.PublishStatusAsync(status);
            }
            else
            {
                status.FailedSkill("ExecuteTasks", "Some task failed", 1);
                await _notifier.PublishStatusAsync(status);
            }

            try
            {
                await _stepService.SummarizeAndStoreAsync(userId, sessionId, string.Join("\n", sessionMessages),
                    freshSkillOutputs: string.Join("\n", sessionMessages.Where(m => m.StartsWith("[TaskOutput:"))),
                    requestId: requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SummarizeAndStore failed — continuing without summary");
            }

            st.Stop();
            _logger.LogInformation("Total orchestration time: {ElapsedSeconds:F2} seconds", st.Elapsed.TotalSeconds);

            return new CXOAgentResponse
            {
                IsSuccess = groupResults.Any(g => g.IsSuccess),
                NeedsInputForUser = groupResults.Any(g => g.NeedsInputForUser),
                IsReport = groupResults.Any(g => g.IsReport),
                Response = mergedResponse,
                IsUIComponent = uiGroup is not null,
                UIComponent = uiGroup?.UIComponent ?? string.Empty,
                Groups = groupResults,
            };

            } // end try
            catch (LlmOperationException ex)
            {
                _logger.LogError(ex, "Pipeline failed at step '{StepName}': {Message}", ex.StepName, ex.Message);
                status.FailStep(ex.StepName, ex.UserMessage);
                await _notifier.PublishStatusAsync(status);
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    NeedsInputForUser = false,
                    Response = ex.UserMessage,
                    IsUIComponent = false,
                    UIComponent = string.Empty,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RunAsync failed with unhandled exception");
                status.FailStep(status.CurrentStep ?? "Unknown", OrchestratorMessages.GracefulError);
                await _notifier.PublishStatusAsync(status);
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    NeedsInputForUser = false,
                    Response = OrchestratorMessages.GracefulError,
                    IsUIComponent = false,
                    UIComponent = string.Empty,
                };
            }
            } // end using BeginScope
        }
    }
}
