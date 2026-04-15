namespace CXOAI.ConfigurationStore;

using System.Text.Json;

public class Skill
{
    public string Name { get; set; } = string.Empty;

    public string MarkDown { get; set; } = string.Empty;

    public SkillDefinition Definition { get; set; } = new();
}

// NOTE: No longer used after Task Planner replaced GetRelevantSkillsAsync.
// Kept for reference � remove after production migration is confirmed.

//public class TinySkill
//{
//    public string Name { get; set; } = string.Empty;
//    public string Description { get; set; } = string.Empty;
//
//    public override string ToString()
//    {
//        return $"### Name:\n {Name}\n ### Description:\n {Description}";
//    }
//}

public class SkillConfiguration
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Describes what input this skill expects in its prompt.
    /// Used by the prompt generator LLM to know exactly what information to extract
    /// from the context and include in the sub-prompt.
    /// Example: "provide me aspect name (e.g., get_csat_score), original user prompt,
    /// and UI context (entity name, entity ID, entity type, and active filters)"</summary>
    public string ExpectedSkillInput { get; set; } = string.Empty;

    /// <summary>LLM sampling temperature for this skill. Defaults to 0 (deterministic) when null.</summary>
    public float? Temperature { get; set; }

    /// <summary>Seed for deterministic sampling. Defaults to 42 when null.</summary>
    public long? Seed { get; set; }

    /// <summary>Execution timeout in seconds. Defaults to 60 when null.</summary>
    public int? Timeout { get; set; }

    /// <summary>Configuration type (e.g., "skill"). Defaults to "skill" when null or empty.</summary>
    public string? Type { get; set; }
}

public class SkillDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Configuration { get; set; } = string.Empty;

    private SkillConfiguration? _parsed;
    private SkillConfiguration Parsed => _parsed ??= ParseConfiguration();

    public string ModelName => Parsed.ModelName;
    public string SystemPrompt => Parsed.SystemPrompt;

    public List<SkillTool> Tools { get; set; } = [];

    private SkillConfiguration ParseConfiguration()
    {
        if (string.IsNullOrWhiteSpace(Configuration) || Configuration == "TODO")
            return new SkillConfiguration();

        try
        {
            return JsonSerializer.Deserialize<SkillConfiguration>(Configuration, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new SkillConfiguration();
        }
        catch
        {
            return new SkillConfiguration();
        }
    }
}

public class SkillTool
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Schema { get; set; } = string.Empty;
}
