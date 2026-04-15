using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Tools;

/// <summary>
/// Tools for querying Azure Advisor recommendations across categories.
/// Uses Azure Resource Graph (advisorresources table) via POST /providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01.
/// </summary>
public sealed class AdvisorRecommendationTools : ToolBase
{
    // Map user-friendly category names to the Advisor properties.category values in ARG
    private static readonly Dictionary<string, string> CategoryFilterMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Cost"] = "Cost",
        ["Security"] = "Security",
        ["Reliability"] = "HighAvailability",
        ["HighAvailability"] = "HighAvailability",
        ["OperationalExcellence"] = "OperationalExcellence",
        ["Performance"] = "Performance"
    };

    public AdvisorRecommendationTools(ILogger<AdvisorRecommendationTools> logger, HttpClient httpClient) : base(logger, httpClient) { }

    /// <summary>
    /// Retrieves Azure Advisor recommendations across one or more subscriptions via Azure Resource Graph.
    /// ARG table: advisorresources | where type =~ 'microsoft.advisor/recommendations'
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "subscriptionId": "...", "category": "All",
    ///   "recommendations": [
    ///     { "id": "rec-001", "category": "Reliability", "impact": "High", "title": "Enable zone redundancy for Cosmos DB accounts", "impactedResources": 1, "lastRefreshed": "2026-04-13T14:30:00Z" },
    ///     { "id": "rec-002", "category": "Cost", "impact": "High", "title": "Right-size underutilized virtual machines", "impactedResources": 12, "monthlySavings": 4200, "lastRefreshed": "2026-04-13T14:30:00Z" },
    ///     { "id": "rec-003", "category": "Security", "impact": "High", "title": "Enable Microsoft Defender for Cloud on all subscriptions", "impactedResources": 3, "lastRefreshed": "2026-04-12T08:45:00Z" }
    ///   ]
    /// }
    /// </remarks>
    [Description("Retrieves Azure Advisor recommendations for one or more subscriptions, optionally filtered by category (Cost, Security, Reliability, OperationalExcellence, Performance).")]
    public async Task<string> GetRecommendations(
        [Description("Comma-separated Azure subscription IDs to query")] string subscriptionIds,
        [Description("Optional category filter: Cost, Security, Reliability, OperationalExcellence, Performance")] string? category = null)
    {
        Logger.LogInformation("[AdvisorRecs] GetRecommendations for {Subs}, category={Cat}", subscriptionIds, category ?? "All");
        try
        {
            var subs = ParseSubscriptionIds(subscriptionIds);
            var kql = "advisorresources | where type =~ 'microsoft.advisor/recommendations'";

            if (!string.IsNullOrWhiteSpace(category) && CategoryFilterMap.TryGetValue(category, out var argCategory))
            {
                kql += $" | where properties.category =~ '{argCategory}'";
            }

            kql += " | project id, name, type, subscriptionId, resourceGroup, properties | take 20";

            var json = await ResourceGraphQueryAsync(kql, subs);
            Logger.LogInformation("[AdvisorRecs] GetRecommendations completed");
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[AdvisorRecs] GetRecommendations failed");
            return $$"""{"error": "Failed to fetch Advisor recommendations: {{ex.Message}}"}""";
        }
    }

    /// <summary>
    /// Gets detailed information about a specific Advisor recommendation via Azure Resource Graph.
    /// Queries: advisorresources | where id =~ '{recommendationId}'
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "id": "rec-002", "title": "Right-size underutilized virtual machines", "category": "Cost", "impact": "High",
    ///   "description": "We have detected low CPU/memory utilization for your virtual machines over the past 14 days.",
    ///   "remediation": "Resize these VMs to a smaller SKU or deallocate idle instances.",
    ///   "impactedResources": [
    ///     { "resourceId": "/subscriptions/sub-1/resourceGroups/prod-rg/providers/Microsoft.Compute/virtualMachines/vm-web-01", "avgCpuPercent": 8.2 },
    ///     { "resourceId": "/subscriptions/sub-1/resourceGroups/prod-rg/providers/Microsoft.Compute/virtualMachines/vm-api-03", "avgCpuPercent": 5.1 }
    ///   ],
    ///   "estimatedMonthlySavings": 4200
    /// }
    /// </remarks>
    [Description("Gets detailed information about a specific Advisor recommendation including affected resources and remediation steps. Pass the full recommendation resource ID.")]
    public async Task<string> GetRecommendationDetails(
        [Description("The full ARM resource ID of the recommendation (as returned by GetRecommendations)")] string recommendationId)
    {
        Logger.LogInformation("[AdvisorRecs] GetRecommendationDetails for {RecId}", recommendationId);
        try
        {
            var subId = ExtractSubscriptionId(recommendationId);
            var subs = subId is not null ? new[] { subId } : null;

            var kql = $"advisorresources | where id =~ '{recommendationId}'";
            var json = await ResourceGraphQueryAsync(kql, subs);
            Logger.LogInformation("[AdvisorRecs] GetRecommendationDetails completed");
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[AdvisorRecs] GetRecommendationDetails failed");
            return $$"""{"error": "Failed to fetch recommendation details: {{ex.Message}}"}""";
        }
    }
}
