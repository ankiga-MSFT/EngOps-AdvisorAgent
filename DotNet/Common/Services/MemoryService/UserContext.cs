using System.Text.Json.Serialization;

namespace CXOAI.Memory;

/// <summary>
/// A single global-level filter applied in the UI.
/// Only the fields needed by the orchestration pipeline are captured;
/// UI-only fields (ColumnType, AnalyzerType, ShowSelectAll, endpointAddress, etc.) are ignored.
/// </summary>
public class GlobalLevelFilter
{
    /// <summary>Display name shown in the UI (e.g., "Subscription Status").</summary>
    [JsonPropertyName("Name")]
    public string UIFilterName { get; set; } = string.Empty;

    /// <summary>Backend column name used in API/KQL queries (e.g., "SubscriptionStatus").</summary>
    [JsonPropertyName("Column")]
    public string BackendFilterName { get; set; } = string.Empty;

    /// <summary>Filter operator (e.g., "==" or "in").</summary>
    [JsonPropertyName("FilterClause")]
    public string FilterClause { get; set; } = "==";

    /// <summary>Values selected by the user (e.g., ["ACTIVE"]).</summary>
    [JsonPropertyName("SelectedValues")]
    public List<string> SelectedValues { get; set; } = [];
}

/// <summary>
/// User context sent from the portal/chat UI alongside the prompt.
/// Contains the entity being viewed, active filters, and other UI selections.
/// Context is injected directly into the enhanced prompt (not stored in long-term memory —
/// UI selections are navigation context, not permanent facts).
/// </summary>
public class UserContext
{
    [JsonPropertyName("aspectName")]
    public string? AspectName { get; set; }

    [JsonPropertyName("filters")]
    public Dictionary<string, string> Filters { get; set; } = [];

    [JsonPropertyName("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = [];

    [JsonPropertyName("groupBy")]
    public List<string> GroupBy { get; set; } = [];

    [JsonPropertyName("EntityName")]
    public string? EntityName { get; set; }

    [JsonPropertyName("EntityId")]
    public string? EntityId { get; set; }

    [JsonPropertyName("EntityType")]
    public string? EntityType { get; set; }

    [JsonPropertyName("GlobalLevelFilters")]
    public List<GlobalLevelFilter> GlobalLevelFilters { get; set; } = [];

    /// <summary>Convert UI selections to discrete facts for embedding and storage.</summary>
    public List<ExtractedFact> ToFacts()
    {
        var facts = new List<ExtractedFact>();

        if (!string.IsNullOrEmpty(EntityName))
            facts.Add(Fact($"User selected entity: {EntityName}", ["entity", EntityName]));

        if (!string.IsNullOrEmpty(EntityId))
            facts.Add(Fact($"{EntityName ?? "Selected entity"} entity ID is {EntityId}", ["entity_id", EntityName ?? "entity"]));

        if (!string.IsNullOrEmpty(AspectName))
            facts.Add(Fact($"User selected aspect: {AspectName}", ["aspect", AspectName]));

        foreach (var (name, value) in Filters)
            facts.Add(Fact($"User applied filter {name} = {value}", ["filter", name]));

        foreach (var (name, value) in Parameters)
            facts.Add(Fact($"User set parameter {name} = {value}", ["parameter", name]));

        if (GroupBy.Count > 0)
            facts.Add(Fact($"User wants results grouped by: {string.Join(", ", GroupBy)}", ["groupby"]));

        return facts;
    }

    private static ExtractedFact Fact(string text, List<string> tags) => new()
    {
        Fact = text,
        Category = FactCategory.Temporal,
        Scope = MemoryScope.User,
        Source = "ui_selection",
        Tags = tags
    };
}
