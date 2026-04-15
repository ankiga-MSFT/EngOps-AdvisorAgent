using Newtonsoft.Json.Linq;

namespace Provider.Interfaces
{
    public interface IAzureEventHubProvider
    {
        Task ReceiveMessagesAsync(Func<List<JObject>, Task> processMessagesAsync, int batchSize, int waitTimeInSeconds);
        Task SendMessagesAsync(List<JObject> messages, Func<JObject, string> partitionKeySelector = null!);
    }
}