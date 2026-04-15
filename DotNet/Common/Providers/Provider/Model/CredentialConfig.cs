using Newtonsoft.Json;

namespace Provider.Model
{
    public class CredentialConfig
    {
        [JsonProperty("muiClientId")]
        public string? MuiClientId { get; set; }

        [JsonProperty("appClientId")]
        public string? AppClientId { get; set; }

        [JsonProperty("tenantId")]
        public string? TenantId { get; set; }
    }
}
