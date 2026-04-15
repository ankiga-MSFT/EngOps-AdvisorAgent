# Domain Models

The advisor agent uses a small set of domain models to represent Azure context, user intent, conversation history, and responses.

## AzureContext

Captures the Azure resource scope extracted from the user's prompt by the LLM.

```csharp
public class AzureContext
{
    // Single-value fields
    public string? SubscriptionId { get; set; }
    public string? ResourceGroup { get; set; }
    public string? ServiceGroup { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceName { get; set; }
    public string? Region { get; set; }

    // Multi-value fields
    public string[]? SubscriptionIds { get; set; }
    public string[]? ResourceGroups { get; set; }
    public string[]? ResourceNames { get; set; }
}
```

### Key Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `HasScope` | `bool` | `true` if any resource scope field is populated |
| `GetAllSubscriptionIds()` | `string[]` | De-duplicated union of `SubscriptionId` + `SubscriptionIds` |
| `GetAllResourceGroups()` | `string[]` | De-duplicated union of `ResourceGroup` + `ResourceGroups` |
| `GetAllResourceNames()` | `string[]` | De-duplicated union of `ResourceName` + `ResourceNames` |
| `ToContextSummary()` | `string` | Human-readable summary (e.g., `"Subscription: sub-123, Resource: vm-prod (Microsoft.Compute/virtualMachines), Region: eastus"`) |

### Context Resolution Flow

```
User Prompt + Conversation History
            │
            ▼
    LLM Context Extraction
    (system prompt: "extract, never invent")
            │
            ▼
    JSON parsing → AzureContext
            │
            ▼
    HasScope check
    ├─ true  → Use resolved context
    └─ false → Trigger Subscription Gate
```

---

## UserIntent

Classifies the user's request to determine pipeline routing.

```csharp
public enum UserIntentType
{
    Informational,
    ActionRequired,
    Unknown
}

public class UserIntent
{
    public UserIntentType Intent { get; set; }
    public string Reasoning { get; set; }
}
```

| Intent | Pipeline Path | Description |
|--------|--------------|-------------|
| `Informational` | Direct LLM answer | General Azure knowledge questions |
| `ActionRequired` | Task decomposition → skill execution | Queries requiring live Azure data |
| `Unknown` | Error response | Ambiguous or unsupported queries |

---

## AdvisorAgentResponse

Unified response structure returned by all skills and the orchestrator.

```csharp
public class AdvisorAgentResponse
{
    public bool IsSuccess { get; set; }
    public string Response { get; set; }        // Markdown-formatted
    public bool NeedsUserInput { get; set; }
    public string? UiAction { get; set; }       // e.g., "subscriptionPicker"
    public object? UiData { get; set; }         // Structured UI payload
    public string? Category { get; set; }       // Skill name
}
```

### Factory Methods

```csharp
AdvisorAgentResponse.Success(response, category)  // IsSuccess = true
AdvisorAgentResponse.Failure(reason)               // IsSuccess = false
```

### UI Action Pattern

When the orchestrator needs user input, it returns a response with:
- `NeedsUserInput = true`
- `UiAction` = action identifier (e.g., `"subscriptionPicker"`)
- `UiData` = structured payload the client uses to render the interaction

---

## ConversationTurn

Represents a single exchange in a multi-turn conversation.

```csharp
public class ConversationTurn
{
    public string Prompt { get; set; }
    public string Response { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? RequestId { get; set; }
}
```

---

## AgentSkillDefinition

Metadata for a skill loaded from `skills.json`.

```csharp
public class AgentSkillDefinition
{
    public string SkillName { get; set; }
    public string Description { get; set; }
    public string SystemPrompt { get; set; }
    public string ModelName { get; set; }         // Default: gpt-4o
    public string ExpectedInput { get; set; }
    public List<SkillToolRef> Tools { get; set; }
    public float Temperature { get; set; }        // Default: 0.0
    public int Timeout { get; set; }              // Default: 120s
}

public class SkillToolRef
{
    public string Name { get; set; }              // "ClassName-MethodName"
    public string Description { get; set; }
}
```

---

## TaskPlanItem

A single task in the execution plan DAG.

```csharp
public class TaskPlanItem
{
    public string Task { get; set; }              // Description of the task
    public string SkillName { get; set; }         // Which skill executes this
    public int[] DependsOn { get; set; }          // 0-based indices of prerequisites
}
```

---

## Orchestrator DTOs

Serializable data transfer objects used between orchestrator and activities:

| DTO | Purpose |
|-----|---------|
| `AdvisorOrchestratorInput` | userId, prompt, sessionId, requestId, accessToken |
| `ConversationTurnDto` | Serializable conversation turn (prompt, response) |
| `SubscriptionSummary` | subscriptionId, displayName |
| `SkillExecutionResult` | isSuccess, response, needsUserInput |
| `SubOrchestratorResult` | isSuccess, aggregatedResponse |
| `OrchestrationProgress` | steps[], isCompleted |
| `StepProgress` | stepName, state, message |
