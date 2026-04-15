# Durable Functions

The advisor agent uses **Azure Durable Functions** to manage the multi-step orchestration pipeline. This provides resilient, resumable, long-running workflows with built-in state management.

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  AdvisorOrchestratorMain (Orchestrator)                       │
│                                                              │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │LoadHistory│  │Resolve   │  │Classify  │  │Decompose │    │
│  │Activity   │→ │Context   │→ │Intent    │→ │Tasks     │    │
│  │           │  │Activity  │  │Activity  │  │Activity  │    │
│  └──────────┘  └──────────┘  └──────────┘  └────┬─────┘    │
│                                                   │          │
│  ┌─────────────────────────────────────────────────┘          │
│  │                                                           │
│  ▼                                                           │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │  SkillExecutionSubOrchestrator (Sub-Orchestrator)       │ │
│  │                                                         │ │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐              │ │
│  │  │Generate  │→ │Execute   │→ │Generate  │→ ...         │ │
│  │  │Prompt    │  │Skill     │  │Prompt    │              │ │
│  │  │Activity  │  │Activity  │  │Activity  │              │ │
│  │  └──────────┘  └──────────┘  └──────────┘              │ │
│  └─────────────────────────────────────────────────────────┘ │
│                                                              │
│  ┌──────────┐                                                │
│  │SaveTurn  │                                                │
│  │Activity  │                                                │
│  └──────────┘                                                │
└──────────────────────────────────────────────────────────────┘
```

## Orchestrator: `AdvisorOrchestratorMain`

The main orchestrator implements the full advisory pipeline as a sequence of activity calls. Each step calls one or more activities and publishes progress status.

### Input

```csharp
public class AdvisorOrchestratorInput
{
    public string UserId { get; set; }
    public string Prompt { get; set; }
    public string SessionId { get; set; }
    public string RequestId { get; set; }
    public string AccessToken { get; set; }
}
```

### Progress Tracking

After each pipeline step, the orchestrator:
1. Calls `PublishStatusActivity` to log progress
2. Sets `CustomStatus` on the orchestration instance via `SetCustomStatus()`
3. Maintains an `OrchestrationProgress` object with step details

```csharp
public class OrchestrationProgress
{
    public List<StepProgress> Steps { get; set; }
    public bool IsCompleted { get; set; }
}

public class StepProgress
{
    public string StepName { get; set; }
    public string State { get; set; }     // "InProgress", "Completed", "Failed"
    public string Message { get; set; }
}
```

Clients poll `GET /api/advisor/status/{instanceId}` and read `customStatus` for real-time progress.

### Subscription Gate

When no subscription scope is resolved from the prompt, the orchestrator enters an interactive gate:

1. Calls `FetchSubscriptionsActivity` — lists all subscriptions via ARM API
2. Returns response with `NeedsUserInput = true` and `UiAction = "subscriptionPicker"`
3. The client displays a subscription picker; the user selects subscriptions
4. A new orchestration is started with the selected subscription context

If 10 or more subscriptions are available, only the first 10 are returned to avoid UI overload.

## Sub-Orchestrator: `SkillExecutionSubOrchestrator`

Handles execution of the task plan after decomposition:

1. Receives: task plan items, execution order (topologically sorted), access token, Azure context
2. For each task in execution order:
   - Collects upstream task outputs (from `dependsOn` references)
   - Calls `GenerateSkillPromptActivity` → builds context-rich prompt
   - Calls `ExecuteSkillActivity` → runs skill with LLM + tools
   - Stores result indexed by task position
3. Aggregates all results into a single markdown response

### Output

```csharp
public class SubOrchestratorResult
{
    public bool IsSuccess { get; set; }
    public string AggregatedResponse { get; set; }
}
```

## Activity Functions

All 12 activities are defined in `AdvisorActivities.cs`:

| Activity | Input → Output | Purpose |
|----------|---------------|---------|
| `LoadConversationHistoryActivity` | `LoadConversationHistoryInput` → `ConversationTurnDto[]` | Load recent conversation turns |
| `SaveConversationTurnActivity` | `SaveConversationTurnInput` → void | Persist prompt + response |
| `ResolveContextActivity` | `ResolveContextInput` → `AzureContext` | LLM-based Azure scope extraction |
| `ClassifyIntentActivity` | `ClassifyIntentInput` → `UserIntent` | LLM intent classification |
| `AnswerDirectlyActivity` | `ClassifyIntentInput` → `AdvisorAgentResponse` | Direct LLM answer for informational queries |
| `DecomposeTasksActivity` | `DecomposeTasksInput` → `TaskPlanItem[]` | LLM task plan generation |
| `GetSkillDefinitionsActivity` | void → `Dictionary<string, AgentSkillDefinition>` | Return skill catalog |
| `GenerateSkillPromptActivity` | `GenerateSkillPromptInput` → `string` | Build skill-specific prompt with context |
| `ExecuteSkillActivity` | `ExecuteSkillInput` → `SkillExecutionResult` | Run skill with LLM + tool invocations |
| `FetchSubscriptionsActivity` | `FetchSubscriptionsInput` → `SubscriptionSummary[]` | List Azure subscriptions via ARM |
| `PublishStatusActivity` | `PublishStatusInput` → void | Log step progress |
| `PublishCompletedActivity` | `PublishCompletedInput` → void | Log orchestration completion |

## Runtime Configuration

From `host.json`:

```json
{
  "extensions": {
    "durableTask": {
      "hubName": "%TASKHUB_NAME%",
      "storageProvider": {
        "type": "azure-managed"
      },
      "maxConcurrentActivityFunctions": 10,
      "maxConcurrentOrchestratorFunctions": 5
    }
  }
}
```

| Setting | Value | Purpose |
|---------|-------|---------|
| `hubName` | Environment variable | Isolates task hubs across environments |
| `storageProvider` | `azure-managed` | Uses Azure-managed Durable Task backend (not Azure Storage) |
| `maxConcurrentActivityFunctions` | 10 | Limits parallel activity execution |
| `maxConcurrentOrchestratorFunctions` | 5 | Limits parallel orchestrator replay |
