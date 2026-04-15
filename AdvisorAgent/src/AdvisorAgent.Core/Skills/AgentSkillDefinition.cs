using System.Text.Json.Serialization;

namespace AdvisorAgent.Core.Skills;

/// <summary>
/// Defines a skill the Advisor Agent can execute — loaded from skills.json configuration.
/// </summary>
public sealed class AgentSkillDefinition
{
    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("systemPrompt")]
    public string SystemPrompt { get; set; } = string.Empty;

    [JsonPropertyName("modelName")]
    public string ModelName { get; set; } = "gpt-4o";

    [JsonPropertyName("expectedInput")]
    public string ExpectedInput { get; set; } = string.Empty;

    [JsonPropertyName("tools")]
    public List<SkillToolRef> Tools { get; set; } = [];

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 120;
}

public sealed class SkillToolRef
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
