using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Tools;

/// <summary>
/// Tools for assessing and improving the resiliency posture of Azure workloads.
/// Uses Azure Resource Graph (advisorresources table) via
/// POST /providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01.
/// </summary>
public sealed class ResiliencyTools : ToolBase
{
    public ResiliencyTools(ILogger<ResiliencyTools> logger, HttpClient httpClient) : base(logger, httpClient) { }

    /// <summary>
    /// Retrieves the Advisor Score (including Reliability category) across one or more subscriptions.
    /// ARG query: advisorresources | where type =~ 'microsoft.advisor/advisorscore'
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "subscriptionId": "...", "resourceGroup": null, "overallScore": 62,
    ///   "breakdown": { "zoneRedundancy": 38, "multiRegionFailover": 25, "backupAndRecovery": 68, "healthMonitoring": 71 },
    ///   "estimatedScoreAfterRemediation": 89, "lastUpdated": "2026-04-13T16:00:00Z"
    /// }
    /// </remarks>
    [Description("Retrieves the Advisor Score and per-category breakdown for one or more subscriptions.")]
    public async Task<string> GetResiliencyScore(
        [Description("Comma-separated Azure subscription IDs to assess")] string subscriptionIds,
        [Description("Optional resource group to scope the assessment")] string? resourceGroup = null)
    {
        Logger.LogInformation("[Resiliency] GetResiliencyScore for {Subs}/{RG}", subscriptionIds, resourceGroup ?? "all");
        try
        {
            var subs = ParseSubscriptionIds(subscriptionIds);
            var kql = "advisorresources | where type =~ 'microsoft.advisor/advisorscore' | project id, name, subscriptionId, properties";

            var json = await ResourceGraphQueryAsync(kql, subs);
            Logger.LogInformation("[Resiliency] GetResiliencyScore completed");
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Resiliency] GetResiliencyScore failed");
            return $$"""{"error": "Failed to fetch Advisor resiliency score: {{ex.Message}}"}""";
        }
    }

    /// <summary>
    /// Retrieves Advisor Reliability (HighAvailability) recommendations across one or more subscriptions.
    /// ARG query: advisorresources | where type =~ 'microsoft.advisor/recommendations' | where properties.category =~ 'HighAvailability'
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "subscriptionId": "...",
    ///   "recommendations": [
    ///     { "title": "Enable zone redundancy for Cosmos DB accounts", "impact": "High", "impactedResources": 1, "scoreImpact": "+5%", "costImpact": "High" },
    ///     { "title": "Consider having at least two origins in Front Door", "impact": "High", "impactedResources": 2, "scoreImpact": "+4%", "costImpact": "No Cost Impact" },
    ///     { "title": "Enable Health check for App Service", "impact": "High", "impactedResources": 2, "scoreImpact": "+3%", "costImpact": "No Cost Impact" },
    ///     { "title": "Set minimum instance count for App Service to 2", "impact": "High", "impactedResources": 29, "scoreImpact": "+4%", "costImpact": "Low" },
    ///     { "title": "Enable ZRS for Recovery Services vault", "impact": "High", "impactedResources": 1, "scoreImpact": "+3%", "costImpact": "Low" },
    ///     { "title": "Set up Geo-replication for Event Hubs namespace", "impact": "High", "impactedResources": 3, "scoreImpact": "+3%", "costImpact": "Medium" }
    ///   ]
    /// }
    /// </remarks>
    [Description("Retrieves Advisor Reliability recommendations with impact scores and remediation guidance for one or more subscriptions.")]
    public async Task<string> GetResiliencyRecommendations(
        [Description("Comma-separated Azure subscription IDs to get reliability recommendations for")] string subscriptionIds)
    {
        Logger.LogInformation("[Resiliency] GetResiliencyRecommendations for {Subs}", subscriptionIds);
        try
        {
            var subs = ParseSubscriptionIds(subscriptionIds);
            var kql = "advisorresources | where type =~ 'microsoft.advisor/recommendations' | where properties.category =~ 'HighAvailability' | project id, name, subscriptionId, resourceGroup, properties | take 20";

            var json = await ResourceGraphQueryAsync(kql, subs);
            Logger.LogInformation("[Resiliency] GetResiliencyRecommendations completed");
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Resiliency] GetResiliencyRecommendations failed");
            return $$"""{"error": "Failed to fetch resiliency recommendations: {{ex.Message}}"}""";
        }
    }
}
