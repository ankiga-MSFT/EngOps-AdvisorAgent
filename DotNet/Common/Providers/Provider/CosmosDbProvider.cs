using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Provider.Interfaces;
using Provider.Model;

namespace Provider
{


    public class CosmosDbProvider : ICosmosDbProvider
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private readonly Container _leaseContainer;

        public Container Container => _container;

        private static CosmosClientOptions CreateDefaultOptions() => new()
        {
            ConnectionMode = ConnectionMode.Gateway,
            MaxRetryAttemptsOnRateLimitedRequests = 9,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30),
            Serializer = new CosmosJsonNetSerializer(new JsonSerializerSettings
            {
                Converters = { new StringEnumConverter() },
                NullValueHandling = NullValueHandling.Ignore
            })
        };

        public CosmosDbProvider(string databaseId, string containerId, string leaseDatabaseId, string leaseContainerId, string accountEndpoint)
        {
            var options = CreateDefaultOptions();

#if DEBUG
            var credential = new DefaultAzureCredential();
#else
            var credential = new ManagedIdentityCredential();
#endif
            _cosmosClient = new CosmosClient(accountEndpoint, credential, options);
            _container = _cosmosClient.GetContainer(databaseId, containerId);
            _leaseContainer = _cosmosClient.GetContainer(leaseDatabaseId, leaseContainerId);
        }

        public CosmosDbProvider(CosmosDbConfig config)
        {
            var options = CreateDefaultOptions();

#if DEBUG
            var credential = new DefaultAzureCredential();
#else
            var credential = new ManagedIdentityCredential();
#endif
            _cosmosClient = new CosmosClient(config.AccountEndpoint, credential, options);
            _container = _cosmosClient.GetContainer(config.DatabaseId, config.ContainerId);
            _leaseContainer = _cosmosClient.GetContainer(config.LeaseDatabaseId, config.LeaseContainerId);
        }

        private IEnumerable<List<T>> GetBatches<T>(IEnumerable<T> items, int batchSize)
        {
            var batches = new List<List<T>>();
            var currentBatch = new List<T>();

            foreach (var item in items)
            {
                currentBatch.Add(item);
                if (currentBatch.Count >= batchSize)
                {
                    batches.Add(new List<T>(currentBatch));
                    currentBatch.Clear();
                }
            }

            if (currentBatch.Any())
            {
                batches.Add(currentBatch);
            }

            return batches;
        }

        public async Task InsertDocumentsBatchAsync(IEnumerable<JObject> documents, string partitionKey, int batchSize)
        {
            // Group documents by partition key value
            var groupedDocuments = documents.GroupBy(doc => doc.SelectToken(partitionKey)!.ToString());

            // Process each group separately
            foreach (var group in groupedDocuments)
            {
                var partitionKeyValue = group.Key;
                var batches = GetBatches(group, batchSize);

                foreach (var batch in batches)
                {
                    var transactionBatch = _container.CreateTransactionalBatch(new PartitionKey(partitionKeyValue));
                    foreach (var doc in batch)
                    {
                        transactionBatch.CreateItem(doc);
                    }
                    var response = await transactionBatch.ExecuteAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        var statusCode = response.StatusCode;
                        var diagnostics = response.Diagnostics.ToString();
                        var errorMessages = string.Join(", ", response.Select(r => r.StatusCode.ToString()));
                        throw new Exception($"Failed to execute batch operation. Response: statusCode:{statusCode},AggregateStatusCodes:{errorMessages}, Diagnostics:{diagnostics}");
                    }
                }
            }
        }


        public async Task UpdateDocumentsBatchAsync(IEnumerable<JObject> documents, string partitionKey, int batchSize)
        {
            // Group documents by partition key value
            var groupedDocuments = documents.GroupBy(doc => doc.SelectToken(partitionKey)!.ToString());
             List<Exception> exceptions = new List<Exception>();
            // Process each group separately
            foreach (var group in groupedDocuments)
            {
                var partitionKeyValue = group.Key;
                if (partitionKeyValue == string.Empty)
                    continue;
                var batches = GetBatches(group, batchSize);

                foreach (var batch in batches)
                {
                    var transactionBatch = _container.CreateTransactionalBatch(new PartitionKey(partitionKeyValue));
                    foreach (var doc in batch)
                    {
                        transactionBatch.UpsertItem(doc);
                    }
                    var response = await transactionBatch.ExecuteAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        var statusCode = response.StatusCode; 
                        var diagnostics = response.Diagnostics.ToString();
                        var errorMessages = string.Join(", ", response.Select(r => r.StatusCode.ToString()));
                        exceptions.Add(new Exception($"Failed to execute batch operation. Response: statusCode:{statusCode},AggregateStatusCodes:{errorMessages}, Diagnostics:{diagnostics}"));
                    }
                }
            }
            if(exceptions.Any())
            {
                // Aggregate all exceptions into a single exception
                var aggregatedException = new AggregateException("One or more errors occurred during batch update.", exceptions);
                throw aggregatedException;
            }
        }


        public async Task DeleteDocumentsBatchAsync(Dictionary<string,List<string>> documentIds, int batchSize)
        {
            foreach (var kv in documentIds)
            {
                var batches = GetBatches(kv.Value, batchSize);

                foreach (var batch in batches)
                {
                    var transactionBatch = _container.CreateTransactionalBatch(new PartitionKey(kv.Key));
                    foreach (var id in batch)
                    {
                        transactionBatch.DeleteItem(id);
                    }
                    var response = await transactionBatch.ExecuteAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        var statusCodes = new List<string>();
                        foreach (var operationResult in response)
                        {
                            if (operationResult.StatusCode == System.Net.HttpStatusCode.NotFound)
                                continue;
                            else if (!operationResult.IsSuccessStatusCode)
                            {
                                statusCodes.Add(operationResult.StatusCode.ToString());
                            }
                        }
                        if (statusCodes.Any())
                        {
                            var diagnostics = response.Diagnostics.ToString();
                            var errorMessages = string.Join(", ", statusCodes);
                            throw new Exception($"Failed to execute batch operation. Response: AggregateStatusCodes:{errorMessages}, Diagnostics:{diagnostics}");
                        }
                    }
                }
            }
        }

        public async Task InsertDocumentAsync(JObject document)
        {
            await _container.CreateItemAsync(document);
        }

        public async Task UpdateDocumentAsync(string id, JObject document)
        {
            await _container.UpsertItemAsync(document, new PartitionKey(id));
        }

        public async Task UpdateDocumentAsync(string id, string partitionKeyValue, JObject document)
        {
            await _container.UpsertItemAsync(document, new PartitionKey(partitionKeyValue));
        }

        public async Task DeleteDocumentAsync(string id, string partitionKeyValue)
        {
            await _container.DeleteItemAsync<JObject>(id, new PartitionKey(partitionKeyValue));
        }

        public async  Task<JObject> GetDocumentsAsync(string id, string partionkeyValue)
        {
           return await _container.ReadItemAsync<JObject>(id, new PartitionKey(partionkeyValue));
        }

        public async Task ExecuteStoredProcedureAsync(string storedProcedureName, params dynamic[] parameters)
        {
            var storedProcedure = _container.Scripts;
            await storedProcedure.ExecuteStoredProcedureAsync<dynamic>(storedProcedureName, new PartitionKey(string.Empty), parameters);
        }

        public async Task ReadChangeFeedAsync(Action<JObject> onChangeFeedReceived, string instanceName)
        {
            ChangeFeedProcessor changeFeedProcessor = _cosmosClient.GetContainer(_container.Database.Id, _container.Id)
                .GetChangeFeedProcessorBuilder<JObject>(instanceName, (changes, cancellationToken) =>
                {
                    foreach (var change in changes)
                    {
                        onChangeFeedReceived(change);
                    }
                    return Task.CompletedTask;
                })
                .WithInstanceName(instanceName)
                .WithLeaseContainer(_leaseContainer)
                .Build();

            await changeFeedProcessor.StartAsync();
        }

        public async Task StartChangeFeedProcessorAsync(string instanceName, Action<JObject> onChangeFeedReceived)
        {
            ChangeFeedProcessor changeFeedProcessor = _container.GetChangeFeedProcessorBuilder<JObject>(
                processorName: instanceName,
                onChangesDelegate: async (changes, cancellationToken) =>
                {
                    foreach (var change in changes)
                    {
                        onChangeFeedReceived(change);
                    }

                    await Task.CompletedTask; // This is to ensure the async method is awaited.
                })
                .WithInstanceName(instanceName)
                .WithLeaseContainer(_leaseContainer)
                .Build();

            await changeFeedProcessor.StartAsync();
        }
        public async Task StopChangeFeedProcessorAsync(string instanceName)
        {
            ChangeFeedProcessor changeFeedProcessor = _container.GetChangeFeedProcessorBuilder<JObject>(
                processorName: instanceName,
                onChangesDelegate: (changes, cancellationToken) => Task.CompletedTask)
                .WithInstanceName(instanceName)
                .WithLeaseContainer(_leaseContainer)
                .Build();

            await changeFeedProcessor.StopAsync();
        }

        public async Task<Dictionary<string, JObject>> GetDocumentsBatchAsync(Dictionary<string,List<string>> ids,string partitionPath,  int batchSize)
        {
            var result = new Dictionary<string, JObject>(); 
            ids.Values.ToList().ForEach(k=> k.ForEach(id=> result[id]=null!));
            foreach (var kv in ids)
            {
                var batches = GetBatches(kv.Value, batchSize);
                foreach (var batch in batches)
                {
                    var transactionBatch = _container.CreateTransactionalBatch(new PartitionKey(kv.Key));
                    foreach (var id in batch)
                    {
                        transactionBatch.ReadItem(id);
                    }
                    var response = await transactionBatch.ExecuteAsync();
                    var statusCodes = new List<string>();

                    for (int j = 0; j < response.Count; j++)
                    {
                        var operationResult = response[j];
                        if (operationResult.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            continue;
                        }
                        else if(operationResult.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            using (var stream = operationResult.ResourceStream)
                            using (var reader = new System.IO.StreamReader(stream))
                            using (var jsonReader = new Newtonsoft.Json.JsonTextReader(reader))
                            {
                                var resource = JObject.Load(jsonReader);
                                var key= resource.SelectToken(partitionPath)!.ToString() ?? "";
                                result[key] = resource;
                            }
                        }
                        else
                        {
                            statusCodes.Add(operationResult.StatusCode.ToString());
                            
                        }

                    }
                    if (statusCodes.Any())
                    {
                        var diagnostics = response.Diagnostics.ToString();
                        var errorMessages = string.Join(", ", statusCodes);
                        throw new Exception($"Failed to execute batch operation. Response: AggregateStatusCodes:{errorMessages}, Diagnostics:{diagnostics}");
                    }
                }
            }
            return result;

        }

        //Adding an overload to get documents based on a query string
        //Adding an overload to get documents based on a query string
        public async Task<List<JObject>> QueryItemsAsync(string queryString)
        {
            var queryDefinition = new QueryDefinition(queryString);
            var queryResultSetIterator = _container.GetItemQueryIterator<JObject>(queryDefinition);

            var results = new List<JObject>();

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<JObject> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (var item in currentResultSet)
                {
                    results.Add(item);
                }
            }

            return results;
        }
    }

}
