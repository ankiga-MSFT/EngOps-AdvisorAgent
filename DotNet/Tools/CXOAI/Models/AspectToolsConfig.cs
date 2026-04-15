using Newtonsoft.Json;

namespace CXOAI.Tools.Models;

/// <summary>
/// Root configuration model for AspectTools.
/// Maps to the JSON configuration containing API endpoints, CosmosDB, Kusto, and Aspect domain settings.
/// </summary>
public class AspectToolsConfig
{
    [JsonProperty("metadataBaseApiUrl")]
    public string MetadataBaseApiUrl { get; set; } = string.Empty;

    [JsonProperty("cxobserveBaseUrl")]
    public string CxObserveBaseUrl { get; set; } = string.Empty;

    [JsonProperty("keyVaultUrl")]
    public string KeyVaultUrl { get; set; } = string.Empty;

    [JsonProperty("cosmosDbsMaps")]
    public Dictionary<string, AspectCosmosDbConfig> CosmosDbsMaps { get; set; } = new();

    [JsonProperty("aspectsDetailsMap")]
    public Dictionary<string, AspectApiConfig> AspectsDetailsMap { get; set; } = new();

    [JsonProperty("kustoConnectionMaps")]
    public Dictionary<string, AspectKustoDbConfig> KustoConnectionMaps { get; set; } = new();
}

public class AspectCosmosDbConfig
{
    [JsonProperty("databaseId")]
    public string DatabaseId { get; set; } = string.Empty;

    [JsonProperty("containerId")]
    public string ContainerId { get; set; } = string.Empty;

    [JsonProperty("leaseDatabaseId")]
    public string LeaseDatabaseId { get; set; } = string.Empty;

    [JsonProperty("leaseContainerId")]
    public string LeaseContainerId { get; set; } = string.Empty;

    [JsonProperty("accountEndpoint")]
    public string AccountEndpoint { get; set; } = string.Empty;
}

public class AspectKustoDbConfig
{
    [JsonProperty("kustoClusterUrl")]
    public string KustoClusterUrl { get; set; } = string.Empty;

    [JsonProperty("kustoDatabaseName")]
    public string KustoDatabaseName { get; set; } = string.Empty;

    [JsonProperty("credentialConfig")]
    public CredentialConfig? CredentialConfig { get; set; }
}

public class CredentialConfig
{
    [JsonProperty("muiClientId")]
    public string? MuiClientId { get; set; }

    [JsonProperty("appClientId")]
    public string? AppClientId { get; set; }

    [JsonProperty("tenantId")]
    public string? TenantId { get; set; }
}

/// <summary>
/// Per-domain configuration for calling the Insights API (e.g., Support, Customer, Product).
/// </summary>
public class AspectApiConfig
{
    [JsonProperty("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonProperty("tokenAcquisitionConfig")]
    public TokenAcquisitionConfig? TokenAcquisitionConfig { get; set; }
}

/// <summary>
/// Configuration for acquiring tokens via the On-Behalf-Of (OBO) flow with certificate assertion.
/// </summary>
public class TokenAcquisitionConfig
{
    [JsonProperty("appClientId")]
    public string AppClientId { get; set; } = string.Empty;

    [JsonProperty("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonProperty("scopes")]
    public List<string> Scopes { get; set; } = new();

    [JsonProperty("clientCertificateName")]
    public string ClientCertificateName { get; set; } = string.Empty;

    [JsonIgnore]
    public System.Security.Cryptography.X509Certificates.X509Certificate2? Certificate { get; set; }
}
