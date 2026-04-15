# Deployment

The advisor agent is deployed as an Azure Functions app with supporting Azure services.

## Infrastructure Components

```
┌──────────────────────────────────────────────────────────┐
│  Azure Subscription                                       │
│                                                          │
│  ┌──────────────────┐  ┌────────────────────────────┐    │
│  │ Azure Functions   │  │ Azure OpenAI Service        │    │
│  │ (Isolated Worker) │──│ Model: gpt-4o / gpt-4.1    │    │
│  │                   │  └────────────────────────────┘    │
│  │ • HTTP Triggers   │                                    │
│  │ • Durable Orch.   │  ┌────────────────────────────┐    │
│  │ • Activities      │──│ Azure Cosmos DB             │    │
│  └───────┬───────────┘  │ Container: conversations    │    │
│          │              │ Partition: /SessionId        │    │
│          │              └────────────────────────────┘    │
│          │                                                │
│          │              ┌────────────────────────────┐    │
│          ├──────────────│ Durable Task Scheduler      │    │
│          │              │ (Azure-managed backend)     │    │
│          │              └────────────────────────────┘    │
│          │                                                │
│          │              ┌────────────────────────────┐    │
│          ├──────────────│ Azure SignalR Service        │    │
│          │              │ Hub: advisor                 │    │
│          │              └────────────────────────────┘    │
│          │                                                │
│          │              ┌────────────────────────────┐    │
│          └──────────────│ Application Insights        │    │
│                         │ (Telemetry & Logging)       │    │
│                         └────────────────────────────┘    │
└──────────────────────────────────────────────────────────┘
```

## Authentication

### Managed Identity

The Functions app uses `DefaultAzureCredential` for server-to-server authentication:
- **Azure OpenAI** — managed identity with Cognitive Services User role
- **Cosmos DB** — managed identity with Cosmos DB Data Contributor role
- **Durable Task Scheduler** — connection string from app settings

### User Identity

ARM and Resource Graph API calls use the **caller's Bearer token** (passed via HTTP request). The agent operates with the caller's Azure permissions — no elevation occurs.

## Cosmos DB Schema

### Container: `conversations`

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | `{sessionId}_history` |
| `SessionId` | string | Partition key |
| `userId` | string | Owner of the conversation |
| `turns` | array | Append-only array of conversation turns |
| `turns[].prompt` | string | User message |
| `turns[].response` | string | Agent response |
| `turns[].timestamp` | string | ISO 8601 timestamp |
| `turns[].requestId` | string | Correlation ID |
| `updatedAt` | string | Last modification timestamp |

**Partition strategy:** `/SessionId` — each session is an independent partition, enabling efficient reads and writes without cross-partition queries.

## Local Development

### Prerequisites
- .NET 8+ SDK
- Azure Functions Core Tools v4
- Azure OpenAI access (endpoint + model deployment)
- Optional: Cosmos DB emulator or test instance

### local.settings.json

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AZURE_OPENAI_ENDPOINT": "<your-endpoint>",
    "AZURE_OPENAI_MODEL": "gpt-4o",
    "CONVERSATION_STORE_TYPE": "InMemory",
    "DURABLE_TASK_SCHEDULER_CONNECTION_STRING": "Endpoint=http://localhost:8080",
    "TASKHUB_NAME": "advisor-dev"
  },
  "Host": {
    "CORS": "http://localhost:3000,http://localhost:5500"
  }
}
```

### Running Locally

```bash
cd AdvisorAgent/src/AdvisorAgent.Functions
dotnet build
func start
```

The API will be available at `http://localhost:7071/api/`.

## Scaling Considerations

| Dimension | Configuration | Notes |
|-----------|--------------|-------|
| Concurrent orchestrations | 5 per host | Controlled by `maxConcurrentOrchestratorFunctions` |
| Concurrent activities | 10 per host | Controlled by `maxConcurrentActivityFunctions` |
| Skill timeout | 120s per skill | Each skill can make multiple tool calls within this window |
| LLM token budget | ~19K tokens/skill | 5 tools × ~3,750 tokens (15K chars) per tool response |
| Conversation history | 5 most recent turns | Truncated to 1,000 chars each to manage context size |
| Subscription cap | 10 max | Subscription picker UI limited to first 10 subscriptions |
