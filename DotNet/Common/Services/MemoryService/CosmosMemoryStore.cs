using System.Diagnostics;
using System.Text.Json;
using Azure.Core;
using InfraService.OpenTelemetryProvider;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Provider.Interfaces;

namespace CXOAI.Memory;

/// <summary>
/// Cosmos DB NoSQL memory store with native vector search.
/// Requires a container with vectorEmbeddingPolicy and quantizedFlat vector index.
/// Uses ICosmosDbProvider from the Providers project for Cosmos connectivity.
/// </summary>
public class CosmosMemoryStore : IMemoryStore
{
    private readonly Container _container;
    private readonly MemoryFactExtractor _extractor;
    private readonly MemoryConflictResolver _resolver;
    private readonly MemoryEmbedder _embedder;
    private readonly float _conflictThreshold;
    private readonly ILogger<CosmosMemoryStore> _logger;
    private readonly IMetricsProvider? _metricsProvider;

    private const int TemporalTtlSeconds = 3600; // 1 hour

    public CosmosMemoryStore(
        ICosmosDbProvider cosmosDbProvider,
        string openAIEndpoint,
        TokenCredential credential,
        ILoggerFactory loggerFactory,
        string embeddingDeployment = "text-embedding-3-small",
        int embeddingDimensions = 512,
        float conflictThreshold = 0.85f,
        IMetricsProvider? metricsProvider = null)
    {
        _container = cosmosDbProvider.Container;
        _logger = loggerFactory.CreateLogger<CosmosMemoryStore>();
        _metricsProvider = metricsProvider;
        _extractor = new MemoryFactExtractor(openAIEndpoint, credential, loggerFactory.CreateLogger<MemoryFactExtractor>(), metricsProvider: metricsProvider);
        _resolver = new MemoryConflictResolver(openAIEndpoint, credential, loggerFactory.CreateLogger<MemoryConflictResolver>(), metricsProvider: metricsProvider);
        _embedder = new MemoryEmbedder(openAIEndpoint, credential, loggerFactory.CreateLogger<MemoryEmbedder>(), embeddingDeployment, embeddingDimensions);
        _conflictThreshold = conflictThreshold;
    }

    /// <inheritdoc/>
    public async Task ExtractAndStoreAsync(string userId, string conversationContent, MemoryScope scope = MemoryScope.User)
    {
        using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.DataStoreOperation,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ExtractAndStore"));
        try
        {
            var extractedFacts = await _extractor.ExtractFactsAsync(conversationContent, scope);
            if (extractedFacts.Count == 0)
            {
                latency?.SetState(ActivityStatusCode.Ok);
                _metricsProvider?.TrackAvailabilityMetric(MetricNames.DataStoreOperation, 1, null,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ExtractAndStore"));
                return;
            }

            var permanentCount = extractedFacts.Count(f => f.Category == FactCategory.Permanent);
            var temporalCount = extractedFacts.Count(f => f.Category == FactCategory.Temporal);
            _logger.LogInformation("Extracted {PermanentCount} permanent + {TemporalCount} temporal facts",
                permanentCount, temporalCount);

            await UpsertFactsAsync(userId, extractedFacts);
            latency?.SetState(ActivityStatusCode.Ok);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.DataStoreOperation, 1, null,
                new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ExtractAndStore"));
        }
        catch (Exception ex)
        {
            latency?.SetState(ActivityStatusCode.Error);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.DataStoreOperation, 1, ex,
                new KeyValuePair<string, object?>(MetricNames.TagOperationName, "ExtractAndStore"));
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StoreFactsAsync(string userId, List<ExtractedFact> facts)
    {
        if (facts.Count == 0)
            return;

        await UpsertFactsAsync(userId, facts);
    }

    /// <inheritdoc/>
    public async Task<List<MemoryFact>> RecallAsync(string userId, string query, int topK = 10, float minScore = 0.7f, MemoryScope? scope = null)
    {
        using var latency = _metricsProvider?.LatencyMeasureOperation(MetricNames.DataStoreOperation,
            new KeyValuePair<string, object?>(MetricNames.TagOperationName, "MemoryRecall"));
        try
        {
            // For Org scope: try CacheKey deterministic match in parallel with embedding (zero extra latency)
            if (scope == MemoryScope.Org)
            {
                var cacheKeyTask = _extractor.ExtractCacheKeyAsync(query);
                var embeddingTask = _embedder.EmbedAsync(query);
                await Task.WhenAll(cacheKeyTask, embeddingTask);

                var cacheKey = cacheKeyTask.Result;
                if (!string.IsNullOrEmpty(cacheKey))
                {
                    // CacheKey extracted - use deterministic match only. 
                    // Do NOT fall back to vector search: a missing CacheKey match means
                    // "no cached data for this exact query" (e.g., 1m vs 6m are distinct).
                    var cacheResults = await FindByCacheKeyPrefixAsync(userId, cacheKey);
                    _logger.LogInformation("RecallAsync: CacheKey '{CacheKey}' matched {Count} fact(s)",
                        cacheKey, cacheResults.Count);
                    latency?.SetState(ActivityStatusCode.Ok);
                    _metricsProvider?.TrackAvailabilityMetric(MetricNames.DataStoreOperation, 1, null,
                        new KeyValuePair<string, object?>(MetricNames.TagOperationName, "MemoryRecall"));
                    return ApplyTemporalTtlFilter(cacheResults);
                }

                // CacheKey extraction failed (vague/compound query) - fall back to vector search
                _logger.LogInformation("RecallAsync: No CacheKey extracted, falling back to vector search");
                var vectorResults = await VectorSearchAsync(userId, embeddingTask.Result, topK, minScore, scope);
                latency?.SetState(ActivityStatusCode.Ok);
                _metricsProvider?.TrackAvailabilityMetric(MetricNames.DataStoreOperation, 1, null,
                    new KeyValuePair<string, object?>(MetricNames.TagOperationName, "MemoryRecall"));
                return ApplyTemporalTtlFilter(vectorResults);
            }

            // User/System scope: vector search only (no CacheKey)
            var queryEmbedding = await _embedder.EmbedAsync(query);
            var results = await VectorSearchAsync(userId, queryEmbedding, topK, minScore, scope);
            latency?.SetState(ActivityStatusCode.Ok);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.DataStoreOperation, 1, null,
                new KeyValuePair<string, object?>(MetricNames.TagOperationName, "MemoryRecall"));
            return ApplyTemporalTtlFilter(results);
        }
        catch (Exception ex)
        {
            latency?.SetState(ActivityStatusCode.Error);
            _metricsProvider?.TrackAvailabilityMetric(MetricNames.DataStoreOperation, 1, ex,
                new KeyValuePair<string, object?>(MetricNames.TagOperationName, "MemoryRecall"));
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task ForgetAsync(string userId, string factId)
    {
        try
        {
            await _container.DeleteItemAsync<MemoryFact>(factId, new PartitionKey(userId));
            _logger.LogInformation("Forgot fact '{FactId}' for user '{UserId}'", factId, userId);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Fact '{FactId}' not found for user '{UserId}' � already deleted", factId, userId);
        }
    }

    /// <inheritdoc/>
    public async Task<List<MemoryFact>> GetAllFactsAsync(string userId, MemoryScope? scope = null)
    {
        var sql = scope.HasValue
            ? "SELECT * FROM c WHERE c.UserId = @userId AND (c.scope = @scopeStr OR c.scope = @scopeInt)"
            : "SELECT * FROM c WHERE c.UserId = @userId";

        var query = new QueryDefinition(sql)
            .WithParameter("@userId", userId);

        if (scope.HasValue)
        {
            query = query
                .WithParameter("@scopeStr", scope.Value.ToString())
                .WithParameter("@scopeInt", (int)scope.Value);
        }

        var results = new List<MemoryFact>();
        using var iterator = _container.GetItemQueryIterator<MemoryFact>(query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (iterator.HasMoreResults)
        {
            var batch = await iterator.ReadNextAsync();
            results.AddRange(batch);
        }

        return results;
    }

    private async Task UpsertFactsAsync(string userId, List<ExtractedFact> extractedFacts)
    {
        var factTexts = extractedFacts.Select(f => f.Fact).ToList();
        var embeddings = await _embedder.EmbedBatchAsync(factTexts);

        for (int i = 0; i < extractedFacts.Count; i++)
        {
            var extracted = extractedFacts[i];
            var embedding = embeddings[i];

            // For facts with CacheKey (Org data cache OR User entity mappings):
            // deterministic conflict detection bypasses vector search and LLM resolver.
            if (!string.IsNullOrEmpty(extracted.CacheKey))
            {
                var existingByKey = await FindByCacheKeyExactAsync(userId, extracted.CacheKey);
                if (existingByKey != null)
                {
                    existingByKey.Fact = extracted.Fact;
                    existingByKey.Embedding = embedding;
                    existingByKey.UpdatedAt = DateTimeOffset.UtcNow;
                    existingByKey.TimeToLive = extracted.Scope == MemoryScope.Org ? TemporalTtlSeconds : null;
                    existingByKey.CacheKey = extracted.CacheKey;
                    existingByKey.Tags = extracted.Tags;
                    existingByKey.EntityType = extracted.EntityType;
                    existingByKey.EntityId = extracted.EntityId;
                    await _container.UpsertItemAsync(existingByKey, new PartitionKey(userId));
                    _logger.LogInformation("[Memory:UPDATE] [{Scope}] CacheKey='{CacheKey}' '{Fact}'",
                        extracted.Scope, extracted.CacheKey, extracted.Fact);
                }
                else
                {
                    var newFact = CreateFact(userId, extracted, embedding);
                    await _container.CreateItemAsync(newFact, new PartitionKey(userId));
                    _logger.LogInformation("[Memory:ADD] [{Scope}] CacheKey='{CacheKey}' '{Fact}'",
                        extracted.Scope, extracted.CacheKey, extracted.Fact);
                }
                continue;
            }

            var similar = await VectorSearchAsync(userId, embedding, topK: 1, minScore: _conflictThreshold, scope: extracted.Scope);

            if (similar.Count > 0)
            {
                var existing = similar[0];

                // Deterministic guard: different entity IDs are always separate entities.
                // Skip the LLM resolver — prevents "Walmart Inc." (TPID:784852) from being
                // merged with "Walmart Canada Bank" (TPID:902415), or "Azure Data Box" from
                // being merged with "Azure Data Explorer".
                if (HasDifferentEntityIds(extracted, existing))
                {
                    var addFact = CreateFact(userId, extracted, embedding);
                    await _container.CreateItemAsync(addFact, new PartitionKey(userId));
                    _logger.LogInformation("[Memory:ADD] [{Scope}] '{Fact}' (different entity ID from nearest match)",
                        extracted.Scope, extracted.Fact);
                    continue;
                }

                var resolution = await _resolver.ResolveAsync(extracted.Fact, existing.Fact);

                switch (resolution.Operation)
                {
                    case MemoryOperationType.Update:
                        existing.Fact = resolution.MergedFact;
                        existing.Category = resolution.Category;
                        existing.Embedding = embedding;
                        existing.UpdatedAt = DateTimeOffset.UtcNow;
                        // Preserve original expiry: compute remaining TTL from CreatedAt so
                        // upserts don't reset the clock (prevents cyclic freshness problem).
                        if (resolution.Category == FactCategory.Temporal)
                        {
                            var elapsed = (int)(DateTimeOffset.UtcNow - existing.CreatedAt).TotalSeconds;
                            var remaining = Math.Max(60, TemporalTtlSeconds - elapsed); // min 60s to avoid immediate expiry
                            existing.TimeToLive = remaining;
                        }
                        else
                        {
                            existing.TimeToLive = null;
                        }
                        await _container.UpsertItemAsync(existing, new PartitionKey(userId));
                        _logger.LogInformation("[Memory:UPDATE] [{Scope}] '{Fact}'", extracted.Scope, resolution.MergedFact);
                        break;

                    case MemoryOperationType.Add:
                        var addFact = CreateFact(userId, extracted, embedding);
                        await _container.CreateItemAsync(addFact, new PartitionKey(userId));
                        _logger.LogInformation("[Memory:ADD] [{Scope}] '{Fact}' (conflict resolved as separate)", extracted.Scope, extracted.Fact);
                        break;

                    case MemoryOperationType.Noop:
                        _logger.LogDebug("[Memory:NOOP] [{Scope}] '{Fact}'", extracted.Scope, extracted.Fact);
                        break;
                }
            }
            else
            {
                var newFact = CreateFact(userId, extracted, embedding);
                await _container.CreateItemAsync(newFact, new PartitionKey(userId));
                _logger.LogInformation("[Memory:ADD] [{Scope}] '{Fact}'", extracted.Scope, extracted.Fact);
            }
        }
    }

    private async Task<List<MemoryFact>> VectorSearchAsync(string userId, float[] embedding, int topK, float minScore, MemoryScope? scope = null)
    {
        var vectorLiteral = $"[{string.Join(",", embedding.Select(v => v.ToString("G9")))}]";

        var scopeFilter = scope.HasValue ? $" AND c.scope = '{scope.Value}'" : "";

        var sql = $"""
            SELECT TOP {topK} c.id, c.UserId, c.scope, c.fact, c.category, c.source, c.tags,
                   c.embedding, c.createdAt, c.updatedAt, c.ttl, c.entityType, c.entityId, c.cacheKey,
                   VectorDistance(c.embedding, {vectorLiteral}) AS distance
            FROM c
            WHERE c.UserId = @userId{scopeFilter}
            ORDER BY VectorDistance(c.embedding, {vectorLiteral})
            """;

        var query = new QueryDefinition(sql)
            .WithParameter("@userId", userId);

        var results = new List<MemoryFact>();
        try
        {
            using var iterator = _container.GetItemQueryIterator<MemoryFactWithDistance>(query,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

            while (iterator.HasMoreResults)
            {
                var batch = await iterator.ReadNextAsync();
                foreach (var item in batch)
                {
                    // VectorDistance returns cosine SIMILARITY (higher = more similar):
                    //   1.0 = identical, 0.0 = orthogonal, -1.0 = opposite
                    if (item.Distance < minScore)
                        continue;

                    results.Add(item.ToMemoryFact());
                }
            }
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "VectorSearchAsync FAILED: StatusCode={StatusCode}", ex.StatusCode);
        }

        _logger.LogInformation("VectorSearchAsync: userId={UserId}, scope={Scope}, found {Count} results (minScore={MinScore})",
            userId, scope?.ToString() ?? "all", results.Count, minScore);

        return results;
    }

    private static MemoryFact CreateFact(string userId, ExtractedFact extracted, float[] embedding)
    {
        // Org-scoped facts are always Temporal with a fixed TTL — they act as a shared cache.
        var isOrg = extracted.Scope == MemoryScope.Org;
        var category = isOrg ? FactCategory.Temporal : extracted.Category;

        return new MemoryFact
        {
            UserId = userId,
            Scope = extracted.Scope,
            Fact = extracted.Fact,
            Category = category,
            Source = extracted.Source,
            Tags = extracted.Tags,
            Embedding = embedding,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            TimeToLive = (isOrg || category == FactCategory.Temporal) ? TemporalTtlSeconds : null,
            EntityType = extracted.EntityType,
            EntityId = extracted.EntityId,
            CacheKey = extracted.CacheKey
        };
    }

    /// <summary>
    /// Returns true when both the new fact and the existing fact have entity IDs and they differ.
    /// This is a deterministic guard — no LLM needed to know these are separate entities.
    /// </summary>
    private static bool HasDifferentEntityIds(ExtractedFact newFact, MemoryFact existing)
    {
        return !string.IsNullOrEmpty(newFact.EntityId)
            && !string.IsNullOrEmpty(existing.EntityId)
            && !string.Equals(newFact.EntityId, existing.EntityId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Exact CacheKey match — used during storage for conflict detection.
    /// </summary>
    private async Task<MemoryFact?> FindByCacheKeyExactAsync(string userId, string cacheKey)
    {
        var sql = "SELECT * FROM c WHERE c.UserId = @userId AND c.cacheKey = @cacheKey";
        var query = new QueryDefinition(sql)
            .WithParameter("@userId", userId)
            .WithParameter("@cacheKey", cacheKey);

        using var iterator = _container.GetItemQueryIterator<MemoryFact>(query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (iterator.HasMoreResults)
        {
            var batch = await iterator.ReadNextAsync();
            if (batch.Count > 0)
                return batch.First();
        }
        return null;
    }

    /// <summary>
    /// Prefix CacheKey match — used during recall to find all facts matching a query scope.
    /// E.g., prefix "incident-impact:12345" matches "incident-impact:12345:11111" and "incident-impact:12345:22222".
    /// </summary>
    private async Task<List<MemoryFact>> FindByCacheKeyPrefixAsync(string userId, string cacheKeyPrefix)
    {
        var sql = "SELECT * FROM c WHERE c.UserId = @userId AND STARTSWITH(c.cacheKey, @prefix)";
        var query = new QueryDefinition(sql)
            .WithParameter("@userId", userId)
            .WithParameter("@prefix", cacheKeyPrefix);

        var results = new List<MemoryFact>();
        using var iterator = _container.GetItemQueryIterator<MemoryFact>(query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (iterator.HasMoreResults)
        {
            var batch = await iterator.ReadNextAsync();
            results.AddRange(batch);
        }

        _logger.LogInformation("FindByCacheKeyPrefixAsync: prefix='{Prefix}', found {Count} fact(s)",
            cacheKeyPrefix, results.Count);
        return results;
    }

    /// <summary>
    /// Filters out stale temporal facts at read time. Cosmos TTL auto-deletes them eventually,
    /// but this ensures we never serve stale data even if TTL hasn't fired yet.
    /// </summary>
    private List<MemoryFact> ApplyTemporalTtlFilter(List<MemoryFact> facts)
    {
        var now = DateTimeOffset.UtcNow;
        var filtered = facts
            .Where(f => f.Category != FactCategory.Temporal
                        || (now - f.UpdatedAt) <= TimeSpan.FromSeconds(TemporalTtlSeconds))
            .ToList();

        if (filtered.Count < facts.Count)
            _logger.LogInformation("ApplyTemporalTtlFilter: Filtered out {Count} stale temporal facts",
                facts.Count - filtered.Count);

        return filtered;
    }

    /// <summary>
    /// computed "distance" column from VectorDistance(). StringEnumConverter on the
    /// CosmosClient handles Scope/Category as strings automatically.
    /// </summary>
    private class MemoryFactWithDistance
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("UserId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("scope")]
        public MemoryScope Scope { get; set; }

        [JsonProperty("fact")]
        public string Fact { get; set; } = string.Empty;

        [JsonProperty("category")]
        public FactCategory Category { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = [];

        [JsonProperty("embedding")]
        public float[] Embedding { get; set; } = [];

        [JsonProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTimeOffset UpdatedAt { get; set; }

        [JsonProperty("distance")]
        public float Distance { get; set; }

        [JsonProperty("ttl")]
        public int? TimeToLive { get; set; }

        [JsonProperty("entityType")]
        public string? EntityType { get; set; }

        [JsonProperty("entityId")]
        public string? EntityId { get; set; }

        [JsonProperty("cacheKey")]
        public string? CacheKey { get; set; }

        public MemoryFact ToMemoryFact() => new()
        {
            Id = Id,
            UserId = UserId,
            Scope = Scope,
            Fact = Fact,
            Category = Category,
            Source = Source,
            Tags = Tags,
            Embedding = Embedding,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            TimeToLive = TimeToLive,
            EntityType = EntityType,
            EntityId = EntityId,
            CacheKey = CacheKey
        };
    }
}
