using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Tools;

/// <summary>
/// Tools for post-outage analysis and remediation planning.
/// Uses Azure Resource Graph (servicehealthresources + advisorresources tables) via
/// POST /providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01.
/// </summary>
public sealed class OutageRemediationTools : ToolBase
{
    public OutageRemediationTools(ILogger<OutageRemediationTools> logger, HttpClient httpClient) : base(logger, httpClient) { }

    /// <summary>
    /// Retrieves recent Azure incidents and Service Health events within the last 30 days
    /// across one or more subscriptions via Azure Resource Graph.
    /// ARG query: servicehealthresources | where type =~ 'microsoft.resourcehealth/events'
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "subscriptionId": "...",
    ///   "incidents": [
    ///     { "incidentId": "INC-2026-04-10-001", "title": "Intermittent connectivity issues - East US App Services", "status": "Resolved", "severity": "High",
    ///       "startTime": "2026-04-10T08:15:00Z", "endTime": "2026-04-10T11:42:00Z", "impactedServices": ["App Service", "Azure Functions"], "impactedRegions": ["East US"] }
    ///   ],
    ///   "serviceHealthEvents": [
    ///     { "eventType": "Service issue", "title": "Elevated error rates for Storage accounts in East US", "status": "Resolved",
    ///       "startTime": "2026-04-08T14:00:00Z", "endTime": "2026-04-08T16:30:00Z" }
    ///   ]
    /// }
    /// </remarks>
    [Description("Retrieves recent Azure incidents and Service Health events within the last 30 days across one or more subscriptions.")]
    public async Task<string> GetActiveIncidents(
        [Description("Comma-separated Azure subscription IDs to check for recent incidents")] string subscriptionIds)
    {
        Logger.LogInformation("[OutageRemediation] GetActiveIncidents for {Subs}", subscriptionIds);
        try
        {
            var subs = ParseSubscriptionIds(subscriptionIds);
            var startTime = DateTimeOffset.UtcNow.AddDays(-30).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var kql = $"servicehealthresources | where type =~ 'microsoft.resourcehealth/events' | where properties.eventType == 'ServiceIssue' | where properties.impactStartTime >= datetime('{startTime}') | project id, name, subscriptionId, properties | take 20";

            var json = await ResourceGraphQueryAsync(kql, subs);
            Logger.LogInformation("[OutageRemediation] GetActiveIncidents completed");
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[OutageRemediation] GetActiveIncidents failed");
            return $$"""{"error": "Failed to fetch Service Health events: {{ex.Message}}"}""";
        }
    }

    /// <summary>
    /// Retrieves details of a specific Service Health event by tracking ID via Azure Resource Graph.
    /// ARG query: servicehealthresources | where properties.trackingId == '{trackingId}'
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "incidentId": "...", "rootCause": "Single-region deployment with no failover; health check endpoints not configured.",
    ///   "detectionGap": "No Service Health alerts configured; incident discovered via user reports after 45 minutes.",
    ///   "remediationSteps": [
    ///     "Configure Service Health alerts on all production subscriptions",
    ///     "Enable Health check endpoints on all App Services",
    ///     "Add Application Insights for real-time availability monitoring",
    ///     "Set up multi-region failover with Azure Front Door"
    ///   ]
    /// }
    /// </remarks>
    [Description("Retrieves details of a specific Service Health incident by its tracking ID for root cause analysis and remediation planning.")]
    public async Task<string> GetRemediationPlan(
        [Description("Comma-separated Azure subscription IDs")] string subscriptionIds,
        [Description("The Service Health event tracking ID (from GetActiveIncidents response)")] string trackingId)
    {
        Logger.LogInformation("[OutageRemediation] GetRemediationPlan for {Subs}, tracking {TrackingId}", subscriptionIds, trackingId);
        try
        {
            var subs = ParseSubscriptionIds(subscriptionIds);
            var kql = $"servicehealthresources | where type =~ 'microsoft.resourcehealth/events' | where properties.trackingId == '{trackingId}' | project id, name, subscriptionId, properties";

            var json = await ResourceGraphQueryAsync(kql, subs);
            Logger.LogInformation("[OutageRemediation] GetRemediationPlan completed for tracking {TrackingId}", trackingId);
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[OutageRemediation] GetRemediationPlan failed");
            return $$"""{"error": "Failed to fetch incident details: {{ex.Message}}"}""";
        }
    }

    /// <summary>
    /// Fetches both Service Health events and Advisor Reliability recommendations
    /// to provide a comprehensive post-outage gap analysis via Azure Resource Graph.
    /// Two ARG queries: servicehealthresources + advisorresources (HighAvailability).
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "subscriptionId": "...",
    ///   "gaps": { "monitoring": "No Service Health alerts on 4 subscriptions", "bcdr": "No geo-redundant failover", "alerting": "MTTD was 45 minutes" },
    ///   "relatedRecommendations": [
    ///     { "title": "Create an Azure Service Health alert", "impact": "High", "impactedResources": "4 Subscriptions", "costImpact": "No Cost Impact" },
    ///     { "title": "Enable Health check for App Service", "impact": "High", "impactedResources": "2 App Services", "costImpact": "No Cost Impact" }
    ///   ]
    /// }
    /// </remarks>
    [Description("Generates a comprehensive post-outage action plan covering monitoring, BCDR, and resilience improvements by combining Service Health events with Advisor Reliability recommendations.")]
    public async Task<string> GetPostOutageActionPlan(
        [Description("Comma-separated Azure subscription IDs to generate a post-outage plan for")] string subscriptionIds)
    {
        Logger.LogInformation("[OutageRemediation] GetPostOutageActionPlan for {Subs}", subscriptionIds);
        try
        {
            var subs = ParseSubscriptionIds(subscriptionIds);

            // ARG query 1: Service Health events (last 30 days)
            var startTime = DateTimeOffset.UtcNow.AddDays(-30).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var eventsKql = $"servicehealthresources | where type =~ 'microsoft.resourcehealth/events' | where properties.impactStartTime >= datetime('{startTime}') | project id, name, subscriptionId, properties | take 20";
            var eventsJson = await ResourceGraphQueryAsync(eventsKql, subs);

            // ARG query 2: Advisor Reliability recommendations
            var recsKql = "advisorresources | where type =~ 'microsoft.advisor/recommendations' | where properties.category =~ 'HighAvailability' | project id, name, subscriptionId, resourceGroup, properties | take 20";
            var recsJson = await ResourceGraphQueryAsync(recsKql, subs);

            // Combine both datasets for the LLM to analyze
            var combined = $$"""
            {
                "subscriptionIds": "{{subscriptionIds}}",
                "serviceHealthEvents": {{eventsJson}},
                "advisorReliabilityRecommendations": {{recsJson}}
            }
            """;

            Logger.LogInformation("[OutageRemediation] GetPostOutageActionPlan completed");
            return combined;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[OutageRemediation] GetPostOutageActionPlan failed");
            return $$"""{"error": "Failed to generate post-outage action plan: {{ex.Message}}"}""";
        }
    }
}
