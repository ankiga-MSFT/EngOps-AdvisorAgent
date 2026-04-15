using System.Text.Json.Serialization;
using AdvisorAgent.Core.Models;

namespace AdvisorAgent.Functions.Models;

// ── Orchestrator Input / Output ──────────────────────────

public sealed class AdvisorOrchestratorInput
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }
}

// ── Conversation History DTO ────────────────────────────

public sealed class ConversationTurnDto
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;
}

// ── Activity Inputs ──────────────────────────────────────

public sealed class LoadConversationHistoryInput
{
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public int Count { get; set; } = 5;
}

public sealed class SaveConversationTurnInput
{
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class ResolveContextInput
{
    public string Prompt { get; set; } = string.Empty;
    public List<ConversationTurnDto> ConversationHistory { get; set; } = [];
}

public sealed class ClassifyIntentInput
{
    public string Prompt { get; set; } = string.Empty;
    public string AzureContextSummary { get; set; } = string.Empty;
    public List<ConversationTurnDto> ConversationHistory { get; set; } = [];
}

public sealed class DecomposeTasksInput
{
    public string Prompt { get; set; } = string.Empty;
    public string AzureContextSummary { get; set; } = string.Empty;
    public List<ConversationTurnDto> ConversationHistory { get; set; } = [];
}

public sealed class GenerateSkillPromptInput
{
    public string TaskLabel { get; set; } = string.Empty;
    public string SkillDescription { get; set; } = string.Empty;
    public string ExpectedInput { get; set; } = string.Empty;
    public string AzureContextSummary { get; set; } = string.Empty;
    public string UpstreamOutputs { get; set; } = string.Empty;
    public string OriginalPrompt { get; set; } = string.Empty;
    public List<ConversationTurnDto> ConversationHistory { get; set; } = [];
}

public sealed class FetchSubscriptionsInput
{
    public string? AccessToken { get; set; }
}

public sealed class SubscriptionSummary
{
    [JsonPropertyName("subscriptionId")]
    public string SubscriptionId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ExecuteSkillInput
{
    public string SkillName { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
}

public sealed class SkillExecutionResult
{
    public bool IsSuccess { get; set; }
    public string Response { get; set; } = string.Empty;
    public bool NeedsUserInput { get; set; }
}

public sealed class PublishStatusInput
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public string StepState { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public sealed class PublishCompletedInput
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public AdvisorAgentResponse Response { get; set; } = new();
}

// ── Sub-Orchestrator ─────────────────────────────────────

public sealed class SkillExecutionSubInput
{
    public string Prompt { get; set; } = string.Empty;
    public string AzureContextSummary { get; set; } = string.Empty;
    public List<TaskPlanItemDto> TaskPlan { get; set; } = [];
    public List<int> ExecutionOrder { get; set; } = [];
    public string SessionId { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public List<ConversationTurnDto> ConversationHistory { get; set; } = [];
}

public sealed class TaskPlanItemDto
{
    [JsonPropertyName("task")]
    public string Task { get; set; } = string.Empty;

    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = string.Empty;

    [JsonPropertyName("dependsOn")]
    public List<int> DependsOn { get; set; } = [];
}

public sealed class SubOrchestratorResult
{
    public bool IsSuccess { get; set; }
    public string AggregatedResponse { get; set; } = string.Empty;
}

// ── Orchestration Progress (for SetCustomStatus) ─────

public sealed class OrchestrationProgress
{
    [JsonPropertyName("steps")]
    public List<StepProgress> Steps { get; set; } = [];

    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }
}

public sealed class StepProgress
{
    [JsonPropertyName("stepName")]
    public string StepName { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
