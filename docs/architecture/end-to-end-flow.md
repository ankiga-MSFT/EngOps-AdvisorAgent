# End-to-End Flow

This page traces a user request from HTTP entry to final response, covering every step the system takes.

## Request Lifecycle

```
User Request (HTTP POST)
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│  AdvisorHttpTrigger.cs — POST /api/advisor/orchestrate          │
│  • Extract userId, prompt, sessionId, accessToken               │
│  • Generate sessionId if absent                                 │
│  • Schedule durable orchestration instance                      │
│  • Return 202 Accepted { instanceId, sessionId, statusUri }    │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  AdvisorOrchestratorMain — Durable Orchestration                │
│                                                                 │
│  Step 0 ─ Load Conversation History                             │
│     └─→ ConversationStore.GetRecentTurnsAsync (last 5 turns)   │
│                                                                 │
│  Step 1 ─ Resolve Azure Context                                 │
│     └─→ LLM extracts subscription, resource group, resource     │
│         IDs from prompt + history                               │
│                                                                 │
│  Step 1.5 ─ Subscription Gate (conditional)                     │
│     └─→ If no subscriptions found:                              │
│         • Fetch all subscriptions via ARM API                   │
│         • Return subscriptionPicker UI action                   │
│         • ⏸ Wait for user selection                              │
│                                                                 │
│  Step 2 ─ Classify Intent                                       │
│     └─→ LLM classifies: Informational│ActionRequired│Unknown   │
│                                                                 │
│  Step 3 ─ Route by Intent                                       │
│     ├─ Informational → LLM answers directly → Done              │
│     ├─ Unknown → Return error → Done                            │
│     └─ ActionRequired → Continue to Step 4                      │
│                                                                 │
│  Step 4 ─ Decompose Tasks                                       │
│     └─→ LLM generates task plan (JSON array)                   │
│         • Each task → one skill + dependencies                  │
│         • Validated & topologically sorted                      │
│                                                                 │
│  Step 5 ─ Execute Skills (Sub-Orchestrator)                     │
│     └─→ For each task in sorted order:                          │
│         1. Gather upstream outputs                              │
│         2. Generate skill-specific prompt                       │
│         3. Execute skill (LLM + tool invocations)               │
│         4. Store result for downstream tasks                    │
│     └─→ Aggregate all skill outputs                             │
│                                                                 │
│  Final ─ Save Conversation Turn                                 │
│     └─→ ConversationStore.AppendTurnAsync                       │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  Client polls GET /api/advisor/status/{instanceId}              │
│  • runtimeStatus: Running│Completed│Failed                     │
│  • customStatus: real-time step progress                        │
│  • output: final AdvisorAgentResponse                           │
└─────────────────────────────────────────────────────────────────┘
```

## Walkthrough: "Show me cost savings for my production subscription"

### 1. HTTP Entry
The client sends:
```json
{
  "userId": "user@contoso.com",
  "prompt": "Show me cost savings for my production subscription",
  "sessionId": "sess-abc-123"
}
```
The `Authorization: Bearer <token>` header carries the user's Azure access token.

The trigger returns `202 Accepted` with an `instanceId` and status polling URI.

### 2. Load Conversation History
The orchestrator loads the last 5 conversation turns for `user@contoso.com / sess-abc-123` from Cosmos DB. These provide context for follow-up questions.

### 3. Resolve Azure Context
The LLM analyzes the prompt and conversation history to extract Azure identifiers:
```json
{
  "SubscriptionId": null,
  "ResourceGroup": null,
  "ResourceType": null
}
```
No subscription ID was mentioned explicitly, so context is empty.

### 4. Subscription Gate
Since no subscriptions were resolved, the orchestrator:
1. Calls `SubscriptionTools.ListSubscriptions()` via ARM API
2. Returns an `AdvisorAgentResponse` with:
   - `NeedsUserInput = true`
   - `UiAction = "subscriptionPicker"`
   - `UiData` = list of subscription names and IDs

The client renders a picker. The user selects "Production (sub-prod-001)".

### 5. Classify Intent
With the subscription now in context, the LLM classifies the intent:
```json
{
  "intent": "ActionRequired",
  "reasoning": "User is requesting cost analysis which requires querying Azure Advisor and cost data"
}
```

### 6. Decompose Tasks
The LLM generates a task plan:
```json
[
  {
    "task": "Retrieve cost optimization recommendations from Azure Advisor",
    "skillName": "CostOptimizationSkill",
    "dependsOn": []
  }
]
```

### 7. Execute Skills
The sub-orchestrator processes the single task:
1. **Generate prompt** — creates a skill-specific prompt with the user's query, subscription context, and CostOptimizationSkill system prompt
2. **Execute skill** — the LLM calls tools:
   - `CostOptimizationTools-GetCostRecommendations(sub-prod-001)` → returns Advisor cost recommendations
   - `CostOptimizationTools-EstimateSavings(sub-prod-001)` → returns estimated monthly savings
   - `AdvisorRecommendationTools-GetRecommendations(sub-prod-001, "Cost")` → returns detailed cost recs
3. **Format response** — the LLM synthesizes tool results into a structured markdown response

### 8. Save & Return
The conversation turn is saved to Cosmos DB. The status endpoint now returns the completed response with cost savings recommendations.

## Intent Routing Summary

```
                    ┌─────────────────┐
                    │  Classify Intent │
                    └────────┬────────┘
                             │
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
        Informational   ActionRequired   Unknown
              │              │              │
              ▼              ▼              ▼
        Answer Directly  Decompose →    Return Error
        (single LLM      Execute Skills
         call)           (multi-step)
```

- **Informational** — general Azure knowledge questions answered directly by the LLM using conversation history and context
- **ActionRequired** — queries requiring live Azure data are decomposed into skill-based tasks and executed
- **Unknown** — ambiguous queries that don't match any known capability
