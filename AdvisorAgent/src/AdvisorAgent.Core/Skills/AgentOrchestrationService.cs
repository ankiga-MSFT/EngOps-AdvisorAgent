using System.Text.Json;
using AdvisorAgent.Core.Models;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Core.Skills;

public sealed class AgentOrchestrationService : IAgentOrchestrationService
{
    private readonly ILogger<AgentOrchestrationService> _logger;
    private readonly string _openAiEndpoint;
    private readonly string _modelName;
    private readonly TokenCredential _credential;
    private readonly Dictionary<string, AgentSkillDefinition> _skillCatalog;
    private readonly Dictionary<string, object> _toolInstances;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public AgentOrchestrationService(
        ILogger<AgentOrchestrationService> logger,
        string openAiEndpoint,
        string modelName,
        TokenCredential credential,
        Dictionary<string, AgentSkillDefinition> skillCatalog,
        Dictionary<string, object> toolInstances)
    {
        _logger = logger;
        _openAiEndpoint = openAiEndpoint;
        _modelName = modelName;
        _credential = credential;
        _skillCatalog = skillCatalog;
        _toolInstances = toolInstances;
    }

    public async Task<AzureContext> ResolveAzureContextAsync(string prompt, AzureContext? existingContext, List<ConversationTurn>? conversationHistory = null)
    {
        if (existingContext is { HasScope: true })
        {
            _logger.LogInformation("[Core:ResolveContext] Using existing context — {Summary}", existingContext.ToContextSummary());
            return existingContext;
        }

        _logger.LogInformation("[Core:ResolveContext] Calling LLM to extract Azure context... (history turns: {Count})", conversationHistory?.Count ?? 0);
        var client = CreateChatClient(_modelName);

        var messages = BuildMessagesWithHistory(
            """
            You extract Azure resource identifiers from user prompts.
            Return a JSON object with these nullable fields:
              subscriptionId, resourceGroup, serviceGroup, resourceId, resourceType, resourceName, region.
            When the user provides MULTIPLE values (e.g., multiple subscription IDs or resource groups), also include:
              subscriptionIds (array of strings), resourceGroups (array of strings), resourceNames (array of strings).
            Use singular fields when exactly one value is found. Use array fields when multiple values are found.
            Only include fields you can confidently extract. Return {} if none are found.
            Do NOT invent or guess values — only extract what is explicitly stated.
            Look at the full conversation history to find identifiers the user may have provided in earlier messages.
            """,
            conversationHistory,
            prompt);

        var response = await client.GetResponseAsync(messages);
        var text = response.Text.Trim();

        // Strip markdown code fences if the model wraps its output
        text = StripCodeFences(text);
        _logger.LogDebug("[Core:ResolveContext] LLM raw response: {Response}", text);

        try
        {
            var parsed = JsonSerializer.Deserialize<AzureContext>(text, JsonOpts) ?? new AzureContext();
            _logger.LogInformation("[Core:ResolveContext] Parsed context: {Summary}", parsed.ToContextSummary());
            return parsed;
        }
        catch (JsonException)
        {
            _logger.LogWarning("[Core:ResolveContext] Failed to parse AzureContext from LLM response: {Response}", text);
            return new AzureContext();
        }
    }

    public async Task<UserIntent> ClassifyIntentAsync(string prompt, string azureContextSummary, List<ConversationTurn>? conversationHistory = null)
    {
        _logger.LogInformation("[Core:ClassifyIntent] Starting classification... (history turns: {Count})", conversationHistory?.Count ?? 0);
        var skillList = string.Join("\n", _skillCatalog.Values.Select(s => $"- {s.SkillName}: {s.Description}"));

        var client = CreateChatClient(_modelName);

        var messages = BuildMessagesWithHistory(
            $$"""
            You are an intent classifier for the Azure Advisor Agent.
            The agent supports these skills:
            {{skillList}}

            Classify the user's intent as one of:
            - Informational: The user asks a factual or conceptual question that can be answered directly.
            - ActionRequired: The user wants analysis, recommendations, or an action plan that requires tool execution.
            - Unknown: The request is unclear or outside scope.

            IMPORTANT: Consider the full conversation history when classifying intent.
            If the user previously asked a question and is now providing additional information (like a subscription ID,
            resource group, or clarification), treat this as a continuation of the original intent — NOT as a new Unknown request.

            Azure context (if any): {{azureContextSummary}}

            Respond with a JSON object: { "intent": "...", "reasoning": "..." }
            """,
            conversationHistory,
            prompt);

        var response = await client.GetResponseAsync(messages);
        var text = StripCodeFences(response.Text.Trim());
        _logger.LogDebug("[Core:ClassifyIntent] LLM raw response: {Response}", text);

        try
        {
            var parsed = JsonSerializer.Deserialize<UserIntent>(text, JsonOpts) ?? new UserIntent { Intent = UserIntentType.Unknown };
            _logger.LogInformation("[Core:ClassifyIntent] Result — Intent: {Intent}, Reasoning: {Reasoning}", parsed.Intent, parsed.Reasoning);
            return parsed;
        }
        catch (JsonException)
        {
            _logger.LogWarning("[Core:ClassifyIntent] Failed to parse UserIntent: {Response}", text);
            return new UserIntent { Intent = UserIntentType.Unknown, Reasoning = "Failed to classify intent." };
        }
    }

    public async Task<string> AnswerDirectlyAsync(string prompt, string azureContextSummary, List<ConversationTurn>? conversationHistory = null)
    {
        _logger.LogInformation("[Core:AnswerDirectly] Generating direct answer... (history turns: {Count})", conversationHistory?.Count ?? 0);
        var client = CreateChatClient(_modelName);

        var messages = BuildMessagesWithHistory(
            $"""
            You are the Azure Advisor Agent, an expert on Azure services, architecture, reliability, 
            cost optimization, security, and operational excellence.
            Provide a clear, accurate answer to the user's informational question.
            If Azure context is available, tailor your response accordingly.
            Use conversation history for context when available.
            Azure context: {azureContextSummary}
            """,
            conversationHistory,
            prompt);

        var response = await client.GetResponseAsync(messages);
        _logger.LogInformation("[Core:AnswerDirectly] Complete — ResponseLength: {Length} chars", response.Text.Length);
        return response.Text;
    }

    public async Task<List<TaskPlanItem>> DecomposeTasksAsync(string prompt, string azureContextSummary, List<ConversationTurn>? conversationHistory = null)
    {
        _logger.LogInformation("[Core:DecomposeTasks] Decomposing prompt into task plan... (history turns: {Count})", conversationHistory?.Count ?? 0);
        var skillList = string.Join("\n", _skillCatalog.Values.Select(s => $"- {s.SkillName}: {s.Description}"));

        var client = CreateChatClient(_modelName);

        var messages = BuildMessagesWithHistory(
            $$"""
            You are a task planner for the Azure Advisor Agent.
            Available skills:
            {{skillList}}

            Azure context: {{azureContextSummary}}

            Consider the full conversation history to understand the user's complete intent.
            Decompose the user's request into a list of tasks. Each task uses exactly one skill.
            Tasks may depend on prior tasks (by 0-based index).
            Return a JSON array: [{ "task": "...", "skillName": "...", "dependsOn": [int] }]
            Only use skills from the list above.

            IMPORTANT: Keep the plan strictly focused on what the user explicitly asked for.
            Do NOT speculatively add skills that seem tangentially related.
            For example, if the user asks about cost optimization, only use CostOptimizationSkill — do NOT add RetirementSkill unless the user specifically mentioned retirements or migrations.
            Each skill should be included only if the user's request directly requires its capabilities.
            
            CRITICAL: Each skill must appear AT MOST ONCE in the plan. Never create multiple tasks for the same skill.
            Each skill is self-contained — it handles discovery, analysis, and action plan generation internally via its own tools.
            Do NOT break a single skill's work into multiple tasks (e.g., do NOT create separate "find resources" and "generate plan" tasks for the same skill).
            
            Prefer fewer tasks over more.
            """,
            conversationHistory,
            prompt);

        // Attempt up to 2 times on parse failure
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var response = await client.GetResponseAsync(messages);
            var text = StripCodeFences(response.Text.Trim());
            _logger.LogDebug("[Core:DecomposeTasks] Attempt {Attempt} LLM response: {Response}", attempt + 1, text);

            try
            {
                var plan = JsonSerializer.Deserialize<List<TaskPlanItem>>(text, JsonOpts);
                if (plan is { Count: > 0 })
                {
                    _logger.LogInformation("[Core:DecomposeTasks] Parsed {Count} tasks on attempt {Attempt}", plan.Count, attempt + 1);
                    return plan;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[Core:DecomposeTasks] Parse failure on attempt {Attempt}", attempt + 1);
            }
        }

        _logger.LogError("[Core:DecomposeTasks] Failed after retries for prompt: {Prompt}", prompt);
        return [];
    }

    public List<AgentSkillDefinition> GetSkillDefinitions(List<string> skillNames)
    {
        return skillNames
            .Where(name => _skillCatalog.ContainsKey(name))
            .Select(name => _skillCatalog[name])
            .ToList();
    }

    public async Task<string> GenerateSkillPromptAsync(
        string taskLabel,
        string skillDescription,
        string expectedInput,
        string azureContextSummary,
        string upstreamOutputs,
        string originalPrompt,
        List<ConversationTurn>? conversationHistory = null)
    {
        var client = CreateChatClient(_modelName);

        // Build conversation summary for skill prompt context
        var historyContext = "";
        if (conversationHistory is { Count: > 0 })
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Previous conversation context:");
            foreach (var turn in conversationHistory)
            {
                sb.AppendLine($"  User: {Truncate(turn.Prompt, 200)}");
                sb.AppendLine($"  Agent: {Truncate(turn.Response, 500)}");
            }
            historyContext = sb.ToString();
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, $"""
                You generate a focused prompt for a specific skill to execute.
                
                Task: {taskLabel}
                Skill: {skillDescription}
                Expected input: {expectedInput}
                Azure context: {azureContextSummary}
                
                Upstream task outputs (if any):
                {(string.IsNullOrWhiteSpace(upstreamOutputs) ? "None" : upstreamOutputs)}

                {(string.IsNullOrWhiteSpace(historyContext) ? "" : historyContext)}

                Original user request: {originalPrompt}

                Produce a clear, focused prompt that tells the skill exactly what to do.
                Include any relevant context from upstream outputs, conversation history, and Azure scope.
                Return only the prompt text — no JSON wrapping.
                """),
            new(ChatRole.User, "Generate the skill prompt.")
        };

        var response = await client.GetResponseAsync(messages);
        return response.Text.Trim();
    }

    public async Task<AdvisorAgentResponse> ExecuteSkillAsync(AgentSkillDefinition skill, string prompt, string? accessToken = null)
    {
        // Set the ARM access token on all tool instances that will be used by this skill.
        // Uses reflection to avoid a direct dependency from Core → Tools.
        if (!string.IsNullOrEmpty(accessToken))
        {
            foreach (var toolRef in skill.Tools)
            {
                var className = toolRef.Name.Split('-', 2)[0];
                if (_toolInstances.TryGetValue(className, out var instance))
                {
                    var method = instance.GetType().GetMethod("SetAccessToken");
                    method?.Invoke(instance, [accessToken]);
                }
            }
        }

        var tools = ResolveTools(skill);
        var client = CreateChatClient(skill.ModelName);

        var options = new ChatOptions
        {
            Tools = tools,
            Temperature = skill.Temperature
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, skill.SystemPrompt + SkillResponseProtocol),
            new(ChatRole.User, prompt)
        };

        _logger.LogInformation("[Core:ExecuteSkill] ▶ Skill: {SkillName}, Model: {Model}, Tools: [{Tools}], Temp: {Temp}",
            skill.SkillName, skill.ModelName,
            string.Join(", ", tools.Select(t => t.Name)),
            skill.Temperature);
        _logger.LogDebug("[Core:ExecuteSkill] Input prompt:\n{Prompt}", prompt);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(skill.Timeout));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.GetResponseAsync(messages, options, cts.Token);
            sw.Stop();

            var text = response.Text ?? string.Empty;

            // Log tool call details from the response
            var toolCalls = response.Messages
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .ToList();
            var toolResults = response.Messages
                .SelectMany(m => m.Contents.OfType<FunctionResultContent>())
                .ToList();

            if (toolCalls.Count > 0)
            {
                _logger.LogInformation("[Core:ExecuteSkill] ◆ {Count} tool call(s) made during skill execution:", toolCalls.Count);
                foreach (var call in toolCalls)
                {
                    var argsStr = call.Arguments is not null
                        ? JsonSerializer.Serialize(call.Arguments, JsonOpts)
                        : "{}";
                    _logger.LogInformation("[Core:ExecuteSkill]   → Tool: {ToolName}, Args: {Args}",
                        call.Name, argsStr.Length > 500 ? argsStr[..500] + "…" : argsStr);
                }
                foreach (var result in toolResults)
                {
                    var resultStr = result.Result?.ToString() ?? "(null)";
                    _logger.LogInformation("[Core:ExecuteSkill]   ← Tool: {ToolName}, Result: {Result}",
                        result.CallId, resultStr.Length > 500 ? resultStr[..500] + "…" : resultStr);
                }
            }
            else
            {
                _logger.LogInformation("[Core:ExecuteSkill] No tool calls were made — LLM answered directly");
            }

            _logger.LogInformation("[Core:ExecuteSkill] ■ Skill: {SkillName} completed in {ElapsedMs}ms, ResponseLength: {Length} chars",
                skill.SkillName, sw.ElapsedMilliseconds, text.Length);

            // Try to parse structured response
            var stripped = StripCodeFences(text);
            try
            {
                var parsed = JsonSerializer.Deserialize<AdvisorAgentResponse>(stripped, JsonOpts);
                if (parsed is not null)
                    return parsed;
            }
            catch (JsonException)
            {
                // Not structured — use raw text
            }

            return AdvisorAgentResponse.Success(text, skill.SkillName);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[Core:ExecuteSkill] ✖ Skill {SkillName} TIMED OUT after {Timeout}s", skill.SkillName, skill.Timeout);
            return AdvisorAgentResponse.Failure($"Skill '{skill.SkillName}' timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Core:ExecuteSkill] ✖ Skill {SkillName} FAILED: {Error}", skill.SkillName, ex.Message);
            return AdvisorAgentResponse.Failure($"Skill '{skill.SkillName}' failed: {ex.Message}");
        }
    }

    public List<AITool> ResolveTools(AgentSkillDefinition skill)
    {
        var resolved = new List<AITool>();
        _logger.LogDebug("[Core:ResolveTools] Resolving {Count} tool references for skill {Skill}",
            skill.Tools.Count, skill.SkillName);

        foreach (var toolRef in skill.Tools)
        {
            var parts = toolRef.Name.Split('-', 2);
            if (parts.Length != 2) continue;

            var className = parts[0];
            var methodName = parts[1];

            if (!_toolInstances.TryGetValue(className, out var instance))
            {
                _logger.LogWarning("[Core:ResolveTools] Tool class '{ClassName}' not registered", className);
                continue;
            }

            var method = instance.GetType().GetMethod(methodName);
            if (method is null)
            {
                _logger.LogWarning("[Core:ResolveTools] Method '{MethodName}' not found on '{ClassName}'", methodName, className);
                continue;
            }

            var aiFunction = AIFunctionFactory.Create(method, instance, new AIFunctionFactoryOptions
            {
                Name = toolRef.Name.Replace("-", "_"),
                Description = toolRef.Description
            });

            resolved.Add(aiFunction);
        }

        return resolved;
    }

    // ── Private helpers ──────────────────────────────────

    /// <summary>
    /// Builds a chat message list with system prompt, conversation history turns, and the current user prompt.
    /// Truncates historical responses to manage token budget.
    /// </summary>
    private List<ChatMessage> BuildMessagesWithHistory(
        string systemPrompt,
        List<ConversationTurn>? conversationHistory,
        string currentUserPrompt)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, systemPrompt) };

        if (conversationHistory is { Count: > 0 })
        {
            foreach (var turn in conversationHistory)
            {
                messages.Add(new ChatMessage(ChatRole.User, turn.Prompt));
                messages.Add(new ChatMessage(ChatRole.Assistant, Truncate(turn.Response, 1000)));
            }
            _logger.LogDebug("[Core:BuildMessages] Injected {Count} conversation history turns", conversationHistory.Count);
        }

        messages.Add(new ChatMessage(ChatRole.User, currentUserPrompt));
        return messages;
    }

    private static string Truncate(string? value, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(value)) return "(empty)";
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }

    private IChatClient CreateChatClient(string modelName)
    {
        var aoaiClient = new AzureOpenAIClient(new Uri(_openAiEndpoint), _credential);
        return aoaiClient.GetChatClient(modelName).AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
    }

    private static string StripCodeFences(string text)
    {
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
            if (text.EndsWith("```")) text = text[..^3];
            text = text.Trim();
        }
        return text;
    }

    private const string SkillResponseProtocol = """


        When you have completed the task, return a JSON response:
        { "isSuccess": true, "response": "<your detailed markdown response>", "needsUserInput": false }
        
        Structure your response with these sections where applicable:
        ## Insights
        <analysis of findings>
        
        ## Recommendations  
        <table of specific recommendations>
        CRITICAL: Every Recommendations table MUST include a "Resource ID" column containing the full ARM resource ID (e.g. /subscriptions/.../resourceGroups/.../providers/...) for each affected resource.
        The table MUST also include columns for Resource Type, Recommendation, and Impact at minimum.
        Never summarize or omit resource IDs — the user needs them to take action.
        
        ## Action Plan
        <numbered prioritized steps>
        
        If you need more information from the user, return:
        { "isSuccess": true, "response": "<what you need>", "needsUserInput": true }
        """;
}
