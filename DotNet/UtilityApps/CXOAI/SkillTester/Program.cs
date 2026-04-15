using CXOAI.ConfigurationStore;
using CXOAI.SkillTester;
using CXOAI.SkillTester.Skills;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Provider;
using Provider.Model;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Information);
    builder.AddConsole();
});

var storeProvider = new TreeJsonConfigurationStoreProvider("StoreConfigs/test.SeedData.json");

// Load KustoProvider config from User Secrets
var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var kustoDbConfig = new KustoDbConfig
{
    KustoClusterUrl = configuration["kustoClusterUrl"] ?? "https://supportadxwus3test.westus3.kusto.windows.net/",
    KustoDatabaseName = configuration["kustoDatabaseName"] ?? "ICMRatioData"
};
var kustoProvider = new KustoProvider(kustoDbConfig);

// Register all skill testers — add new ones here
List<ISkillTester> testers =
[
    new AspectSkillTester(loggerFactory, storeProvider),
    new ReportingSkillTester(loggerFactory, storeProvider),
    new NLTKqlSkillTester(loggerFactory, storeProvider, kustoProvider),
    new UXGeneratorSkillTester(loggerFactory, storeProvider),  // [UX_GENERATOR_SKILL] Section 3.2
    new SummarizationSkillTester(loggerFactory, storeProvider),
    new ManagedReviewSkillTester(loggerFactory, storeProvider)
];

Console.WriteLine("??????????????????????????????????????????");
Console.WriteLine("  CXOAI Skill Tester");
Console.WriteLine("  Test your skill prompts independently");
Console.WriteLine("??????????????????????????????????????????");

while (true)
{
    Console.WriteLine();
    Console.WriteLine("Select a skill to test:");
    for (int i = 0; i < testers.Count; i++)
        Console.WriteLine($"  [{i + 1}] {testers[i].Name}");
    Console.WriteLine($"  [P] Patch Skills.json (sync tester prompt → Skills.json)");
    Console.WriteLine("  [0] Exit");
    Console.Write("Choice: ");

    var input = Console.ReadLine()?.Trim();
    if (input is "0" or null or "")
    {
        Console.WriteLine("Goodbye!");
        return;
    }

    if (input.Equals("P", StringComparison.OrdinalIgnoreCase))
    {
        await SkillPromptPatcher.RunAsync(testers);
        continue;
    }

    if (int.TryParse(input, out var choice) && choice >= 1 && choice <= testers.Count)
    {
        await testers[choice - 1].RunAsync();
    }
    else
    {
        Console.WriteLine("Invalid choice.");
    }
}
