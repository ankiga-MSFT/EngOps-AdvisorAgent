using CXOAI.SkillFramework;
using CXOAI.Memory;
using CXOAI.StatusNotifier;
using System.Text.Json.Serialization;

namespace CXOAI.Functions.Models;

/// <summary>
/// HTTP request body to start an orchestration.
/// </summary>
public class OrchestratorInput
{
    public string UserId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Unique identifier for this request. If not provided by the caller, the orchestrator generates one.</summary>
    public string? RequestId { get; set; }

    /// <summary>UI context payload containing entity, filters, and other page state.</summary>
    [JsonPropertyName("context")]
    public UserContext? UserContext { get; set; }

    /// <summary>Bearer access token from the Authorization header, set by the HTTP trigger after middleware validation.</summary>
    public string? AccessToken { get; set; }
}

public class ClassifyIntentInput
{
    public string Prompt { get; set; } = string.Empty;
    public string GeneralKnowledge { get; set; } = string.Empty;

    /// <summary>Entity name from UI context, if the user is viewing a specific entity page.</summary>
    public string? UIContextEntityName { get; set; }

    /// <summary>Session ID for log correlation.</summary>
    public string SessionId { get; set; } = string.Empty;
}

public class KnowledgeAnswerInput
{
    public string Prompt { get; set; } = string.Empty;
    public string GeneralKnowledge { get; set; } = string.Empty;

    /// <summary>Session ID for log correlation.</summary>
    public string SessionId { get; set; } = string.Empty;
}

public class DecomposeTasksInput
{
    public string EnhancedPrompt { get; set; } = string.Empty;
    public string OriginalPrompt { get; set; } = string.Empty;

    /// <summary>Session ID for log correlation.</summary>
    public string SessionId { get; set; } = string.Empty;
}

public class SkillsByNameInput
{
    public List<string> SkillNames { get; set; } = [];
}

public class SkillExecutionInput
{
    public string EnhancedPrompt { get; set; } = string.Empty;
    public string OriginalPrompt { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public List<AgentSkill> Skills { get; set; } = [];
    public List<TaskPlanItem> TaskPlan { get; set; } = [];
    public List<string> Order { get; set; } = [];
    public HistoryAnswerResult historyResult  { get; set; }

    /// <summary>Domain knowledge (metric definitions, relationships) from the knowledge graph.</summary>
    public string? DomainKnowledge { get; set; }

    /// <summary>UI context (entity, filters, page state) forwarded from the authenticated request.</summary>
    public UserContext? UserContext { get; set; }

    /// <summary>Bearer access token forwarded from the authenticated request.</summary>
    public string? AccessToken { get; set; }
    public OrchestratorStatus Status { get; set; } = new();
}

public class GenerateSkillPromptInput
{
    public TaskPlanItem Task { get; set; } = new();
    public string SkillDescription { get; set; } = string.Empty;
    public string ExpectedSkillInput { get; set; } = string.Empty;
    public string DomainKnowledge { get; set; } = string.Empty;
    public string UIContext { get; set; } = string.Empty;
    public string UpstreamOutputs { get; set; } = string.Empty;
    public string OriginalUserPrompt { get; set; } = string.Empty;

    /// <summary>Session ID for log correlation.</summary>
    public string SessionId { get; set; } = string.Empty;
}

public class ExecuteSkillInput
{
    public string SkillName { get; set; } = string.Empty;
    public AgentSkill SkillInfo { get; set; } = new();
    public string Prompt { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = [];
    public string? UserResponse { get; set; }

    /// <summary>Bearer access token forwarded from the authenticated request.</summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Opaque continuation token from the previous execution round.
    /// Serialized JObject string. Passed to the tool so it can detect
    /// continuation mode (e.g., reconnect to an external agent after user input).
    /// </summary>
    public string? PayloadJson { get; set; }
}

public class SkillExecutionResult
{
    public bool IsSuccess { get; set; } = true;
    public bool NeedsUserInput { get; set; }
    public bool IsReport { get; set; }
    public string Response { get; set; } = string.Empty;
    public string? UserPrompt { get; set; }
    public bool IsUIComponent { get; set; }
    public string UIComponent { get; set; } = string.Empty;

    /// <summary>Serialized JSON of the opaque continuation token (JObject).
    /// Stored as string to avoid Newtonsoft/System.Text.Json serialization issues
    /// in Durable Functions activity results.</summary>
    public string? PayloadJson { get; set; }
}

public class CheckHistoryInput
{
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Entity name from UI context, for disambiguating multi-entity history.</summary>
    public string? UIContextEntityName { get; set; }
}

public class SummarizeInput
{
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ConversationContent { get; set; } = string.Empty;
    /// <summary>When provided, Org-scoped memory extraction uses only these fresh skill outputs
    /// instead of the full conversation content. Prevents the cyclic freshness problem where
    /// injected Org memory facts get re-extracted and their TTL is reset.</summary>
    public string? FreshSkillOutputs { get; set; }
    /// <summary>Request ID for per-request log correlation and history tracking.</summary>
    public string? RequestId { get; set; }
}

public class PublishStatusInput
{
    public string SessionId { get; set; } = string.Empty;
    public OrchestratorStatus Status { get; set; } = new();
}

public class UserInputNotification
{
    public string SessionId { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Full skill execution result for rich UI rendering (buttons, payload, UI components).
    /// Nullable for backward compatibility — older in-flight orchestrations will have this as null.</summary>
    public SkillExecutionResult? SkillResult { get; set; }
}

public class PublishCompletedInput
{
    public string SessionId { get; set; } = string.Empty;
    public CXOAgentResponse Result { get; set; } = new();
}
