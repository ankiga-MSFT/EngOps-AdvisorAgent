# Azure Context Resolution

Azure Context Resolution is the first step in the orchestration pipeline. It uses the LLM to extract Azure resource identifiers from the user's natural language prompt and conversation history.

## Purpose

Before the agent can execute any skill, it needs to know **which Azure resources** the user is asking about. The context resolver extracts:

- Subscription IDs (one or many)
- Resource groups (one or many)
- Resource names (one or many)
- Resource type (e.g., `Microsoft.Compute/virtualMachines`)
- Full ARM resource ID
- Region (e.g., `eastus`)

## Resolution Flow

```
┌─────────────────────────────────────────┐
│ Input:                                   │
│  • User prompt                           │
│  • Existing context (from prior steps)   │
│  • Conversation history (last 5 turns)   │
└──────────────────┬──────────────────────┘
                   │
                   ▼
           ┌──────────────┐
           │ HasScope?     │
           └──────┬───────┘
              Yes │    No
              ┌───┘    └───┐
              ▼            ▼
    ┌──────────────┐  ┌──────────────┐
    │ Reuse existing│  │ LLM Context  │
    │ context       │  │ Extraction   │
    └──────────────┘  └──────┬───────┘
                             │
                             ▼
                   ┌──────────────────┐
                   │ Parse JSON output │
                   │ → AzureContext    │
                   └──────────────────┘
```

## LLM Extraction

The system prompt instructs the LLM to:

1. **Extract** Azure identifiers mentioned in the prompt — never invent them
2. Support both singular fields (`SubscriptionId`) and plural fields (`SubscriptionIds`)
3. Return a JSON object matching the `AzureContext` schema
4. Use conversation history to resolve pronouns and references (e.g., "that subscription" → the one from the previous turn)

### Example

**Prompt:** "Check the resiliency of VMs in resource group prod-rg in subscription abc-123"

**LLM Output:**
```json
{
  "SubscriptionId": "abc-123",
  "ResourceGroup": "prod-rg",
  "ResourceType": "Microsoft.Compute/virtualMachines"
}
```

**Prompt:** "Now check the other subscription too"  
**History:** Turn 1 discussed subscription `abc-123`, Turn 2 mentioned `def-456`

**LLM Output:**
```json
{
  "SubscriptionIds": ["abc-123", "def-456"],
  "ResourceType": "Microsoft.Compute/virtualMachines"
}
```

## Subscription Gate

When context resolution produces an `AzureContext` with no subscription scope (`HasScope == false`), the orchestrator enters the **Subscription Gate**:

1. Calls `SubscriptionTools.ListSubscriptions()` via ARM API
2. Returns up to 10 subscriptions as `SubscriptionSummary` objects
3. Sends response with `UiAction = "subscriptionPicker"` to the client
4. Client renders a subscription picker; user selects subscriptions
5. New orchestration starts with the selected subscription(s) in context

```
No subscriptions in context
        │
        ▼
┌─────────────────┐     ┌──────────────────┐
│ FetchSubscriptions│──▶│ Return picker      │
│ Activity          │    │ UiAction:          │
│ (ARM API call)    │    │ "subscriptionPicker"│
└─────────────────┘     └──────────────────┘
```

## De-duplication

The `AzureContext` model provides helper methods that merge singular and plural fields:

```
GetAllSubscriptionIds():
    Union(SubscriptionId, SubscriptionIds) → Distinct()

GetAllResourceGroups():
    Union(ResourceGroup, ResourceGroups) → Distinct()

GetAllResourceNames():
    Union(ResourceName, ResourceNames) → Distinct()
```

This handles cases where the LLM populates both `SubscriptionId` and `SubscriptionIds`, or where the same ID appears in both fields.

## Architecture

```csharp
// Interface
public interface IAzureContextResolver
{
    Task<AzureContext> ResolveAsync(
        string prompt,
        AzureContext existingContext,
        List<ConversationTurn> conversationHistory);
}

// Implementation delegates to orchestration service
public class AzureContextResolver : IAzureContextResolver
{
    public async Task<AzureContext> ResolveAsync(...)
    {
        return await _orchestrationService.ResolveAzureContextAsync(
            prompt, existingContext, conversationHistory);
    }
}
```

The resolver is a thin wrapper around `IAgentOrchestrationService.ResolveAzureContextAsync()`, allowing the context resolution step to be independently testable and replaceable.
