using Newtonsoft.Json;

namespace CXOAI.Tools.Models;

/// <summary>
/// Filter configuration for a metric aspect.
/// </summary>
public class FilterConfig
{
    [JsonProperty("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("ValueEnums")]
    public List<string> ValueEnums { get; set; } = new();

    [JsonProperty("Default")]
    public string Default { get; set; } = string.Empty;

    [JsonProperty("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("Required")]
    public bool Required { get; set; }

    [JsonProperty("Expression")]
    public string Expression { get; set; } = string.Empty;

    [JsonProperty("Keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonProperty("SupportedEntities")]
    public List<string> SupportedEntities { get; set; } = new();

    [JsonProperty("BackendFilterName")]
    public string? BackendFilterName { get; set; }

    [JsonProperty("IsActive")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("IsGlobal")]
    public bool IsGlobal { get; set; } = false;
}

/// <summary>
/// Select field configuration for a metric aspect.
/// </summary>
public class SelectFieldConfig
{
    [JsonProperty("DefaultFields")]
    public List<string> DefaultFields { get; set; } = new();

    [JsonProperty("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("Required")]
    public bool Required { get; set; }
}

/// <summary>
/// GroupBy configuration for a metric aspect.
/// </summary>
public class GroupByConfig
{
    [JsonProperty("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("Keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonProperty("SupportedEntities")]
    public List<string>? SupportedEntities { get; set; }

    [JsonProperty("IsActive")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Parameter configuration for a metric aspect.
/// </summary>
public class ParameterConfig
{
    [JsonProperty("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("Required")]
    public bool Required { get; set; }

    [JsonProperty("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("ValueEnums")]
    public List<string> ValueEnums { get; set; } = new();

    [JsonProperty("Default")]
    public string Default { get; set; } = string.Empty;
}
