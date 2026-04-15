using System.Text.Json.Serialization;

namespace CXOAI.Memory;

/// <summary>
/// Raw payload from the portal UI. Matches the full JSON shape sent by the frontend.
/// This is the ingestion model — it accepts everything the UI sends.
/// Use <see cref="ToUserContext"/> to map to the orchestration-relevant <see cref="UserContext"/>.
/// </summary>
public class UIPayload
{
    [JsonPropertyName("EntityName")]
    public string? EntityName { get; set; }

    [JsonPropertyName("EntityId")]
    public string? EntityId { get; set; }

    [JsonPropertyName("EntityType")]
    public string? EntityType { get; set; }

    [JsonPropertyName("FavoriteCustomers")]
    public List<EntityReference>? FavoriteCustomers { get; set; }

    [JsonPropertyName("RecentCustomers")]
    public List<EntityReference>? RecentCustomers { get; set; }

    [JsonPropertyName("FavoriteProduct")]
    public List<EntityReference>? FavoriteProducts { get; set; }

    [JsonPropertyName("RecentProducts")]
    public List<EntityReference>? RecentProducts { get; set; }

    [JsonPropertyName("FavoritePrograms")]
    public List<EntityReference>? FavoritePrograms { get; set; }

    [JsonPropertyName("RecentPrograms")]
    public List<EntityReference>? RecentPrograms { get; set; }

    /// <summary>UI sends a flat array of filters.
    /// Passed directly to UserContext in <see cref="ToUserContext"/>.</summary>
    [JsonPropertyName("GlobalLevelFilters")]
    public List<GlobalLevelFilter>? GlobalLevelFilters { get; set; }

    [JsonPropertyName("SessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("UserId")]
    public string? UserId { get; set; }

    [JsonPropertyName("RequestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("Prompt")]
    public string? Prompt { get; set; }

    /// <summary>
    /// Maps the raw UI payload to the orchestration pipeline's <see cref="UserContext"/>.
    /// Runs lightweight entity resolution against the prompt to determine the best entity.
    /// </summary>
    /// <param name="prompt">The user's prompt text, used for entity resolution.</param>
    public UserContext ToUserContext(string? prompt = null)
    {
        var entityName = EntityName;
        var entityId = EntityId;
        var entityType = EntityType;

        // Run entity resolution if a prompt is provided
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            var resolved = EntityResolver.Resolve(prompt, this);
            if (resolved != null)
            {
                entityName = resolved.EntityName;
                entityId = resolved.EntityId;
                entityType = resolved.EntityType;
            }
        }

        return new UserContext
        {
            EntityName = entityName,
            EntityId = entityId,
            EntityType = entityType,
            GlobalLevelFilters = GlobalLevelFilters ?? []
        };
    }
}
