using Azure.Core;
using Azure.Identity;
using CXOAI.AppServices;
using CXOAI.ConfigurationStore;
using CXOAI.ConversationStore;
using CXOAI.Memory;
using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;
using CXOAI.Tools;
using InfraService.OpenTelemetryProvider;
using InfraService.OpenTelemetryProvider.Extensions;
using InfraService.OpenTelemetryProvider.Models;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Middleware.Auth.Configuration;
using Middleware.OpenTelemetryAuditLogger;
using Newtonsoft.Json;
using Provider;
using Provider.Interfaces;
using Provider.Model;
using System.Diagnostics.CodeAnalysis;


namespace CXOAI
{
    [ExcludeFromCodeCoverage]
#pragma warning disable CS8618
    public class Program
    {
        public static ILogger<Program> logger;
        public static IConfiguration Configuration { get; set; }

        public static void Main()
        {
            var host = new HostBuilder()
           .ConfigureFunctionsWebApplication(worker =>
           {

               worker.UseMiddleware<OpenTelemetryAuditMiddleware>();
           }
             )

            .ConfigureAppConfiguration(config =>
            {
                var environment = Environment.GetEnvironmentVariable(AppSettingConstants.EnvironmentVariableName);
                var currentContextPath = Directory.GetCurrentDirectory();
                var configPath = Path.Combine(currentContextPath, AppSettingConstants.EnvironmentSettingsFolderName);

                config.SetBasePath(configPath)
                        .AddJsonFile($"{environment}.environment.settings.json", true, true)
                        .AddEnvironmentVariables();

                Configuration = config.Build();
            })
            .ConfigureServices(static services =>
            {
                AddOpenTelemetryLogging(services, Configuration);
                OpenTelemetryAuditLogger.Init(Configuration[AppSettingConstants.ServiceTreeId]);

                logger = services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();

                // -- Authentication context --
                services.AddScoped<IUserAuthContext, UserAuthContext>();

                services.AddSingleton<IAzureStorageProvider>(sp =>
                {
                    AzureStorageProvider azureStorageProvider = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating AzureStorageProvider Instance");
                        var appStorageAccountName = Environment.GetEnvironmentVariable(AppSettingConstants.AppStorageAccountNameKey)!.ToLower();
                        azureStorageProvider = new AzureStorageProvider(appStorageAccountName);
                        logger.AppLogInformation("Program | DI Registration | Created AzureStorageProvider Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating AzureStorageProvider Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return azureStorageProvider;
                });

                services.AddSingleton<OpenTelemetryAuditMiddleware>(sp =>
                {
                    OpenTelemetryAuditMiddleware openTelemetryAuditMiddleware = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating OpenTelemetryAuditMiddleware Instance");
                        var openTelemetryLogger = sp.GetRequiredService<ILogger<OpenTelemetryAuditMiddleware>>();
                        openTelemetryAuditMiddleware = new OpenTelemetryAuditMiddleware(openTelemetryLogger);
                        logger.AppLogInformation("Program | DI Registration | Created OpenTelemetryAuditMiddleware Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating OpenTelemetryAuditMiddleware Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return openTelemetryAuditMiddleware;
                });

                services.AddSingleton<IStorageAppSettingService>(sp =>
                {
                    StorageAppSettingService appConfig = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating StorageAppSettingService Instance");
                        var provider = sp.GetRequiredService<IAzureStorageProvider>();
                        appConfig = new StorageAppSettingService(provider);
                        logger.AppLogInformation("Program | DI Registration | Created StorageAppSettingService Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating StorageAppSettingService Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return appConfig;
                });

                services.AddSingleton<IAppSettingService>(sp =>
                {
                    AppSettingService appConfig = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating AppSettingService Instance");
                        var appSettingConfiguration = sp.GetRequiredService<IStorageAppSettingService>();
                        var blobFileName = $"{Environment.GetEnvironmentVariable(AppSettingConstants.EnvironmentVariableName)!.ToLower()}.environment.settings.json";
                        logger.AppLogInformation($"Program | DI Registration | Creating AppSettingService Instance blobFileName:{blobFileName}");
                        var configDictionary = appSettingConfiguration.ReadConfigAsync(blobFileName).Result;
                        appConfig = new AppSettingService(configDictionary);
                        logger.AppLogInformation("Program | DI Registration | Created AppSettingService Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating AppSettingService Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return appConfig;
                });

                services.AddSingleton<TokenCredential>(sp =>
                {
                    TokenCredential credential = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating TokenCredential Instance");
#if DEBUG
                        credential = new VisualStudioCredential();
#else
                        var appSettingService = sp.GetRequiredService<IAppSettingService>();
                        var config = appSettingService.Configuration;
                        credential = new ManagedIdentityCredential();
#endif
                        logger.AppLogInformation("Program | DI Registration | Created TokenCredential Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating TokenCredential Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return credential;
                });

                services.AddSingleton<IAzureSearchProvider>(sp =>
                {
                    AzureSearchProvider searchProvider = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating AzureSearchProvider Instance");
                        var appSettingService = sp.GetRequiredService<IAppSettingService>();
                        var config = appSettingService.Configuration;
                        var searchLogger = sp.GetRequiredService<ILogger<AzureSearchProvider>>();
                        var searchConfig = new AzureSearchConnectionConfig(
                            config[AppSettingConstants.Configuration_SearchServiceEndpoint],
                            config[AppSettingConstants.Configuration_SearchIndexName]);//,
                            //config[AppSettingConstants.Configuration_MuiClientId]);
                        searchProvider = new AzureSearchProvider(searchLogger, searchConfig);
                        logger.AppLogInformation("Program | DI Registration | Created AzureSearchProvider Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating AzureSearchProvider Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return searchProvider;
                });

                services.AddSingleton<ITreeConfigurationStoreProvider>(sp =>
                {
                    TreeConfigurationStoreProvider configStoreProvider = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating TreeConfigurationStoreProvider Instance");
                        var appSettingService = sp.GetRequiredService<IAppSettingService>();
                        var config = appSettingService.Configuration;
                        var searchProvider = sp.GetRequiredService<IAzureSearchProvider>();
                        var openAIEndpoint = new Uri(config[AppSettingConstants.Configuration_AzureOpenAIEndpoint]);
                        var embeddingDeployment = config[AppSettingConstants.Configuration_EmbeddingDeployment];
                        var cred = sp.GetRequiredService<TokenCredential>();
                        configStoreProvider = new TreeConfigurationStoreProvider(searchProvider, openAIEndpoint, embeddingDeployment, cred);
                        logger.AppLogInformation("Program | DI Registration | Created TreeConfigurationStoreProvider Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating TreeConfigurationStoreProvider Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return configStoreProvider;
                });

                services.AddSingleton<IKustoProvider>(sp =>
                {
                    KustoProvider kustoProvider = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating KustoProvider Instance");
                        var appSettingService = sp.GetRequiredService<IAppSettingService>();
                        var config = appSettingService.Configuration;
                        var kustoDbConfig = JsonConvert.DeserializeObject<KustoDbConfig>(config[AppSettingConstants.Configuration_KustoDbConfig])!;
                        kustoProvider = new KustoProvider(kustoDbConfig);
                        logger.AppLogInformation("Program | DI Registration | Created KustoProvider Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating KustoProvider Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return kustoProvider;
                });

                services.AddKeyedSingleton<ICosmosDbProvider>("MemoryStore", (sp, _) =>
                {
                    CosmosDbProvider cosmosDbProvider = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating CosmosDbProvider for MemoryStore Instance");
                        var appSettingService = sp.GetRequiredService<IAppSettingService>();
                        var config = appSettingService.Configuration;
                        var allStores = JsonConvert.DeserializeObject<Dictionary<string, CosmosDbConfig>>(config[AppSettingConstants.Configuration_CosmosDbsMaps])!;
                        cosmosDbProvider = new CosmosDbProvider(allStores["MemoryStore"]);
                        logger.AppLogInformation("Program | DI Registration | Created CosmosDbProvider for MemoryStore Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating CosmosDbProvider for MemoryStore Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return cosmosDbProvider;
                });

                services.AddKeyedSingleton<ICosmosDbProvider>("ConversationStore", (sp, _) =>
                {
                    CosmosDbProvider cosmosDbProvider = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating CosmosDbProvider for ConversationStore Instance");
                        var appSettingService = sp.GetRequiredService<IAppSettingService>();
                        var config = appSettingService.Configuration;
                        var allStores = JsonConvert.DeserializeObject<Dictionary<string, CosmosDbConfig>>(config[AppSettingConstants.Configuration_CosmosDbsMaps])!;
                        cosmosDbProvider = new CosmosDbProvider(allStores["ConversationStore"]);
                        logger.AppLogInformation("Program | DI Registration | Created CosmosDbProvider for ConversationStore Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating CosmosDbProvider for ConversationStore Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return cosmosDbProvider;
                });

                services.AddSingleton<IConversationStore>(sp =>
                {
                    CosmosConversationStore conversationStore = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating ConversationStore Instance");
                        var cosmosProvider = sp.GetRequiredKeyedService<ICosmosDbProvider>("ConversationStore");
                        var storeLogger = sp.GetRequiredService<ILogger<CosmosConversationStore>>();
                        var storeMetrics = sp.GetService<IMetricsProvider>();
                        conversationStore = new CosmosConversationStore(cosmosProvider, storeLogger, storeMetrics);
                        logger.AppLogInformation("Program | DI Registration | Created ConversationStore Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating ConversationStore Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return conversationStore;
                });

                services.AddSingleton<IMemoryStore>(sp =>
                {
                    CosmosMemoryStore memoryStore = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating MemoryStore Instance");
                        var appSettingService = sp.GetRequiredService<IAppSettingService>();
                        var config = appSettingService.Configuration;
                        var cosmosProvider = sp.GetRequiredKeyedService<ICosmosDbProvider>("MemoryStore");
                        var openAIEndpoint = config[AppSettingConstants.Configuration_AzureOpenAIEndpoint];
                        var cred = sp.GetRequiredService<TokenCredential>();
                        var embeddingDeployment = config[AppSettingConstants.Configuration_EmbeddingDeployment];
                        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                        memoryStore = new CosmosMemoryStore(cosmosProvider, openAIEndpoint, cred, loggerFactory, embeddingDeployment, metricsProvider: sp.GetService<IMetricsProvider>());
                        logger.AppLogInformation("Program | DI Registration | Created MemoryStore Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating MemoryStore Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return memoryStore;
                });

 
                services.AddSingleton<ArtifactStore>();


                // ── Artifact blob store (durable artifact storage) ──
                services.AddSingleton<IArtifactStore>(sp =>
                {
                    ArtifactBlobStore artifactBlobStore = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating ArtifactBlobStore Instance");
                        var appSettingService = sp.GetRequiredService<IAppSettingService>();
                        var config = appSettingService.Configuration;
                        var artifactBlobEndpoint = config["ArtifactBlobEndpoint"];
                        var artifactBlobContainer = config["ArtifactBlobContainerName"];
                        if (!string.IsNullOrEmpty(artifactBlobEndpoint) && !string.IsNullOrEmpty(artifactBlobContainer))
                        {
                            artifactBlobStore = new ArtifactBlobStore(
                                new Uri(artifactBlobEndpoint),
                                artifactBlobContainer,
                                sp.GetRequiredService<ILogger<ArtifactBlobStore>>());
                        }
                        logger.AppLogInformation("Program | DI Registration | Created ArtifactBlobStore Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating ArtifactBlobStore Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return artifactBlobStore;
                });

                //services.AddSignalRStatusNotifier(signalRConnStr);

                // ── Status notifier: SignalR or Console ──
                // Controlled by "UseSignalR" in local.settings.json / Azure App Settings.
                // Backend devs (Postman): UseSignalR=false | UI devs (browser): UseSignalR=true
                var useSignalR = !string.Equals(
                    Environment.GetEnvironmentVariable("UseSignalR"), "false",
                    StringComparison.OrdinalIgnoreCase);

                if (!useSignalR)
                {
                    services.AddSingleton<IToolStatusNotifier, ConsoleToolStatusNotifier>();
                }
                else
                {
                    services.AddSingleton(sp =>
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating SignalR Connection");
                        var credential = sp.GetRequiredService<TokenCredential>();
                        var appSettingService = sp.GetRequiredService<IAppSettingService>();
                        var config = appSettingService.Configuration;
                        var signalRConnStr = config[AppSettingConstants.Configuration_SignarRConnectionKey];
                        var serviceEndpoint = new ServiceEndpoint(new Uri(signalRConnStr), credential);

                        var serviceManager = new ServiceManagerBuilder()
                            .WithOptions(o =>
                            {
                                o.ServiceEndpoints = new ServiceEndpoint[]
                                {
                                    serviceEndpoint
                                };
                            })
                            .BuildServiceManager();
                        logger.AppLogInformation("Program | DI Registration | Created SignalR Connection");

                        return serviceManager.CreateHubContextAsync("orchestrator", default)
                            .GetAwaiter().GetResult();
                    });
                    services.AddSingleton<IToolStatusNotifier>(sp =>
                        new SignalRToolStatusNotifier(
                            sp.GetRequiredService<ServiceHubContext>(),
                            sp.GetRequiredService<ILogger<SignalRToolStatusNotifier>>()));
                }

                services.AddTransient<AspectTools>(sp =>
                {
                    return new AspectTools(
                        sp.GetRequiredService<ILogger<AspectTools>>(),
                        sp.GetRequiredService<ITreeConfigurationStoreProvider>(),
                        sp.GetRequiredService<IUserAuthContext>(),
                        sp.GetRequiredService<IToolStatusNotifier>());
                });
                services.AddTransient<ReportingTools>(sp =>
                    new ReportingTools(
                        sp.GetRequiredService<ILogger<ReportingTools>>(),
                        sp.GetRequiredService<ITreeConfigurationStoreProvider>(),
                        sp.GetRequiredService<IUserAuthContext>(),
                        sp.GetRequiredService<IToolStatusNotifier>(),
                        sp.GetService<IArtifactStore>()));
                services.AddTransient<NLTKqlTools>();
                services.AddTransient<UXGeneratorTool>(sp =>
                    new UXGeneratorTool(
                        sp.GetRequiredService<ILogger<UXGeneratorTool>>(),
                        sp.GetRequiredService<IToolStatusNotifier>()));
                services.AddHttpClient("ExternalAgentDelegator", (sp, client) =>
                {
                    client.Timeout = TimeSpan.FromMinutes(6);
                });
                services.AddTransient<Dictionary<string, object>>(sp =>
                {
                    var tools = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["AspectTools"] = sp.GetRequiredService<AspectTools>(),
                    ["ReportingTool"] = sp.GetRequiredService<ReportingTools>(),
                    ["NLTKqlTools"] = sp.GetRequiredService<NLTKqlTools>(),
                        ["UXGeneratorTool"] = sp.GetRequiredService<UXGeneratorTool>()
                    };

                    // Register ExternalAgentDelegator for external agent delegation (e.g., Managed Review Agent)
                    // Base URL loaded from config store (ToolConfiguration / ExternalAgentDelegator-EnvironmentSettings)
                    tools["ExternalAgentDelegator"] = new ExternalAgentDelegator(
                        sp.GetRequiredService<IHttpClientFactory>().CreateClient("ExternalAgentDelegator"),
                        sp.GetRequiredService<IUserAuthContext>(),
                        sp.GetRequiredService<IToolStatusNotifier>(),
                        sp.GetRequiredService<TokenCredential>(),
                        sp.GetRequiredService<ITreeConfigurationStoreProvider>(),
                        sp.GetRequiredService<ILoggerFactory>().CreateLogger<ExternalAgentDelegator>());

                    return tools;
                });

                // Register KnowledgeGraphTools as Singleton — it reads a static JSON file
                // and should NOT be recreated per request (causes static field accumulation)
                services.AddSingleton<KnowledgeGraphTools>();

                services.AddTransient<IOrchestratorStepService>(sp =>
                {
                    OrchestratorStepService orchestratorStepService = default!;
                    try
                    {
                        logger.AppLogInformation("Program | DI Registration | Creating OrchestratorStepService Instance");
                        var appSettingService = sp.GetRequiredService<IAppSettingService>();
                        var config = appSettingService.Configuration;
                        var configStore = sp.GetRequiredService<ITreeConfigurationStoreProvider>();
                        var conversationStore = sp.GetRequiredService<IConversationStore>();
                        var toolInstances = sp.GetRequiredService<Dictionary<string, object>>();
                        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                        var memStore = sp.GetService<IMemoryStore>();
                        var cred = sp.GetRequiredService<TokenCredential>();
                        var knowledgeGraph = sp.GetRequiredService<KnowledgeGraphTools>();
                        orchestratorStepService = new OrchestratorStepService(
                            configStore, conversationStore, toolInstances, loggerFactory,
                            openAIEndpoint: config[AppSettingConstants.Configuration_AzureOpenAIEndpoint],
                            primaryModelName: config[AppSettingConstants.Configuration_AzureOpenAIModelName],
                            secondaryModelName: config[AppSettingConstants.Configuration_AzureOpenAIModelNameV2],
                            credential: cred,
                            memoryStore: memStore,
                            knowledgeLookup: knowledgeGraph.GetSystemKnowledgeAsync,
                            metricsProvider: sp.GetService<IMetricsProvider>());
                        logger.AppLogInformation("Program | DI Registration | Created OrchestratorStepService Instance");
                    }
                    catch (Exception ex)
                    {
                        logger.AppLogError($"Program | DI Registration | Creating OrchestratorStepService Instance Failed with Exception:{ex.StackTrace}");
                    }
                    return orchestratorStepService;
                });

            })
            .Build();

            host.Run();
        }

        private static void AddOpenTelemetryLogging(IServiceCollection services, IConfiguration Configuration)
        {
            var settings = new OpenTelemetrySettingsConfiguration();
            Configuration.Bind(settings);

            string assemblyVersion = AddOpenTelemetryExtensions.GetInfoVersion<Program>();
            var otelSettings = settings.OpenTelemetrySettings;
            otelSettings.GenevaInstrumentation.Region = Environment.GetEnvironmentVariable(AppSettingConstants.AppLocationName)!;
            AddOpenTelemetryExtensions.ConfigureLoggingTelemetry(services, otelSettings, assemblyVersion, addConsoleExporter: false);
        }
    }
}
