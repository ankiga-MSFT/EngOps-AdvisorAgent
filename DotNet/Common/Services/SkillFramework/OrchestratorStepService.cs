using Azure.AI.OpenAI;
using Azure.Core;
using CXOAI.ConfigurationStore;
using CXOAI.ConversationStore;
using CXOAI.Memory;
using CXOAI.StatusNotifier;
using InfraService.OpenTelemetryProvider;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.Chat;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace CXOAI.SkillFramework;

/// <summary>
/// Single implementation of every orchestrator pipeline step.
/// Injected into both the console <see cref="SkillOrchestrator"/> and the
/// Azure Durable Functions activities so business logic is never duplicated.
/// </summary>
public class OrchestratorStepService : IOrchestratorStepService
{
    private readonly ITreeConfigurationStoreProvider _configStore;
    private readonly IConversationStore _conversationStore;
    private readonly IMemoryStore? _memoryStore;
    private readonly Dictionary<string, object> _toolInstances;
    private readonly Func<string, Task<string>>? _knowledgeLookup;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<OrchestratorStepService> _logger;
    private readonly ILogger _debugLogger;
    private readonly bool _enableSensitiveData;
    private readonly string _openAIEndpoint;
    private readonly string _primaryModelName;
    private readonly string _secondaryModelName;
    private readonly TokenCredential _credential;
    private readonly IMetricsProvider? _metricsProvider;

    // ── Per-step LLM timeout (seconds) ───────────────────────────────
    // Each step has its own timeout variable so they can be independently
    // tuned and eventually read from configuration.
    private readonly int _classifyIntentTimeoutSeconds = 60;
    private readonly int _answerFromKnowledgeTimeoutSeconds = 60;
    private readonly int _tryAnswerFromHistoryTimeoutSeconds = 60;
    private readonly int _decomposeTasksTimeoutSeconds = 120;
    private readonly int _generateSkillPromptTimeoutSeconds = 60;
    private readonly int _summarizeTimeoutSeconds = 90;
    private readonly int _extractAspectNameTimeoutSeconds = 30;

    private static readonly string SkillSystemPromptSuffix = $"""

        ## User Input Protocol
        If you cannot complete the task because a required parameter is missing
        (e.g., a tool returned an error about a missing value), respond with
        EXACTLY this format:
        {CXOAgentResponse.NeedInputMarker} <your question to the user>

        Example: {CXOAgentResponse.NeedInputMarker} Please provide the TPID for Walmart.

        If you have everything you need, respond normally with your result.
        Do NOT include {CXOAgentResponse.NeedInputMarker} when you have a complete answer.
        """;

    public OrchestratorStepService(
        ITreeConfigurationStoreProvider configStore,
        IConversationStore conversationStore,
        Dictionary<string, object> toolInstances,
        ILoggerFactory loggerFactory,
        string openAIEndpoint,
        string primaryModelName,
        string secondaryModelName,
        TokenCredential credential,
        IMemoryStore? memoryStore = null,
        Func<string, Task<string>>? knowledgeLookup = null,
        IMetricsProvider? metricsProvider = null)
    {
        _configStore = configStore;
        _conversationStore = conversationStore;
        _toolInstances = toolInstances;
        _memoryStore = memoryStore;
        _knowledgeLookup = knowledgeLookup;
        _metricsProvider = metricsProvider;
        _openAIEndpoint = openAIEndpoint;
        _primaryModelName = primaryModelName;
        _secondaryModelName = secondaryModelName;
        _credential = credential;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<OrchestratorStepService>();
        _debugLogger = loggerFactory.CreateLogger("CXOAI.Debug.Sensitive");
        _enableSensitiveData = string.Equals(
            Environment.GetEnvironmentVariable("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT"),
            "true", StringComparison.OrdinalIgnoreCase);
    }

    private AzureOpenAIClient CreateOpenAIClient()
    {
        return new AzureOpenAIClient(new Uri(_openAIEndpoint), _credential);
    }

    private ChatClientAgent CreateInstrumentedAgent(string modelName, string instructions,
        List<AITool>? tools = null, float? temperature = null, long? seed = null)
    {
        var builder = CreateOpenAIClient()
            .GetChatClient(modelName)
            .AsIChatClient()
            .AsBuilder();

        if (temperature.HasValue || seed.HasValue)
        {
            builder = builder.ConfigureOptions(options =>
            {
                if (temperature.HasValue) options.Temperature = temperature.Value;
                if (seed.HasValue) options.Seed = seed.Value;
            });
        }

        var chatClient = builder
            .UseOpenTelemetry(
                _loggerFactory,
                sourceName: "CXOAIFunctions",
                configure: c => c.EnableSensitiveData = _enableSensitiveData)
            .UseLogging(_loggerFactory)
            .Build();

        return chatClient.AsAIAgent(instructions: instructions, tools: tools);
    }

    // ── LLM timeout + exception wrapper ──────────────────────────────
    // All LLM calls in the pipeline go through this helper so timeout
    // and transient-error handling is centralized in one place.

    /// <summary>
    /// Wraps an LLM call with a per-step timeout and exception handling.
    /// Throws <see cref="LlmOperationException"/> on timeout or transient failure.
    /// </summary>
    private async Task<T> RunWithTimeoutAsync<T>(Func<Task<T>> llmCall, string stepName, int timeoutSeconds)
    {
        using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.LlmCall,
            new KeyValuePair<string, object?>(MetricNames.TagStepName, stepName));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            var llmTask = llmCall();
            var delayTask = Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
            var winner = await Task.WhenAny(llmTask, delayTask);

            if (winner == llmTask)
            {
                cts.Cancel();
                var result = await llmTask;
                latency?.SetState(ActivityStatusCode.Ok);
                _metricsProvider?.TrackAvailabilityMetric(MetricNames.LlmCall, 1, null,
                    new KeyValuePair<string, object?>(MetricNames.TagStepName, stepName));
                return result;
            }

            _logger.LogError("LLM call timed out for step '{StepName}' after {TimeoutSeconds}s", stepName, timeoutSeconds);
            latency?.SetState(ActivityStatusCode.Error);
            var timeoutEx = new LlmOperationException(stepName,
                $"LLM call for '{stepName}' timed out after {timeoutSeconds}s.",
                isTimeout: true);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.LlmCall, 1, timeoutEx,
                new KeyValuePair<string, object?>(MetricNames.TagStepName, stepName));
            throw timeoutEx;
        }
        catch (LlmOperationException) { throw; }
        catch (ToolParameterException) { throw; }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogError("LLM call timed out (cancelled) for step '{StepName}' after {TimeoutSeconds}s", stepName, timeoutSeconds);
            latency?.SetState(ActivityStatusCode.Error);
            var timeoutEx = new LlmOperationException(stepName,
                $"LLM call for '{stepName}' timed out after {timeoutSeconds}s.",
                isTimeout: true);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.LlmCall, 1, timeoutEx,
                new KeyValuePair<string, object?>(MetricNames.TagStepName, stepName));
            throw timeoutEx;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            _logger.LogError(ex, "Transient LLM failure in step '{StepName}'", stepName);
            latency?.SetState(ActivityStatusCode.Error);
            var wrappedEx = new LlmOperationException(stepName,
                $"Transient error during '{stepName}': {ex.Message}",
                innerException: ex, isTransient: true);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.LlmCall, 1, wrappedEx,
                new KeyValuePair<string, object?>(MetricNames.TagStepName, stepName));
            throw wrappedEx;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected LLM failure in step '{StepName}'", stepName);
            latency?.SetState(ActivityStatusCode.Error);
            var wrappedEx = new LlmOperationException(stepName,
                $"Unexpected error during '{stepName}': {ex.Message}",
                innerException: ex);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.LlmCall, 1, wrappedEx,
                new KeyValuePair<string, object?>(MetricNames.TagStepName, stepName));
            throw wrappedEx;
        }
    }

    // ?? Tool session setup

    public void SetToolSession(string sessionId)
    {
        foreach (var instance in _toolInstances.Values)
        {
            if (instance is ISessionAware sessionAware)
                sessionAware.SetSession(sessionId);
        }
    }

    public void SetToolContinuationPayload(string? payloadJson)
    {
        if (string.IsNullOrEmpty(payloadJson)) return;

        var payload = Newtonsoft.Json.Linq.JObject.Parse(payloadJson);
        foreach (var instance in _toolInstances.Values)
        {
            if (instance is IContinuationAware continuationAware)
                continuationAware.SetContinuationPayload(payload);
        }
    }

    // ?? Conversation history helper ??????????????????????????????????

    public async Task<string?> GetSessionSummaryAsync(string userId, string sessionId)
    {
        return await _conversationStore.GetSessionSummaryAsync(userId, sessionId);
    }

    /// <summary>Get the most recent N conversation turns for a session.</summary>
    public async Task<List<ConversationTurnEntry>?> GetRecentHistoryAsync(string userId, string sessionId, int lastN = 5)
    {
        return await _conversationStore.GetSessionHistoryAsync(userId, sessionId, lastN);
    }

    // ?? Step 1: Enhance Prompt ???????????????????????????????????????

    public async Task<EnhancePromptResult> EnhancePromptAsync(string userId, string sessionId, string prompt, UserContext? userContext)
    {
        _logger.LogInformation("EnhancePromptAsync: sessionId={SessionId}", sessionId);
        _debugLogger.LogDebug("EnhancePromptAsync: userId={UserId}", userId);

        var sb = new StringBuilder();
        sb.AppendLine($"# UserQuery:\n **{prompt}**");

        // Inject UI context (entity, filters) with priority instructions — BEFORE UserPreference
        // so the LLM sees current page state as the most immediate context.
        if (userContext is not null)
        {
            var hasEntity = !string.IsNullOrEmpty(userContext.EntityName) || !string.IsNullOrEmpty(userContext.EntityId);
            var hasFilters = userContext.GlobalLevelFilters?.Any(f => f.SelectedValues?.Count > 0) == true;

            if (hasEntity || hasFilters)
            {
                var ctxBlock = new StringBuilder();
                ctxBlock.AppendLine("# UI Context (PRIORITY)");
                ctxBlock.AppendLine("> INSTRUCTION: Use the entity and filters below as defaults for all data queries.");
                ctxBlock.AppendLine("> If the user's prompt explicitly mentions a DIFFERENT entity or filter, use the user's explicit request instead.");
                ctxBlock.AppendLine("> If the user's prompt does not mention any entity or filter, use these defaults.");

                if (hasEntity)
                {
                    ctxBlock.AppendLine($"- Entity Name: {userContext.EntityName}");
                    if (!string.IsNullOrEmpty(userContext.EntityId))
                        ctxBlock.AppendLine($"- Entity ID: {userContext.EntityId}");
                    if (!string.IsNullOrEmpty(userContext.EntityType))
                        ctxBlock.AppendLine($"- Entity Type: {userContext.EntityType}");
                }

                if (hasFilters)
                {
                    ctxBlock.AppendLine("- Active Filters:");
                    foreach (var f in userContext.GlobalLevelFilters!)
                    {
                        if (f.SelectedValues?.Count > 0)
                            ctxBlock.AppendLine($"  - {f.UIFilterName} ({f.BackendFilterName}) = {string.Join(", ", f.SelectedValues)}");
                    }
                }

                sb.AppendLine(ctxBlock.ToString());
                _logger.LogInformation("EnhancePromptAsync: Injected UI context — Entity={Entity}, Filters={FilterCount}",
                    userContext.EntityName ?? "none", userContext.GlobalLevelFilters?.Count ?? 0);
            }
        }

        sb.AppendLine($"# UserPreference:\n {await GetUserPreferenceAsync(userId, prompt)}");

        // Inject short-term session context from last N conversation turns (raw Q&A pairs)
        // instead of the LLM-generated rolling summary. This preserves exact data values
        // and avoids lossy summarization that drops metric details on follow-up turns.
        _logger.LogInformation("Calling GetSessionHistory for userId={UserId}, sessionId={SessionId}, lastN=5", userId, sessionId);
        var recentHistory = await _conversationStore.GetSessionHistoryAsync(userId, sessionId, lastN: 5);
        _logger.LogInformation("Called GetSessionHistory, retrieved {Count} turn(s)", recentHistory?.Count ?? 0);
        if (recentHistory is { Count: > 0 })
        {
            for (int i = 0; i < recentHistory.Count; i++)
            {
                var t = recentHistory[i];
                _logger.LogInformation("  History Turn {Idx}: RequestId={RequestId}, Timestamp={Timestamp}, PromptLength={PromptLen}, ResponseLength={ResponseLen}",
                    i + 1, t.RequestId, t.Timestamp, t.Prompt.Length, t.Response.Length);
            }
        }
        string? sessionSummary = null; // kept for knowledge graph fallback below
        if (recentHistory is { Count: > 0 })
        {
            var historyBlock = new StringBuilder();
            historyBlock.AppendLine("# SessionContext (recent conversation turns):");
            for (int i = 0; i < recentHistory.Count; i++)
            {
                var turn = recentHistory[i];
                historyBlock.AppendLine($"## Turn {i + 1} ({turn.Timestamp})");
                historyBlock.AppendLine($"**User**: {turn.Prompt}");
                historyBlock.AppendLine($"**Assistant**: {turn.Response}");
                historyBlock.AppendLine();
            }

            // Inject Org-scoped memory facts (shared data cache) for cross-session data continuity
            if (_memoryStore is not null)
            {
                _logger.LogInformation("Calling MemoryRecall (Org scope) for query: {Query}", prompt[..Math.Min(200, prompt.Length)]);
                var orgFacts = await _memoryStore.RecallAsync(MemoryConstants.OrgUserId, prompt, topK: 10, minScore: 0.65f, scope: MemoryScope.Org);
                _logger.LogInformation("Called MemoryRecall (Org scope), retrieved {Count} fact(s): [{Facts}]",
                    orgFacts.Count, string.Join(" | ", orgFacts.Select(f => f.Fact[..Math.Min(80, f.Fact.Length)])));
                if (orgFacts.Count > 0)
                {
                    historyBlock.AppendLine("## Cached Data (from previous queries across sessions):");
                    foreach (var fact in orgFacts)
                        historyBlock.AppendLine($"- {fact.Fact}");
                }
            }

            sb.AppendLine(historyBlock.ToString());
            _logger.LogInformation("EnhancePromptAsync: Injected {Count} recent history turns", recentHistory.Count);

            // Build a compact summary string for the knowledge graph fallback below
            sessionSummary = string.Join(" | ", recentHistory.Select(t => $"{t.Prompt}: {t.Response[..Math.Min(200, t.Response.Length)]}"));
        }
        else
        {
            // No history turns yet — check for rolling summary as fallback (first-turn edge case)
            sessionSummary = await _conversationStore.GetSessionSummaryAsync(userId, sessionId);
            if (!string.IsNullOrWhiteSpace(sessionSummary))
            {
                sb.AppendLine($"# SessionContext (previous conversation in this session):\n{sessionSummary}");
                _logger.LogInformation("EnhancePromptAsync: Injected session summary fallback ({Length} chars)", sessionSummary.Length);
            }
        }

        // NOTE: UI context facts (entity, filters) are NOT stored in long-term memory.
        // They are Temporal (navigation state) and would be dropped by Layer 2 anyway.
        // Context reaches the LLM via the prompt injection above and persists in the
        // ConversationStore rolling summary for follow-up turns within the session.

        var swKnowledge = System.Diagnostics.Stopwatch.StartNew();
        var systemKnowledge = _knowledgeLookup is not null
            ? await _knowledgeLookup(prompt)
            : string.Empty;

        // Fallback: if no domain knowledge found for the current prompt but session
        // context exists (follow-up query like "add subscription filter"), re-query
        // with session context appended so the knowledge graph LLM can match metrics
        // mentioned in previous turns (e.g., "FDR" from Turn 1's summary).
        if (!systemKnowledge.Contains("##")
            && !string.IsNullOrWhiteSpace(sessionSummary)
            && _knowledgeLookup is not null)
        {
            _logger.LogInformation("EnhancePrompt: No domain knowledge for direct query, retrying with session context");
            systemKnowledge = await _knowledgeLookup(
                $"{prompt} (context from previous conversation: {sessionSummary})");

            if (systemKnowledge.Contains("##"))
                _logger.LogInformation("EnhancePrompt: Session-context fallback matched domain knowledge");
            else
                _logger.LogInformation("EnhancePrompt: Session-context fallback also found no matches");
        }

        swKnowledge.Stop();
        _logger.LogInformation("EnhancePrompt: Knowledge graph lookup completed in {ElapsedMs}ms", swKnowledge.ElapsedMilliseconds);
        sb.AppendLine(systemKnowledge);

        var result = new EnhancePromptResult
        {
            EnhancedPrompt = sb.ToString(),
            GeneralKnowledge = systemKnowledge
        };

        _debugLogger.LogDebug("EnhancePromptAsync output: {EnhancedPrompt}", result.EnhancedPrompt);
        return result;
    }

    private static readonly string[] ActionVerbs = ["export", "send", "email", "generate", "create", "download"];

    private static bool IsActionQuery(string prompt)
    {
        return ActionVerbs.Any(v => prompt.Contains(v, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> GetUserPreferenceAsync(string userId, string prompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Current userId is {userId}");

        if (_memoryStore is not null)
        {
            // ── User scope: preferences only (Permanent, per-user) ──
            _logger.LogInformation("Calling MemoryRecall (User scope) for userId={UserId}, query: {Query}",
                userId, prompt[..Math.Min(200, prompt.Length)]);
            var userFacts = await _memoryStore.RecallAsync(userId, prompt, topK: 10, minScore: 0.65f, scope: MemoryScope.User);
            _logger.LogInformation("Called MemoryRecall (User scope), retrieved {Count} fact(s): [{Facts}]",
                userFacts.Count, string.Join(" | ", userFacts.Select(f => f.Fact[..Math.Min(80, f.Fact.Length)])));

            if (userFacts.Count > 0)
            {
                sb.AppendLine("## Known preferences for this user:");
                foreach (var fact in userFacts)
                    sb.AppendLine($"- {fact.Fact}");
            }

            // ── Org scope: shared data cache (Temporal, cross-user, cross-session) ──
            // For action queries, fetch ALL org facts so the pipeline has data to act on.
            // For normal queries, use semantic recall to find relevant cached data.
            List<MemoryFact> orgFacts;
            if (IsActionQuery(prompt))
            {
                _logger.LogInformation("Calling MemoryGetAllFacts (Org scope, action query)");
                orgFacts = await _memoryStore.GetAllFactsAsync(MemoryConstants.OrgUserId, MemoryScope.Org);
                _logger.LogInformation("Called MemoryGetAllFacts (Org scope), retrieved {Count} fact(s): [{Facts}]",
                    orgFacts.Count, string.Join(" | ", orgFacts.Select(f => f.Fact[..Math.Min(80, f.Fact.Length)])));
            }
            else
            {
                _logger.LogInformation("Calling MemoryRecall (Org scope, preference) for query: {Query}",
                    prompt[..Math.Min(200, prompt.Length)]);
                orgFacts = await _memoryStore.RecallAsync(MemoryConstants.OrgUserId, prompt, topK: 10, minScore: 0.65f, scope: MemoryScope.Org);
                _logger.LogInformation("Called MemoryRecall (Org scope, preference), retrieved {Count} fact(s): [{Facts}]",
                    orgFacts.Count, string.Join(" | ", orgFacts.Select(f => f.Fact[..Math.Min(80, f.Fact.Length)])));
            }

            if (orgFacts.Count > 0)
            {
                sb.AppendLine("## Cached data (shared across sessions):");
                foreach (var fact in orgFacts)
                    sb.AppendLine($"- {fact.Fact}");
            }
        }

        return sb.ToString();
    }

    // ?? Step 1a: Check History ????????????????????????????????????????

    public async Task<HistoryAnswerResult> TryAnswerFromHistoryAsync(string prompt, string? sessionSummary, string? uiContextEntityName = null)
    {
        var noResult = new HistoryAnswerResult();

        if (string.IsNullOrWhiteSpace(sessionSummary))
            return noResult;

        var historyText = sessionSummary;

        var systemPrompt = """
            You decide if a question can be answered using previous session summaries.

            ## STEP 1: Check for Placeholders
            Before anything else, scan every summary for placeholder patterns:
            [value], [number], [score], [N/A], TBD, N/A, or any text in square brackets instead of a real number.
            If you find ANY placeholder — that metric has NO real data. Proceed to Step 2.

            ## STEP 2: Decide CanAnswer
            Set CanAnswer = true ONLY when ALL conditions are met:
            1. The summary contains the EXACT metric(s) the user is asking about.
            2. Each metric has a CONCRETE NUMERIC VALUE (e.g., 72.45, 1234, 98.7%) or a complete data series.
            3. ALL parts of the question are answerable from the summaries — including trend data,
               analysis, explanations, and causal reasoning if previously generated.
            4. The **time range, filters, and parameters match**. If the user asks for "last 6 months"
               but the summary only has data for "last 3 months", that is NOT a match — CanAnswer = false.
               If the summary does not mention a time range but the user specifies one — CanAnswer = false.
            Set CanAnswer = false if ANY of these conditions are met:
            1. user asks for visualization, export, why, correlation, or action on data that is present in the summary but has NO concrete values (e.g., "CSAT: [value]").
            2. user asks for latest data, or ignore the history, or explicitly states they want fresh data.
            3. user asks to "visualize" or "show chart" and the summary contains ONLY prose/text analysis (no raw monthly data series). Visualization requires raw {label,value} arrays — prose summaries are NOT sufficient.
            ### Compound queries (multiple parts in one question):
            - If the user asks "show me X AND why is it trending" and the summary contains BOTH
              the data values AND the trend analysis/explanation — CanAnswer = true.
              Combine both parts into a single Answer.
            - If the summary has the data but NOT the analysis (or vice versa) — CanAnswer = false.
            - The key test: would re-running the query produce MATERIALLY different output?
              If the summary already has the complete answer from a previous identical query — CanAnswer = true.

            If even ONE condition fails — CanAnswer = false. No exceptions.

            ## STEP 3: Decide HasRelevantContext
            Set HasRelevantContext = true when EITHER of these is true:
            A) The user requests an ACTION (export, email, format) on data that HAS concrete values in the summaries.
               → Extract the actual data into RelevantContext.
            B) The summaries mention metric NAMES that match the user's query but values are placeholders or missing.
               → Extract ONLY the metric names formatted in bold into RelevantContext.
               → Example: "Previously discussed metrics: **CSAT**, **Average Aging**"
               → This tells the pipeline which metrics are relevant without claiming we have data.
            C) The summary has PARTIAL answers (e.g., data values exist but analysis is missing).
               → Set HasRelevantContext = true, extract the available data into RelevantContext
                 so the pipeline can skip re-fetching and only generate the missing parts.

            ## STEP 4: Both False
            Set both CanAnswer and HasRelevantContext to false when:
            - The user asks for a completely different metric than what's in the summaries.
            - The summaries are about a different entity/customer.
            - There is no overlap between the user's request and the summary content.

            ## Examples
            History: "CSAT: 72.45 for Walmart" → User: "what was the csat?"
            → CanAnswer=true, Answer="CSAT for Walmart: 72.45", HasRelevantContext=true

            History: "CSAT trend for Walmart last 6 months: Oct=72, Nov=68, Dec=71, Jan=75, Feb=73, Mar=69. Analysis: CSAT dropped in Nov due to increased case volume." → User: "What does CSAT look like for Walmart over last 6 months, why is it trending that way"
            → CanAnswer=true, Answer="CSAT trend for Walmart...[full data + analysis]", HasRelevantContext=true

            History: "CSAT trend for Walmart last 6 months: Oct=72, Nov=68, Dec=71, Jan=75, Feb=73, Mar=69" (no analysis) → User: "What does CSAT look like for Walmart over last 6 months, why is it trending that way"
            → CanAnswer=false, HasRelevantContext=true, RelevantContext="CSAT trend data available: Oct=72, Nov=68, Dec=71, Jan=75, Feb=73, Mar=69. Analysis/explanation not available — needs fresh generation."

            History: "CSAT: [value], Average Aging: [value] for Walmart" → User: "show csat and aging"
            → CanAnswer=false, HasRelevantContext=true, RelevantContext="Previously discussed metrics: **CSAT**, **Average Aging**"

            History: "CSAT: 72.45 for Walmart" → User: "export to word"
            → CanAnswer=false, HasRelevantContext=true, RelevantContext="CSAT: 72.45 for Walmart"

            History: "CSAT: 72.45 for Walmart" → User: "show me revenue"
            → CanAnswer=false, HasRelevantContext=false (different metric entirely)

            History: "CSAT: 72.45 for Walmart, last 3 months" → User: "show csat for walmart for last 6 months"
            → CanAnswer=false, HasRelevantContext=false (different time range — must re-fetch)
            """;

        var entityHint = !string.IsNullOrEmpty(uiContextEntityName)
            ? $"\n\n## UI Context\nThe user is currently viewing: **{uiContextEntityName}**. Prioritize data for this entity when answering."
            : string.Empty;
        var userPrompt = $"## Previous Session Summaries\n{historyText}{entityHint}\n\n## User Question\n{prompt}";

        var agent = CreateInstrumentedAgent(_secondaryModelName, systemPrompt);

        _logger.LogInformation("Calling TryAnswerFromHistory with below userPrompt: {UserPrompt}", userPrompt);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await RunWithTimeoutAsync(
            async () => await agent.RunAsync<HistoryAnswerResult>(userPrompt, await agent.CreateSessionAsync()),
            "TryAnswerFromHistory", _tryAnswerFromHistoryTimeoutSeconds);
        sw.Stop();
        var result = response.Result;
        _logger.LogInformation("Called TryAnswerFromHistory ({ElapsedMs}ms), here is response: CanAnswer={CanAnswer}, HasRelevantContext={HasContext}, AnswerLength={AnswerLen}",
            sw.ElapsedMilliseconds, result.CanAnswer, result.HasRelevantContext, result.Answer?.Length ?? 0);

        if (result.CanAnswer && !string.IsNullOrWhiteSpace(result.Answer))
        {
            _logger.LogInformation("TryAnswerFromHistoryAsync: Answered from history, answerLength={Length}", result.Answer.Length);
            _debugLogger.LogDebug("TryAnswerFromHistoryAsync answer: {AnswerPreview}", result.Answer[..Math.Min(200, result.Answer.Length)]);
        }
        else if (result.HasRelevantContext && !string.IsNullOrWhiteSpace(result.RelevantContext))
        {
            _logger.LogInformation("TryAnswerFromHistoryAsync: Has relevant context, length={Length}", result.RelevantContext.Length);
            _debugLogger.LogDebug("TryAnswerFromHistoryAsync context: {ContextPreview}", result.RelevantContext[..Math.Min(200, result.RelevantContext.Length)]);
        }
        else
        {
            _logger.LogInformation("TryAnswerFromHistoryAsync: No relevant history found");
        }

        return result;
    }

    // ?? Step 2: Classify Intent ??????????????????????????????????????

    public async Task<UserIntent> ClassifyIntentAsync(string prompt, string generalKnowledge, string? uiContextEntityName = null)
    {
        var hasKnowledge = generalKnowledge.Contains("##");
        var hasUIEntity = !string.IsNullOrEmpty(uiContextEntityName);

        var systemPrompt = $"""
            # Intent Classifier

            ## Role
            Classify the user's query into one of three intents.

            ## Intents
            - **Informational**: The user is asking a definition, explanation, or conceptual question.
              Examples: "what does csat mean?", "explain air reboot rate", "what is cfr?", "how is revenue calculated?"
            - **DataAction**: The user wants to retrieve data, run a metric, generate a report, or perform an action.
              Examples: "show me csat of walmart", "export revenue to excel", "get incidents for last month"
            - **Unknown**: The query is nonsensical, unrelated to the domain, contains random text, or you cannot determine what the user wants.
              Examples: "abc, pqr", "asdfgh", "hello world", "test 123", random characters or words with no clear intent

            ## Context
            Domain knowledge was {(hasKnowledge ? "found" : "NOT found")} for this query.
            {(hasUIEntity ? $"The user is currently viewing the entity page for: **{uiContextEntityName}**. This counts as an implicit entity reference." : "No entity is selected in the UI.")}

            ## Rules
            - **HIGHEST PRIORITY — Mixed-content queries are ALWAYS Unknown**: If the query contains
              BOTH domain-related content (metrics, entity names, data requests) AND content that is
              clearly unrelated to the support/data/engineering domain (e.g., cooking recipes, sports,
              weather, personal advice, trivia, jokes, or any off-topic subject), classify as **Unknown**.
              The presence of a valid domain query does NOT redeem the unrelated content.
              A well-formed query must be ENTIRELY about the domain — any off-topic contamination
              makes the entire query Unknown.
              Examples:
                - "show me csat for walmart and how to make chicken curry" → **Unknown**
                - "get incidents for last month also tell me about the weather" → **Unknown**
                - "export revenue to excel and what is the capital of France" → **Unknown**
                - "csat trend for contoso and best pizza recipe" → **Unknown**
              Counter-examples (these are NOT mixed — all parts are domain-related):
                - "show me csat and irmet for walmart" → **DataAction** (both are domain metrics)
                - "get incidents and export to word" → **DataAction** (data + domain action)
            - If the query asks "what is", "what does", "explain", "define", "how is X calculated", "tell me about"
              **without mentioning a specific customer, entity, or company name AND no entity is selected in the UI** → **Informational**
            - If the query mentions a **customer/entity name** (e.g., Walmart, Contoso, Microsoft) it is always **DataAction**,
              even if phrased as "what is X for <entity>" — the entity makes it a data lookup, not a definition.
            - **If an entity is selected in the UI** (see Context above), treat metric queries as **DataAction** — the user
              is asking about that entity's data, not asking for a definition.
              Examples (when UI entity is selected):
                - "what is csat" → **DataAction** (user is asking for the entity's CSAT, not a definition)
                - "show me aging" → **DataAction** (user wants the entity's aging data)
            - If the query asks "show me", "get", "give me", or requests specific data → **DataAction**
            - **Action verbs are ALWAYS DataAction**: If the query contains "document", "doc", "export", "send", "email", "generate","visualize","summarize", "correlation", "create"
              "create", "download" — it is **DataAction** regardless of whether the user specifies what data.
              The user is referring to data from prior conversation. NEVER classify these as Unknown.
              **Exception**: This rule is OVERRIDDEN by the mixed-content rule above. If the query has an
              action verb but ALSO contains off-domain content, it is **Unknown**.
            - **Follow-up / modification queries are ALWAYS DataAction**: If domain knowledge was found
              (meaning session context matched a metric) and the query modifies, refines, or adds filters
              to a previous request (e.g., "add subscription filter", "break it down by region",
              "filter by severity A", "show it monthly", "change to last 3 months"), classify as **DataAction**.
              These are modifications of a previous data query, NOT new standalone questions.
            - If the query is gibberish, random text, unrelated to support/data domain, or has no clear intent → **Unknown**
            - When ambiguous between meaningful and nonsensical, prefer **Unknown**
            - NEVER classify a query containing an action verb (export, send, email, generate, visualize) as Unknown
              **UNLESS** the mixed-content rule above applies.
            """;
        
        var newPrompt = $"""
            ## User Query
            {prompt}
            ## General Knowledge
            {generalKnowledge}
            """;

        var agent = CreateInstrumentedAgent(_secondaryModelName, systemPrompt);

        _logger.LogInformation("Calling ClassifyIntent with below userPrompt: {UserPrompt}", newPrompt);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await RunWithTimeoutAsync(
            async () => await agent.RunAsync<UserIntent>(newPrompt, await agent.CreateSessionAsync()),
            "ClassifyIntent", _classifyIntentTimeoutSeconds);
        sw.Stop();
        var intent = response.Result;
        _logger.LogInformation("Called ClassifyIntent ({ElapsedMs}ms), here is response: Intent={Intent}, Reasoning={Reasoning}",
            sw.ElapsedMilliseconds, intent.Intent, intent.Reasoning);
        return intent;
    }

    // ?? Step 2a: Answer From Knowledge ???????????????????????????????

    public async Task<string> AnswerFromKnowledgeAsync(string prompt, string generalKnowledge)
    {
        var hasDomainKnowledge = generalKnowledge.Contains("##");

        var systemPrompt = $"""
            # Domain Knowledge Assistant

            ## Role
            Answer the user's question accurately and helpfully. Use markdown formatting.

            ## Knowledge Sources (in priority order)
            1. **Domain Knowledge** (provided below) � treat as the authoritative source.
            2. **General Knowledge** � use your training knowledge to supplement when domain knowledge is incomplete.

            ## Rules
            - Always start from the domain knowledge when it covers the topic.
            - If domain knowledge is partial, supplement with your general knowledge and clearly indicate which parts come from general knowledge.
            - Do NOT fabricate specific data values, metrics, or statistics.
            - Include relevant aliases/tags and relationships from domain knowledge if they help the explanation.
            - If domain knowledge is not available for the topic at all, answer from general knowledge and note that no organization-specific definition was found.

            {(hasDomainKnowledge ? $"## Domain Knowledge\n{generalKnowledge}" : "## Domain Knowledge\nNo domain-specific knowledge available for this query.")}
            """;

        var newPrompt = $"""
            ## User Question
            {prompt}
            """;

        var agent = CreateInstrumentedAgent(_secondaryModelName, systemPrompt);

        _logger.LogInformation("Calling AnswerFromKnowledge with below userPrompt: {UserPrompt}", newPrompt);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await RunWithTimeoutAsync(
            async () => await agent.RunAsync(newPrompt, await agent.CreateSessionAsync()),
            "AnswerFromKnowledge", _answerFromKnowledgeTimeoutSeconds);
        sw.Stop();
        var answer = response.Text ?? string.Empty;
        _logger.LogInformation("Called AnswerFromKnowledge ({ElapsedMs}ms), here is response: {Response}",
            sw.ElapsedMilliseconds, answer);
        return answer;
    }

    // ── Step 2b: Decompose Tasks (Task Planner) ────────────────────

    private const int MaxPlannerRetries = 3;

    public async Task<List<TaskPlanItem>> DecomposeTasksAsync(string enhancedPrompt, string originalPrompt)
    {
        // ── Step 2b-1: Load available skill descriptions ──
        var skillConfigs = await _configStore.GetConfigurations("Skill", false);
        var knownSkills = new HashSet<string>(
            skillConfigs.Select(c => c.ConfigurationName), StringComparer.OrdinalIgnoreCase);

        var skillDescBlock = new StringBuilder();
        skillDescBlock.AppendLine("## Available Skills");
        foreach (var cfg in skillConfigs.OrderBy(c => c.ConfigurationName))
        {
            skillDescBlock.AppendLine($"- **{cfg.ConfigurationName}**: {cfg.Description}");
        }

        

        var plannerSystemPrompt = $$"""
            # Task Planner Agent

            You decompose a user prompt into a structured task plan.
            You decide WHICH skills to call, HOW MANY instances, and WHAT ORDER.
            You do NOT write sub-prompts — that happens in a separate step.

            {{skillDescBlock}}

            ## Rules
            1. **SkillName** must be one of the Available Skills above. Do NOT invent skills.
            2. **DependsOn** contains zero-based indices of tasks that must complete before this task can run.
               AspectSkill tasks MUST always have DependsOn: [] — they are independent data-fetching tasks
               that run in parallel and NEVER depend on each other.
               Other skills should list the indices of tasks they depend on.
            3. **Task** is a short human-readable label (e.g., "Get CSAT score for Walmart").
            4. **Critical**:While creating **Task** DO NOT MISS any time ranges, conditions, or qualifiers the user specified.
            5. Each data-fetching task should call EXACTLY ONE metric/aspect. If the user needs
               multiple metrics, create separate tasks for each.
            6. **Simple retrieval — DO NOT expand relationships:**
               When the user asks a straightforward data question (e.g., "what is csat", "show me csat",
               "get csat for walmart", "csat over the last 30 days"), create ONLY the task for the
               requested metric. Do NOT create tasks for related metrics listed in the Relationships
               section. The presence of relationships in the Domain Knowledge does NOT mean they should
               be fetched — they are contextual metadata only.
            7. **Analysis / Why / Root cause — MANDATORY relationship expansion:**
               ONLY when the user EXPLICITLY asks for analysis, explanation, or causation using phrases
               like "why is it trending", "what is driving", "root cause", "explain the trend",
               "what factors impact", or "analyze why" — THEN you MUST expand the Relationships section.
               For EVERY relationship listed under the primary metric, create a SEPARATE data-fetching task.
               Do NOT skip any. Do NOT merge similar ones. If the Domain Knowledge shows 5 relationships,
               you must create 5 additional data-fetching tasks (one per relationship) PLUS the primary metric task.
               Count the relationships and verify your task count matches.
            8. **Skill routing — AspectSkill vs NLTKqlSkill:**
               - **AspectSkill**: ONLY for predefined metric names (CSAT, IRMET, FDR, TTM, Incident Volume,
                 Aging, Customer Summary, etc.) with a single entity. If the Domain Knowledge contains a
                 matching aspect, use AspectSkill.
               - **NLTKqlSkill**: Use for ICM/incident analysis (any ICM ID reference), cross-entity queries
                 (which customers were impacted, related support tickets), blast radius analysis, and any
                 query that does NOT map to a known aspect/metric name. When a user query has multiple
                 related sub-questions about the same incident or event (e.g., "impacted customers + related
                 tickets + recommendations"), use a SINGLE NLTKqlSkill task — it handles multi-part queries
                 internally via query decomposition.
               - If unsure, check: does the query reference a specific ICM/incident ID or ask about
                 cross-entity impact? → NLTKqlSkill. Does it ask for a named metric for an entity? → AspectSkill.
            9. **Sibling tasks — do NOT chain independent consumers:**
               When the user asks for multiple INDEPENDENT operations on the same data
               (e.g., "visualize AND export to word", "show chart AND generate report"),
               each consumer task should depend on the DATA SOURCE task, NOT on each other.
               The visualization does NOT produce the export — both consume the same upstream data.
               **Test:** Ask yourself — does Task B need the OUTPUT of Task A to do its job?
               - "Export to Word" needs the DATA, not the chart → depends on the data task, not the visualization task.
               - "Summarize data" needs the DATA, not the chart → depends on the data task, not the visualization task.
               - Only chain when the downstream task genuinely consumes the upstream task's output
                 (e.g., SummarizationSkill after multiple AspectSkill tasks that all feed into it).
            10. **Multi-topic queries — keep consumer tasks scoped to their data source:**
                When the user asks about MULTIPLE independent topics in one prompt (e.g., "get FDR and
                visualize it, also get ICM impact and export to word"), each consumer/action task must
                depend ONLY on the data source it acts upon — NOT on unrelated data sources.
                Parse "visualize it" and "export to word" to determine which specific data each refers to.
                - "visualize it" after mentioning FDR → UXGeneratorSkill depends on the FDR AspectSkill task only.
                - "export it to word" after mentioning ICM → ReportingSkill depends on the NLTKqlSkill task only.
                Do NOT merge all data sources into one consumer. Do NOT chain consumers across topics.

            ### Examples of when to expand vs. not expand relationships:
            - "what is csat for walmart over the last 30 days" → 1 AspectSkill task (CSAT only). NO relationship expansion.
            - "what is csat for walmart and export to word" → 2 tasks: 1 AspectSkill (CSAT) + 1 ReportingSkill. NO relationship expansion.
            - "why is csat trending down for walmart" → 1 + N tasks: 1 AspectSkill (CSAT) + 1 AspectSkill per relationship + 1 SummarizationSkill.
            - "what factors are impacting csat" → same as above — explicit analysis request triggers expansion.

            ### Examples of AspectSkill vs NLTKqlSkill routing:
            - "show me csat for walmart" → 1 AspectSkill (known metric, single entity)
            - "For ICM 1234, how many S500 customers were impacted?" → 1 NLTKqlSkill (ICM ID, cross-entity query)
            - "For ICM 1234, impacted customers + related tickets + recommendations" → 1 NLTKqlSkill + 1 SummarizationSkill (NOT 3 AspectSkill tasks)
            - "which support tickets were opened last week" → 1 NLTKqlSkill (no predefined aspect)

            ### Examples of sibling vs. chained dependencies:
            - "visualize csat and export to word" →
              Task 0: AspectSkill (DependsOn: []), Task 1: UXGeneratorSkill (DependsOn: [0]), Task 2: ReportingSkill (DependsOn: [0])
              Both Task 1 and 2 depend on Task 0, NOT on each other.
            - "can you please visualize it and export to word document" (AFTER a session that already fetched raw metric data) →
              Task 0: UXGeneratorSkill (DependsOn: []), Task 1: ReportingSkill (DependsOn: [])
              Both are independent actions on prior session data — ONLY when raw chart data ({label,value} arrays) is available in session history.
            - "can you visualize it" (AFTER a session that only produced a text summary with NO raw chart data) →
              MUST re-fetch the data. Create AspectSkill tasks for the relevant metrics, then UXGeneratorSkill depending on them.
              Example: Task 0: AspectSkill (Get CSAT trend, DependsOn: []), Task 1: UXGeneratorSkill (DependsOn: [0])
              Do NOT emit UXGeneratorSkill (DependsOn: []) when session only has prose/text — it will have no numeric data to chart.
            - "get csat, then summarize it, then export the summary" →
              Task 0: AspectSkill (DependsOn: []), Task 1: SummarizationSkill (DependsOn: [0]), Task 2: ReportingSkill (DependsOn: [1])
              This IS a chain — export genuinely needs the summary output.

            ### Examples of multi-topic queries with scoped consumers:
            - "get FDR trend and visualize it, also get customers impacted by ICM 1234 and export to word" →
              Task 0: AspectSkill (FDR, DependsOn: []), Task 1: NLTKqlSkill (ICM, DependsOn: []),
              Task 2: UXGeneratorSkill (DependsOn: [0]), Task 3: ReportingSkill (DependsOn: [1])
              UXGen depends ONLY on FDR. Report depends ONLY on ICM. They are independent groups.
            - "get csat and fdr, why is csat trending, visualize everything and export to word" →
              Task 0: AspectSkill (CSAT, DependsOn: []), Task 1-N: AspectSkill (relationships, DependsOn: []),
              Task N+1: AspectSkill (FDR, DependsOn: []),
              Task N+2: SummarizationSkill (DependsOn: [0..N+1]),
              Task N+3: UXGeneratorSkill (DependsOn: [N+2]), Task N+4: ReportingSkill (DependsOn: [N+2])
              Both consumers depend on the summary, NOT on raw data.

            ## Output Format
            Return a JSON array of objects with ONLY these fields:

            ```json
            [
              {"Task": "Get CSAT score", "SkillName": "AspectSkill", "DependsOn": []},
              {"Task": "Summarize trends", "SkillName": "SummarizationSkill", "DependsOn": [0]},
              {"Task": "Export to Word", "SkillName": "ReportingSkill", "DependsOn": [1]}
            ]
            ```
            Do NOT include PromptToSend — that is generated separately.
            """;


        var userPrompt = $"""
            ## Enhanced Prompt
            {enhancedPrompt}

            ## Original User Query
            {originalPrompt}
            """;

        // Retry loop: if the plan has validation errors, retry with error feedback
        var validator = new PlanValidator(_logger);
        List<PlannerTaskItem> plannerPlan = [];
        List<string> validationErrors = [];

        for (int attempt = 1; attempt <= MaxPlannerRetries; attempt++)
        {
            var attemptPrompt = attempt == 1
                ? userPrompt
                : $"{userPrompt}\n\n## Previous Plan Errors (attempt {attempt})\nYour previous plan had these errors — fix them:\n{string.Join("\n- ", validationErrors)}";

            var agent = CreateInstrumentedAgent("gpt-4o", plannerSystemPrompt, temperature: 0f, seed: 42);
            _logger.LogInformation("Calling DecomposeTasks (attempt {Attempt}) with below userPrompt: {UserPrompt}", attempt, attemptPrompt);
            var swPlanner = System.Diagnostics.Stopwatch.StartNew();
            var response = await RunWithTimeoutAsync(
                async () => await agent.RunAsync<List<PlannerTaskItem>>(attemptPrompt, await agent.CreateSessionAsync()),
                $"DecomposeTasks(attempt {attempt})", _decomposeTasksTimeoutSeconds);
            swPlanner.Stop();
            plannerPlan = response.Result ?? [];
            _logger.LogInformation("Called DecomposeTasks (attempt {Attempt}, {ElapsedMs}ms), here is response: {TaskCount} task(s): [{Tasks}]",
                attempt, swPlanner.ElapsedMilliseconds, plannerPlan.Count,
                string.Join(", ", plannerPlan.Select(t => $"{t.Task}({t.SkillName})")));

            validationErrors = validator.Validate(plannerPlan, knownSkills);
            if (validationErrors.Count == 0)
            {
                _logger.LogInformation("DecomposeTasksAsync: Planner succeeded on attempt {Attempt}, {Count} tasks",
                    attempt, plannerPlan.Count);
                break;
            }

            _logger.LogWarning("DecomposeTasksAsync: Planner attempt {Attempt} had {ErrorCount} validation errors",
                attempt, validationErrors.Count);
        }

        // If still errors after all retries, log and continue with whatever we have
        if (validationErrors.Count > 0)
        {
            _logger.LogError("DecomposeTasksAsync: Plan still invalid after {MaxRetries} attempts. Errors: [{Errors}]",
                MaxPlannerRetries, string.Join("; ", validationErrors));
            // Remove tasks with unknown skills as best-effort cleanup
            plannerPlan.RemoveAll(t => !knownSkills.Contains(t.SkillName));
        }

        _debugLogger.LogDebug("DecomposeTasksAsync planner output: {Plan}", JsonConvert.SerializeObject(plannerPlan));

        // Convert to TaskPlanItem (PromptToSend left empty — filled at execution time via GenerateSkillPromptAsync)
        var finalPlan = plannerPlan.Select(p => new TaskPlanItem
        {
            Task = p.Task,
            SkillName = p.SkillName,
            DependsOn = p.DependsOn
        }).ToList();

        // Deterministic fix: ensure consumer skills (UXGenerator, Reporting) never
        // depend on each other — rewire to their shared data source instead.
        // This catches cases where the LLM planner ignores Rule 9.
        PlanValidator.FixSiblingDependencies(finalPlan, _logger);

        // Derive group numbers from DAG connected components
        PlanGrouper.AssignGroups(finalPlan);

        // Log plan structure at Information level for test observability
        foreach (var (t, idx) in finalPlan.Select((t, i) => (t, i)))
        {
            _logger.LogInformation("PLAN_STRUCTURE idx={Idx} Skill={Skill} Group={Group} DependsOn=[{Deps}] Task={TaskLabel}",
                idx, t.SkillName, t.Group, string.Join(",", t.DependsOn), t.Task);
        }

        _logger.LogInformation("DecomposeTasksAsync: completed, {Count} task(s) in {Groups} group(s)",
            finalPlan.Count, finalPlan.Select(t => t.Group).Distinct().Count());

        return finalPlan;
    }

    /// <summary>
    /// Generates the input prompt for a single skill task at execution time.
    /// Step 1: LLM scopes the original user query down to this specific task (natural language).
    /// Step 2: Code appends structured fields based on ExpectedSkillInput keywords.
    /// </summary>
    public async Task<string> GenerateSkillPromptAsync(TaskPlanItem task, string skillDescription,
        string expectedSkillInput, string domainKnowledge, string uiContext, string upstreamOutputs,
        string originalUserPrompt, string taskPlanSummary = "")
    {
        // ── Step 1: LLM generates a focused, task-scoped prompt ──
        var genSystemPrompt = $"""
            You scope a user query down to a single task.

            Task: {task.Task}

            The user asked a complex query involving multiple metrics and actions.
            Your job is to extract ONLY the part relevant to the task above.
            Include any time ranges, conditions, or qualifiers the user specified.
            **Critical**:DO NOT MISS any time ranges, conditions, or qualifiers the user specified.
            Output a short, focused instruction. Nothing else.
            """;

        var agent = CreateInstrumentedAgent(_secondaryModelName, genSystemPrompt, temperature: 0f, seed: 42);
        _logger.LogInformation("Calling GenerateSkillPrompt for {TaskLabel} ({SkillName}) with below userPrompt: {UserPrompt}",
            task.Task, task.SkillName, originalUserPrompt);
        var swPromptGen = System.Diagnostics.Stopwatch.StartNew();
        var response = await RunWithTimeoutAsync(
            async () => await agent.RunAsync(originalUserPrompt, await agent.CreateSessionAsync()),
            $"GenerateSkillPrompt({task.SkillName})", _generateSkillPromptTimeoutSeconds);
        swPromptGen.Stop();
        var focusedPrompt = response.Text ?? task.Task;
        _logger.LogInformation("Called GenerateSkillPrompt for {TaskLabel} ({SkillName}, {ElapsedMs}ms), here is response: {Response}",
            task.Task, task.SkillName, swPromptGen.ElapsedMilliseconds, focusedPrompt);

        // ── Step 2: Code appends structured fields based on ExpectedSkillInput ──
        var sb = new StringBuilder();
        sb.AppendLine(focusedPrompt);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(expectedSkillInput))
        {
            var expected = expectedSkillInput.ToLowerInvariant();

            // Aspect name — extract from domain knowledge by matching task label to section headings
            if (expected.Contains("aspectname"))
            {
                var aspectLookup = ExtractAspectNamesFromDomainKnowledge(domainKnowledge);
                var aspectName = FindAspectNameForTask(task.Task, aspectLookup);

                // Fallback: if word-overlap matching failed, use LLM to extract aspect name from lookup
                if (aspectName is null && aspectLookup.Count > 0)
                {
                    aspectName = await ExtractAspectNameByLlmAsync(task.Task, aspectLookup);
                }

                sb.AppendLine($"Aspect Name: {aspectName ?? "[NOT FOUND]"}");
                sb.AppendLine($"------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
            }

            // UI Context fields — check for "UIcontext" wrapper or individual fields
            var needsUiContext = expected.Contains("uicontext") || expected.Contains("entityname");
            if (needsUiContext)
            {
                sb.AppendLine();
                sb.AppendLine("## UI Context:");
                sb.AppendLine($"Entity Name: {ExtractFieldFromUiContext(uiContext, "Entity Name")}");
                sb.AppendLine($"Entity ID: {ExtractFieldFromUiContext(uiContext, "Entity ID")}");
                sb.AppendLine($"Entity Type: {ExtractFieldFromUiContext(uiContext, "Entity Type")}");
                if (expected.Contains("globalfilter"))
                    sb.AppendLine($"GlobalFilter: {ExtractFieldFromUiContext(uiContext, "GlobalFilter")}");
                sb.AppendLine($"------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
            }
            // Domain knowledge
            if (expected.Contains("domainknowledge"))
            {
                sb.AppendLine();
                //sb.AppendLine("## Domain Knowledge:");
                sb.AppendLine(domainKnowledge);
                sb.AppendLine($"------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
            }

            // Factual data (upstream outputs)
            if (expected.Contains("factualdata"))
            {
                if (!string.IsNullOrWhiteSpace(upstreamOutputs))
                {
                    sb.AppendLine();
                    sb.AppendLine("## Factual Data (from upstream tasks):");
                    sb.AppendLine(upstreamOutputs);
                    sb.AppendLine($"------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
                }
            }

            // Original user prompt
            if (expected.Contains("originaluserprompt"))
            {
                sb.AppendLine();
                sb.AppendLine($"## Original User Query:");
                sb.AppendLine(originalUserPrompt);
                sb.AppendLine($"------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
            }
        }

        var prompt = sb.ToString().Trim();
        _logger.LogInformation("PLAN_TASK idx={Idx} Skill={Skill} Task={TaskLabel} Prompt={Prompt}",
            task.DependsOn.Count == 0 ? "root" : "downstream", task.SkillName, task.Task, prompt);

        return prompt;
    }

    /// <summary>Extracts all aspect names from domain knowledge text.
    /// Scans for EVERY occurrence of "aspect with name `xxx`" and maps it to the nearest
    /// node name (found as **node_name** in bold or as a ## heading).
    /// Returns: { "get csat score" → "get_csat_score", "get irmet value trend" → "get_irmet_value_trend", ... }</summary>
    private static Dictionary<string, string> ExtractAspectNamesFromDomainKnowledge(string domainKnowledge)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(domainKnowledge)) return result;

        // Scan line by line, tracking the current node context.
        // Node names come from ## headings or **bold** relationship text.
        // Also extract "Also known as" tags to support alias matching (e.g., "TTM" → "get_time_to_mitigate_p75").
        string? currentNode = null;
        string? currentAspect = null;
        foreach (var line in domainKnowledge.Split('\n'))
        {
            var trimmed = line.Trim();

            // Top-level heading: "## get csat score"
            if (trimmed.StartsWith("## "))
            {
                currentNode = trimmed[3..].Trim();
                currentAspect = null;
            }

            // Relationship node: "- [impacts-csat | positive-correlation] **get irmet value trend**"
            var relMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"\[.+\]\s+\*\*(.+?)\*\*");
            if (relMatch.Success)
            {
                // Find ALL bold text (**node names**) in the line
                var boldMatches = System.Text.RegularExpressions.Regex.Matches(trimmed, @"\*\*([^*]+)\*\*");
                foreach (System.Text.RegularExpressions.Match boldMatch in boldMatches)
                {
                    var boldText = boldMatch.Groups[1].Value;

                    // "Also known as" line — extract tags and map each to the current aspect
                    if (boldText.StartsWith("Also known as", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentAspect != null)
                        {
                            var colonIdx = trimmed.IndexOf(':',
                                trimmed.IndexOf("Also known as", StringComparison.OrdinalIgnoreCase) + 13);
                            if (colonIdx >= 0)
                            {
                                var tagsStr = trimmed[(colonIdx + 1)..].Trim();
                                foreach (var tag in tagsStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
                                {
                                    var cleaned = tag.Trim();
                                    if (!string.IsNullOrWhiteSpace(cleaned))
                                        result.TryAdd(cleaned, currentAspect);
                                }
                            }
                        }
                        continue;
                    }

                    // Skip other markdown labels
                    if (boldText.StartsWith("Details", StringComparison.OrdinalIgnoreCase) ||
                        boldText.StartsWith("Relationships", StringComparison.OrdinalIgnoreCase) ||
                        boldText.StartsWith("Aspect", StringComparison.OrdinalIgnoreCase) ||
                        boldText.All(c => c == '_' || char.IsLetterOrDigit(c)))
                        continue;

                    // This is a node name like "get irmet value trend"
                    currentNode = boldText;
                    currentAspect = null;
                }
            }

            // Aspect name pattern: "aspect with name `xxx`"
            var aspectMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"aspect with name `(\w+)`");
            if (aspectMatch.Success && currentNode != null)
            {
                currentAspect = aspectMatch.Groups[1].Value;
                result.TryAdd(currentNode, currentAspect);
            }
        }
        return result;
    }

    private static readonly HashSet<string> MatchStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "for", "by", "the", "a", "an", "of", "to", "and", "in", "on", "with", "is", "are"
    };

    private static HashSet<string> TokenizeForMatching(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => !MatchStopWords.Contains(w))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Finds the aspect name for a task by matching the task label to domain knowledge headings.
    /// Uses word-overlap matching: if most words from the heading appear in the task label, it's a match.
    /// Ignores common stop words and requires at least 50% word overlap.</summary>
    private static string? FindAspectNameForTask(string taskLabel, Dictionary<string, string> aspectLookup)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "get", "show", "fetch", "find", "for", "of", "the", "a", "an", "and", "in", "to", "with", "by", "from", "me", "my", "is", "are", "was", "on", "at" };

        var labelWords = taskLabel.ToLowerInvariant()
            .Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !stopWords.Contains(w))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string? bestMatch = null;
        int bestOverlap = 0;

        foreach (var (heading, aspectName) in aspectLookup)
        {
            var headingWords = heading.ToLowerInvariant()
                .Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !stopWords.Contains(w))
                .ToList();

            if (headingWords.Count == 0) continue;

            var overlap = headingWords.Count(w => labelWords.Contains(w));
            var overlapRatio = (double)overlap / headingWords.Count;

            // Require at least 50% of heading words to appear in the task label
            if (overlapRatio >= 0.5 && overlap > bestOverlap)
            {
                bestOverlap = overlap;
                bestMatch = aspectName;
            }
        }

        return bestMatch;
    }

    /// <summary>LLM fallback for aspect name extraction when word-overlap matching fails.
    /// Sends the task label and the aspect lookup dictionary to the secondary model.</summary>
    private async Task<string?> ExtractAspectNameByLlmAsync(string taskLabel, Dictionary<string, string> aspectLookup)
    {
        var lookupText = string.Join("\n", aspectLookup.Select(kv => $"- \"{kv.Key}\" → {kv.Value}"));

        var systemPrompt = $"""
            You are an aspect name matcher based on task label . Given a task label, find the best matching node name with the task label
            from the lookup below and return its corresponding aspect name (the value after →).

            The task label may use abbreviations, synonyms, or partial names. Use **semantic
            understanding** to match. For example:
            - "TTM" matches "time to mitigate"
            - "CSAT" matches "customer satisfaction" or "csat score"
            - "P75 TTM" matches "time to mitigate p75"

            ## Lookup (node name → aspect name)
            {lookupText}

            Return ONLY the aspect name value (e.g., get_csat_score). No explanation NO formating
            """;

        try
        {
            var agent = CreateInstrumentedAgent("gpt-4o", systemPrompt, temperature: 0f, seed: 42);
            _logger.LogInformation("Calling ExtractAspectNameByLlm for {TaskLabel} with below userPrompt: {UserPrompt}",
                taskLabel, taskLabel);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await RunWithTimeoutAsync(
                async () => await agent.RunAsync(taskLabel, await agent.CreateSessionAsync()),
                "ExtractAspectNameByLlm", _extractAspectNameTimeoutSeconds);
            sw.Stop();

            var result = response.Text?.Trim();
            _logger.LogInformation("Called ExtractAspectNameByLlm for {TaskLabel} ({ElapsedMs}ms), here is response: {Response}",
                taskLabel, sw.ElapsedMilliseconds, result);

            if (string.IsNullOrWhiteSpace(result) || result.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            var cleaned = result.Trim('`').Trim();

            // Validate the result exists in the lookup values
            if (aspectLookup.Values.Contains(cleaned, StringComparer.OrdinalIgnoreCase))
                return cleaned;

            // LLM might return the node name instead of the aspect identifier — check keys and map
            if (aspectLookup.TryGetValue(cleaned, out var mappedAspect))
            {
                _logger.LogInformation("ExtractAspectNameByLlm: LLM returned node name '{Result}', mapped to '{Aspect}'", cleaned, mappedAspect);
                return mappedAspect;
            }

            _logger.LogWarning("ExtractAspectNameByLlm: LLM returned '{Result}' which is not in the lookup, ignoring", cleaned);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ExtractAspectNameByLlm failed for task '{Task}', returning null", taskLabel);
            return null;
        }
    }

    /// <summary>Extracts a field value from the UI context string by field label.</summary>
    private static string ExtractFieldFromUiContext(string uiContext, string fieldLabel)
    {
        var lines = uiContext.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith(fieldLabel, StringComparison.OrdinalIgnoreCase))
            {
                // Check if this is a multi-line value (starts with "[")
                var colonIdx = lines[i].IndexOf(':');
                if (colonIdx < 0) continue;
                var value = lines[i][(colonIdx + 1)..].Trim();

                // If value starts with "[", capture all lines until closing "]"
                if (value.StartsWith("["))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(value);
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        sb.AppendLine(lines[j]);
                        if (lines[j].Trim().StartsWith("]"))
                            break;
                    }
                    return sb.ToString().Trim();
                }

                return value;
            }
        }
        return "[NOT FOUND]";
    }

    // ── Load skills by exact name (for task plan) ────────────────────

    public async Task<List<AgentSkill>> GetSkillsByNameAsync(List<string> skillNames)
    {
        var skills = await _configStore.GetSkillsByNameAsync(skillNames);
        var agentSkills = skills.Select(s =>
        {
            // Parse ExpectedSkillInput from the Configuration JSON
            string? expectedInput = null;
            float? temperature = null;
            long? seed = null;
            int? timeout = null;
            string? type = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(s.Definition.Configuration) && s.Definition.Configuration != "TODO")
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<SkillConfiguration>(
                        s.Definition.Configuration, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    expectedInput = parsed?.ExpectedSkillInput;
                    temperature = parsed?.Temperature;
                    seed = parsed?.Seed;
                    timeout = parsed?.Timeout;
                    type = parsed?.Type;
                }
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"SkillConfiguration parsing failed for skill: {s.Definition.Name}"); 
            }

            return new AgentSkill
            {
                SkillName = s.Definition.Name,
                Description = s.Definition.Description,
                Tools = s.Definition.Tools,
                SystemPrompt = s.Definition.SystemPrompt,
                ModelName = s.Definition.ModelName,
                ExpectedSkillInput = expectedInput,
                Temperature = temperature,
                Seed = seed,
                Timeout = timeout ?? 60,
                Type = type ?? "skill"
            };
        }).ToList();

        _logger.LogDebug("GetSkillsByNameAsync: Loaded {Count} skills: [{Skills}]",
            agentSkills.Count, string.Join(", ", agentSkills.Select(s => s.SkillName)));
        return agentSkills;
    }

    // ?? Step 7: Execute Single Skill ?????????????????????????????????

    public async Task<CXOAgentResponse> ExecuteSkillAsync(AgentSkill skillInfo, string prompt, List<AITool> resolvedTools)
    {
        var instructionBuilder = new StringBuilder(skillInfo.SystemPrompt ?? string.Empty);
        instructionBuilder.Append(SkillSystemPromptSuffix);

        var agent = CreateInstrumentedAgent(skillInfo.ModelName, instructionBuilder.ToString(), resolvedTools, temperature: skillInfo.Temperature ?? 0f, seed: skillInfo.Seed ?? 42);
        var stepName = $"ExecuteSkill({skillInfo.SkillName})";
        using var skillLatency = _metricsProvider?.LatencyMeasureOperation(MetricNames.SkillExecution,
            new KeyValuePair<string, object?>(MetricNames.TagSkillName, skillInfo.SkillName));

        try
        {
            _logger.LogInformation("Calling ExecuteSkill for {SkillName} with below userPrompt: {UserPrompt}",
                skillInfo.SkillName, prompt);
            var swSkill = Stopwatch.StartNew();
            var response = await RunWithTimeoutAsync(
                async () => await agent.RunAsync<CXOAgentResponse>(prompt),
                stepName, skillInfo.Timeout);
            swSkill.Stop();
            var result = response.Result;
            _logger.LogInformation("Called ExecuteSkill for {SkillName} ({ElapsedMs}ms), here is response: IsSuccess={IsSuccess}, NeedsInput={NeedsInput}, ResponseLength={ResponseLen}",
                skillInfo.SkillName, swSkill.ElapsedMilliseconds, result.IsSuccess, result.NeedsInputForUser, result.Response?.Length ?? 0);

            // If structured deserialization produced an empty Response,
            // fall back to the raw LLM text (the agent answered naturally
            // instead of producing JSON matching CXOAgentResponse).
            if (string.IsNullOrWhiteSpace(result.Response) && !string.IsNullOrWhiteSpace(response.Text))
            {
                result.Response = response.Text;
            }

            // Carry any payload emitted by tool code (e.g., continuation tokens).
            // Tools set this via EmitPayload() since the LLM response JSON
            // cannot carry JObject payloads set in C#.
            if (result.Payload is null)
            {
                foreach (var instance in _toolInstances.Values)
                {
                    if (instance is IPayloadEmitter emitter)
                    {
                        var emitted = emitter.ConsumeEmittedPayload();
                        if (emitted is not null)
                        {
                            result.Payload = emitted;
                            break;
                        }
                    }
                }
            }

            skillLatency?.SetState(ActivityStatusCode.Ok);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.SkillExecution, 1, null,
                new KeyValuePair<string, object?>(MetricNames.TagSkillName, skillInfo.SkillName));
            return result;
        }
        catch (ToolParameterException tpe)
        {
            return JsonConvert.DeserializeObject<CXOAgentResponse>(tpe.Message);
        }
        catch (LlmOperationException ex)
        {
            _logger.LogError(ex, "ExecuteSkill failed for skill '{SkillName}': {Message}", skillInfo.SkillName, ex.Message);
            skillLatency?.SetState(ActivityStatusCode.Error);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.SkillExecution, 1, ex,
                new KeyValuePair<string, object?>(MetricNames.TagSkillName, skillInfo.SkillName));
            return new CXOAgentResponse
            {
                IsSuccess = false,
                NeedsInputForUser = false,
                Response = ex.UserMessage
            };
        }
    }

    // ?? Step 8: Summarize ????????????????????????????????????????????

    public async Task SummarizeAndStoreAsync(string userId, string sessionId, string conversationContent, string? freshSkillOutputs = null, string? requestId = null)
    {
        if (string.IsNullOrWhiteSpace(conversationContent))
            return;

        // Read previous rollup summary for this session
        var previousSummary = await _conversationStore.GetSessionSummaryAsync(userId, sessionId);

        var systemPrompt = previousSummary is not null
            ? """
              You are a conversation summarizer. Merge the previous session summary with the new conversation turn
              into ONE consolidated rolling summary.

              ## Format Rules
              - Start with a **one-line summary** of the overall session topic.
              - If data values exist (numbers, metrics, scores, dates, counts), present them in a **markdown table**.
              - If file references or action confirmations exist, list them as bullet points.
              - Use markdown headers (`##`, `###`) to organize sections when multiple topics were discussed.

              ## Content Rules
              - KEEP all data values from the previous summary — do not lose any metrics.
              - ADD the new turn's data to the consolidated summary.
              - If the new turn updates a previously seen metric, show the LATEST value.
              - Ignore all internal orchestration details.
              - Keep the summary factual and concise.
              """
            : """
              You are a conversation summarizer. Produce a markdown summary of the conversation below.

              ## Format Rules
              - Start with a **one-line summary** of what the user asked.
              - If skill outputs contain data values (numbers, metrics, scores, dates, counts), present them in a **markdown table**.
              - If skill outputs contain file references or action confirmations, list them as bullet points.
              - If there are no data values, use a brief paragraph instead of a table.
              - Use markdown headers (`##`, `###`) to organize sections when multiple skills produced output.

              ## Content Rules
              - Focus ONLY on the user prompt and skill outputs.
              - Ignore all internal orchestration details.
              - Do NOT include domain knowledge definitions or explanations.
              - Keep the summary factual and concise.
              """;

        var userPrompt = previousSummary is not null
            ? $"## Previous Session Summary\n{previousSummary}\n\n## New Conversation Turn\n{conversationContent}"
            : $"## Conversation\n{conversationContent}";

        var agent = CreateInstrumentedAgent(_secondaryModelName, systemPrompt);

        string summary;
        try
        {
            _logger.LogInformation("Calling SummarizeAndStore with below userPrompt: {UserPrompt}", userPrompt);
            var swSummarize = System.Diagnostics.Stopwatch.StartNew();
            var response = await RunWithTimeoutAsync(
                async () => await agent.RunAsync(userPrompt, await agent.CreateSessionAsync()),
                "Summarize", _summarizeTimeoutSeconds);
            swSummarize.Stop();
            summary = response.Text ?? string.Empty;
            _logger.LogInformation("Called SummarizeAndStore ({ElapsedMs}ms), here is response: {Response}",
                swSummarize.ElapsedMilliseconds, summary);
        }
        catch (Exception ex)
        {
            // Summarization is best-effort — the user already has their answer.
            // Log and continue with history append and memory extraction.
            _logger.LogError(ex, "SummarizeAndStoreAsync: LLM summarization failed — continuing with history and memory");
            summary = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(summary))
        {
            await _conversationStore.UpsertSessionSummaryAsync(userId, sessionId, summary);
            _logger.LogInformation("SummarizeAndStoreAsync: Summary stored for session '{SessionId}'", sessionId);
        }

        // Append raw turn to history (extract prompt and response from conversationContent)
        var promptMarker = "[UserPrompt] ";
        var responseMarkers = new[] { "\n[Response] ", "\n[SkillOutput:" };
        var promptEnd = -1;
        foreach (var marker in responseMarkers)
        {
            var idx = conversationContent.IndexOf(marker, StringComparison.Ordinal);
            if (idx > 0 && (promptEnd < 0 || idx < promptEnd))
                promptEnd = idx;
        }
        var rawPrompt = promptEnd > 0
            ? conversationContent[promptMarker.Length..promptEnd]
            : conversationContent;
        var rawResponse = promptEnd > 0
            ? conversationContent[(promptEnd + 1)..]
            : string.Empty;

        try
        {
            _logger.LogInformation("Calling AppendToHistory for sessionId={SessionId}, requestId={RequestId}, promptLength={PromptLen}, responseLength={ResponseLen}",
                sessionId, requestId ?? "N/A", rawPrompt.Length, rawResponse.Length);
            await _conversationStore.AppendToHistoryAsync(userId, sessionId, rawPrompt, rawResponse, requestId);
            _logger.LogInformation("Called AppendToHistory successfully for sessionId={SessionId}, requestId={RequestId}",
                sessionId, requestId ?? "N/A");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SummarizeAndStoreAsync: Failed to append history for session '{SessionId}'", sessionId);
        }

        if (_memoryStore is not null)
        {
            // Extract User-scoped facts (preferences only — no data values)
            try
            {
                _logger.LogInformation("Calling MemoryExtractAndStore (User scope) for userId={UserId}, contentLength={ContentLen}",
                    userId, conversationContent.Length);
                await _memoryStore.ExtractAndStoreAsync(userId, conversationContent, MemoryScope.User);
                _logger.LogInformation("Called MemoryExtractAndStore (User scope) successfully for userId={UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SummarizeAndStoreAsync: User fact extraction failed");
            }

            // Extract Org-scoped facts (data values — shared cache under "EngOps")
            // Use freshSkillOutputs when available to avoid re-extracting injected memory
            // (cyclic freshness problem: injected Org facts would get re-extracted and
            // reset their TTL, preventing them from ever expiring).
            var orgContent = !string.IsNullOrWhiteSpace(freshSkillOutputs) ? freshSkillOutputs : conversationContent;
            try
            {
                _logger.LogInformation("Calling MemoryExtractAndStore (Org scope) fromFreshOutputs={FromFresh}, contentLength={ContentLen}",
                    !string.IsNullOrWhiteSpace(freshSkillOutputs), orgContent.Length);
                await _memoryStore.ExtractAndStoreAsync(MemoryConstants.OrgUserId, orgContent, MemoryScope.Org);
                _logger.LogInformation("Called MemoryExtractAndStore (Org scope) successfully, fromFreshOutputs={FromFresh}",
                    !string.IsNullOrWhiteSpace(freshSkillOutputs));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SummarizeAndStoreAsync: Org fact extraction failed");
            }
        }
    }

    // ?? Tool Resolution ??????????????????????????????????????????????

    public List<AITool> ResolveTools(AgentSkill skillInfo)
    {
        var resolvedTools = new List<AITool>();
        if (skillInfo.Tools is not { Count: > 0 })
            return resolvedTools;

        foreach (var tool in skillInfo.Tools)
        {
            var parts = tool.Name.Split('-', 2);
            if (parts.Length != 2)
                throw new InvalidOperationException($"Tool name '{tool.Name}' must be in 'ClassName.MethodName' format.");

            var className = parts[0];
            var methodName = parts[1];

            if (!_toolInstances.TryGetValue(className, out var instance))
                throw new InvalidOperationException($"No tool instance registered for class '{className}'.");

            var method = instance.GetType().GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"Method '{methodName}' not found on class '{className}'.");

            resolvedTools.Add(AIFunctionFactory.Create(method, instance, new AIFunctionFactoryOptions
            {
                Name = tool.Name.Replace(".", "_"),
                Description = tool.Description
            }));
        }

        return resolvedTools;
    }
}
