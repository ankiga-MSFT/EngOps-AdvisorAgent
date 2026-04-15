using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Tools;

/// <summary>
/// Tools for discovering Azure subscriptions and resource groups the user has access to.
/// Uses ARM List Subscriptions and List Resource Groups APIs.
/// </summary>
public sealed class SubscriptionTools : ToolBase
{
    public SubscriptionTools(ILogger<SubscriptionTools> logger, HttpClient httpClient) : base(logger, httpClient) { }

    /// <summary>
    /// Lists all Azure subscriptions the current user has access to.
    /// ARM API: GET /subscriptions?api-version=2022-12-01
    /// </summary>
    [Description("Lists all Azure subscriptions the user has access to. Use this when the user wants to see their subscriptions or to run analysis across all subscriptions.")]
    public async Task<string> ListSubscriptions()
    {
        Logger.LogInformation("[SubscriptionTools] ListSubscriptions called");
        try
        {
            var json = await ArmGetAsync("/subscriptions?api-version=2022-12-01");
            Logger.LogInformation("[SubscriptionTools] ListSubscriptions completed");
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[SubscriptionTools] ListSubscriptions failed");
            return $$"""{"error": "Failed to list subscriptions: {{ex.Message}}"}""";
        }
    }

    /// <summary>
    /// Lists all resource groups in an Azure subscription.
    /// ARM API: GET /subscriptions/{subscriptionId}/resourcegroups?api-version=2024-07-01
    /// </summary>
    [Description("Lists all resource groups in a specific Azure subscription. Use this when the user wants to see resources organized by resource group.")]
    public async Task<string> ListResourceGroups(
        [Description("The Azure subscription ID")] string subscriptionId)
    {
        Logger.LogInformation("[SubscriptionTools] ListResourceGroups for {Sub}", subscriptionId);
        try
        {
            var json = await ArmGetAsync(
                $"/subscriptions/{Uri.EscapeDataString(subscriptionId)}/resourcegroups?api-version=2024-07-01");
            Logger.LogInformation("[SubscriptionTools] ListResourceGroups completed for {Sub}", subscriptionId);
            return json;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[SubscriptionTools] ListResourceGroups failed for {Sub}", subscriptionId);
            return $$"""{"error": "Failed to list resource groups: {{ex.Message}}"}""";
        }
    }
}
