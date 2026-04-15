using System.Text.Json;
using AdvisorAgent.Core.ContextResolution;
using AdvisorAgent.Core.Conversation;
using AdvisorAgent.Core.Skills;
using AdvisorAgent.Tools;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// ── Configuration values ────────────────────────────────────────
var openAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is required");
var modelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL") ?? "gpt-4o";

// ── Azure credential ────────────────────────────────────────────
builder.Services.AddSingleton(new DefaultAzureCredential());

// ── Load skill catalog from skills.json ─────────────────────────
builder.Services.AddSingleton<Dictionary<string, AgentSkillDefinition>>(sp =>
{
    var skillsPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "skills.json");
    if (!File.Exists(skillsPath))
        throw new FileNotFoundException("skills.json not found", skillsPath);

    var json = File.ReadAllText(skillsPath);
    var skills = JsonSerializer.Deserialize<List<AgentSkillDefinition>>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Failed to deserialize skills.json");

    return skills.ToDictionary(s => s.SkillName);
});

// ── Register tool instances (reflection-resolved by AgentOrchestrationService) ─
builder.Services.AddSingleton<Dictionary<string, object>>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var httpClient = new HttpClient();
    return new Dictionary<string, object>
    {
        ["AdvisorRecommendationTools"] = new AdvisorRecommendationTools(loggerFactory.CreateLogger<AdvisorRecommendationTools>(), httpClient),
        ["RetirementTools"] = new RetirementTools(loggerFactory.CreateLogger<RetirementTools>(), httpClient),
        ["ResiliencyTools"] = new ResiliencyTools(loggerFactory.CreateLogger<ResiliencyTools>(), httpClient),
        ["CostOptimizationTools"] = new CostOptimizationTools(loggerFactory.CreateLogger<CostOptimizationTools>(), httpClient),
        ["OutageRemediationTools"] = new OutageRemediationTools(loggerFactory.CreateLogger<OutageRemediationTools>(), httpClient),
        ["ResourceGraphTools"] = new ResourceGraphTools(loggerFactory.CreateLogger<ResourceGraphTools>(), httpClient),
        ["SubscriptionTools"] = new SubscriptionTools(loggerFactory.CreateLogger<SubscriptionTools>(), httpClient),
    };
});

// ── Core services ───────────────────────────────────────────────
builder.Services.AddSingleton<IAgentOrchestrationService>(sp =>
{
    var credential = sp.GetRequiredService<DefaultAzureCredential>();
    var skillCatalog = sp.GetRequiredService<Dictionary<string, AgentSkillDefinition>>();
    var toolInstances = sp.GetRequiredService<Dictionary<string, object>>();
    var logger = sp.GetRequiredService<ILogger<AgentOrchestrationService>>();

    return new AgentOrchestrationService(
        logger, openAiEndpoint, modelName, credential, skillCatalog, toolInstances);
});

builder.Services.AddSingleton<IConversationStore>(sp =>
{
    var storeType = Environment.GetEnvironmentVariable("CONVERSATION_STORE_TYPE") ?? "InMemory";
    var logger = sp.GetRequiredService<ILoggerFactory>();

    if (storeType.Equals("Cosmos", StringComparison.OrdinalIgnoreCase))
    {
        var endpoint = Environment.GetEnvironmentVariable("COSMOS_CONVERSATION_ENDPOINT")
            ?? throw new InvalidOperationException("COSMOS_CONVERSATION_ENDPOINT is required when CONVERSATION_STORE_TYPE=Cosmos");
        var database = Environment.GetEnvironmentVariable("COSMOS_CONVERSATION_DATABASE") ?? "AdvisorAgent";
        var container = Environment.GetEnvironmentVariable("COSMOS_CONVERSATION_CONTAINER") ?? "ConversationStore";

        var credential = sp.GetRequiredService<DefaultAzureCredential>();
        var cosmosClient = new CosmosClient(endpoint, credential, new CosmosClientOptions
        {
            UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }
        });
        var cosmosContainer = cosmosClient.GetContainer(database, container);

        logger.CreateLogger("Program").LogInformation("[DI] Using CosmosConversationStore — Endpoint: {Endpoint}, Database: {Database}, Container: {Container}",
            endpoint, database, container);
        return new CosmosConversationStore(cosmosContainer, logger.CreateLogger<CosmosConversationStore>());
    }

    logger.CreateLogger("Program").LogInformation("[DI] Using InMemoryConversationStore");
    return new InMemoryConversationStore();
});
builder.Services.AddSingleton<IAzureContextResolver>(sp =>
{
    var orchestration = sp.GetRequiredService<IAgentOrchestrationService>();
    return new AzureContextResolver(orchestration);
});

// ── Build and run ───────────────────────────────────────────────
builder.Build().Run();
