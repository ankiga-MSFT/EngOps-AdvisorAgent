# Configuration

The advisor agent is configured through environment variables, `host.json`, and the declarative skill catalog.

## Environment Variables

### Required

| Variable | Description | Example |
|----------|-------------|---------|
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI service endpoint | `https://myopenai.openai.azure.com/` |
| `AZURE_OPENAI_MODEL` | Model deployment name | `gpt-4o` |
| `CONVERSATION_STORE_TYPE` | Conversation storage backend | `Cosmos` or `InMemory` |

### Cosmos DB (when `CONVERSATION_STORE_TYPE=Cosmos`)

| Variable | Description | Example |
|----------|-------------|---------|
| `COSMOS_CONVERSATION_ENDPOINT` | Cosmos DB account endpoint | `https://mydb.documents.azure.com:443/` |
| `COSMOS_CONVERSATION_DATABASE` | Database name | `advisoragent` |
| `COSMOS_CONVERSATION_CONTAINER` | Container name | `conversations` |

### Durable Functions

| Variable | Description | Example |
|----------|-------------|---------|
| `DURABLE_TASK_SCHEDULER_CONNECTION_STRING` | Durable Task Scheduler connection | `Endpoint=https://...` |
| `TASKHUB_NAME` | Durable Task hub name | `advisoragent-hub` |

### Optional

| Variable | Description | Default |
|----------|-------------|---------|
| `CORS_ALLOWED_ORIGINS` | Comma-separated allowed CORS origins | — |
| `AzureSignalRConnectionString` | SignalR service connection string | — |

## host.json

Runtime configuration for the Azure Functions host:

```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "excludedTypes": "Request"
      }
    },
    "logLevel": {
      "Azure.Core": "Warning",
      "Azure.Identity": "Warning"
    }
  },
  "extensions": {
    "durableTask": {
      "hubName": "%TASKHUB_NAME%",
      "storageProvider": {
        "type": "azure-managed"
      },
      "maxConcurrentActivityFunctions": 10,
      "maxConcurrentOrchestratorFunctions": 5
    }
  },
  "functionTimeout": "00:10:00"
}
```

### Key Settings

| Setting | Value | Purpose |
|---------|-------|---------|
| `durableTask.storageProvider.type` | `azure-managed` | Uses Azure-managed Durable Task backend instead of Azure Storage tables/blobs |
| `maxConcurrentActivityFunctions` | 10 | Maximum parallel activity executions per host instance |
| `maxConcurrentOrchestratorFunctions` | 5 | Maximum parallel orchestrator replays per host instance |
| `functionTimeout` | 10 minutes | Maximum execution time per function invocation |

## Skill Catalog (`skills.json`)

Skills are defined declaratively in `Configuration/skills.json`. This file is loaded at startup and used to drive the entire orchestration pipeline.

### Structure

```json
{
  "RetirementSkill": {
    "SkillName": "RetirementSkill",
    "Description": "...",
    "SystemPrompt": "...",
    "ModelName": "gpt-4o",
    "ExpectedInput": "...",
    "Temperature": 0.0,
    "Timeout": 120,
    "Tools": [
      { "Name": "RetirementTools-GetRetiringResources", "Description": "..." }
    ]
  }
}
```

### Adding a New Skill

1. Add a new entry in `skills.json` with a unique `SkillName`
2. Write a `SystemPrompt` that instructs the LLM on the skill's domain and response format
3. Define `Tools` referencing existing tool methods or new ones
4. Set `Temperature` (typically `0.0` for deterministic output)
5. Set `Timeout` (typically `120` seconds)
6. The orchestration engine will automatically include the new skill in intent classification and task decomposition

## Dependency Injection (`Program.cs`)

The DI container wires up all services at startup:

```
DefaultAzureCredential          → Azure SDK authentication
Dictionary<string, AgentSkillDefinition>  → Loaded from skills.json
Dictionary<string, object>      → Tool class instances (singletons)
IAgentOrchestrationService      → AgentOrchestrationService (singleton)
IConversationStore              → CosmosConversationStore or InMemoryConversationStore
IAzureContextResolver           → AzureContextResolver (singleton)
```

### Conditional Registration

```
if CONVERSATION_STORE_TYPE == "Cosmos":
    register CosmosConversationStore
else:
    register InMemoryConversationStore
```

## Concurrency & Timeouts

| Scope | Timeout | Configurable Via |
|-------|---------|-----------------|
| Skill execution | 120s per skill | `Timeout` in skills.json |
| Function invocation | 10 minutes | `functionTimeout` in host.json |
| Orchestration overall | No hard limit | Durable Functions manages checkpointing |
| Tool response size | 15K characters | `TruncateToolResponse()` in ToolBase |
