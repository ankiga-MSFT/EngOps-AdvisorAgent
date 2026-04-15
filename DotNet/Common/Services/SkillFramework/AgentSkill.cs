using CXOAI.ConfigurationStore;
using System.Text;

namespace CXOAI.SkillFramework;

public class AgentSkill
{
    public string SkillName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<SkillTool> Tools { get; set; } = [];
    public string SystemPrompt { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Describes what input this skill expects in its prompt.
    /// Populated from SkillConfiguration.ExpectedSkillInput in the skill's config.</summary>
    public string? ExpectedSkillInput { get; set; }

    /// <summary>LLM sampling temperature. Null means use default (0f).</summary>
    public float? Temperature { get; set; }

    /// <summary>Seed for deterministic sampling. Null means use default (42).</summary>
    public long? Seed { get; set; }

    /// <summary>Execution timeout in seconds. Defaults to 60.</summary>
    public int Timeout { get; set; } = 60;

    /// <summary>Configuration type (e.g., "skill"). Defaults to "skill".</summary>
    public string Type { get; set; } = "skill";
}

public static class AgentSkillMarkdownExtensions
{
    public static string ToMarkDown(this AgentSkill skill)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"## {skill.SkillName}");
        builder.AppendLine($"- **Description:** {skill.Description}");
        builder.AppendLine($"- **Model:** {skill.ModelName}");
        if (skill.Tools is { Count: > 0 })
        {
            builder.AppendLine($"- **Tools:**");
            foreach (var tool in skill.Tools)
            {
                builder.AppendLine($"  - **{tool.Name}**");
                builder.AppendLine($"    - Description: {tool.Description}");
                builder.AppendLine($"    - Schema: {tool.Schema}");
            }
        }
        return builder.ToString();
    }

    public static string ToMarkDown(this List<AgentSkill> skills)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Skills");
        foreach (var skill in skills)
        {
            builder.AppendLine();
            builder.Append(skill.ToMarkDown());
        }
        return builder.ToString();
    }
}

// NOTE: No longer used after Task Planner replaced GetSkillPromptsAsync / GetSkillExecutionDAGAsync.
// Kept for reference � remove after production migration is confirmed.

//public static string ToSummaryMarkDown(this AgentSkill skill)
//{
//    var builder = new StringBuilder();
//    builder.AppendLine($"## {skill.SkillName}");
//    builder.AppendLine($"- **Description:** {skill.Description}");
//    return builder.ToString();
//}

//public static string ToSummaryMarkDown(this List<AgentSkill> skills)
//{
//    var builder = new StringBuilder();
//    builder.AppendLine("# Skills");
//    foreach (var skill in skills)
//    {
//        builder.AppendLine();
//        builder.Append(skill.ToSummaryMarkDown());
//    }
//    return builder.ToString();
//}

//public class SkillDependency
//{
//    public string SkillName { get; set; } = string.Empty;
//    public List<string> DependsOn { get; set; } = [];
//}

//public class SkillExecutionDAG
//{
//    public List<SkillDependency> Skills { get; set; } = [];
//
//    public Dictionary<string, List<string>> ToDictionary() =>
//        Skills
//            .GroupBy(s => s.SkillName, StringComparer.OrdinalIgnoreCase)
//            .ToDictionary(
//                g => g.Key,
//                g => g.SelectMany(s => s.DependsOn).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
//                StringComparer.OrdinalIgnoreCase);
//}
