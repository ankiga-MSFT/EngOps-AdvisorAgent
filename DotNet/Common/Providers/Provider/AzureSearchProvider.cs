using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Provider.Interfaces;
using Provider.Model;

namespace Provider
{
    public class AzureSearchProvider : IAzureSearchProvider
    {
        private readonly ILogger logger;
        private readonly SearchClient searchClient;

        public AzureSearchProvider(ILogger log, AzureSearchConnectionConfig connectionConfig)
        {
            if (connectionConfig == null) throw new ArgumentNullException(nameof(connectionConfig));
            if (string.IsNullOrWhiteSpace(connectionConfig.SearchEndpoint)) throw new ArgumentException("Search endpoint cannot be null or empty.", nameof(connectionConfig.SearchEndpoint));
            if (string.IsNullOrWhiteSpace(connectionConfig.SearchIndex)) throw new ArgumentException("Search index cannot be null or empty.", nameof(connectionConfig.SearchIndex));
            this.logger = log;
            this.searchClient = CreateSearchClient(connectionConfig);
        }

        public AzureSearchProvider(ILogger log, SearchClient searchClient)
        {
            this.logger = log ?? throw new ArgumentNullException(nameof(log));
            this.searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
        }

        private SearchClient CreateSearchClient(AzureSearchConnectionConfig connectionConfig)
        {
            TokenCredential credential;

            if (connectionConfig.MuiClientId == null)
            {
                logger.LogInformation($"Creating the search client with AzureCredential for endpoint: {connectionConfig.SearchEndpoint}");
#if DEBUG
                credential = new DefaultAzureCredential();
#else
                credential = new ManagedIdentityCredential();
#endif
            }
            else
            {
                logger.LogInformation($"Creating the search client with ManagedIdentityCredential (ClientId: {connectionConfig.MuiClientId}) for endpoint: {connectionConfig.SearchEndpoint}");
                credential = new ManagedIdentityCredential(connectionConfig.MuiClientId);
            }

            logger.LogInformation($"Validating search endpoint URI: {connectionConfig.SearchEndpoint}");
            if (!Uri.TryCreate(connectionConfig.SearchEndpoint, UriKind.Absolute, out var searchEndpoint))
            {
                throw new ArgumentException("Invalid search endpoint URI", nameof(connectionConfig.SearchEndpoint));
            }

            var options = new SearchClientOptions
            {
                Retry =
                {
                    MaxRetries = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(5),
                    Mode = RetryMode.Exponential
                }
            };

            return new SearchClient(searchEndpoint, connectionConfig.SearchIndex, credential, options);
        }


        public async Task<IndexDocumentsResult> MergeOrUploadDocument<T>(List<T> documents, IndexDocumentsOptions options)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            if (documents.Count == 0) throw new ArgumentException("Document list cannot be empty.", nameof(documents));
            int documentCount = documents.Count;
            logger.LogInformation($"Merging or uploading {documentCount} documents to Azure Search Index");

            Response<IndexDocumentsResult> response = null!;
            try
            {
                response = await searchClient.MergeOrUploadDocumentsAsync(documents, options).ConfigureAwait(false);

                logger.LogInformation($"Successfully uploaded {documentCount} documents to Azure Search index '{searchClient.IndexName}'");

                return response ?? throw new InvalidOperationException("Failed to merge or upload documents to Azure Search Index.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error while merging or uploading documents to Azure Search Index: {ex.Message}");
                logger.LogInformation("Documents failed: {@Documents}", documents);
                throw;
            }
        }

        public async Task<SearchResults<T>> SearchDocumentsByFilterAsync<T>(string searchText, SearchOptions searchOptions = null!)
        {
            if (string.IsNullOrWhiteSpace(searchText)) throw new ArgumentNullException(nameof(searchText), "Search text cannot be null or empty.");            

            logger.LogInformation($"Searching documents in index '{searchClient.IndexName}' with search text: '{searchText}'");

            try
            {                
                var results = await searchClient.SearchAsync<T>(searchText, searchOptions).ConfigureAwait(false);

                if (results == null)
                {
                    throw new InvalidOperationException($"Failed to search documents in index '{searchClient.IndexName}'");
                }

                return results;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error while searching documents in index '{searchClient.IndexName}' with query '{searchText}': {ex.Message}");
                throw;
            }
        }

        public async Task<SearchResults<T>> SearchAsync<T>(SearchOptions searchOptions)
        {
            if (searchOptions == null) throw new ArgumentNullException(nameof(searchOptions));

            logger.LogInformation("Searching documents in index '{IndexName}' with filter-only/vector query", searchClient.IndexName);

            try
            {
                var results = await searchClient.SearchAsync<T>(null, searchOptions).ConfigureAwait(false);

                return results ?? throw new InvalidOperationException($"Failed to search documents in index '{searchClient.IndexName}'");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while searching documents in index '{IndexName}': {Message}", searchClient.IndexName, ex.Message);
                throw;
            }
        }

        public async Task<IndexDocumentsResult> DeleteDocumentByKeyAsync(string documentKeyFieldName, IEnumerable<string> documentKeys)
        {
            if (string.IsNullOrWhiteSpace(documentKeyFieldName)) throw new ArgumentNullException(nameof(documentKeyFieldName), "Key cannot be null or empty.");            

            // convert the documentKeys to comma separated string 
            var keys = string.Join(",", documentKeys);

            logger.LogInformation($"Deleting document with key '{keys}' from Azure Search index '{searchClient.IndexName}'");

            try
            {                
                var response = await searchClient.DeleteDocumentsAsync(documentKeyFieldName, documentKeys).ConfigureAwait(false);

                logger.LogInformation($"Successfully deleted document with key '{keys}' from Azure Search index '{searchClient.IndexName}'");
                return response ?? throw new InvalidOperationException("Failed to merge or upload documents to Azure Search Index.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error while deleting document with key '{keys}' from Azure Search index '{searchClient.IndexName}': {ex.Message}");
                throw;
            }
        }


    }
}
