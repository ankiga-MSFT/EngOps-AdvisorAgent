using Azure.Data.Tables;
using Newtonsoft.Json.Linq;

namespace Provider.Interfaces
{
    public interface IAzureStorageProvider
    {
        Task<JObject?> DequeueMessageAsync(string queueName);
        Task<string> DownloadBlobAsync(string containerName, string blobName);
        Task<string> DownloadDataLakeFileAsync(string fileSystemName, string fileName);
        Task<string> DownloadFileAsync(string shareName, string fileName);
        Task EnqueueMessageAsync(string queueName, string message, TimeSpan delay);
        Task EnqueueMessagesAsync(string queueName, IEnumerable<string> messages, int batchSize, TimeSpan delay);
        Task InsertOrMergeEntitiesAsync<T>(string tableName, IEnumerable<T> entities, int batchSize) where T : class, ITableEntity, new();
        Task InsertOrMergeEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity, new();
        Task<T> RetrieveEntityAsync<T>(string tableName, string partitionKey, string rowKey) where T : class, ITableEntity, new();
        Task UploadBlobAsync(string containerName, string blobName, string content);
        Task UploadBlobsAsync(string containerName, Dictionary<string, string> blobs, int batchSize);
        Task UploadDataLakeFileAsync(string fileSystemName, string fileName, string content);
        Task UploadDataLakeFilesAsync(string fileSystemName, Dictionary<string, string> files, int batchSize);
        Task UploadFileAsync(string shareName, string fileName, string content);
        Task UploadFilesAsync(string shareName, Dictionary<string, string> files, int batchSize);
        Task DeleteMessageAsync(string queueName, string messageId, string popReceipt);
    }
}