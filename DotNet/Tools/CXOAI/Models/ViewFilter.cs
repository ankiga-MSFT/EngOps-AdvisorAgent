using Newtonsoft.Json;

namespace CXOAI.Tools.Models;

/// <summary>
/// UI-level filter passed from the request context (global filters applied across all metrics).
/// Matches the old repo's AppService.Models.ViewFilter structure.
/// </summary>
public class ViewFilter
{
    [JsonProperty("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("Column")]
    public string Column { get; set; } = string.Empty;

    [JsonProperty("ColumnType")]
    public int ColumnType { get; set; }

    [JsonProperty("ServerColumnType")]
    public int ServerColumnType { get; set; }

    [JsonProperty("defaultSelectedValues")]
    public List<object> DefaultSelectedValues { get; set; } = new();

    [JsonProperty("SelectedValues")]
    public List<object> SelectedValues { get; set; } = new();

    [JsonProperty("FilterClause")]
    public string FilterClause { get; set; } = "==";

    [JsonProperty("From")]
    public object? From { get; set; }

    [JsonProperty("To")]
    public object? To { get; set; }

    [JsonProperty("ContainsSelectedValues")]
    public List<string> ContainsSelectedValues { get; set; } = new();
}

/// <summary>
/// Filter root payload for posting temp filter details to the metadata service.
/// Matches the UI's AssistantAI.Common.Models.Root structure.
/// </summary>
public class FilterRoot
{
    [JsonProperty("CurrentPage")]
    public int CurrentPage { get; set; } = 1;

    [JsonProperty("PageSize")]
    public int PageSize { get; set; } = 10;

    [JsonProperty("Filters")]
    public List<ViewFilter> Filters { get; set; } = new();

    [JsonProperty("FilterType")]
    public string FilterType { get; set; } = "inline";

    [JsonProperty("SortColumns")]
    public List<object> SortColumns { get; set; } = new();
}

/// <summary>
/// Result of temp filter URL generation.
/// </summary>
public class TempFilterUrlResult
{
    /// <summary>
    /// The generated temp filter URL, or null if no filters applied.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Whether filters were applied.
    /// </summary>
    public bool HasFilters { get; set; }

    /// <summary>
    /// List of filter names that were applied.
    /// </summary>
    public List<string> AppliedFilters { get; set; } = new();

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? Error { get; set; }
}
