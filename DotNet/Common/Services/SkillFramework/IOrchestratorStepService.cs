using CXOAI.ConfigurationStore;
using CXOAI.ConversationStore;
using CXOAI.Memory;
using Microsoft.Extensions.AI;

namespace CXOAI.SkillFramework;

/// <summary>
/// Output of the enhance-prompt step. Carries both the enhanced prompt and general knowledge
/// so downstream steps don't need to re-fetch.
/// </summary>
public class EnhancePromptResult
{
    public string EnhancedPrompt { get; set; } = string.Empty;
    public string GeneralKnowledge { get; set; } = string.Empty;
}

/// <summary>
/// Defines the individual pipeline steps shared between the console orchestrator
/// (<see cref="SkillOrchestrator"/>) and the Azure Durable Functions activities.
/// Implementations are injected via DI so each host (console, Azure Functions) can
/// supply the appropriate backing providers (file vs. Cosmos, local JSON vs. Azure Search).
/// </summary>
public interface IOrchestratorStepService
{
    /// <summary>Enhance the user prompt with preferences, memory, and knowledge graph context.</summary>
    Task<EnhancePromptResult> EnhancePromptAsync(string userId, string sessionId, string prompt, UserContext? userContext);

    /// <summary>Check if the question can be answered from conversation history.</summary>
    Task<HistoryAnswerResult> TryAnswerFromHistoryAsync(string prompt, string? sessionSummary, string? uiContextEntityName = null);

    /// <summary>Classify the user's intent as Informational, DataAction, or Unknown.</summary>
    Task<UserIntent> ClassifyIntentAsync(string prompt, string generalKnowledge, string? uiContextEntityName = null);

    /// <summary>Answer an informational query from domain knowledge.</summary>
    Task<string> AnswerFromKnowledgeAsync(string prompt, string generalKnowledge);

    /// <summary>Decompose the user prompt into a task plan.
    /// Planner LLM decides which skills, how many instances, and execution order (DependsOn indices).
    /// Includes validation with retry (up to 3 attempts) for structural correctness.
    /// PromptToSend is NOT filled here — it's generated at execution time via GenerateSkillPromptAsync.</summary>
    Task<List<TaskPlanItem>> DecomposeTasksAsync(string enhancedPrompt, string originalPrompt);

    /// <summary>Generate the input prompt for a single skill task at execution time.
    /// Uses the skill's ExpectedSkillInput (from config) to determine what information to include.
    /// Called inline during task execution when all context (including upstream outputs) is available.</summary>
    Task<string> GenerateSkillPromptAsync(TaskPlanItem task, string skillDescription, string expectedSkillInput,
        string domainKnowledge, string uiContext, string upstreamOutputs, string originalUserPrompt,
        string taskPlanSummary = "");

    /// <summary>Load AgentSkill configs by exact name for skills referenced in a task plan.</summary>
    Task<List<AgentSkill>> GetSkillsByNameAsync(List<string> skillNames);

    /// <summary>Execute a single skill agent with its tools.</summary>
    Task<CXOAgentResponse> ExecuteSkillAsync(AgentSkill skillInfo, string prompt, List<AITool> resolvedTools);

    /// <summary>Summarize the conversation and store in conversation history and long-term memory.
    /// When <paramref name="freshSkillOutputs"/> is provided, Org-scoped memory extraction uses only
    /// fresh skill outputs to avoid the cyclic freshness problem (re-extracting injected memory).</summary>
    Task SummarizeAndStoreAsync(string userId, string sessionId, string conversationContent, string? freshSkillOutputs = null, string? requestId = null);

    /// <summary>Resolve tool instances for a skill using reflection.</summary>
    List<AITool> ResolveTools(AgentSkill skillInfo);

    /// <summary>Set the session context on all tool instances so they can send notifications.</summary>
    void SetToolSession(string sessionId);

    /// <summary>Set continuation payload on all tool instances for multi-round external agent delegation.</summary>
    void SetToolContinuationPayload(string? payloadJson);

    /// <summary>Get rolling session summary for a user and session.</summary>
    Task<string?> GetSessionSummaryAsync(string userId, string sessionId);

    /// <summary>Get the most recent N conversation turns for a session.</summary>
    Task<List<ConversationTurnEntry>?> GetRecentHistoryAsync(string userId, string sessionId, int lastN = 5);
}
