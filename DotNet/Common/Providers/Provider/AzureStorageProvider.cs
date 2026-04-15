using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Data.Tables;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.DataLake;
using Provider.Interfaces;
using Azure.Core;
using System.Text;
using Newtonsoft.Json.Linq;
using Azure.Storage.Queues.Models;

namespace Provider
{
    public class AzureStorageProvider : IAzureStorageProvider
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly QueueServiceClient _queueServiceClient;
        private readonly TableServiceClient _tableServiceClient;
        private readonly ShareServiceClient _fileServiceClient;
        private readonly DataLakeServiceClient _dataLakeServiceClient;

        

        public AzureStorageProvider(string storageAccountName)
        {
#if DEBUG
            var credential = new DefaultAzureCredential();
#else
            var credential = new ManagedIdentityCredential();
#endif
            var baseUri = $"https://{storageAccountName}.core.windows.net";

            _blobServiceClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), credential);
            _queueServiceClient = new QueueServiceClient(new Uri($"https://{storageAccountName}.queue.core.windows.net"), credential);
            _tableServiceClient = new TableServiceClient(new Uri($"https://{storageAccountName}.table.core.windows.net"), credential);
            _fileServiceClient = new ShareServiceClient(new Uri($"https://{storageAccountName}.file.core.windows.net"), credential);
            _dataLakeServiceClient = new DataLakeServiceClient(new Uri($"https://{storageAccountName}.dfs.core.windows.net"), credential);
        }


        // Single Blob operations
        public async Task UploadBlobAsync(string containerName, string blobName, string content)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(new BinaryData(content));
        }

        public async Task<string> DownloadBlobAsync(string containerName, string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            var downloadInfo = await blobClient.DownloadContentAsync();
            var result= downloadInfo.Value.Content.ToString();
            if(result.StartsWith("\uFEFF"))  // Remove BOM
                result =result.Replace("\uFEFF", string.Empty);
            return result;
        }

        // Batch Blob operations
        public async Task UploadBlobsAsync(string containerName, Dictionary<string, string> blobs, int batchSize)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            foreach (var batch in blobs.Select((v, i) => new { v, i }).GroupBy(x => x.i / batchSize, x => x.v))
            {
                foreach (var blob in batch)
                {
                    var blobClient = containerClient.GetBlobClient(blob.Key);
                    await blobClient.UploadAsync(new BinaryData(blob.Value));
                }
            }
        }

        // Single Queue operations
        public async Task EnqueueMessageAsync(string queueName, string message,TimeSpan delay)
        {
            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            await queueClient.CreateIfNotExistsAsync();
            var retryOptions = new QueueClientOptions
            {
                Retry =  {
                            MaxRetries = 5,
                            Mode = RetryMode.Fixed,
                            Delay = TimeSpan.FromSeconds(2)
                        }
            };
#if DEBUG
            var credential = new DefaultAzureCredential();
#else
            var credential = new ManagedIdentityCredential();
#endif
            var retryQueueClient = new QueueClient(queueClient.Uri, credential, retryOptions);
            var bytes = Encoding.UTF8.GetBytes(message);
            await retryQueueClient.SendMessageAsync(Convert.ToBase64String(bytes), delay);
        }

        public async Task<JObject?> DequeueMessageAsync(string queueName)
        {
            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            QueueMessage? message = await queueClient.ReceiveMessageAsync();

            if (message == null)
            {
                return null;
            }

            var base64EncodedMessage = message.MessageText;
            var bytes = Convert.FromBase64String(base64EncodedMessage);
            var messageText = Encoding.UTF8.GetString(bytes);

            var jsonObject = new JObject
            {
                { "MessageText", messageText },
                { "MessageId", message.MessageId },
                { "PopReceipt", message.PopReceipt }
            };

            return jsonObject;
        }

        public async Task DeleteMessageAsync(string queueName, string messageId, string popReceipt)
        {
            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            await queueClient.DeleteMessageAsync(messageId, popReceipt);
        }

        // Batch Queue operations
        public async Task EnqueueMessagesAsync(string queueName, IEnumerable<string> messages, int batchSize, TimeSpan delay)
        {
            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            await queueClient.CreateIfNotExistsAsync();

            var retryOptions = new QueueClientOptions
            {
                Retry =  {
                            MaxRetries = 5,
                            Mode = RetryMode.Fixed,
                            Delay = TimeSpan.FromSeconds(2)
                        }
            };
#if DEBUG
            var credential = new DefaultAzureCredential();
#else
            var credential = new ManagedIdentityCredential();
#endif
            var retryQueueClient = new QueueClient(queueClient.Uri, credential, retryOptions);


            foreach (var batch in messages.Select((v, i) => new { v, i }).GroupBy(x => x.i / batchSize, x => x.v))
            {
                foreach (var message in batch)
                {
                    var bytes = Encoding.UTF8.GetBytes(message);
                    await retryQueueClient.SendMessageAsync(Convert.ToBase64String(bytes), delay);
                }
            }
        }

        // Single Table operations
        public async Task InsertOrMergeEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity, new()
        {
            var tableClient = _tableServiceClient.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();
            await tableClient.UpsertEntityAsync(entity);
        }

        public async Task<T> RetrieveEntityAsync<T>(string tableName, string partitionKey, string rowKey) where T : class, ITableEntity, new()
        {
            var tableClient = _tableServiceClient.GetTableClient(tableName);
            var entity = await tableClient.GetEntityAsync<T>(partitionKey, rowKey);
            return entity.Value;
        }

        // Batch Table operations
        public async Task InsertOrMergeEntitiesAsync<T>(string tableName, IEnumerable<T> entities, int batchSize) where T : class, ITableEntity, new()
        {
            var tableClient = _tableServiceClient.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();

            foreach (var batch in entities.Select((v, i) => new { v, i }).GroupBy(x => x.i / batchSize, x => x.v))
            {
                var transactionActions = new List<TableTransactionAction>();
                foreach (var entity in batch)
                {
                    transactionActions.Add(new TableTransactionAction(TableTransactionActionType.UpsertMerge, entity));
                }
                await tableClient.SubmitTransactionAsync(transactionActions);
            }
        }

        // Single File operations
        public async Task UploadFileAsync(string shareName, string fileName, string content)
        {
            var shareClient = _fileServiceClient.GetShareClient(shareName);
            await shareClient.CreateIfNotExistsAsync();
            var fileClient = shareClient.GetRootDirectoryClient().GetFileClient(fileName);
            await fileClient.CreateAsync(content.Length);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            await fileClient.UploadRangeAsync(new Azure.HttpRange(0, content.Length), stream);
        }

        public async Task<string> DownloadFileAsync(string shareName, string fileName)
        {
            var shareClient = _fileServiceClient.GetShareClient(shareName);
            var fileClient = shareClient.GetRootDirectoryClient().GetFileClient(fileName);
            var downloadInfo = await fileClient.DownloadAsync();
            using var streamReader = new StreamReader(downloadInfo.Value.Content);
            return await streamReader.ReadToEndAsync();
        }

        // Batch File operations
        public async Task UploadFilesAsync(string shareName, Dictionary<string, string> files, int batchSize)
        {
            var shareClient = _fileServiceClient.GetShareClient(shareName);
            await shareClient.CreateIfNotExistsAsync();

            foreach (var batch in files.Select((v, i) => new { v, i }).GroupBy(x => x.i / batchSize, x => x.v))
            {
                foreach (var file in batch)
                {
                    var fileClient = shareClient.GetRootDirectoryClient().GetFileClient(file.Key);
                    await fileClient.CreateAsync(file.Value.Length);
                    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(file.Value));
                    await fileClient.UploadRangeAsync(new Azure.HttpRange(0, file.Value.Length), stream);
                }
            }
        }


        // Single Data Lake operations
        public async Task UploadDataLakeFileAsync(string fileSystemName, string fileName, string content)
        {
            var fileSystemClient = _dataLakeServiceClient.GetFileSystemClient(fileSystemName);
            await fileSystemClient.CreateIfNotExistsAsync();
            var fileClient = fileSystemClient.GetFileClient(fileName);
            await fileClient.CreateAsync();
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            await fileClient.AppendAsync(stream, offset: 0);
            await fileClient.FlushAsync(content.Length);
        }

        public async Task<string> DownloadDataLakeFileAsync(string fileSystemName, string fileName)
        {
            var fileSystemClient = _dataLakeServiceClient.GetFileSystemClient(fileSystemName);
            var fileClient = fileSystemClient.GetFileClient(fileName);
            var downloadInfo = await fileClient.ReadAsync();
            using var streamReader = new StreamReader(downloadInfo.Value.Content);
            return await streamReader.ReadToEndAsync();
        }

        // Batch Data Lake operations
        public async Task UploadDataLakeFilesAsync(string fileSystemName, Dictionary<string, string> files, int batchSize)
        {
            var fileSystemClient = _dataLakeServiceClient.GetFileSystemClient(fileSystemName);
            await fileSystemClient.CreateIfNotExistsAsync();

            foreach (var batch in files.Select((v, i) => new { v, i }).GroupBy(x => x.i / batchSize, x => x.v))
            {
                foreach (var file in batch)
                {
                    var fileClient = fileSystemClient.GetFileClient(file.Key);
                    await fileClient.CreateAsync();
                    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(file.Value));
                    await fileClient.AppendAsync(stream, offset: 0);
                    await fileClient.FlushAsync(file.Value.Length);
                }
            }
        }
    }
}
