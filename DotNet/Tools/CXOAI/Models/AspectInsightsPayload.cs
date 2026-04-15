using Newtonsoft.Json;

namespace CXOAI.Tools.Models;

/// <summary>
/// Payload sent to the Insights API for aspect queries and entity searches.
/// </summary>
[Serializable]
[JsonObject]
public class AspectInsightsPayload
{
    /// <summary>
    /// Gets or sets field selection
    /// </summary>
    [JsonProperty(nameof(Select))]
    public IList<string>? Select { get; set; }

    /// <summary>
    /// Gets or set which fields to order by
    /// </summary>
    [JsonProperty(nameof(OrderBy))]
    public IList<string>? OrderBy { get; set; }

    /// <summary>
    /// Gets or sets filters
    /// </summary>
    [JsonProperty(nameof(Filter))]
    public string? Filter { get; set; }

    /// <summary>
    /// Gets or set facets
    /// </summary>
    [JsonProperty(nameof(Facets))]
    public IList<string>? Facets { get; set; }

    /// <summary>
    /// Gets or set number of records to return
    /// </summary>
    [JsonProperty(nameof(Top))]
    public int? Top { get; set; }

    /// <summary>
    /// Gets or sets number of records to skip
    /// </summary>
    [JsonProperty(nameof(Skip))]
    public int? Skip { get; set; }

    /// <summary>
    /// Gets or set search text
    /// </summary>
    [JsonProperty(nameof(SearchText))]
    public string? SearchText { get; set; }

    /// <summary>
    /// Gets or set flag for including total result count
    /// </summary>
    [JsonProperty(nameof(IncludeTotalResultCount))]
    public bool IncludeTotalResultCount { get; set; } = true;

    /// <summary>
    /// Gets or sets search mode
    /// </summary>
    [JsonProperty(nameof(SearchMode))]
    public string? SearchMode { get; set; }

    /// <summary>
    /// Gets or sets search fields
    /// </summary>
    [JsonProperty(nameof(SearchFields))]
    public IList<string>? SearchFields { get; set; }

    /// <summary>
    /// Gets or sets scoring profile
    /// </summary>
    [JsonProperty(nameof(ScoringProfile))]
    public string? ScoringProfile { get; set; }

    /// <summary>
    /// Gets or sets query type
    /// </summary>
    [JsonProperty(nameof(QueryType))]
    public string? QueryType { get; set; }
}
