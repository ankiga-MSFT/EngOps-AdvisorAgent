using CXOAI.SkillFramework;
using CXOAI.Functions.Models;
using CXOAI.StatusNotifier;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CXOAI.Functions.Agents.CXOAI;

public class CxoaiOrchestrator
{
    private readonly ILogger<CxoaiOrchestrator> _logger;
    private readonly IOrchestratorStepService _stepService;

    public CxoaiOrchestrator(ILogger<CxoaiOrchestrator> logger, IOrchestratorStepService stepService)
    {
        _logger = logger;
        _stepService = stepService;
    }

    private static Task PublishStatusAsync(TaskOrchestrationContext context, string sessionId, OrchestratorStatus status)
    {
        return context.CallActivityAsync(
            nameof(CxoaiActivities.PublishStatusActivity),
            new PublishStatusInput { SessionId = sessionId, Status = status });
    }

    private static Task PublishCompletedAsync(TaskOrchestrationContext context, string sessionId, CXOAgentResponse result)
    {
        return context.CallActivityAsync(
            nameof(CxoaiActivities.PublishCompletedActivity),
            new PublishCompletedInput { SessionId = sessionId, Result = result });
    }

    private static Task<bool> NotifyUserInputAsync(TaskOrchestrationContext context, string sessionId, string instanceId, string taskId, string skillName, string prompt, SkillExecutionResult? skillResult = null)
    {
        return context.CallActivityAsync<bool>(
            nameof(CxoaiActivities.NotifyUserInputNeededActivity),
            new UserInputNotification { SessionId = sessionId, InstanceId = instanceId, TaskId = taskId, SkillName = skillName, Prompt = prompt, SkillResult = skillResult });
    }

    [Function(nameof(OrchestratorMain))]
    public async Task<CXOAgentResponse> OrchestratorMain(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<OrchestratorInput>()
            ?? throw new InvalidOperationException("OrchestratorInput is required.");

        var log = context.CreateReplaySafeLogger(nameof(OrchestratorMain));
        var sessionId = !string.IsNullOrWhiteSpace(input.SessionId) ? input.SessionId : context.InstanceId;
        var requestId = !string.IsNullOrWhiteSpace(input.RequestId) ? input.RequestId : Guid.NewGuid().ToString("N");

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["SessionId"] = sessionId,
            ["RequestId"] = requestId,
            ["StepName"] = "OrchestratorMain"
        }))
        {
            try
            {
            var status = new OrchestratorStatus
            {
                SessionId = sessionId,
                UserId = input.UserId,
                OriginalPrompt = input.Prompt
            };

            // Step 1: Enhance prompt
            status.BeginStep("EnhancePrompt", "Building prompt with user preferences and knowledge graph");
            await PublishStatusAsync(context, sessionId, status);

                _logger.LogInformation("Step 1: Enhancing prompt");
            var enhanceResult = await context.CallActivityAsync<EnhancePromptResult>(
                nameof(CxoaiActivities.EnhancePromptActivity), input);
                _logger.LogInformation($"Step 1: Enhancing prompt | Result -> {enhanceResult}");


                status.CompleteStep("EnhancePrompt", "Prompt enhanced");
            await PublishStatusAsync(context, sessionId, status);

            // Step 2: Classify intent (BEFORE history check so definitional
            // queries like "what is csat?" go straight to AnswerFromKnowledge)
            status.BeginStep("ClassifyIntent", "Determining query intent");
            await PublishStatusAsync(context, sessionId, status);

                _logger.LogInformation("Step 2: Classifying intent");
            var intent = await context.CallActivityAsync<UserIntent>(
                nameof(CxoaiActivities.ClassifyIntentActivity),
                new ClassifyIntentInput
                {
                    Prompt = input.Prompt,
                    GeneralKnowledge = enhanceResult.GeneralKnowledge,
                    UIContextEntityName = input.UserContext?.EntityName,
                    SessionId = sessionId
                });
                _logger.LogInformation($"Step 2: Classifying intent | Result -> {intent.ToString()}");


                status.CompleteStep("ClassifyIntent", $"{intent.Intent}: {intent.Reasoning}");
            await PublishStatusAsync(context, sessionId, status);

            if (intent.Intent == UserIntentType.Informational)
            {
                _logger.LogInformation("Intent: Informational - answering from knowledge");
                var answer = await context.CallActivityAsync<string>(
                    nameof(CxoaiActivities.AnswerFromKnowledgeActivity),
                    new KnowledgeAnswerInput
                    {
                        Prompt = input.Prompt,
                        GeneralKnowledge = enhanceResult.GeneralKnowledge,
                        SessionId = sessionId
                    });
                    _logger.LogInformation($"Intent: Informational - answering from knowledge | Result -> {answer}");

                    status.BeginStep("SaveMemory", "Extract memory and save from output");
                    await PublishStatusAsync(context, sessionId, status);

                    await context.CallActivityAsync(nameof(CxoaiActivities.SummarizeActivity),
                    new SummarizeInput
                    {
                        UserId = input.UserId,
                        SessionId = sessionId,
                        ConversationContent = $"[UserPrompt] {input.Prompt}\n[SkillOutput:AnswerFromKnowledge] {answer}",
                        RequestId = requestId
                    });
                    status.CompleteStep("SaveMemory", "Memory saved");
                    await PublishStatusAsync(context, sessionId, status);

                    var result = new CXOAgentResponse { IsSuccess = true, Response = answer };
                await PublishCompletedAsync(context, sessionId, result);
                return result;
            }

            if (intent.Intent == UserIntentType.Unknown)
            {
                var msg = $"{OrchestratorMessages.UnknownIntent} {intent.Reasoning}";
                    status.BeginStep("SaveMemory", "Extract memory and save from output");
                    await PublishStatusAsync(context, sessionId, status);

                    await context.CallActivityAsync(nameof(CxoaiActivities.SummarizeActivity),
                    new SummarizeInput { UserId = input.UserId, SessionId = sessionId, ConversationContent = $"[UserPrompt] {input.Prompt}\n[Response] {msg}", RequestId = requestId });
                    status.CompleteStep("SaveMemory", "Memory saved");
                    await PublishStatusAsync(context, sessionId, status);

                    var result = new CXOAgentResponse { IsSuccess = true, Response = msg };
                    _logger.LogInformation($"Intent: Unknown  | Result -> {msg}");

                    await PublishCompletedAsync(context, sessionId, result);
                return result;
            }


            // Step 2a: Check conversation history (only for DataAction queries)
            status.BeginStep("CheckHistory", "Checking conversation history");
            await PublishStatusAsync(context, sessionId, status);

            var historyResult = await context.CallActivityAsync<HistoryAnswerResult>(
                nameof(CxoaiActivities.CheckHistoryActivity),
                new CheckHistoryInput { UserId = input.UserId, SessionId = sessionId, Prompt = input.Prompt, UIContextEntityName = input.UserContext?.EntityName });
                    _logger.LogInformation($"Can Answer from History  | Result -> {historyResult.CanAnswer.ToString()}");

                if (historyResult.CanAnswer && !string.IsNullOrWhiteSpace(historyResult.Answer))
            {
                _logger.LogInformation("Answered from conversation history");
                status.CompleteStep("CheckHistory", "Answered from history");
                await PublishStatusAsync(context, sessionId, status);
                    status.BeginStep("SaveMemory", "Extract memory and save from output");
                    await PublishStatusAsync(context, sessionId, status);

                    await context.CallActivityAsync(nameof(CxoaiActivities.SummarizeActivity),
                    new SummarizeInput
                    {
                        UserId = input.UserId,
                        SessionId = sessionId,
                        ConversationContent = $"[UserPrompt] {input.Prompt}\n[SkillOutput:HistoryAnswer] {historyResult.Answer}",
                        RequestId = requestId
                    });
                    status.CompleteStep("SaveMemory", "Memory saved");
                    await PublishStatusAsync(context, sessionId, status);

                    var result = new CXOAgentResponse { IsSuccess = true, Response = historyResult.Answer };
                await PublishCompletedAsync(context, sessionId, result);
                return result;
            }


            string? historyContext = historyResult.HasRelevantContext
                ? historyResult.RelevantContext
                : null;
            status.CompleteStep("CheckHistory", historyContext is not null
                ? "Found relevant context, continuing pipeline"
                : "No history match, continuing pipeline");
            await PublishStatusAsync(context, sessionId, status);

            // Step 3: Decompose into task plan
            status.BeginStep("DecomposeTasks", "Breaking prompt into tasks with skills and dependencies");
            await PublishStatusAsync(context, sessionId, status);
                    _logger.LogInformation($"Step 3: Decomposing into task plan");

                _logger.LogInformation("Step 3: Decomposing user prompt into task plan");
            var taskPlan = await context.CallActivityAsync<List<TaskPlanItem>>(
                nameof(CxoaiActivities.DecomposeTasksActivity),
                new DecomposeTasksInput { EnhancedPrompt = enhanceResult.EnhancedPrompt, OriginalPrompt = input.Prompt, SessionId = sessionId });
                    _logger.LogInformation($"Step 3: Decomposed into task plan  | Result -> {string.Join(",",taskPlan.Select(t=> $"({t.Task.ToString()})-[{t.SkillName}]"))}");

                if (taskPlan is not { Count: > 0 })
            {
                var noTasksMsg = OrchestratorMessages.NoTasksGenerated;
                status.CompleteStep("DecomposeTasks", "No tasks generated");
                await PublishStatusAsync(context, sessionId, status);
                    status.BeginStep("SaveMemory", "Extract memory and save from output");
                    await PublishStatusAsync(context, sessionId, status);

                    await context.CallActivityAsync(nameof(CxoaiActivities.SummarizeActivity),
                    new SummarizeInput { UserId = input.UserId, SessionId = sessionId, ConversationContent = $"[UserPrompt] {input.Prompt}\n[Response] {noTasksMsg}", RequestId = requestId });
                    status.CompleteStep("SaveMemory", "Memory saved");
                    await PublishStatusAsync(context, sessionId, status);

                    var result = new CXOAgentResponse { IsSuccess = false, Response = noTasksMsg };
                await PublishCompletedAsync(context, sessionId, result);
                return result;
            }


            status.CompleteStep("DecomposeTasks", $"{taskPlan.Count} task(s) in {taskPlan.Select(t => t.Group).Distinct().Count()} group(s)");
            await PublishStatusAsync(context, sessionId, status);

            // Step 4: Resolve skill configs for unique skills in the plan
            var uniqueSkillNames = taskPlan.Select(t => t.SkillName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                _logger.LogInformation("Step 4: Resolving {Count} skill config(s): [{Skills}]", uniqueSkillNames.Count, string.Join(", ", uniqueSkillNames));

            var resolvedSkills = await context.CallActivityAsync<List<AgentSkill>>(
                nameof(CxoaiActivities.GetSkillsByNameActivity),
                new SkillsByNameInput { SkillNames = uniqueSkillNames });
                    _logger.LogInformation($"Step 4: Resolved | Result -> {string.Join(",", resolvedSkills.Select(a=> a.SkillName))}");
            
            // Step 5: Validate & safely remove unknown skills with re-indexing
            var skillLookupForValidation = resolvedSkills.ToDictionary(s => s.SkillName, StringComparer.OrdinalIgnoreCase);
            PlanValidator.RemoveUnknownSkillsAndReindex(taskPlan,
                new HashSet<string>(skillLookupForValidation.Keys, StringComparer.OrdinalIgnoreCase), _logger);

            // Fix sibling dependency violations (consumer skills chained instead of siblings)
            PlanValidator.FixSiblingDependencies(taskPlan, _logger);

            // Step 5a: Build DAG from task plan (Id-based) and topological sort
            var dagForSort = PlanValidator.ToDag(taskPlan);
            var order = TopologicalSort.Sort(dagForSort, _logger);
            _logger.LogInformation($"Step 5: Topological sorting completed| Result -> {string.Join(",",order)}");

                // Step 6: Sub-orchestrator for task execution
                _logger.LogInformation("Step 6: Executing {Count} task(s)", order.Count);

            status.BeginStep("ExecuteTasks", $"Running {taskPlan.Count} task(s)");
            await PublishStatusAsync(context, sessionId, status);
                _logger.LogInformation($"Step 6: Executing All Tasks");
                //CXOAgentResponse finalResponse= new CXOAgentResponse();

                var subResult = await context.CallSubOrchestratorAsync<SubOrchestratorResult>(
                nameof(SkillExecutionSubOrchestrator),
                new SkillExecutionInput
                {
                    EnhancedPrompt = enhanceResult.EnhancedPrompt,
                    OriginalPrompt = input.Prompt,
                    UserId = input.UserId,
                    SessionId = sessionId,
                    RequestId = requestId,
                    Skills = resolvedSkills,
                    TaskPlan = taskPlan,
                    Order = order,
                    historyResult = historyResult,
                    DomainKnowledge = enhanceResult.GeneralKnowledge,
                    UserContext = input.UserContext,
                    AccessToken = input.AccessToken,
                    Status=status

                });
                status = subResult.Status;
                var finalResponse = subResult.Response;


                if (finalResponse.IsSuccess)
            {
                status.CompleteStep("ExecuteTasks", "All tasks complete");
                await PublishStatusAsync(context, sessionId, status);
            }
            else
            {
                status.FailedSkill("ExecuteTasks", "Some task failed", 1);
                await PublishStatusAsync(context, sessionId, status);
            }


                // Step 8: Summarize
                status.BeginStep("SaveMemory", "Extract memory and save from output");
                await PublishStatusAsync(context, sessionId, status);

                await context.CallActivityAsync(nameof(CxoaiActivities.SummarizeActivity),
                new SummarizeInput
                {
                    UserId = input.UserId,
                    SessionId = sessionId,
                    ConversationContent = $"[UserPrompt] {input.Prompt}\n[Response] {finalResponse.Response}",
                    FreshSkillOutputs = subResult.FreshSkillOutputs,
                    RequestId = requestId
                });
                status.CompleteStep("SaveMemory", "Memory saved");
                await PublishStatusAsync(context, sessionId, status);



                await PublishCompletedAsync(context, sessionId, finalResponse);
            return finalResponse;
            }
            catch (Exception ex)
            {
                var userMessage = OrchestratorMessages.GracefulError;

                // Surface step-level detail from LlmOperationException if the activity
                // re-threw it wrapped in TaskFailedException
                if (ex is TaskFailedException tfe && tfe.InnerException is LlmOperationException llmEx)
                {
                    userMessage = llmEx.UserMessage;
                    _logger.LogError(ex, "OrchestratorMain failed at step '{StepName}'", llmEx.StepName);
                }
                else if (ex is LlmOperationException directLlmEx)
                {
                    userMessage = directLlmEx.UserMessage;
                    _logger.LogError(ex, "OrchestratorMain failed at step '{StepName}'", directLlmEx.StepName);
                }
                else
                {
                    _logger.LogError(ex, "OrchestratorMain failed with unhandled exception");
                }

                var errorResponse = new CXOAgentResponse { IsSuccess = false, Response = userMessage };
                try { await PublishCompletedAsync(context, sessionId, errorResponse); } catch { /* best-effort */ }
                return errorResponse;
            }
        }
    }


    [Function(nameof(SkillExecutionSubOrchestrator))]
    public async Task<SubOrchestratorResult> SkillExecutionSubOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<SkillExecutionInput>()
            ?? throw new InvalidOperationException("SkillExecutionInput is required.");

        var log = context.CreateReplaySafeLogger(nameof(SkillExecutionSubOrchestrator));
        var sessionId = !string.IsNullOrWhiteSpace(input.SessionId) ? input.SessionId : context.InstanceId;
        var requestId = input.RequestId;
        var status = input.Status;


        try
        {
        // Memory keyed by task index (string), stores full skill result
        var memory = new Dictionary<string, SkillExecutionResult>(StringComparer.OrdinalIgnoreCase);
        var sessionMessages = new List<string> { $"[UserPrompt] {input.OriginalPrompt}" };

        // Two-layer user-input deduplication:
        // Layer 1: Q&A context injection — appended to skill prompts so the LLM already has answers
        // Layer 2: Param-key cache — fallback interceptor if the LLM ignores injected context
        var answeredQAPairs = new System.Text.StringBuilder();
        var userInputCache = new UserInputCache();

        var skillLookup = input.Skills.ToDictionary(s => s.SkillName, StringComparer.OrdinalIgnoreCase);
        var taskPlan = input.TaskPlan;

            foreach (var taskIdStr in input.Order)
            {
                var taskIdx = int.Parse(taskIdStr);
                var task = taskPlan[taskIdx];

                if (memory.ContainsKey(taskIdStr))
                {
                    _logger.LogWarning("Task {TaskId} '{TaskLabel}' already executed, skipping", taskIdStr, task.Task);
                    continue;
                }

                if (!skillLookup.TryGetValue(task.SkillName, out var skillInfo))
                {
                    _logger.LogWarning("Skill '{Skill}' for task {TaskId} not found in resolved skills, skipping", task.SkillName, taskIdStr);
                    continue;
                }

                // Build upstream outputs from completed dependencies
                var upstreamOutputs = new System.Text.StringBuilder();
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

                // Build UI context string (same as Console SkillOrchestrator)
                var userContext = input.UserContext;
                var uiContextStr = userContext is not null
                    ? $"## UI Context:\nEntity Name: {userContext.EntityName}\nEntity ID: {userContext.EntityId}\nEntity Type: {userContext.EntityType}"
                      + (userContext.GlobalLevelFilters?.Any(f => f.SelectedValues?.Count > 0) == true
                          ? "\nGlobalFilter: [\n" + string.Join(",\n", userContext.GlobalLevelFilters!
                              .Where(f => f.SelectedValues?.Count > 0)
                              .Select(f => $"  {{ UI_Filter_Name: {f.UIFilterName}, FilterClause: {f.FilterClause}, SelectedValues: {string.Join(", ", f.SelectedValues!)} }}"))
                            + "\n]"
                          : "")
                    : "No UI context available";

                // Generate the skill prompt at execution time via Activity (mirrors Console GenerateSkillPromptAsync)
                var TaskUIName = $"Task_{taskIdStr}({task.SkillName.Replace("Skill", "")})";
                //status.BeginSkill($"{TaskUIName} (Generate task prompt)", "Executing..");
                //await PublishStatusAsync(context, sessionId, status);

                var basePrompt = await context.CallActivityAsync<string>(
                    nameof(CxoaiActivities.GenerateSkillPromptActivity),
                    new GenerateSkillPromptInput
                    {
                        Task = task,
                        SkillDescription = skillInfo.Description ?? "",
                        ExpectedSkillInput = skillInfo.ExpectedSkillInput ?? "",
                        DomainKnowledge = input.DomainKnowledge ?? "",
                        UIContext = uiContextStr,
                        UpstreamOutputs = upstreamOutputs.ToString(),
                        OriginalUserPrompt = input.OriginalPrompt,
                        SessionId = sessionId
                    });
                //status.CompleteSkill($"{TaskUIName} (Generate task prompt)", "Done");
                //await PublishStatusAsync(context, sessionId, status);
                var separatorIdx = basePrompt.IndexOf("-----", StringComparison.Ordinal);
                var skillunmodifiedPrompt = separatorIdx >= 0
                    ? basePrompt[..separatorIdx].Trim()
                    : basePrompt;
                _logger.LogInformation($"Task_{taskIdStr}| GeneratedPrompt | {basePrompt}");

                // Inject history context for root tasks with no upstream data
                if (input.historyResult!=null! && input.historyResult.HasRelevantContext
                    && upstreamOutputs.Length == 0
                      && task.DependsOn.Count == 0)
                {
                    var fullSessionSummary = await context.CallActivityAsync<string?>(
                        nameof(CxoaiActivities.GetSessionSummaryActivity),
                        new GetSessionSummaryInput { UserId = input.UserId, SessionId = input.SessionId });

                    if (!string.IsNullOrWhiteSpace(fullSessionSummary))
                    {
                        basePrompt = $"{basePrompt}\n\n## Data from previous session\n{fullSessionSummary}";
                    }
                }

                // Layer 1: Inject previously answered Q&A pairs so the skill has answers upfront
                if (answeredQAPairs.Length > 0)
                {
                    basePrompt = $"{basePrompt}\n------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n## Previous User Responses\n{answeredQAPairs}";
                }


                _logger.LogInformation("Executing task {TaskId} '{TaskLabel}' with skill '{Skill}'", taskIdStr, task.Task, task.SkillName);

                var execInput = new ExecuteSkillInput
                {
                    SkillName = task.SkillName,
                    SkillInfo = skillInfo,
                    Prompt = basePrompt,
                    SessionId = sessionId,
                    Dependencies = task.DependsOn.Select(d => d.ToString()).ToList(),
                    AccessToken = input.AccessToken
                };
                var actualTaskUIName = $"{TaskUIName} {task.Task})";
                status.BeginSkill($"{actualTaskUIName}", "Executing..");
                await PublishStatusAsync(context, sessionId, status);
                _logger.LogInformation($"Task_{taskIdStr}| ExecuteSkill (user prompt) | {basePrompt}");


                var result = await context.CallActivityAsync<SkillExecutionResult>(
                nameof(CxoaiActivities.ExecuteSkillActivity), execInput);
                _logger.LogInformation($"Task_{taskIdStr}| ExecuteSkill (Result) | {result.Response}");


                var inputRound = 0;
                var conversationHistory = new System.Text.StringBuilder();
                while (result.NeedsUserInput && inputRound < 5)
                {
                    inputRound++;
                    _logger.LogInformation("Task {TaskId} needs user input (round {Round})",
                    taskIdStr, inputRound);

                    var questionText = result.UserPrompt ?? "Please provide input.";

                    // Accumulate the skill's question in conversation history
                    conversationHistory.AppendLine($"## Skill Question (round {inputRound})");
                    conversationHistory.AppendLine(questionText);
                    conversationHistory.AppendLine();

                    // Layer 2: Check param-key cache before prompting the user
                    var cachedAnswer = userInputCache.TryGetAnswer(questionText);
                    string userResponse;

                    if (cachedAnswer != null)
                    {
                        // Cache hit — skip SignalR + WaitForExternalEvent entirely
                        userResponse = cachedAnswer;
                        _logger.LogInformation(
                        "UserInputCache HIT for Task {TaskId}, reusing cached answer. Question: {Question}",
                        taskIdStr, questionText);
                    }
                    else
                    {
                        // Cache miss — notify client and wait for input via durable event
                        status.SkillNeedsInput($"{actualTaskUIName}", questionText, inputRound);
                        await PublishStatusAsync(context, sessionId, status);

                        var useSignalR = !string.Equals(
                            Environment.GetEnvironmentVariable("UseSignalR"), "false",
                            StringComparison.OrdinalIgnoreCase);

                        if (useSignalR)
                        {
                            // SignalR mode — notify the UI client
                            var notified = await NotifyUserInputAsync(context, sessionId, context.InstanceId, taskIdStr, task.SkillName, questionText, result);
                            if (!notified)
                            {
                                _logger.LogWarning("SignalR notification failed for skill '{Skill}'", task.SkillName);
                                status.FailedSkill($"{actualTaskUIName}", "Failed to notify client via SignalR", inputRound);
                                await PublishStatusAsync(context, sessionId, status);
                                result.IsSuccess = false;
                                break;
                            }
                        }
                        else
                        {
                            // Console mode — print Postman-ready instructions
                            var baseUrl = Environment.GetEnvironmentVariable("FunctionAppBaseUrl") ?? "http://localhost:7071";
                            var inputUrl = $"{baseUrl}/api/instances/{context.InstanceId}/tasks/{taskIdStr}/skills/{task.SkillName}/input";

                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine();
                            Console.WriteLine("═══════════════════════════════════════════════════════════");
                            Console.WriteLine("  🟡 SKILL NEEDS USER INPUT");
                            Console.WriteLine("═══════════════════════════════════════════════════════════");
                            Console.WriteLine($"  Question: {questionText}");
                            Console.WriteLine();
                            Console.WriteLine("  POST your answer via Postman:");
                            Console.WriteLine($"  URL:  {inputUrl}");
                            Console.WriteLine("  Body: <your answer text>");
                            Console.WriteLine("  Content-Type: text/plain");
                            Console.WriteLine("═══════════════════════════════════════════════════════════");
                            Console.ResetColor();
                        }

                        // WaitForExternalEvent works identically for both flows —
                        // SignalR callback or Postman POST both hit RaiseUserInput
                        // which calls client.RaiseEventAsync with the same event name.
                        var eventName = $"UserInput_{taskIdStr}_{task.SkillName}";
                        using var timeoutCts = new CancellationTokenSource();
                        var inputTask = context.WaitForExternalEvent<string>(eventName);
                        var timerTask = context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(120), timeoutCts.Token);

                        var winner = await Task.WhenAny(inputTask, timerTask);
                        if (winner == inputTask)
                        {
                            timeoutCts.Cancel();
                            userResponse = await inputTask;

                            // Store in both layers for future tasks
                            userInputCache.Store(questionText, userResponse);
                            answeredQAPairs.AppendLine($"Question: {questionText}");
                            answeredQAPairs.AppendLine($"UserResponse: {userResponse}");
                            answeredQAPairs.AppendLine();
                        }
                        else
                        {
                            _logger.LogWarning("User input timed out for skill '{Skill}' (round {Round})", task.SkillName, inputRound);
                            status.FailedSkill($"{actualTaskUIName}", $"User input timed out after 2 hours (round {inputRound})", inputRound);
                            await PublishStatusAsync(context, sessionId, status);
                            result.IsSuccess = false;
                            break;
                        }
                    }

                    // Accumulate the user's answer in conversation history
                    conversationHistory.AppendLine($"## User Response (round {inputRound})");
                    conversationHistory.AppendLine(userResponse);
                    conversationHistory.AppendLine();

                    status.BeginSkill($"{actualTaskUIName}", $"Re-executing with user input (round {inputRound})...");
                    await PublishStatusAsync(context, sessionId, status);

                    execInput.UserResponse = userResponse;
                    // Send basePrompt + FULL conversation history so the skill has all context
                    execInput.Prompt = $"{basePrompt}\n\n## Previous Interaction\n{conversationHistory}";
                    // Forward continuation token so tools can detect resume mode (e.g., reconnect to external agent)
                    execInput.PayloadJson = result.PayloadJson;
                    _logger.LogInformation($"Task_{taskIdStr}-Round{inputRound + 1}| ExecuteSkill (user prompt) | {execInput}");

                    result = await context.CallActivityAsync<SkillExecutionResult>(
                    nameof(CxoaiActivities.ExecuteSkillActivity), execInput);
                    _logger.LogInformation($"Task_{taskIdStr}-Round{inputRound + 1}| ExecuteSkill (Result) | {result.Response}");

                }
               

                if (!result.IsSuccess)
                {
                    _logger.LogError("Task {TaskId} '{TaskLabel}' execution failed, responseLength={ResponseLength}", taskIdStr, task.Task, result.Response.Length);
                    // Fix: was FailStep which searches Steps[] — "Task_0" only exists in SkillExecutions[],
                    // so the old call was a silent no-op and the UI never saw the failure status.
                    status.FailedSkill($"{actualTaskUIName}", "Failed", 0);
                    await PublishStatusAsync(context, sessionId, status);
                    break;
                }


                memory[taskIdStr] = result;
                sessionMessages.Add($"[TaskOutput:{taskIdStr}:{task.SkillName}:{task.Task}] {result.Response}");

                status.CompleteSkill($"{actualTaskUIName}", $"Done");
                await PublishStatusAsync(context, sessionId, status);
            }

        // ── Assemble per-task results (output tasks only) ────────────
        var outputIndices = PlanValidator.GetOutputTaskIndices(taskPlan);
        var groupResults = new List<GroupResult>();
        var responseBuilder = new System.Text.StringBuilder();

        foreach (var taskIdStr in input.Order)
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
                NeedsInputForUser= taskResult.NeedsUserInput,
                Payload = !string.IsNullOrEmpty(taskResult.PayloadJson)
                    ? Newtonsoft.Json.Linq.JObject.Parse(taskResult.PayloadJson)
                    : null,
                IsUIComponent = taskResult.IsUIComponent,
                UIComponent = taskResult.UIComponent
            };

           

            groupResults.Add(gr);
            responseBuilder.AppendLine(gr.Response);
        }

        var mergedResponse = responseBuilder.ToString().TrimEnd();
        if (string.IsNullOrWhiteSpace(mergedResponse))
            mergedResponse = string.Join("\n", sessionMessages);

            // Aggregate flags from all task results
            var uiGroup = groupResults.FirstOrDefault(g => g.IsUIComponent);

            var ret = new CXOAgentResponse
            {
                IsSuccess = groupResults.Any(g => g.IsSuccess),
                NeedsInputForUser = groupResults.Any(g => g.NeedsInputForUser),
                IsReport = groupResults.Any(g => g.IsReport),
                Response = mergedResponse,
                IsUIComponent = uiGroup is not null,
                UIComponent = uiGroup?.UIComponent ?? string.Empty,
                Groups = groupResults,
            };
            // success path
            var freshOutputs = string.Join("\n", sessionMessages.Where(m => m.StartsWith("[TaskOutput:")));
            return new SubOrchestratorResult { Status = status, Response = ret, FreshSkillOutputs = freshOutputs };
        }
        catch (Exception ex)
        {
            var failedStep = "SkillExecution";
            var userMessage = OrchestratorMessages.GracefulError;

            if (ex is TaskFailedException tfe && tfe.InnerException is LlmOperationException llmEx)
            {
                failedStep = llmEx.StepName;
                userMessage = llmEx.UserMessage;
            }
            else if (ex is LlmOperationException directLlmEx)
            {
                failedStep = directLlmEx.StepName;
                userMessage = directLlmEx.UserMessage;
            }

            _logger.LogError(ex, "SkillExecutionSubOrchestrator failed at step '{StepName}', sessionId={SessionId}",
                failedStep, sessionId);
            status.FailStep(failedStep, userMessage);
            try { await PublishStatusAsync(context, sessionId, status); } catch { /* best-effort */ }

            var ret = new CXOAgentResponse { IsSuccess = false, Response = userMessage };
            return new SubOrchestratorResult { Status = status, Response = ret };
        }
    }   
}

































































































































































































































































