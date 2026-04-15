# Orchestration Pipeline

The orchestration pipeline is implemented in `AgentOrchestrationService` — the central brain of the system. It uses Azure OpenAI for every decision point and `Microsoft.Extensions.AI` for tool invocation.

## Pipeline Stages

### Stage 1: Azure Context Resolution

```csharp
ResolveAzureContextAsync(prompt, existingContext, conversationHistory)
```

The LLM extracts Azure resource identifiers from the user's prompt and conversation history:

- **Subscription IDs** (single or multiple)
- **Resource groups** (single or multiple)
- **Resource names** (single or multiple)
- **Resource type** (e.g., `Microsoft.Compute/virtualMachines`)
- **Resource ID** (full ARM resource path)
- **Region** (e.g., `eastus`)

**Key behaviors:**
- If `existingContext` already has scope (`HasScope == true`), it is reused without an LLM call
- The LLM is explicitly instructed to **extract, never invent** identifiers
- Both singular fields (`SubscriptionId`) and plural fields (`SubscriptionIds`) are supported
- JSON code fences in LLM output are automatically stripped

**Output:** `AzureContext` object with de-duplicated scope fields.

### Stage 2: Intent Classification

```csharp
ClassifyIntentAsync(prompt, conversationHistory, skillDefinitions)
```

The LLM categorizes the user's request into one of three intents:

| Intent | Description | Pipeline Path |
|--------|-------------|---------------|
| `Informational` | General Azure knowledge question | Direct LLM answer |
| `ActionRequired` | Requires live Azure data via skills | Task decomposition → skill execution |
| `Unknown` | Ambiguous or unsupported | Error response |

**Key behaviors:**
- The LLM receives the list of available skills as context
- Follow-up messages in a conversation are treated as continuations of prior intent, not new `Unknown` inputs
- Returns structured JSON: `{ "intent": "...", "reasoning": "..." }`

### Stage 3: Direct Answer (Informational path)

```csharp
AnswerDirectlyAsync(prompt, azureContext, conversationHistory)
```

For informational queries, the LLM answers directly using its knowledge and the conversation context. The system prompt positions it as an expert across:
- Reliability & resiliency
- Cost optimization
- Security best practices
- Operational excellence

No tools are invoked for this path.

### Stage 4: Task Decomposition (ActionRequired path)

```csharp
DecomposeTasksAsync(prompt, azureContext, conversationHistory, skillDefinitions)
```

The LLM breaks down the user's request into a task plan — a JSON array of tasks, each assigned to exactly one skill:

```json
[
  {
    "task": "Retrieve cost optimization recommendations",
    "skillName": "CostOptimizationSkill",
    "dependsOn": []
  },
  {
    "task": "Analyze resiliency posture of identified resources",
    "skillName": "ResiliencySkill",
    "dependsOn": [0]
  }
]
```

**Key behaviors:**
- Each task is assigned to exactly one skill from the catalog
- `dependsOn` is an array of 0-based task indices (forming a DAG)
- The LLM retries up to 2 times on JSON parse failure
- Tasks are validated against known skills and topologically sorted

### Stage 5: Skill Prompt Generation

```csharp
GenerateSkillPromptAsync(task, skillDefinition, azureContext, upstreamOutputs, conversationHistory)
```

For each task, a skill-specific prompt is constructed containing:
1. The task description
2. Skill's `SystemPrompt` and `ExpectedInput`
3. Azure context (subscription, resource group, etc.)
4. Outputs from upstream dependency tasks (context chaining)
5. Relevant conversation history

### Stage 6: Skill Execution

```csharp
ExecuteSkillAsync(skillDefinition, prompt, accessToken)
```

Each skill executes as a self-contained LLM conversation with tool access:

1. **Tool resolution** — parses tool names (`ClassName-MethodName`), finds method via reflection, wraps with `AIFunctionFactory`
2. **Access token injection** — calls `SetAccessToken()` on each tool instance so they can make authenticated ARM/ARG calls
3. **Chat completion** — creates an `AzureOpenAI` chat client with `FunctionInvocation` middleware
4. **Tool loop** — the LLM calls tools as needed; the middleware automatically invokes them and feeds results back
5. **Response parsing** — the structured response is extracted and wrapped in `AdvisorAgentResponse`

**Execution parameters per skill:**
- `ModelName` — defaults to `gpt-4o`
- `Temperature` — `0.0` for deterministic output
- `Timeout` — `120s` default per skill

## Conversation History Integration

All LLM calls (context resolution, intent classification, task decomposition, direct answer, skill execution) receive conversation history via `BuildMessagesWithHistory()`:

```
[System Prompt]
[Turn 1: User prompt] → [Turn 1: Assistant response (truncated to 1000 chars)]
[Turn 2: User prompt] → [Turn 2: Assistant response (truncated to 1000 chars)]
...
[Current User prompt]
```

This enables context-aware follow-up questions like:
- "What about subscription B?" (references prior subscription discussion)
- "Can you break that down by resource group?" (references prior analysis)

## Error Handling

| Scenario | Behavior |
|----------|----------|
| LLM returns invalid JSON | Retry up to 2 times; fall back to raw text |
| Unknown skill in task plan | Filtered out by `PlanValidator.RemoveUnknownSkills()` |
| Cycle in task dependencies | `TopologicalSort()` throws; orchestrator falls back to sequential execution |
| Skill execution timeout | 120s default; returns timeout error |
| Tool method not found | Logged as warning; missing tool skipped |
| ARM API failure | Exception propagated; skill returns failure response |
