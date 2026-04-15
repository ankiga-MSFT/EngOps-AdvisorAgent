# Tool Reference

All tools inherit from `ToolBase` and expose public methods that the LLM can invoke during skill execution. Tools make authenticated REST calls to Azure ARM APIs and Azure Resource Graph.

## Tool Base Class

Every tool class inherits from `ToolBase`, which provides:

### Authentication
- `SetAccessToken(token)` — injects the user's Bearer token for ARM API calls
- `SetSession(sessionId)` — stores session context for logging

### ARM REST Helpers

| Method | Purpose |
|--------|---------|
| `ArmGetAsync(urlOrPath)` | HTTP GET to ARM; accepts full URL or relative path (auto-prefixes `management.azure.com`) |
| `ArmPostAsync(urlOrPath, jsonBody)` | HTTP POST to ARM with JSON payload |
| `ResourceGraphQueryAsync(kql, subscriptionIds?)` | POST to Azure Resource Graph with a KQL query and optional subscription scope |

### Response Budgeting

```csharp
TruncateToolResponse(response, toolLabel)
```

Enforces a **15,000 character maximum** on tool responses. This prevents blowing the LLM context window when multiple tools are invoked per skill (~15K chars ≈ 3,750 tokens; with 5 tools/skill ≈ 18,750 tokens).

### Utility Helpers
- `ParseSubscriptionIds(string)` — splits comma/semicolon-separated subscription lists
- `ExtractSubscriptionId(resourceId)` — extracts subscription ID from a full ARM resource path
- `EscapeJsonString()` — escapes special characters for JSON embedding

---

## Tool Classes

### AdvisorRecommendationTools

Queries Azure Advisor recommendations via Azure Resource Graph.

| Method | Parameters | Description |
|--------|-----------|-------------|
| `GetRecommendations` | `subscriptionIds`, `category?` | KQL query against `advisorresources` for recommendations; category filter supports: Cost, Security, Reliability/HighAvailability, OperationalExcellence, Performance. Returns up to 20 results. |
| `GetRecommendationDetails` | `recommendationId` | Retrieves full details for a single Advisor recommendation by its ARM resource ID |

### CostOptimizationTools

Cost analysis via Advisor recommendations.

| Method | Parameters | Description |
|--------|-----------|-------------|
| `GetCostRecommendations` | `subscriptionIds` | Filters Advisor recommendations by `Cost` category; returns cost-saving recommendations with savings estimates |
| `EstimateSavings` | `subscriptionIds` | Aggregates savings from `savingsAmount` or `annualSavingsAmount/12` across all cost recommendations; returns total potential monthly savings |

### ResiliencyTools

Reliability and high availability assessment.

| Method | Parameters | Description |
|--------|-----------|-------------|
| `GetResiliencyScore` | `subscriptionIds`, `resourceGroup?` | Queries Advisor score for reliability/HA; returns overall score (0-100) and per-category breakdown |
| `GetResiliencyRecommendations` | `subscriptionIds` | Filters Advisor recommendations by `HighAvailability` category with score impact |

### RetirementTools

Service retirement detection and migration planning.

| Method | Parameters | Description |
|--------|-----------|-------------|
| `GetRetiringResources` | `subscriptionIds` | Queries Advisor recommendations (all categories) plus Service Health HealthAdvisory events (180-day window); returns combined retirement data |
| `GetRetirementTimeline` | `resourceId` | Retrieves retirement timeline and migration guidance for a specific ARM resource |
| `GenerateRetirementActionPlan` | `subscriptionIds` | Delegates to `GetRetiringResources`; provides raw data for LLM to synthesize into an action plan |

### OutageRemediationTools

Post-outage analysis and remediation.

| Method | Parameters | Description |
|--------|-----------|-------------|
| `GetActiveIncidents` | `subscriptionIds` | Queries Service Health for `ServiceIssue` events in the last 30 days |
| `GetRemediationPlan` | `subscriptionIds`, `trackingId` | Retrieves root cause and remediation guidance for a specific incident |
| `GetPostOutageActionPlan` | `subscriptionIds` | Combines Service Health events (30 days) with Advisor HA recommendations; provides combined data for LLM synthesis |

### ResourceGraphTools

General-purpose Azure resource querying.

| Method | Parameters | Description |
|--------|-----------|-------------|
| `QueryResources` | `subscriptionIds`, `resourceType?` | KQL query returning `id, name, type, resourceGroup, location, sku, tags`; optional type filter; up to 50 results |
| `GetResourceDetails` | `resourceId` | Full resource details including properties, tags, identity, and kind |

### SubscriptionTools

Subscription and resource group discovery.

| Method | Parameters | Description |
|--------|-----------|-------------|
| `ListSubscriptions` | _(none)_ | ARM API: `GET /subscriptions?api-version=2022-12-01`; returns all accessible subscriptions |
| `ListResourceGroups` | `subscriptionId` | ARM API: `GET /subscriptions/{sub}/resourcegroups?api-version=2024-07-01`; returns resource groups |

---

## Tool Registration

All tool instances are registered as singletons in `Program.cs`:

```csharp
var toolInstances = new Dictionary<string, object>
{
    ["AdvisorRecommendationTools"] = new AdvisorRecommendationTools(loggerFactory),
    ["RetirementTools"]            = new RetirementTools(loggerFactory),
    ["ResiliencyTools"]            = new ResiliencyTools(loggerFactory),
    ["CostOptimizationTools"]      = new CostOptimizationTools(loggerFactory),
    ["OutageRemediationTools"]     = new OutageRemediationTools(loggerFactory),
    ["ResourceGraphTools"]         = new ResourceGraphTools(loggerFactory),
    ["SubscriptionTools"]          = new SubscriptionTools(loggerFactory),
};
```

At skill execution time, the orchestration service:
1. Parses tool names from the skill definition (e.g., `ResourceGraphTools-QueryResources`)
2. Looks up the class instance in the dictionary
3. Finds the method via reflection
4. Wraps it as an `AIFunction` using `AIFunctionFactory.Create()`
5. Registers it with the LLM chat client's `FunctionInvocation` middleware

## Adding a New Tool

1. Create a public method in an existing tool class (or create a new class inheriting from `ToolBase`)
2. If new class: register the instance in `Program.cs` under `toolInstances`
3. Add a tool reference in the relevant skill's `Tools` array in `skills.json`:
   ```json
   { "Name": "MyTools-NewMethod", "Description": "What this tool does" }
   ```
4. The orchestration service will automatically discover and bind the tool at runtime
