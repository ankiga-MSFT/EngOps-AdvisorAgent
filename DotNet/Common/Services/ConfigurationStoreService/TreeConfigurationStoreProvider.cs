using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure.AI.OpenAI;
using Newtonsoft.Json;
using OpenAI.Embeddings;
using Provider.Interfaces;

namespace CXOAI.ConfigurationStore;

public class TreeConfigurationStoreProvider : ITreeConfigurationStoreProvider
{
    private readonly IAzureSearchProvider _searchProvider;
    private readonly EmbeddingClient _embeddingClient;
    private readonly string _embeddingDeployment;

    public TreeConfigurationStoreProvider(
        IAzureSearchProvider searchProvider,
        Uri openAIEndpoint,
        string embeddingDeployment,
        TokenCredential credential)
    {
        _searchProvider = searchProvider;
        _embeddingDeployment = embeddingDeployment;

        var openAIClient = new AzureOpenAIClient(openAIEndpoint, credential);
        _embeddingClient = openAIClient.GetEmbeddingClient(_embeddingDeployment);
    }

    public async Task<Response<IndexDocumentsResult>> UploadDocumentAsync(TreeConfiguration configStore)
    {
        var embeddingInput = $"{configStore.ConfigurationName} {configStore.Description}";

        OpenAIEmbedding embedding = await _embeddingClient.GenerateEmbeddingAsync(embeddingInput);
        configStore.Embedding = embedding.ToFloats().ToArray();

        var result = await _searchProvider.MergeOrUploadDocument([configStore]);
        return Response.FromValue(result, null!);
    }

    public async Task<List<TreeConfiguration>> GetConfigurationsWithDescription(
        string componentName, string searchText, bool needNestedConfigs)
    {
        OpenAIEmbedding embedding = await _embeddingClient.GenerateEmbeddingAsync(searchText);
        float[] vector = embedding.ToFloats().ToArray();

        var searchOptions = new SearchOptions
        {
            Filter = $"ComponentName eq '{EscapeODataValue(componentName)}'",
            VectorSearch = new()
            {
                Queries =
                {
                    new VectorizedQuery(vector)
                    {
                        KNearestNeighborsCount = 50,
                        Fields = { "Embedding" }
                    }
                }
            }
        };

        var results = await ExecuteSearchAsync(searchOptions);

        if (needNestedConfigs)
            await ResolveNestedConfigurationsAsync(results);

        return results;
    }

    public async Task<List<TreeConfiguration>> GetConfigurationsWithNames(
        string componentName, List<string> configurationNames, bool needNestedConfigs)
    {
        var nameFilters = string.Join(" or ",
            configurationNames.Select(n => $"ConfigurationName eq '{EscapeODataValue(n)}'"));
        var filter = $"ComponentName eq '{EscapeODataValue(componentName)}' and ({nameFilters})";

        var searchOptions = new SearchOptions
        {
            Filter = filter,
            Size = 1000
        };

        var results = await ExecuteSearchAsync(searchOptions);

        if (needNestedConfigs)
            await ResolveNestedConfigurationsAsync(results);

        return results;
    }

    public async Task<List<TreeConfiguration>> GetConfigurations(
        string componentName, bool needNestedConfigs)
    {
        var searchOptions = new SearchOptions
        {
            Filter = $"ComponentName eq '{EscapeODataValue(componentName)}'",
            Size = 1000
        };

        var results = await ExecuteSearchAsync(searchOptions);

        if (needNestedConfigs)
            await ResolveNestedConfigurationsAsync(results);

        return results;
    }

    private async Task<List<TreeConfiguration>> ExecuteSearchAsync(SearchOptions searchOptions)
    {
        var results = new List<TreeConfiguration>();
        var response = await _searchProvider.SearchAsync<TreeConfiguration>(searchOptions);

        await foreach (var result in response.GetResultsAsync())
        {
            if (result.Document is not null)
                results.Add(result.Document);
        }

        return results;
    }

    private async Task ResolveNestedConfigurationsAsync(List<TreeConfiguration> results)
    {
        var visited = new HashSet<string>(results.Select(r => r.Id));
        var queue = new Queue<DependsOnEntry>(results.SelectMany(r => r.DependsOn ?? []));

        while (queue.Count > 0)
        {
            var dep = queue.Dequeue();
            var depId = $"{dep.ComponentName}-{dep.ConfigurationName}";

            if (!visited.Add(depId))
                continue;

            var searchOptions = new SearchOptions
            {
                Filter = $"ComponentName eq '{EscapeODataValue(dep.ComponentName)}' and ConfigurationName eq '{EscapeODataValue(dep.ConfigurationName)}'",
                Size = 1
            };

            var nested = await ExecuteSearchAsync(searchOptions);

            foreach (var config in nested)
            {
                results.Add(config);

                foreach (var entry in config.DependsOn ?? [])
                    queue.Enqueue(entry);
            }
        }
    }

    private static string EscapeODataValue(string? value) =>
        value?.Replace("'", "''") ?? string.Empty;
}
