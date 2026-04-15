using Newtonsoft.Json;

namespace Provider.Model
{
#pragma warning disable CS8618
    public class KustoDbConfig
    {
        [JsonProperty("kustoDatabaseName")]
        public string KustoDatabaseName { get; set; }
        [JsonProperty("kustoClusterUrl")]
        public string KustoClusterUrl { get; set; }
        [JsonProperty("credentialConfig")]
        public CredentialConfig CredentialConfig { get; set; }
    }
}
