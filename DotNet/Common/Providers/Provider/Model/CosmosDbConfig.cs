using Newtonsoft.Json;

namespace Provider.Model
{
#pragma warning disable CS8618
    public class CosmosDbConfig
    {
        [JsonProperty("databaseId")]
        public string DatabaseId { get; set; }
        [JsonProperty("containerId")]
        public string ContainerId { get; set; }
        [JsonProperty("leaseDatabaseId")]
        public string LeaseDatabaseId { get; set; }
        [JsonProperty("leaseContainerId")]
        public string LeaseContainerId { get; set; }
        [JsonProperty("accountEndpoint")]
        public string AccountEndpoint { get; set; }
    }
}
