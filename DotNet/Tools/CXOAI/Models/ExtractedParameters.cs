namespace CXOAI.Tools.Models;

/// <summary>
/// Parameters extracted from the user query by the LLM, used to build aspect API requests.
/// </summary>
public class ExtractedParameters
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string View { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Aggregation { get; set; } = string.Empty;
    public Dictionary<string, string> FilterValues { get; set; } = new();
    public List<string> SelectGroupByFields { get; set; } = new();
    public List<string> SelectFields { get; set; } = new();

    /// <summary>
    /// Whether the user explicitly provided date parameters.
    /// Used to decide whether to honor config-defined DefaultDateRangeMonths.
    /// </summary>
    public bool IsDateExplicitlyProvided { get; set; }

    /// <summary>
    /// Global-level filters from the request context (applied across all metrics in the session).
    /// </summary>
    public List<ViewFilter>? GlobalLevelFilters { get; set; }
}
