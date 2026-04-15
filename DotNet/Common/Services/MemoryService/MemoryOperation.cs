namespace CXOAI.Memory;

/// <summary>
/// Result of the conflict resolution LLM call.
/// </summary>
public class MemoryOperationResult
{
    public MemoryOperationType Operation { get; set; }

    public string MergedFact { get; set; } = string.Empty;

    public FactCategory Category { get; set; } = FactCategory.Permanent;

    public string Reasoning { get; set; } = string.Empty;
}

public enum MemoryOperationType
{
    Add,
    Update,
    Noop
}

/// <summary>
/// Structured output from the fact extraction LLM.
/// </summary>
public class ExtractedFacts
{
    public List<ExtractedFact> Facts { get; set; } = [];
}

public class ExtractedFact
{
    public string Fact { get; set; } = string.Empty;

    public FactCategory Category { get; set; } = FactCategory.Permanent;

    public MemoryScope Scope { get; set; } = MemoryScope.User;

    public string Source { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Entity type this fact relates to: customer, product, workload, program, or null for non-entity facts.
    /// Used for deterministic conflict guard — facts with different EntityIds are never merged.
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Entity identifier (TPID or CH URI) this fact relates to, or null for non-entity facts.
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Normalized cache key for Org-scoped facts. Format: {metric}:{entityTarget}:{timeRange}[:{filters}].
    /// Enables deterministic cache matching — prevents time-range conflation in vector search.
    /// Null for User-scoped facts.
    /// </summary>
    public string? CacheKey { get; set; }
}
