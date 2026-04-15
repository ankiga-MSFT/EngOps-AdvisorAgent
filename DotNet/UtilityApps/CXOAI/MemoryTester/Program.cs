using Azure.Core;
using Azure.Identity;
using CXOAI.AppServices;
using CXOAI.Memory;
using CXOAI.SkillFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Provider;
using Provider.Interfaces;
using Provider.Model;
using System.Text;

namespace CXOAI.MemoryTester;

internal class Program
{
    static async Task Main(string[] args)
    {
        // ?? Build DI container (mirrors CXOAIConsole / Functions Program.cs) ??
        var services = new ServiceCollection();

        // Load environment settings (same pattern as CXOAIConsole)
        var environment = Environment.GetEnvironmentVariable(AppSettingConstants.EnvironmentVariableName) ?? "test";
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), AppSettingConstants.EnvironmentSettingsFolderName);
        var jsonFile = Path.Combine(configPath, $"{environment}.environment.settings.json");

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });

        // IAppSettingService (read local JSON — same as StorageAppSettingService in DEBUG)
        var jsonContent = await File.ReadAllTextAsync(jsonFile);
        var configDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent, new StringDictionaryConverter())!;
        services.AddSingleton<IAppSettingService>(new AppSettingService(configDictionary));

        // TokenCredential (VisualStudioCredential for local development)
        services.AddSingleton<TokenCredential>(sp => new VisualStudioCredential());

        // CosmosDB provider (keyed — same as CXOAIConsole / Functions)
        services.AddKeyedSingleton<ICosmosDbProvider>("MemoryStore", (sp, _) =>
        {
            var appSettingService = sp.GetRequiredService<IAppSettingService>();
            var config = appSettingService.Configuration;
            var allStores = JsonConvert.DeserializeObject<Dictionary<string, CosmosDbConfig>>(config[AppSettingConstants.Configuration_CosmosDbsMaps])!;
            return new CosmosDbProvider(allStores["MemoryStore"]);
        });

        // IMemoryStore (same registration as CXOAIConsole / Functions)
        services.AddSingleton<IMemoryStore>(sp =>
        {
            var appSettingService = sp.GetRequiredService<IAppSettingService>();
            var config = appSettingService.Configuration;
            var cosmosProvider = sp.GetRequiredKeyedService<ICosmosDbProvider>("MemoryStore");
            var openAIEndpoint = config[AppSettingConstants.Configuration_AzureOpenAIEndpoint];
            var cred = sp.GetRequiredService<TokenCredential>();
            var embeddingDeployment = config[AppSettingConstants.Configuration_EmbeddingDeployment];
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new CosmosMemoryStore(cosmosProvider, openAIEndpoint, cred, loggerFactory, embeddingDeployment);
        });

        await using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var memoryStore = serviceProvider.GetRequiredService<IMemoryStore>();

        logger.LogInformation("MemoryTester starting with environment: {Environment}", environment);

        Console.WriteLine("??????????????????????????????????????????");
        Console.WriteLine("  CXOAI Memory Tester (Cosmos DB)");
        Console.WriteLine("  Test extraction, storage, and recall");
        Console.WriteLine("??????????????????????????????????????????");
        Console.WriteLine($"  Environment: {environment}");

        // ?? Collect session info ??
        Console.Write("\nUserId: ");
        var userId = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(userId))
            userId = "testuser";

        Console.Write("SessionId: ");
        var sessionId = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(sessionId))
            sessionId = Guid.NewGuid().ToString("N")[..8];

        Console.WriteLine($"\n  Using userId={userId}, sessionId={sessionId}");
        Console.WriteLine("  Org facts stored under: {0}\n", MemoryConstants.OrgUserId);

        // ?? Main menu loop ??
        while (true)
        {
            Console.WriteLine("?????????????????????????????????????????");
            Console.WriteLine("  [1] Simulate conversation turn (extract + store)");
            Console.WriteLine("  [2] Recall facts (semantic search)");
            Console.WriteLine("  [3] Show all facts");
            Console.WriteLine("  [4] Forget a fact by ID");
            Console.WriteLine("  [5] Change userId / sessionId");
            Console.WriteLine("  [0] Exit");
            Console.Write("Choice: ");

            var choice = Console.ReadLine()?.Trim();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    await SimulateConversationTurn(memoryStore, userId, logger);
                    break;
                case "2":
                    await RecallFacts(memoryStore, userId);
                    break;
                case "3":
                    await ShowAllFacts(memoryStore, userId);
                    break;
                case "4":
                    await ForgetFact(memoryStore, userId);
                    break;
                case "5":
                    Console.Write("New userId: ");
                    userId = Console.ReadLine()?.Trim() ?? userId;
                    Console.Write("New sessionId: ");
                    sessionId = Console.ReadLine()?.Trim() ?? sessionId;
                    Console.WriteLine($"  Now using userId={userId}, sessionId={sessionId}");
                    break;
                case "0":
                case null:
                case "":
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
        }
    }

    /// <summary>
    /// Simulates a conversation turn: collects user query + system output,
    /// then runs both User-scope and Org-scope extraction, just like the real orchestrator.
    /// </summary>
    static async Task SimulateConversationTurn(IMemoryStore store, string userId, ILogger logger)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("?? Simulate Conversation Turn ??");
        Console.ResetColor();

        Console.Write("User query: ");
        var userQuery = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            Console.WriteLine("  (empty query, skipping)");
            return;
        }

        Console.WriteLine("System/skill output (paste multi-line, then type END on a new line):");
        var outputBuilder = new StringBuilder();
        while (true)
        {
            var line = Console.ReadLine();
            if (line is null || line.Equals("END", StringComparison.OrdinalIgnoreCase))
                break;
            outputBuilder.AppendLine(line);
        }
        var systemOutput = outputBuilder.ToString().TrimEnd();

        // Build conversation content in the same format SummarizeAndStoreAsync uses
        var conversationContent = $"[UserPrompt] {userQuery}";
        if (!string.IsNullOrWhiteSpace(systemOutput))
            conversationContent += $"\n[SkillOutput:Result] {systemOutput}";

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n  Conversation content ({conversationContent.Length} chars):");
        Console.WriteLine($"  {conversationContent[..Math.Min(200, conversationContent.Length)]}...");
        Console.ResetColor();

        // ?? Extract User-scoped facts (preferences only) ??
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  [User Scope] Extracting preferences...");
        Console.ResetColor();
        try
        {
            await store.ExtractAndStoreAsync(userId, conversationContent, MemoryScope.User);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [User Scope] Done");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [User Scope] Failed: {ex.Message}");
            Console.ResetColor();
        }

        // ?? Extract Org-scoped facts (data values — shared cache) ??
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  [Org Scope] Extracting data facts under '{MemoryConstants.OrgUserId}'...");
        Console.ResetColor();
        try
        {
            await store.ExtractAndStoreAsync(MemoryConstants.OrgUserId, conversationContent, MemoryScope.Org);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [Org Scope] Done");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [Org Scope] Failed: {ex.Message}");
            Console.ResetColor();
        }

        // Show what was stored
        Console.WriteLine("\n  ?? Facts after this turn ??");
        PrintFacts("User", await store.GetAllFactsAsync(userId, MemoryScope.User));
        PrintFacts("Org", await store.GetAllFactsAsync(MemoryConstants.OrgUserId, MemoryScope.Org));
    }

    /// <summary>
    /// Semantic recall: embed the query and find matching facts in both User and Org scopes.
    /// </summary>
    static async Task RecallFacts(IMemoryStore store, string userId)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("?? Recall Facts (Semantic Search) ??");
        Console.ResetColor();

        Console.Write("Search query: ");
        var query = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(query))
            return;

        Console.Write("Min similarity score (default 0.65): ");
        var scoreInput = Console.ReadLine()?.Trim();
        var minScore = float.TryParse(scoreInput, out var s) ? s : 0.65f;

        // Recall from User scope
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  [User Scope] Recalling for userId='{userId}'...");
        Console.ResetColor();
        var userFacts = await store.RecallAsync(userId, query, topK: 10, minScore: minScore, scope: MemoryScope.User);
        PrintFacts("User", userFacts);

        // Recall from Org scope
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  [Org Scope] Recalling for userId='{MemoryConstants.OrgUserId}'...");
        Console.ResetColor();
        var orgFacts = await store.RecallAsync(MemoryConstants.OrgUserId, query, topK: 10, minScore: minScore, scope: MemoryScope.Org);
        PrintFacts("Org", orgFacts);
    }

    /// <summary>
    /// Dump all facts for the current user and the shared Org partition.
    /// </summary>
    static async Task ShowAllFacts(IMemoryStore store, string userId)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("?? All Facts ??");
        Console.ResetColor();

        var userFacts = await store.GetAllFactsAsync(userId);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  User facts (userId='{userId}'):");
        Console.ResetColor();
        PrintFacts("User", userFacts);

        var orgFacts = await store.GetAllFactsAsync(MemoryConstants.OrgUserId, MemoryScope.Org);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  Org facts (userId='{MemoryConstants.OrgUserId}'):");
        Console.ResetColor();
        PrintFacts("Org", orgFacts);
    }

    /// <summary>
    /// Delete a fact by ID.
    /// </summary>
    static async Task ForgetFact(IMemoryStore store, string userId)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("?? Forget Fact ??");
        Console.ResetColor();

        Console.Write("Fact ID to forget: ");
        var factId = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(factId))
            return;

        Console.Write($"Delete from userId '{userId}' or '{MemoryConstants.OrgUserId}'? [U/O]: ");
        var owner = Console.ReadLine()?.Trim();
        var targetUserId = owner?.Equals("O", StringComparison.OrdinalIgnoreCase) == true
            ? MemoryConstants.OrgUserId
            : userId;

        await store.ForgetAsync(targetUserId, factId);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Forgot fact '{factId}' from '{targetUserId}'");
        Console.ResetColor();
    }

    /// <summary>
    /// Pretty-print a list of facts to the console.
    /// </summary>
    static void PrintFacts(string label, List<MemoryFact> facts)
    {
        if (facts.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"    ({label}: no facts)");
            Console.ResetColor();
            return;
        }

        foreach (var f in facts)
        {
            var scopeColor = f.Scope == MemoryScope.Org ? ConsoleColor.Magenta : ConsoleColor.White;
            Console.ForegroundColor = scopeColor;
            Console.Write($"    [{f.Scope}]");
            Console.ForegroundColor = f.Category == FactCategory.Temporal ? ConsoleColor.DarkYellow : ConsoleColor.DarkCyan;
            Console.Write($" [{f.Category}]");
            Console.ResetColor();
            Console.Write($" {f.Fact}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            var cacheKeyDisplay = f.CacheKey != null ? $" key={f.CacheKey}" : "";
            Console.WriteLine($"  (id={f.Id[..8]}...{cacheKeyDisplay} entity={f.EntityType ?? "—"}:{f.EntityId ?? "—"} tags=[{string.Join(",", f.Tags)}])");
            Console.ResetColor();
        }
    }
}
