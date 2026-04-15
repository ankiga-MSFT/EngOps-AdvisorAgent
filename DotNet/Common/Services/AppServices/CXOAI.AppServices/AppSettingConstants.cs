namespace CXOAI.AppServices;

public static class AppSettingConstants
{
    // Environment & Folder
    public const string EnvironmentSettingsFolderName = "EnvironmentSettings";
    public const string DefaultContainerName = "deploymentpackages";
    public const string EnvironmentVariableName = "AppEnvironmentName";
    public const string AppStorageAccountNameKey = "AppStorageAccountName";
    public const string AppLocationName = "AppLocationName";

    // Service Identity
    public const string ServiceTreeId = "ServiceTreeId";

    // Auth & Token Validation
    public const string Configuration_TokenValidationConfiguration = "tokenValidationConfiguration";

    // Azure OpenAI
    public const string Configuration_AzureOpenAIEndpoint = "AzureOpenAIEndpoint";
    public const string Configuration_AzureOpenAIModelName = "AzureOpenAIModelName";
    public const string Configuration_AzureOpenAIModelNameV2 = "AzureOpenAIModelNameV2";
    public const string Configuration_EmbeddingDeployment = "EmbeddingDeployment";
    public const string Configuration_SignarRConnectionKey = "AzureSignalRConnectionString";


    // Azure Search
    public const string Configuration_SearchServiceEndpoint = "SearchServiceEndpoint";
    public const string Configuration_SearchIndexName = "SearchIndexName";
    public const string Configuration_MuiClientId = "muiClientId";

    // Storage

    // Cosmos DB
    public const string Configuration_CosmosDbsMaps = "cosmosDbsMaps";

    // Kusto
    public const string Configuration_KustoDbConfig = "kustoDbConfig";
}
