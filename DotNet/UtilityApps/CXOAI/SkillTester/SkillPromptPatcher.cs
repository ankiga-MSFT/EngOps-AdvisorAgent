using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CXOAI.ConfigurationStore;

namespace CXOAI.SkillTester;

/// <summary>
/// Reads Skills.json, lets the user pick a skill, reads the full
/// <see cref="SkillConfiguration"/> from the corresponding <see cref="ISkillTester"/>
/// at runtime, serializes it to a stringified JSON, and patches the entire
/// Configuration field in Skills.json — handling all JSON escaping correctly.
/// Writes directly to the SOURCE file so changes survive rebuilds.
/// </summary>
public static class SkillPromptPatcher
{
    // SkillTester project root is 3 levels up from bin/Debug/net10.0.
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

    // Write to the SOURCE file, not the output copy.
    private static readonly string SourceSkillsJsonPath = Path.GetFullPath(
        Path.Combine(ProjectRoot, "..", "..", "..",
            "Common", "Services", "ConfigurationStoreService", "StoreConfigs", "Skills.json"));

    public static async Task RunAsync(List<ISkillTester> testers)
    {
        if (!File.Exists(SourceSkillsJsonPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Skills.json not found at: {SourceSkillsJsonPath}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Skills.json path: {SourceSkillsJsonPath}");

        // ?? 1. Read and list skills from Skills.json ??
        var json = await File.ReadAllTextAsync(SourceSkillsJsonPath);
        var arr = JsonNode.Parse(json)!.AsArray();

        var skills = arr
            .Where(n => n?["ComponentName"]?.GetValue<string>() == "Skill")
            .ToList();

        if (skills.Count == 0)
        {
            Console.WriteLine("No skills found in Skills.json.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Skills available in Skills.json:");
        for (int i = 0; i < skills.Count; i++)
        {
            var name = skills[i]!["ConfigurationName"]!.GetValue<string>();
            var hasTester = testers.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"  [{i + 1}] {name}{(hasTester ? "" : " (no tester)")}");
        }
        Console.WriteLine("  [A] Patch ALL skills");
        Console.WriteLine("  [0] Back");
        Console.Write("Select skill to patch: ");

        var input = Console.ReadLine()?.Trim();
        if (input is "0" or null or "")
            return;

        if (input.Equals("A", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var skill in skills)
            {
                var name = skill!["ConfigurationName"]!.GetValue<string>();
                await PatchSingleSkillAsync(name, testers);
            }
            return;
        }

        if (!int.TryParse(input, out var choice) || choice < 1 || choice > skills.Count)
        {
            Console.WriteLine("Invalid choice.");
            return;
        }

        var skillName = skills[choice - 1]!["ConfigurationName"]!.GetValue<string>();
        await PatchSingleSkillAsync(skillName, testers);
    }

    private static async Task PatchSingleSkillAsync(string skillName, List<ISkillTester> testers)
    {
        // ?? 2. Find the matching tester by Name ??
        var tester = testers.FirstOrDefault(t =>
            t.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

        if (tester is null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  SKIP '{skillName}' — no matching ISkillTester found.");
            Console.ResetColor();
            return;
        }

        // ?? 3. Get the full configuration from the tester ??
        var config = tester.GetSkillConfiguration();

        if (string.IsNullOrWhiteSpace(config.SystemPrompt))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ERROR: '{skillName}' has empty SystemPrompt. Aborting.");
            Console.ResetColor();
            return;
        }

        if (string.IsNullOrWhiteSpace(config.ModelName))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ERROR: '{skillName}' has empty ModelName. Aborting.");
            Console.ResetColor();
            return;
        }

        // ?? 4. Serialize the full SkillConfiguration to a stringified JSON ??
        // Build a dictionary so null-valued optional fields are omitted from the JSON.
        var configObj = new Dictionary<string, object>
        {
            ["ExpectedSkillInput"] = config.ExpectedSkillInput,
            ["SystemPrompt"] = config.SystemPrompt,
            ["ModelName"] = config.ModelName
        };

        if (config.Temperature.HasValue)
            configObj["Temperature"] = config.Temperature.Value;
        if (config.Seed.HasValue)
            configObj["Seed"] = config.Seed.Value;

        configObj["Timeout"] = config.Timeout ?? 60;
        configObj["Type"] = config.Type ?? "skill";

        var newConfigStr = JsonSerializer.Serialize(configObj);

        // Validate the round-trip
        var validateParsed = JsonNode.Parse(newConfigStr)!.AsObject();
        var validatedModelName = validateParsed["ModelName"]?.GetValue<string>();
        var validatedPrompt = validateParsed["SystemPrompt"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(validatedModelName) || string.IsNullOrWhiteSpace(validatedPrompt))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ERROR: Round-trip validation failed for '{skillName}'. Skills.json NOT modified.");
            Console.ResetColor();
            return;
        }

        // ?? 5. Write to SOURCE file (targeted replacement — zero diff on other skills) ??
        var originalJson = await File.ReadAllTextAsync(SourceSkillsJsonPath);

        // Serialize the new Configuration value as a JSON string value (adds outer quotes + escaping)
        var encodedNewConfig = JsonSerializer.Serialize(newConfigStr);

        // Find and replace only this skill's Configuration value
        var skillNameEscaped = Regex.Escape(skillName);
        var pattern = $@"(""ConfigurationName"":\s*""{skillNameEscaped}"".*?""Configuration"":\s*)"".*?""(\s*,\s*""DependsOn"")";
        var replaced = Regex.Replace(originalJson, pattern, $"$1{encodedNewConfig}$2", RegexOptions.Singleline);

        if (replaced == originalJson)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ERROR: Could not locate Configuration field for '{skillName}'. Skills.json NOT modified.");
            Console.ResetColor();
            return;
        }

        await File.WriteAllTextAsync(SourceSkillsJsonPath, replaced);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ? Patched '{skillName}'");
        Console.WriteLine($"    ModelName: {validatedModelName}");
        Console.WriteLine($"    ExpectedSkillInput: {config.ExpectedSkillInput}");
        Console.WriteLine($"    Timeout: {config.Timeout ?? 60}s");
        Console.WriteLine($"    Type: {config.Type ?? "skill"}");
        Console.WriteLine($"    SystemPrompt: {validatedPrompt.Length} chars");
        Console.ResetColor();
    }
}
