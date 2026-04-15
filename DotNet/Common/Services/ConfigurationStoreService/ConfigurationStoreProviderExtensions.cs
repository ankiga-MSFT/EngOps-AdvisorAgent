using System.Text;

namespace CXOAI.ConfigurationStore;

/// <summary>
/// Extension methods on <see cref="ITreeConfigurationStoreProvider"/> so that
/// <see cref="CXOAI.SkillFramework.OrchestratorStepService"/> can resolve skills
/// regardless of the backing store (local JSON file or Azure Search).
/// </summary>
public static class ConfigurationStoreProviderExtensions
{
    public static async Task<List<Skill>> GetRelevantSkillAsync(
        this ITreeConfigurationStoreProvider provider,
        string vectorSearchString)
    {
        var configurations = await provider.GetConfigurationsWithDescription(
            "Skill", vectorSearchString, true);

        var configLookup = configurations.ToDictionary(c => c.Id);

        return configurations
            .Where(c => c.ComponentName == "Skill")
            .Select(c =>
            {
                var definition = BuildSkillDefinition(c, configLookup);
                return new Skill
                {
                    Name = c.ConfigurationName ?? string.Empty,
                    Definition = definition,
                    MarkDown = ToMarkDown(definition)
                };
            })
            .ToList();
    }

    public static async Task<List<Skill>> GetSkillsByNameAsync(
        this ITreeConfigurationStoreProvider provider,
        List<string> skillNames)
    {
        var configurations = await provider.GetConfigurationsWithNames(
            "Skill", skillNames, true);

        var configLookup = configurations.ToDictionary(c => c.Id);

        return configurations
            .Where(c => c.ComponentName == "Skill")
            .Select(c =>
            {
                var definition = BuildSkillDefinition(c, configLookup);
                return new Skill
                {
                    Name = c.ConfigurationName ?? string.Empty,
                    Definition = definition,
                    MarkDown = ToMarkDown(definition)
                };
            })
            .ToList();
    }

    private static SkillDefinition BuildSkillDefinition(
        TreeConfiguration config,
        Dictionary<string, TreeConfiguration> configLookup)
    {
        var definition = new SkillDefinition
        {
            Name = config.ConfigurationName ?? string.Empty,
            Description = config.Description ?? string.Empty,
            Configuration = config.Configuration ?? string.Empty
        };

        if (config.DependsOn is { Count: > 0 })
        {
            var visited = new HashSet<string> { config.Id };
            CollectTools(config.DependsOn, configLookup, definition.Tools, visited);
        }

        return definition;
    }

    private static void CollectTools(
        IList<DependsOnEntry> dependencies,
        Dictionary<string, TreeConfiguration> configLookup,
        List<SkillTool> tools,
        HashSet<string> visited)
    {
        foreach (var dep in dependencies)
        {
            var depId = $"{dep.ComponentName}-{dep.ConfigurationName}";

            if (!visited.Add(depId))
                continue;

            if (configLookup.TryGetValue(depId, out var depConfig))
            {
                tools.Add(new SkillTool
                {
                    Name = depConfig.ConfigurationName ?? string.Empty,
                    Description = depConfig.Description ?? string.Empty,
                    Schema = depConfig.Configuration ?? string.Empty
                });
            }
        }
    }

    private static string ToMarkDown(SkillDefinition definition)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Skill:");
        builder.AppendLine($"\tName: {definition.Name}");
        builder.AppendLine($"\tDescription: {definition.Description}");
        builder.AppendLine($"\tConfiguration: {definition.Configuration}");
        builder.AppendLine($"\tTools:");
        foreach (var tool in definition.Tools)
        {
            builder.AppendLine($"\t\tToolName: {tool.Name}");
            builder.AppendLine($"\t\t\tToolDescription: {tool.Description}");
            builder.AppendLine($"\t\t\tToolSchema: {tool.Schema}");
        }
        return builder.ToString();
    }
}
