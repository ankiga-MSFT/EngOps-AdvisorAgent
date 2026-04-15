# Resiliency Skill

The Resiliency Skill assesses the reliability posture of Azure resources and produces improvement plans with quantified scores.

## Capability

- Provides a resiliency score (0-100) with category breakdown
- Retrieves High Availability recommendations from Azure Advisor
- Generates prioritized resiliency improvement plans with projected score improvements

## Skill Configuration

| Property | Value |
|----------|-------|
| Skill Name | `ResiliencySkill` |
| Model | `gpt-4o` |
| Temperature | `0.0` |
| Timeout | `120s` |

## System Prompt Summary

The LLM is instructed to:
- Assess resiliency posture using Advisor scores and HA recommendations
- Produce a resiliency score with current vs. projected values
- Format output with Insights, Resiliency Score, Recommendations table, and Action Plan sections

## Expected Output Format

```markdown
## Insights
Analysis of current resiliency posture...

## Resiliency Score
| Metric         | Current | Projected |
|----------------|---------|-----------|
| Overall Score  | 72      | 89        |
| Availability   | 65      | 85        |
| Data Protection| 80      | 92        |

## Recommendations
| # | Recommendation             | Impact | Effort |
|---|---------------------------|--------|--------|
| 1 | Enable zone redundancy    | High   | Medium |
| 2 | Configure auto-failover   | High   | Low    |

## Action Plan
1. Immediate: Enable availability zones...
2. Short-term: Configure geo-replication...
3. Long-term: Implement chaos engineering...
```

## Tools

| Tool | Method | Description |
|------|--------|-------------|
| `ResiliencyTools-GetResiliencyScore` | `GetResiliencyScore(subscriptionIds, resourceGroup?)` | Queries Advisor score for reliability/HA; returns overall score (0-100) and category breakdown |
| `ResiliencyTools-GetResiliencyRecommendations` | `GetResiliencyRecommendations(subscriptionIds)` | Filters Advisor recommendations by category `HighAvailability` |
| `AdvisorRecommendationTools-GetRecommendations` | `GetRecommendations(subscriptionIds, "Reliability")` | Queries advisorresources for Reliability/HighAvailability recommendations (up to 20) |
| `SubscriptionTools-ListResourceGroups` | `ListResourceGroups(subscriptionId)` | Lists resource groups for scoping |
| `ResourceGraphTools-QueryResources` | `QueryResources(subscriptionIds, resourceType?)` | General resource lookup |

## Data Sources

```
Azure Advisor Score
    └─ advisorresources | type == "microsoft.advisor/advisorScore"
    └─ Reliability/HighAvailability category breakdown

Azure Advisor Recommendations
    └─ advisorresources | category == "HighAvailability"
```

## Example Query

> "What's the resiliency posture of my production subscription?"

**Tool Calls:**
1. `ResiliencyTools-GetResiliencyScore(["sub-prod"])` → overall score + breakdown
2. `ResiliencyTools-GetResiliencyRecommendations(["sub-prod"])` → HA recommendations
3. `AdvisorRecommendationTools-GetRecommendations(["sub-prod"], "Reliability")` → detailed recs
