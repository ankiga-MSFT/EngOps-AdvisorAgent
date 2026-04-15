using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Tools;

/// <summary>
/// Tools for identifying retiring Azure resources and building migration plans.
/// Uses Azure Resource Graph (advisorresources + servicehealthresources tables) via
/// POST /providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01.
/// </summary>
public sealed class RetirementTools : ToolBase
{
    public RetirementTools(ILogger<RetirementTools> logger, HttpClient httpClient) : base(logger, httpClient) { }

    /// <summary>
    /// Retrieves all Azure resources affected by upcoming service retirements across one or more subscriptions.
    /// Combines Advisor recommendations with Service Health HealthAdvisory events via Azure Resource Graph.
    /// ARG tables: advisorresources + servicehealthresources
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "subscriptionId": "...", "totalAffected": 7,
    ///   "recommendations": [
    ///     { "title": "Migrate to Application Gateway v2", "impact": "Critical", "impactedResources": 1, "retirementDate": "2026-04-28" },
    ///     { "title": "Azure Key Vault API versions prior to 2026-02-01 are being retired", "impact": "High", "impactedResources": 22, "retirementDate": "2026-06-01", "trackingId": "RN3T-JRG" },
    ///     { "title": "Upgrade AKS cluster to a supported version", "impact": "High", "impactedResources": 1, "retirementDate": "2026-07-15" },
    ///     { "title": "Use Standard or Premium tier for Cache for Redis", "impact": "High", "impactedResources": 2, "retirementDate": "2026-06-30" }
    ///   ],
    ///   "serviceHealthNotices": [
    ///     { "issueName": "Action required: Transition Azure Key Vault access policies to RBAC", "trackingId": "RN3T-JRG", "services": "Key Vault", "startTime": "2026-02-09T05:30:00Z", "endTime": "2027-02-27T00:00:00Z" },
    ///     { "issueName": "Retirement notice: Azure Language Studio retirement update", "trackingId": "9P7S-2G8", "services": "Azure AI Language", "startTime": "2026-02-06T07:53:00Z", "endTime": "2027-03-20T00:00:00Z" }
    ///   ]
    /// }
    /// </remarks>
    [Description("Retrieves all Azure resources affected by upcoming service retirements across one or more subscriptions. Combines Advisor retirement recommendations with Service Health HealthAdvisory notices.")]
    public async Task<string> GetRetiringResources(
        [Description("Comma-separated Azure subscription IDs to scan for retiring resources")] string subscriptionIds)
    {
        Logger.LogInformation("[Retirement] GetRetiringResources for {Subs}", subscriptionIds);
        try
        {
            var subs = ParseSubscriptionIds(subscriptionIds);

            // ARG query 1: Advisor recommendations (all categories — retirement recs span multiple categories)
            var recsKql = "advisorresources | where type =~ 'microsoft.advisor/recommendations' | project id, name, subscriptionId, resourceGroup, properties | take 20";
            var recsJson = await ResourceGraphQueryAsync(recsKql, subs);

            // ARG query 2: Service Health HealthAdvisory events (retirement notices, 180-day window)
            var startTime = DateTimeOffset.UtcNow.AddDays(-180).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var eventsKql = $"servicehealthresources | where type =~ 'microsoft.resourcehealth/events' | where properties.eventType == 'HealthAdvisory' | where properties.impactStartTime >= datetime('{startTime}') | project id, name, subscriptionId, properties | take 20";
            var eventsJson = await ResourceGraphQueryAsync(eventsKql, subs);

            // Combine both datasets — the LLM will filter for retirement-related items
            var combined = $$"""
            {
                "subscriptionIds": "{{subscriptionIds}}",
                "advisorRecommendations": {{recsJson}},
                "serviceHealthEvents": {{eventsJson}}
            }
            """;

            Logger.LogInformation("[Retirement] GetRetiringResources completed");
            return combined;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Retirement] GetRetiringResources failed");
            return $$"""{"error": "Failed to fetch retiring resources: {{ex.Message}}"}""";
        }
    }

    /// <summary>
    /// Gets Advisor recommendations scoped to a specific Azure resource via Azure Resource Graph.
    /// Useful for retirement timeline and migration guidance for an individual resource.
    /// ARG query: advisorresources | where properties.resourceMetadata.resourceId =~ '{resourceId}'
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "resourceId": "...", "serviceName": "Application Gateway v1", "retirementDate": "2026-04-28", "daysRemaining": 14,
    ///   "migrationTarget": "Application Gateway v2",
    ///   "migrationGuideUrl": "https://learn.microsoft.com/azure/application-gateway/migrate-v1-v2",
    ///   "estimatedMigrationEffort": "4-8 hours",
    ///   "breakingChanges": ["WAF configuration format change", "Public IP must use Standard SKU"]
    /// }
    /// </remarks>
    [Description("Gets Advisor recommendations scoped to a specific Azure resource, useful for retirement timeline and migration guidance.")]
    public async Task<string> GetRetirementTimeline(
        [Description("The full Azure resource ID to check retirement status for")] string resourceId)
    {
        Logger.LogInformation("[Retirement] GetRetirementTimeline for {ResourceId}", resourceId);
        try
        {
            var subId = ExtractSubscriptionId(resourceId);
            var subs = subId is not null ? new[] { subId } : null;

            var kql = $"advisorresources | where type =~ 'microsoft.advisor/recommendations' | where properties.resourceMetadata.resourceId =~ '{resourceId}' | project id, name, subscriptionId, resourceGroup, properties";

            var json = await ResourceGraphQueryAsync(kql, subs);
            Logger.LogInformation("[Retirement] GetRetirementTimeline completed for {ResourceId}", resourceId);
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Retirement] GetRetirementTimeline failed for {ResourceId}", resourceId);
            return $$"""{"error": "Failed to fetch retirement timeline: {{ex.Message}}"}""";
        }
    }

    /// <summary>
    /// Generates data for a prioritized migration action plan for all retiring resources.
    /// Uses the same underlying data as GetRetiringResources (Advisor + Service Health via ARG),
    /// providing it in a format suitable for the LLM to synthesize an action plan.
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "subscriptionId": "...",
    ///   "actionPlan": [
    ///     { "priority": 1, "action": "Migrate Application Gateway v1 to v2", "urgency": "Immediate (14 days)", "effort": "4-8 hours", "impactedResources": 1 },
    ///     { "priority": 2, "action": "Transition Key Vault access policies to RBAC across 22 vaults", "urgency": "Next 30 days", "effort": "2-4 hours per vault", "impactedResources": 22 },
    ///     { "priority": 3, "action": "Upgrade AKS cluster to v1.29+", "urgency": "Next 60 days", "effort": "2-4 hours", "impactedResources": 1 }
    ///   ]
    /// }
    /// </remarks>
    [Description("Generates a prioritized migration action plan for all retiring resources across one or more subscriptions by combining Advisor recommendations with Service Health retirement notices.")]
    public async Task<string> GenerateRetirementActionPlan(
        [Description("Comma-separated Azure subscription IDs to generate a retirement action plan for")] string subscriptionIds)
    {
        Logger.LogInformation("[Retirement] GenerateRetirementActionPlan for {Subs}", subscriptionIds);
        // Delegate to GetRetiringResources — the LLM skill prompt will synthesize the action plan
        return await GetRetiringResources(subscriptionIds);
    }
}
