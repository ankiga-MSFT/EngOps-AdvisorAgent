using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace CXOAI.Memory;

/// <summary>
/// A discrete fact stored in the long-term memory store.
/// Supports multiple scopes: user preferences, entity mappings, system config.
/// Both System.Text.Json and Newtonsoft attributes are needed because the Cosmos SDK
/// uses Newtonsoft by default, while other parts of the app use System.Text.Json.
/// </summary>
public class MemoryFact
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("UserId")]
    [JsonProperty("UserId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    [JsonProperty("scope")]
    public MemoryScope Scope { get; set; } = MemoryScope.User;

    [JsonPropertyName("fact")]
    [JsonProperty("fact")]
    public string Fact { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    [JsonProperty("category")]
    public FactCategory Category { get; set; } = FactCategory.Permanent;

    [JsonPropertyName("source")]
    [JsonProperty("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    [JsonProperty("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("embedding")]
    [JsonProperty("embedding")]
    public float[] Embedding { get; set; } = [];

    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("updatedAt")]
    [JsonProperty("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Cosmos DB TTL in seconds. -1 = no expiry (Permanent facts), 3600 = 1 hour (Temporal facts).
    /// Cosmos auto-deletes the document when TTL expires. Ignored by FileMemoryStore.
    /// </summary>
    [JsonPropertyName("ttl")]
    [JsonProperty("ttl", NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeToLive { get; set; }

    /// <summary>
    /// Entity type this fact relates to (customer, product, workload, program), or null for non-entity facts.
    /// </summary>
    [JsonPropertyName("entityType")]
    [JsonProperty("entityType", NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EntityType { get; set; }

    /// <summary>
    /// Entity identifier (TPID or CH URI) this fact relates to, or null for non-entity facts.
    /// </summary>
    [JsonPropertyName("entityId")]
    [JsonProperty("entityId", NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EntityId { get; set; }

    /// <summary>
    /// Normalized cache key for Org-scoped facts. Format: metric:entityTarget:timeRange[:filters].
    /// Used for deterministic lookup — avoids vector search limitations with parameterized queries.
    /// </summary>
    [JsonPropertyName("cacheKey")]
    [JsonProperty("cacheKey", NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CacheKey { get; set; }
}

/// <summary>
/// Scope determines the namespace of facts for filtering and isolation.
/// <list type="bullet">
///   <item><term>User</term><description>Per-user preferences and entity mappings. Always Permanent.</description></item>
///   <item><term>Org</term><description>Shared data cache across all users/sessions (metric values, trends). Always Temporal with 1-hour TTL. Stored under <see cref="MemoryConstants.OrgUserId"/>.</description></item>
///   <item><term>System</term><description>Reserved for future use. Current logic redirects to Org.</description></item>
/// </list>
/// </summary>
public enum MemoryScope
{
    User,
    Org,
    System
}

public enum FactCategory
{
    Permanent,
    Temporal
}

/// <summary>
/// Well-known constants for the memory subsystem.
/// </summary>
public static class MemoryConstants
{
    /// <summary>
    /// The shared userId for all Org-scoped facts. Acts as an app-wide partition key
    /// so data facts are accessible across users and sessions.
    /// </summary>
    public const string OrgUserId = "EngOps";
}
