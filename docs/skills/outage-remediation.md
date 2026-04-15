# Outage Remediation Skill

The Outage Remediation Skill helps with post-outage recovery by analyzing active incidents, generating remediation plans, and recommending preventive measures.

## Capability

- Detects active Azure Service Health incidents
- Generates remediation plans for specific incidents by tracking ID
- Creates comprehensive post-outage action plans combining incident data with reliability recommendations

## Skill Configuration

| Property | Value |
|----------|-------|
| Skill Name | `OutageRemediationSkill` |
| Model | `gpt-4o` |
| Temperature | `0.0` |
| Timeout | `120s` |

## System Prompt Summary

The LLM is instructed to cover:
- Monitoring and alerting improvements
- BCDR (Business Continuity and Disaster Recovery) enhancements
- Chaos engineering recommendations
- Operational process improvements

## Tools

| Tool | Method | Description |
|------|--------|-------------|
| `OutageRemediationTools-GetActiveIncidents` | `GetActiveIncidents(subscriptionIds)` | Queries Service Health for `ServiceIssue` events in the last 30 days |
| `OutageRemediationTools-GetRemediationPlan` | `GetRemediationPlan(subscriptionIds, trackingId)` | Retrieves root cause and remediation guidance for a specific incident by tracking ID |
| `OutageRemediationTools-GetPostOutageActionPlan` | `GetPostOutageActionPlan(subscriptionIds)` | Combines Service Health events (30 days) with Advisor HA recommendations into a single dataset for the LLM to synthesize |
| `SubscriptionTools-ListResourceGroups` | `ListResourceGroups(subscriptionId)` | Lists resource groups for scoping |
| `ResourceGraphTools-QueryResources` | `QueryResources(subscriptionIds, resourceType?)` | General resource lookup |

## Data Sources

```
Azure Service Health
    └─ servicehealthresources
    └─ type == "microsoft.resourcehealth/events"
    └─ eventType == "ServiceIssue"
    └─ 30-day lookback window

Azure Advisor Recommendations
    └─ advisorresources | category == "HighAvailability"
    └─ Used for gap analysis in post-outage action plans
```

## How Post-Outage Plans Work

The `GetPostOutageActionPlan` tool combines two data sources in a single response:

1. **Recent incidents** — Service Health `ServiceIssue` events from the last 30 days
2. **HA recommendations** — Advisor `HighAvailability` recommendations for the same subscriptions

The LLM then:
- Correlates which recommendations could have prevented or mitigated past incidents
- Identifies gaps in monitoring, redundancy, and disaster recovery
- Generates a prioritized action plan covering immediate, short-term, and long-term improvements

## Example Query

> "We had an outage last week. What should we do to prevent it from happening again?"

**Tool Calls:**
1. `OutageRemediationTools-GetActiveIncidents(["sub-prod"])` → recent Service Health incidents
2. `OutageRemediationTools-GetPostOutageActionPlan(["sub-prod"])` → combined incident + HA data
3. `ResourceGraphTools-QueryResources(["sub-prod"])` → affected resource inventory

**Output:** Structured markdown with incident summary, root cause analysis, and multi-phase remediation plan.
