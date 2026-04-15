using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents.Models;

namespace CXOAI.ConfigurationStore;

public class TreeJsonConfigurationStoreProvider : ITreeConfigurationStoreProvider
{
    private readonly List<TreeConfiguration> _store;
    private readonly object _lock = new();

    public TreeJsonConfigurationStoreProvider(string seedDataPath)
    {
        var json = File.ReadAllText(seedDataPath);
        _store = JsonSerializer.Deserialize<List<TreeConfiguration>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
    }

    public Task<Response<IndexDocumentsResult>> UploadDocumentAsync(TreeConfiguration configStore)
    {
        lock (_lock)
        {
            var existing = _store.FindIndex(c =>
                string.Equals(c.ComponentName, configStore.ComponentName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.ConfigurationName, configStore.ConfigurationName, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
                _store[existing] = configStore;
            else
                _store.Add(configStore);
        }

        return Task.FromResult<Response<IndexDocumentsResult>>(null!);
    }

    public Task<List<TreeConfiguration>> GetConfigurationsWithDescription(
        string componentName, string searchText, bool needNestedConfigs)
    {
        var keywords = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var results = _store
            .Where(c => string.Equals(c.ComponentName, componentName, StringComparison.OrdinalIgnoreCase))
            .Select(c => new
            {
                Config = c,
                Score = ComputeRelevanceScore(c, keywords)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Config)
            .ToList();

        if (needNestedConfigs)
            ResolveNestedConfigurations(results);

        return Task.FromResult(results);
    }

    public Task<List<TreeConfiguration>> GetConfigurationsWithNames(
        string componentName, List<string> configurationNames, bool needNestedConfigs)
    {
        var nameSet = new HashSet<string>(configurationNames, StringComparer.OrdinalIgnoreCase);

        var results = _store
            .Where(c =>
                string.Equals(c.ComponentName, componentName, StringComparison.OrdinalIgnoreCase) &&
                c.ConfigurationName is not null &&
                nameSet.Contains(c.ConfigurationName))
            .ToList();

        if (needNestedConfigs)
            ResolveNestedConfigurations(results);

        return Task.FromResult(results);
    }

    public Task<List<TreeConfiguration>> GetConfigurations(
        string componentName, bool needNestedConfigs)
    {
        var results = _store
            .Where(c => string.Equals(c.ComponentName, componentName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (needNestedConfigs)
            ResolveNestedConfigurations(results);

        return Task.FromResult(results);
    }

    private static int ComputeRelevanceScore(TreeConfiguration config, string[] keywords)
    {
        var score = 0;
        var searchableText = $"{config.ComponentName} {config.ConfigurationName} {config.Description} {config.Configuration}"
            .ToLowerInvariant();

        foreach (var keyword in keywords)
        {
            if (searchableText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                score++;
        }

        return score;
    }

    private void ResolveNestedConfigurations(List<TreeConfiguration> results)
    {
        var visited = new HashSet<string>(results.Select(r => r.Id));
        var queue = new Queue<DependsOnEntry>(results.SelectMany(r => r.DependsOn ?? []));

        while (queue.Count > 0)
        {
            var dep = queue.Dequeue();
            var depId = $"{dep.ComponentName}-{dep.ConfigurationName}";

            if (!visited.Add(depId))
                continue;

            var nested = _store
                .Where(c =>
                    string.Equals(c.ComponentName, dep.ComponentName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.ConfigurationName, dep.ConfigurationName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var config in nested)
            {
                results.Add(config);

                foreach (var entry in config.DependsOn ?? [])
                    queue.Enqueue(entry);
            }
        }
    }
}
