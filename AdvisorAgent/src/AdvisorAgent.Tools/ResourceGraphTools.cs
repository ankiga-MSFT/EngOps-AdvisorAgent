using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Tools;

/// <summary>
/// Tools for querying Azure Resource Graph and retrieving resource details.
/// Uses the shared ResourceGraphQueryAsync helper (POST /providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01).
/// </summary>
public sealed class ResourceGraphTools : ToolBase
{
    public ResourceGraphTools(ILogger<ResourceGraphTools> logger, HttpClient httpClient) : base(logger, httpClient) { }

    /// <summary>
    /// Queries Azure Resource Graph to list resources across one or more subscriptions.
    /// ARG table: resources
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "subscriptionId": "...", "resourceType": "all", "totalCount": 156,
    ///   "resources": [
    ///     { "resourceId": "/subscriptions/.../providers/Microsoft.Web/sites/advisor-web-prod", "name": "advisor-web-prod", "type": "Microsoft.Web/sites", "resourceGroup": "prod-rg", "location": "eastus", "sku": "P1v3" },
    ///     { "resourceId": "/subscriptions/.../providers/Microsoft.DocumentDB/databaseAccounts/advisor-cosmos-prod", "name": "advisor-cosmos-prod", "type": "Microsoft.DocumentDB/databaseAccounts", "resourceGroup": "prod-rg", "location": "eastus", "sku": "Standard" },
    ///     { "resourceId": "/subscriptions/.../providers/Microsoft.Compute/virtualMachines/vm-dev-01", "name": "vm-dev-01", "type": "Microsoft.Compute/virtualMachines", "resourceGroup": "dev-rg", "location": "eastus", "sku": "Standard_D4s_v3" }
    ///   ]
    /// }
    /// </remarks>
    [Description("Queries Azure Resource Graph to list resources across one or more subscriptions, optionally filtered by resource type.")]
    public async Task<string> QueryResources(
        [Description("Comma-separated Azure subscription IDs to query")] string subscriptionIds,
        [Description("Optional resource type filter, e.g., Microsoft.Compute/virtualMachines")] string? resourceType = null)
    {
        Logger.LogInformation("[ResourceGraph] QueryResources for {Subs}, type={Type}", subscriptionIds, resourceType ?? "all");
        try
        {
            var subs = ParseSubscriptionIds(subscriptionIds);
            var kql = "Resources | project id, name, type, resourceGroup, location, sku, tags";
            if (!string.IsNullOrWhiteSpace(resourceType))
            {
                kql = $"Resources | where type =~ '{resourceType}' | project id, name, type, resourceGroup, location, sku, tags";
            }
            kql += " | order by type asc, name asc | take 50";

            var json = await ResourceGraphQueryAsync(kql, subs);
            Logger.LogInformation("[ResourceGraph] QueryResources completed");
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ResourceGraph] QueryResources failed");
            return $$"""{"error": "Failed to query Resource Graph: {{ex.Message}}"}""";
        }
    }

    /// <summary>
    /// Gets detailed information about a specific Azure resource via Resource Graph.
    /// ARG table: resources
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "resourceId": "...", "name": "advisor-web-prod", "type": "Microsoft.Web/sites", "resourceGroup": "prod-rg", "location": "eastus",
    ///   "sku": { "name": "P1v3", "tier": "PremiumV3" },
    ///   "properties": { "state": "Running", "healthCheckPath": null, "httpsOnly": true, "minTlsVersion": "1.2", "alwaysOn": true, "numberOfWorkers": 1 },
    ///   "tags": { "environment": "production", "team": "platform", "costCenter": "CC-1234" },
    ///   "diagnosticSettings": { "applicationInsightsEnabled": false, "logAnalyticsWorkspaceId": null }
    /// }
    /// </remarks>
    [Description("Gets detailed information about a specific Azure resource including configuration, tags, and properties.")]
    public async Task<string> GetResourceDetails(
        [Description("The full Azure resource ID")] string resourceId)
    {
        Logger.LogInformation("[ResourceGraph] GetResourceDetails for {ResourceId}", resourceId);
        try
        {
            var subscriptionId = ExtractSubscriptionId(resourceId);
            if (string.IsNullOrEmpty(subscriptionId))
            {
                return """{"error": "Could not extract subscription ID from resource ID"}""";
            }

            var kql = $"Resources | where id =~ '{resourceId}' | project id, name, type, resourceGroup, location, sku, tags, properties, identity, kind";

            var json = await ResourceGraphQueryAsync(kql, [subscriptionId]);
            Logger.LogInformation("[ResourceGraph] GetResourceDetails completed for {ResourceId}", resourceId);
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ResourceGraph] GetResourceDetails failed for {ResourceId}", resourceId);
            return $$"""{"error": "Failed to get resource details: {{ex.Message}}"}""";
        }
    }
}
