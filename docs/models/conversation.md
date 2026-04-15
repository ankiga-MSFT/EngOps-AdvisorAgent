# Conversation Management

The advisor agent supports multi-turn conversations, enabling context-aware follow-up questions and continuity across requests.

## Architecture

```
┌──────────────┐     ┌──────────────────┐     ┌──────────────┐
│ Orchestrator │────▶│ IConversationStore│────▶│ Cosmos DB    │
│              │◀────│                  │◀────│ (production) │
└──────────────┘     └──────────────────┘     └──────────────┘
                              │
                              ├──────────────▶ InMemoryStore
                              │                (development)
```

## IConversationStore Interface

```csharp
public interface IConversationStore
{
    Task AppendTurnAsync(string userId, string sessionId, ConversationTurn turn);
    Task<List<ConversationTurn>> GetRecentTurnsAsync(string userId, string sessionId, int count = 5);
}
```

## Implementations

### CosmosConversationStore (Production)

Backed by Azure Cosmos DB with append-only document updates.

**Document Structure:**
```json
{
  "id": "{sessionId}_history",
  "SessionId": "sess-abc-123",
  "userId": "user@contoso.com",
  "turns": [
    {
      "prompt": "Show me cost savings",
      "response": "## Insights\n...",
      "timestamp": "2025-01-15T10:30:00Z",
      "requestId": "req-001"
    },
    {
      "prompt": "Break that down by resource group",
      "response": "## Resource Group Breakdown\n...",
      "timestamp": "2025-01-15T10:31:00Z",
      "requestId": "req-002"
    }
  ],
  "updatedAt": "2025-01-15T10:31:00Z"
}
```

**Partitioning:** `/SessionId` — each conversation session is an independent partition.

**AppendTurnAsync behavior:**
1. Read existing document by `{sessionId}_history`
2. If found → append turn to `turns` array → replace document
3. If 404 → create new document with first turn

**GetRecentTurnsAsync behavior:**
1. Read document by ID and partition key
2. Return last `count` turns via `Skip()` + `Take()`
3. Return empty list on 404

### InMemoryConversationStore (Development)

Uses `ConcurrentDictionary<string, List<ConversationTurn>>` with key format `"{userId}:{sessionId}"`.

Thread-safe via locks around list operations. No persistence — data is lost on restart.

## How History Is Used

Conversation history flows into every LLM call via `BuildMessagesWithHistory()`:

```
Chat Messages Sequence:
┌─────────────────────────────┐
│ [System] System prompt       │
├─────────────────────────────┤
│ [User]   Turn 1 prompt       │
│ [Asst]   Turn 1 response     │  ← truncated to 1000 chars
├─────────────────────────────┤
│ [User]   Turn 2 prompt       │
│ [Asst]   Turn 2 response     │  ← truncated to 1000 chars
├─────────────────────────────┤
│ [User]   Current prompt      │
└─────────────────────────────┘
```

### Context Window Management

- Only the **5 most recent turns** are loaded
- Each historical response is **truncated to 1,000 characters**
- This keeps the context window budget predictable: ~5 turns × ~1K chars ≈ 5K chars ≈ 1.25K tokens

### Conversation Persistence Timing

Within the orchestrator pipeline:
1. **Start:** Load history (Step 0) — before any LLM calls
2. **End:** Save turn (final step) — after the response is generated

```
Orchestration Start
    │
    ├─ LoadConversationHistoryActivity
    │   └─ store.GetRecentTurnsAsync(userId, sessionId, 5)
    │
    ├─ ... pipeline steps ...
    │
    └─ SaveConversationTurnActivity
        └─ store.AppendTurnAsync(userId, sessionId, {prompt, response})
```

## Multi-Turn Example

**Turn 1:**
- User: "What's the resiliency score of my production subscription?"
- Agent: Executes ResiliencySkill → returns score and recommendations

**Turn 2:**
- User: "What about cost savings?"
- Agent: Loads Turn 1 from history → classifies intent as ActionRequired → executes CostOptimizationSkill
- Context from Turn 1 tells the LLM the user is already scoped to the production subscription

**Turn 3:**
- User: "Can you break that down by resource group?"
- Agent: Loads Turn 1 + Turn 2 from history → understands "that" refers to cost savings → generates resource-group-level breakdown

## Configuration

The conversation store implementation is selected at startup via environment variable:

| `CONVERSATION_STORE_TYPE` | Implementation |
|--------------------------|----------------|
| `Cosmos` | `CosmosConversationStore` |
| `InMemory` (or any other value) | `InMemoryConversationStore` |
