using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Tools;

/// <summary>
/// Tools for analyzing costs and generating savings recommendations.
/// Uses Azure Resource Graph (advisorresources table, Cost category) via
/// POST /providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01.
/// </summary>
/// <remarks>
/// The Cost Management Query API (POST /subscriptions/{sub}/providers/Microsoft.CostManagement/query)
/// was previously used in EstimateSavings for current-spend data but has been removed because
/// Cost Management is not available through Azure Resource Graph, making it impossible to query
/// across multiple subscriptions in a single call. Savings estimation now relies solely on Advisor
/// cost recommendations via ARG.
/// </remarks>
public sealed class CostOptimizationTools : ToolBase
{
    public CostOptimizationTools(ILogger<CostOptimizationTools> logger, HttpClient httpClient) : base(logger, httpClient) { }

    /// <summary>
    /// Retrieves Azure Advisor cost recommendations with estimated savings across one or more subscriptions.
    /// ARG query: advisorresources | where type =~ 'microsoft.advisor/recommendations' | where properties.category =~ 'Cost'
    /// </summary>
    /// <remarks>
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "subscriptionId": "...", "currentMonthlySpend": 48200, "totalPotentialSavings": 13600,
    ///   "recommendations": [
    ///     { "title": "Right-size or shutdown underutilized virtual machines", "impact": "High", "impactedResources": 12, "monthlySavings": 4200 },
    ///     { "title": "Consider purchasing a 1-year Compute Savings Plan", "impact": "High", "impactedResources": "Compute across 3 subscriptions", "monthlySavings": 5800 },
    ///     { "title": "Delete unattached managed disks", "impact": "High", "impactedResources": 23, "monthlySavings": 860 },
    ///     { "title": "Move Storage blobs to cooler access tiers", "impact": "Medium", "impactedResources": 1, "monthlySavings": 840 }
    ///   ]
    /// }
    /// </remarks>
    [Description("Retrieves Azure Advisor cost recommendations with estimated savings for one or more subscriptions.")]
    public async Task<string> GetCostRecommendations(
        [Description("Comma-separated Azure subscription IDs to analyze for cost savings")] string subscriptionIds)
    {
        Logger.LogInformation("[CostOpt] GetCostRecommendations for {Subs}", subscriptionIds);
        try
        {
            var subs = ParseSubscriptionIds(subscriptionIds);
            var kql = "advisorresources | where type =~ 'microsoft.advisor/recommendations' | where properties.category =~ 'Cost' | project id, name, subscriptionId, resourceGroup, properties | take 20";

            var json = await ResourceGraphQueryAsync(kql, subs);
            Logger.LogInformation("[CostOpt] GetCostRecommendations completed");
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[CostOpt] GetCostRecommendations failed");
            return $$"""{"error": "Failed to fetch cost recommendations: {{ex.Message}}"}""";
        }
    }

    /// <summary>
    /// Estimates potential monthly savings by aggregating savingsAmount/annualSavingsAmount
    /// from Advisor cost recommendations queried via Azure Resource Graph.
    /// </summary>
    /// <remarks>
    /// NOTE: The Cost Management Query API (Microsoft.CostManagement/query) was previously used
    /// here to fetch current monthly spend data. It has been removed because Cost Management data
    /// is not available in Azure Resource Graph, preventing multi-subscription queries in a single call.
    /// The currentMonthlySpend field is now omitted from the response.
    ///
    /// Pre-ARM hardcoded sample response:
    /// {
    ///   "subscriptionId": "...", "currentMonthlySpend": 48200, "estimatedSavings": 13600,
    ///   "savingsPercentage": 28.2, "projectedMonthlySpend": 34600,
    ///   "breakdownByCategory": { "rightSizing": 4200, "savingsPlans": 5800, "unusedResources": 860, "tierOptimization": 1900, "storageOptimization": 840 }
    /// }
    /// </remarks>
    [Description("Estimates potential monthly savings from implementing all cost recommendations across one or more subscriptions. Aggregates savings from Advisor recommendations.")]
    public async Task<string> EstimateSavings(
        [Description("Comma-separated Azure subscription IDs to estimate savings for")] string subscriptionIds)
    {
        Logger.LogInformation("[CostOpt] EstimateSavings for {Subs}", subscriptionIds);
        try
        {
            var subs = ParseSubscriptionIds(subscriptionIds);
            var kql = "advisorresources | where type =~ 'microsoft.advisor/recommendations' | where properties.category =~ 'Cost' | project id, name, subscriptionId, resourceGroup, properties | take 25";

            var recsJson = await ResourceGraphQueryAsync(kql, subs);

            // Aggregate savings from the ARG response
            double totalSavings = 0;
            int recCount = 0;
            try
            {
                using var doc = JsonDocument.Parse(recsJson);
                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    foreach (var rec in data.EnumerateArray())
                    {
                        recCount++;
                        if (rec.TryGetProperty("properties", out var props) &&
                            props.TryGetProperty("extendedProperties", out var extProps))
                        {
                            if (extProps.TryGetProperty("savingsAmount", out var savingsEl))
                            {
                                if (double.TryParse(savingsEl.GetString(), out var amt))
                                    totalSavings += amt;
                            }
                            else if (extProps.TryGetProperty("annualSavingsAmount", out var annualEl))
                            {
                                if (double.TryParse(annualEl.GetString(), out var annualAmt))
                                    totalSavings += annualAmt / 12.0;
                            }
                        }
                    }
                }
            }
            catch (JsonException parseEx)
            {
                Logger.LogWarning(parseEx, "[CostOpt] Failed to parse savings aggregation");
            }

            var summary = $$"""
            {
                "subscriptionIds": "{{subscriptionIds}}",
                "estimatedMonthlySavings": {{totalSavings:F2}},
                "costRecommendationCount": {{recCount}}
            }
            """;

            Logger.LogInformation("[CostOpt] EstimateSavings completed: {Count} recs, ${Savings:F2} potential savings",
                recCount, totalSavings);
            return summary;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[CostOpt] EstimateSavings failed");
            return $$"""{"error": "Failed to estimate savings: {{ex.Message}}"}""";
        }
    }
}
