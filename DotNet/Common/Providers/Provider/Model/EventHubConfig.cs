using Newtonsoft.Json;

namespace Provider.Model
{
#pragma warning disable CS8618 
    public class EventHubConfig
    {
        [JsonProperty("eventHubName")]
        public string EventHubName { get; set; }

        [JsonProperty("eventHubNamespace")]
        public string EventHubNamespace { get; set; }
        
        [JsonProperty("credentialConfig")]
        public CredentialConfig CredentialConfig { get; set; }
    }
}
