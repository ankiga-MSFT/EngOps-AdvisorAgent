# HTTP API Reference

The advisor agent exposes four HTTP endpoints via Azure Functions triggers. All endpoints are defined in `AdvisorHttpTrigger.cs`.

## Endpoints

### POST `/api/advisor/orchestrate`

Starts a new advisory orchestration. Returns immediately with an instance ID for status polling.

**Request:**

```http
POST /api/advisor/orchestrate
Authorization: Bearer <azure-access-token>
Content-Type: application/json

{
  "userId": "user@contoso.com",
  "prompt": "Show me cost savings for my production subscription",
  "sessionId": "sess-abc-123",
  "requestId": "req-001"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `userId` | string | Yes | Unique user identifier |
| `prompt` | string | Yes | The user's natural language query |
| `sessionId` | string | No | Conversation session ID; auto-generated if omitted |
| `requestId` | string | No | Client-generated request correlation ID |
| `accessToken` | string | No | Azure access token; can alternatively be passed via `Authorization: Bearer` header |

**Response (202 Accepted):**

```json
{
  "instanceId": "abc123-def456",
  "sessionId": "sess-abc-123",
  "statusQueryGetUri": "/api/advisor/status/abc123-def456"
}
```

---

### GET `/api/advisor/status/{instanceId}`

Polls the status of a running orchestration. Returns progress, runtime status, and the final output when complete.

**Request:**

```http
GET /api/advisor/status/abc123-def456
```

**Response (200 OK):**

```json
{
  "instanceId": "abc123-def456",
  "runtimeStatus": "Completed",
  "customStatus": {
    "steps": [
      { "stepName": "LoadHistory", "state": "Completed", "message": "Loaded 3 turns" },
      { "stepName": "ResolveContext", "state": "Completed", "message": "Subscription: sub-001" },
      { "stepName": "ClassifyIntent", "state": "Completed", "message": "ActionRequired" },
      { "stepName": "DecomposeTasks", "state": "Completed", "message": "1 task planned" },
      { "stepName": "ExecuteSkills", "state": "Completed", "message": "CostOptimizationSkill done" }
    ],
    "isCompleted": true
  },
  "output": {
    "isSuccess": true,
    "response": "## Insights\n...",
    "needsUserInput": false,
    "category": "CostOptimizationSkill"
  }
}
```

**Runtime Status Values:**
| Status | Meaning |
|--------|---------|
| `Running` | Orchestration is still in progress |
| `Completed` | Orchestration finished successfully |
| `Failed` | Orchestration encountered an error |
| `Terminated` | Orchestration was externally terminated |

---

### POST `/api/negotiate`

Returns SignalR connection info for the `advisor` hub. Used by clients to establish real-time update channels.

**Request:**

```http
POST /api/negotiate
```

**Response (200 OK):**

```json
{
  "url": "https://<signalr-service>.service.signalr.net/client/?hub=advisor",
  "accessToken": "<signalr-jwt>"
}
```

---

### GET `/api/advisor/health`

Simple health check endpoint.

**Response (200 OK):**

```
Healthy
```

---

## Response Model: `AdvisorAgentResponse`

All orchestration outputs use this unified response structure:

```json
{
  "isSuccess": true,
  "response": "## Insights\n...\n## Recommendations\n...\n## Action Plan\n...",
  "needsUserInput": false,
  "uiAction": null,
  "uiData": null,
  "category": "CostOptimizationSkill"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `isSuccess` | bool | Whether the orchestration completed successfully |
| `response` | string | Markdown-formatted advisory response |
| `needsUserInput` | bool | `true` when waiting for user input (e.g., subscription picker) |
| `uiAction` | string? | Hint for client UI rendering (e.g., `"subscriptionPicker"`) |
| `uiData` | object? | Structured payload for UI actions (e.g., subscription list) |
| `category` | string? | Skill name that generated the response |

### Subscription Picker Response

When the orchestrator cannot determine the subscription scope:

```json
{
  "isSuccess": true,
  "needsUserInput": true,
  "uiAction": "subscriptionPicker",
  "uiData": [
    { "subscriptionId": "sub-001", "displayName": "Production" },
    { "subscriptionId": "sub-002", "displayName": "Development" }
  ],
  "response": "I found multiple subscriptions. Please select which ones to analyze."
}
```

## Authentication

The agent uses the caller's Azure identity to access ARM APIs:

1. The client obtains an Azure access token (e.g., via `az account get-access-token`)
2. The token is passed via `Authorization: Bearer <token>` header or in the request body's `accessToken` field
3. The orchestrator injects this token into tool instances via `SetAccessToken()`
4. All ARM and Resource Graph calls use this token — the agent operates with the caller's permissions

::: warning
The agent can only access Azure resources that the caller's token has permissions for. No elevation of privilege occurs.
:::
