using System.Text.Json.Serialization;

namespace CXOAI.Memory;

/// <summary>
/// A reference to a customer, product, or program entity.
/// Used in FavoriteCustomers, RecentCustomers, FavoriteProducts, etc.
/// </summary>
public class EntityReference
{
    [JsonPropertyName("EntityName")]
    public string EntityName { get; set; } = string.Empty;

    [JsonPropertyName("EntityId")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("EntityType")]
    public string EntityType { get; set; } = string.Empty;
}
