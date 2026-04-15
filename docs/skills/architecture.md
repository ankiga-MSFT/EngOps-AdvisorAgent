# Architecture Skill

The Architecture Skill provides architectural guidance, modernization recommendations, and Well-Architected Framework assessments for Azure resources.

## Capability

- Analyzes current resource architecture via Azure Resource Graph
- Retrieves Advisor recommendations across all pillars
- Provides modernization and best-practice guidance aligned with the Azure Well-Architected Framework

## Skill Configuration

| Property | Value |
|----------|-------|
| Skill Name | `ArchitectureSkill` |
| Model | `gpt-4o` |
| Temperature | `0.0` |
| Timeout | `120s` |

## System Prompt Summary

The LLM acts as an Azure Solutions Architect with expertise across all Well-Architected Framework pillars:
- **Reliability** — redundancy, failover, disaster recovery
- **Security** — identity, network security, data protection
- **Cost Optimization** — right-sizing, reserved instances, waste elimination
- **Operational Excellence** — monitoring, automation, incident response
- **Performance Efficiency** — scaling, caching, latency optimization

## Tools

| Tool | Method | Description |
|------|--------|-------------|
| `ResourceGraphTools-QueryResources` | `QueryResources(subscriptionIds, resourceType?)` | Lists resources with type, location, SKU, and tags (up to 50) |
| `ResourceGraphTools-GetResourceDetails` | `GetResourceDetails(resourceId)` | Full resource details including properties, identity, and kind |
| `AdvisorRecommendationTools-GetRecommendations` | `GetRecommendations(subscriptionIds, category?)` | Queries Advisor across any category (up to 20 recommendations) |
| `AdvisorRecommendationTools-GetRecommendationDetails` | `GetRecommendationDetails(recommendationId)` | Full details for a specific recommendation |
| `SubscriptionTools-ListResourceGroups` | `ListResourceGroups(subscriptionId)` | Lists resource groups for scoping |

## Data Sources

```
Azure Resource Graph
    └─ Resources | project id, name, type, resourceGroup, location, sku, tags
    └─ Full resource properties via GetResourceDetails

Azure Advisor
    └─ All recommendation categories
    └─ advisorresources | type == "microsoft.advisor/recommendations"
```

## Example Queries

> "Review the architecture of my production environment and suggest improvements"

**Tool Calls:**
1. `ResourceGraphTools-QueryResources(["sub-prod"])` → resource inventory
2. `AdvisorRecommendationTools-GetRecommendations(["sub-prod"])` → all-category recommendations
3. `ResourceGraphTools-GetResourceDetails(resourceId)` → deep-dive on key resources

> "Should I modernize my VM-based workloads to containers?"

**Tool Calls:**
1. `ResourceGraphTools-QueryResources(["sub-prod"], "Microsoft.Compute/virtualMachines")` → VM inventory
2. `AdvisorRecommendationTools-GetRecommendations(["sub-prod"])` → modernization recommendations
