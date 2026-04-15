using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace CXOAI.ConfigurationStore;

public class TreeConfigurationStoreSeeder
{
    private readonly TreeConfigurationStoreProvider _provider;
    private readonly string _searchEndpoint;
    private readonly string _indexName;

    public TreeConfigurationStoreSeeder(TreeConfigurationStoreProvider provider, string searchEndpoint, string indexName)
    {
        _provider = provider;
        _searchEndpoint = searchEndpoint;
        _indexName = indexName;
    }

    /// <summary>
    /// Creates the search index based on the <see cref="TreeConfiguration"/> class schema
    /// if it does not already exist, then seeds documents from the seed data file.
    /// </summary>
    public async Task SeedAsync(string seedDataPath = "StoreConfigs/SeedData.json")
    {
        await EnsureIndexAsync();

        var json = await File.ReadAllTextAsync(seedDataPath);
        var configurations = JsonSerializer.Deserialize<List<TreeConfiguration>>(json)
            ?? throw new InvalidOperationException("Failed to deserialize seed data.");

        foreach (var config in configurations)
        {
            await _provider.UploadDocumentAsync(config);
        }
    }

    /// <summary>
    /// Creates or updates the Azure AI Search index derived from <see cref="TreeConfiguration"/>.
    /// Uses FieldBuilder to reflect the index schema from the class attributes
    /// (SimpleField, SearchableField, VectorSearchField).
    /// </summary>
    public async Task EnsureIndexAsync()
    {
        var credential = new DefaultAzureCredential();
        var indexClient = new SearchIndexClient(new Uri(_searchEndpoint), credential);

        var fields = new FieldBuilder().Build(typeof(TreeConfiguration));

        var vectorSearch = new VectorSearch();
        vectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration("default-hnsw")
        {
            Parameters = new HnswParameters
            {
                M = 4,
                EfConstruction = 400,
                EfSearch = 500,
                Metric = VectorSearchAlgorithmMetric.Cosine
            }
        });
        vectorSearch.Profiles.Add(new VectorSearchProfile("default-vector-profile", "default-hnsw"));

        var semanticConfig = new SemanticConfiguration("default-semantic",
            new SemanticPrioritizedFields
            {
                TitleField = new SemanticField("ConfigurationName"),
                ContentFields = { new SemanticField("Description") }
            });

        var semanticSearch = new SemanticSearch();
        semanticSearch.Configurations.Add(semanticConfig);

        var index = new SearchIndex(_indexName)
        {
            Fields = fields,
            VectorSearch = vectorSearch,
            SemanticSearch = semanticSearch
        };

        await indexClient.CreateOrUpdateIndexAsync(index);
    }
}
