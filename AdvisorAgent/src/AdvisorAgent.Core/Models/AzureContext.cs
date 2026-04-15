using System.Text.Json.Serialization;

namespace AdvisorAgent.Core.Models;

/// <summary>
/// Captures Azure-specific context extracted from the user's prompt.
/// Supports both single-value and multi-value fields for subscription, resource group, and resource name.
/// </summary>
public sealed class AzureContext
{
    // ── Single-value fields (backward compatible) ────────────────

    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [JsonPropertyName("resourceGroup")]
    public string? ResourceGroup { get; set; }

    [JsonPropertyName("serviceGroup")]
    public string? ServiceGroup { get; set; }

    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; set; }

    [JsonPropertyName("resourceName")]
    public string? ResourceName { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    // ── Multi-value fields ───────────────────────────────────────

    [JsonPropertyName("subscriptionIds")]
    public List<string>? SubscriptionIds { get; set; }

    [JsonPropertyName("resourceGroups")]
    public List<string>? ResourceGroups { get; set; }

    [JsonPropertyName("resourceNames")]
    public List<string>? ResourceNames { get; set; }

    // ── Computed helpers ─────────────────────────────────────────

    public bool HasScope =>
        !string.IsNullOrWhiteSpace(SubscriptionId) ||
        SubscriptionIds is { Count: > 0 } ||
        !string.IsNullOrWhiteSpace(ResourceGroup) ||
        ResourceGroups is { Count: > 0 } ||
        !string.IsNullOrWhiteSpace(ResourceId);

    /// <summary>
    /// Returns the de-duplicated union of SubscriptionId and SubscriptionIds.
    /// </summary>
    public List<string> GetAllSubscriptionIds()
    {
        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(SubscriptionId)) ids.Add(SubscriptionId);
        if (SubscriptionIds is not null)
            ids.AddRange(SubscriptionIds.Where(s => !string.IsNullOrWhiteSpace(s) && !ids.Contains(s, StringComparer.OrdinalIgnoreCase)));
        return ids;
    }

    /// <summary>
    /// Returns the de-duplicated union of ResourceGroup and ResourceGroups.
    /// </summary>
    public List<string> GetAllResourceGroups()
    {
        var rgs = new List<string>();
        if (!string.IsNullOrWhiteSpace(ResourceGroup)) rgs.Add(ResourceGroup);
        if (ResourceGroups is not null)
            rgs.AddRange(ResourceGroups.Where(s => !string.IsNullOrWhiteSpace(s) && !rgs.Contains(s, StringComparer.OrdinalIgnoreCase)));
        return rgs;
    }

    /// <summary>
    /// Returns the de-duplicated union of ResourceName and ResourceNames.
    /// </summary>
    public List<string> GetAllResourceNames()
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(ResourceName)) names.Add(ResourceName);
        if (ResourceNames is not null)
            names.AddRange(ResourceNames.Where(s => !string.IsNullOrWhiteSpace(s) && !names.Contains(s, StringComparer.OrdinalIgnoreCase)));
        return names;
    }

    public string ToContextSummary()
    {
        var parts = new List<string>();
        var allSubs = GetAllSubscriptionIds();
        if (allSubs.Count > 0) parts.Add(allSubs.Count == 1 ? $"Subscription: {allSubs[0]}" : $"Subscriptions: {string.Join(", ", allSubs)}");
        var allRgs = GetAllResourceGroups();
        if (allRgs.Count > 0) parts.Add(allRgs.Count == 1 ? $"Resource Group: {allRgs[0]}" : $"Resource Groups: {string.Join(", ", allRgs)}");
        if (!string.IsNullOrWhiteSpace(ServiceGroup)) parts.Add($"Service Group: {ServiceGroup}");
        var allNames = GetAllResourceNames();
        if (allNames.Count > 0) parts.Add($"Resource(s): {string.Join(", ", allNames)} ({ResourceType})");
        else if (!string.IsNullOrWhiteSpace(ResourceName)) parts.Add($"Resource: {ResourceName} ({ResourceType})");
        if (!string.IsNullOrWhiteSpace(Region)) parts.Add($"Region: {Region}");
        return parts.Count > 0 ? string.Join(", ", parts) : "No Azure scope resolved.";
    }
}
