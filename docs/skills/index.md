# Skill System Overview

The advisor agent uses a **skill-based architecture** where each capability domain is encapsulated as an independently executable skill. Skills are defined declaratively in a JSON catalog and executed dynamically by the orchestration engine.

## How Skills Work

```
┌─────────────────────────────────────────────────────────────┐
│  skills.json (Skill Catalog)                                 │
│                                                             │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────────────┐ │
│  │ Retirement   │ │ Resiliency   │ │ CostOptimization     │ │
│  │ Skill        │ │ Skill        │ │ Skill                │ │
│  ├──────────────┤ ├──────────────┤ ├──────────────────────┤ │
│  │ SystemPrompt │ │ SystemPrompt │ │ SystemPrompt         │ │
│  │ Tools[]      │ │ Tools[]      │ │ Tools[]              │ │
│  │ Model: gpt4o │ │ Model: gpt4o │ │ Model: gpt4o         │ │
│  │ Temp: 0.0    │ │ Temp: 0.0    │ │ Temp: 0.0            │ │
│  └──────────────┘ └──────────────┘ └──────────────────────┘ │
│                                                             │
│  ┌──────────────┐ ┌──────────────┐                          │
│  │ Outage       │ │ Architecture │                          │
│  │ Remediation  │ │ Skill        │                          │
│  │ Skill        │ │              │                          │
│  └──────────────┘ └──────────────┘                          │
└─────────────────────────────────────────────────────────────┘
```

## Skill Definition Schema

Each skill in `Configuration/skills.json` has these properties:

```json
{
  "SkillName": "CostOptimizationSkill",
  "Description": "Identifies cost-saving opportunities with estimated savings",
  "SystemPrompt": "You are a cost optimization expert...",
  "ModelName": "gpt-4o",
  "ExpectedInput": "Subscription IDs and optional resource scope",
  "Temperature": 0.0,
  "Timeout": 120,
  "Tools": [
    {
      "Name": "CostOptimizationTools-GetCostRecommendations",
      "Description": "Retrieves cost optimization recommendations"
    }
  ]
}
```

| Property | Type | Description |
|----------|------|-------------|
| `SkillName` | string | Unique identifier used in task plans |
| `Description` | string | Human-readable purpose; shown to LLM for task decomposition |
| `SystemPrompt` | string | Full LLM system prompt used during skill execution |
| `ModelName` | string | Azure OpenAI model (default: `gpt-4o`) |
| `ExpectedInput` | string | Hint about what input the skill needs |
| `Temperature` | float | LLM temperature; `0.0` for deterministic output |
| `Timeout` | int | Max seconds for skill execution (default: 120) |
| `Tools` | array | Tool references in `ClassName-MethodName` format |

## Tool Binding

Tools are resolved dynamically at execution time via reflection:

1. Tool name is parsed: `ResourceGraphTools-QueryResources` → class `ResourceGraphTools`, method `QueryResources`
2. The tool instance is looked up in the DI container's tool registry
3. The method is wrapped using `AIFunctionFactory.Create()` from `Microsoft.Extensions.AI`
4. `SetAccessToken()` is called on the tool instance to inject the user's Bearer token
5. The wrapped function is registered with the LLM chat client's `FunctionInvocation` middleware

This means adding a new tool is as simple as:
1. Add a public method to an existing tool class (or create a new one)
2. Register the tool class in `Program.cs`
3. Reference it in the skill's `Tools` array in `skills.json`

## Skill Response Protocol

All skills are instructed to return structured responses in this format:

```markdown
## Insights
Key findings from the analysis...

## Recommendations
| # | Recommendation | Impact | Effort |
|---|---------------|--------|--------|
| 1 | ...           | High   | Low    |

## Action Plan
1. Immediate: ...
2. Short-term: ...
3. Long-term: ...
```

## Available Skills

| Skill | Domain | Tools | Details |
|-------|--------|-------|---------|
| [RetirementSkill](./retirement) | Service retirement and migration | 5 tools | Retiring resources, timelines, migration plans |
| [ResiliencySkill](./resiliency) | Reliability assessment | 4 tools | Resiliency scores, HA recommendations |
| [CostOptimizationSkill](./cost-optimization) | Cost savings | 5 tools | Cost recommendations, savings estimates |
| [OutageRemediationSkill](./outage-remediation) | Post-outage recovery | 4 tools | Active incidents, remediation plans |
| [ArchitectureSkill](./architecture) | Architectural guidance | 5 tools | Well-Architected Framework analysis |
