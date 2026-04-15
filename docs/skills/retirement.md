# Retirement Skill

The Retirement Skill identifies Azure resources approaching end-of-life and generates actionable migration plans.

## Capability

- Detects retiring resources via Azure Advisor recommendations and Service Health advisories
- Builds retirement timelines for specific resources
- Generates migration action plans with prioritized steps

## Skill Configuration

| Property | Value |
|----------|-------|
| Skill Name | `RetirementSkill` |
| Model | `gpt-4o` |
| Temperature | `0.0` |
| Timeout | `120s` |

## System Prompt Summary

The LLM is instructed to:
- Use tools to retrieve Advisor and Service Health retirement data
- Analyze which resources are at risk of retirement
- NOT call `ListSubscriptions` (subscriptions are already scoped by the orchestrator)
- Generate a structured migration action plan

## Tools

| Tool | Method | Description |
|------|--------|-------------|
| `RetirementTools-GetRetiringResources` | `GetRetiringResources(subscriptionIds)` | Queries Advisor recommendations and Service Health HealthAdvisory events (180-day window) for retirement-related data |
| `RetirementTools-GetRetirementTimeline` | `GetRetirementTimeline(resourceId)` | Retrieves retirement timeline and migration guidance for a specific ARM resource |
| `RetirementTools-GenerateRetirementActionPlan` | `GenerateRetirementActionPlan(subscriptionIds)` | Delegates to GetRetiringResources; the LLM synthesizes a prioritized action plan |
| `SubscriptionTools-ListResourceGroups` | `ListResourceGroups(subscriptionId)` | Lists resource groups in a subscription for scoping |
| `ResourceGraphTools-QueryResources` | `QueryResources(subscriptionIds, resourceType?)` | General resource lookup for additional context |

## Data Sources

```
Azure Advisor Recommendations
    └─ advisorresources (all categories, filtered for retirement-related)

Azure Service Health
    └─ servicehealthresources | eventType == "HealthAdvisory"
    └─ 180-day lookback window
```

## Example Query

> "Which of my resources are being retired and what should I do?"

**Task Plan:**
```json
[
  {
    "task": "Identify all retiring resources across subscriptions",
    "skillName": "RetirementSkill",
    "dependsOn": []
  }
]
```

**Tool Calls:**
1. `RetirementTools-GetRetiringResources(["sub-001", "sub-002"])` → retirement data
2. `ResourceGraphTools-QueryResources(["sub-001", "sub-002"])` → resource inventory

**Output:** Structured markdown with retirement timeline, affected resources, and step-by-step migration plan.
