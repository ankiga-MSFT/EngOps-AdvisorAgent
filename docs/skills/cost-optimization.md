# Cost Optimization Skill

The Cost Optimization Skill identifies cost-saving opportunities across Azure resources and estimates potential monthly savings.

## Capability

- Retrieves cost-related Azure Advisor recommendations
- Estimates aggregate monthly savings across subscriptions
- Provides actionable cost reduction plans

## Skill Configuration

| Property | Value |
|----------|-------|
| Skill Name | `CostOptimizationSkill` |
| Model | `gpt-4o` |
| Temperature | `0.0` |
| Timeout | `120s` |

## Tools

| Tool | Method | Description |
|------|--------|-------------|
| `CostOptimizationTools-GetCostRecommendations` | `GetCostRecommendations(subscriptionIds)` | Filters Advisor recommendations by category `Cost`; returns recommendations with savings estimates |
| `CostOptimizationTools-EstimateSavings` | `EstimateSavings(subscriptionIds)` | Aggregates all cost recommendation savings amounts; returns total potential monthly savings |
| `AdvisorRecommendationTools-GetRecommendations` | `GetRecommendations(subscriptionIds, "Cost")` | Queries advisorresources for cost recommendations (up to 20) |
| `AdvisorRecommendationTools-GetRecommendationDetails` | `GetRecommendationDetails(recommendationId)` | Returns full details for a single Advisor recommendation |
| `SubscriptionTools-ListResourceGroups` | `ListResourceGroups(subscriptionId)` | Lists resource groups for scoping |
| `ResourceGraphTools-QueryResources` | `QueryResources(subscriptionIds, resourceType?)` | General resource lookup |

## How Savings Are Calculated

The `EstimateSavings` tool aggregates savings from Advisor recommendations:

```
For each cost recommendation:
    if savingsAmount exists:
        total += savingsAmount
    else if annualSavingsAmount exists:
        total += annualSavingsAmount / 12
```

::: info
The Cost Management Query API is not used. All cost data comes from Azure Advisor recommendations via Azure Resource Graph.
:::

## Data Sources

```
Azure Advisor Recommendations
    └─ advisorresources | category == "Cost"
    └─ properties.extendedProperties.savingsAmount
    └─ properties.extendedProperties.annualSavingsAmount
```

## Example Query

> "How can I reduce costs across my subscriptions?"

**Tool Calls:**
1. `CostOptimizationTools-GetCostRecommendations(["sub-001", "sub-002"])` → cost recommendations
2. `CostOptimizationTools-EstimateSavings(["sub-001", "sub-002"])` → total monthly savings estimate
3. `AdvisorRecommendationTools-GetRecommendations(["sub-001", "sub-002"], "Cost")` → detailed cost recs

**Output:** Structured markdown with cost recommendations, estimated savings, and prioritized action plan.
