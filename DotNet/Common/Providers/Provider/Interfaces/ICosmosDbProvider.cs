using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;

namespace Provider.Interfaces
{
    public interface ICosmosDbProvider
    {
        Container Container { get; }
        Task DeleteDocumentAsync(string id,string partitionKeyValue);
        Task DeleteDocumentsBatchAsync(Dictionary<string, List<string>> documentIds, int batchSize);
        Task ExecuteStoredProcedureAsync(string storedProcedureName, params dynamic[] parameters);
        Task InsertDocumentAsync(JObject document);
        Task InsertDocumentsBatchAsync(IEnumerable<JObject> documents, string partitionKey, int batchSize);

        Task<Dictionary<string,JObject>> GetDocumentsBatchAsync(Dictionary<string, List<string>> ids, string partitionPath, int batchSize);
        Task<JObject> GetDocumentsAsync(string id,string partionkeyValue);

        Task ReadChangeFeedAsync(Action<JObject> onChangeFeedReceived, string instanceName);
        Task StartChangeFeedProcessorAsync(string instanceName, Action<JObject> onChangeFeedReceived);
        Task StopChangeFeedProcessorAsync(string instanceName);
        Task UpdateDocumentAsync(string id, JObject document);
        Task UpdateDocumentAsync(string id, string partitionKeyValue, JObject document);
        Task UpdateDocumentsBatchAsync(IEnumerable<JObject> documents, string partitionKey, int batchSize);
        Task<List<JObject>> QueryItemsAsync(string queryString);
    }
}