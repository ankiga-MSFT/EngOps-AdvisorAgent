using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace CXOAI.Memory;

/// <summary>
/// File-backed memory store for local development.
/// On startup: reads facts from JSON file ? embeds any facts missing embeddings ? builds in-memory vector index.
/// Vector search is brute-force cosine similarity (fast for per-user fact counts of ~50�500).
/// </summary>
public class FileMemoryStore : IMemoryStore
{
    private readonly string _filePath;
    private readonly MemoryFactExtractor _extractor;
    private readonly MemoryConflictResolver _resolver;
    private readonly MemoryEmbedder _embedder;
    private readonly ConcurrentDictionary<string, List<MemoryFact>> _index = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly float _conflictThreshold;
    private readonly ILogger<FileMemoryStore> _logger;
    private bool _initialized;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new CaseInsensitiveEnumConverterFactory() }
    };

    private const int TemporalTtlSeconds = 3600; // 1 hour

    public FileMemoryStore(
        string openAIEndpoint,
        TokenCredential credential,
        ILoggerFactory loggerFactory,
        string filePath = "memory_store.json",
        string embeddingDeployment = "text-embedding-3-small",
        int embeddingDimensions = 512,
        float conflictThreshold = 0.85f)
    {
        _filePath = filePath;
        _logger = loggerFactory.CreateLogger<FileMemoryStore>();
        _extractor = new MemoryFactExtractor(openAIEndpoint, credential, loggerFactory.CreateLogger<MemoryFactExtractor>());
        _resolver = new MemoryConflictResolver(openAIEndpoint, credential, loggerFactory.CreateLogger<MemoryConflictResolver>());
        _embedder = new MemoryEmbedder(openAIEndpoint, credential, loggerFactory.CreateLogger<MemoryEmbedder>(), embeddingDeployment, embeddingDimensions);
        _conflictThreshold = conflictThreshold;
    }

    /// <summary>
    /// Load facts from file and rebuild in-memory vector index.
    /// Must be called once at startup before any Recall/Store operations.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        await _lock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            LoadFromFile();

            await RebuildEmbeddingsAsync();
            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ExtractAndStoreAsync(string userId, string conversationContent, MemoryScope scope = MemoryScope.User)
    {
        await EnsureInitializedAsync();

        var extractedFacts = await _extractor.ExtractFactsAsync(conversationContent, scope);
        if (extractedFacts.Count == 0)
            return;

        await UpsertFactsAsync(userId, extractedFacts);
    }

    /// <inheritdoc/>
    public async Task StoreFactsAsync(string userId, List<ExtractedFact> facts)
    {
        await EnsureInitializedAsync();

        if (facts.Count == 0)
            return;

        await UpsertFactsAsync(userId, facts);
    }

    /// <inheritdoc/>
    public async Task<List<MemoryFact>> RecallAsync(string userId, string query, int topK = 10, float minScore = 0.7f, MemoryScope? scope = null)
    {
        await EnsureInitializedAsync();

        if (!_index.TryGetValue(userId, out var userFacts) || userFacts.Count == 0)
            return [];

        var queryEmbedding = await _embedder.EmbedAsync(query);

        var now = DateTimeOffset.UtcNow;
        var candidates = scope.HasValue
            ? userFacts.Where(f => f.Scope == scope.Value)
            : userFacts;

        // TTL filter: skip temporal facts older than 1 hour
        candidates = candidates.Where(f =>
            f.Category != FactCategory.Temporal
            || (now - f.UpdatedAt) <= TimeSpan.FromSeconds(TemporalTtlSeconds));

        var results = candidates
            .Select(f => (fact: f, score: MemoryEmbedder.CosineSimilarity(queryEmbedding, f.Embedding)))
            .Where(r => r.score >= minScore)
            .OrderByDescending(r => r.score)
            .Take(topK)
            .Select(r => r.fact)
            .ToList();

        _logger.LogDebug("Recalled {Count} facts for user '{UserId}' (scope={Scope}, query='{Query}')",
            results.Count, userId, scope?.ToString() ?? "all", query[..Math.Min(50, query.Length)]);

        return results;
    }

    /// <inheritdoc/>
    public async Task ForgetAsync(string userId, string factId)
    {
        await EnsureInitializedAsync();

        if (_index.TryGetValue(userId, out var userFacts))
        {
            var removed = userFacts.RemoveAll(f => f.Id == factId);
            if (removed > 0)
            {
                _logger.LogInformation("Forgot fact '{FactId}' for user '{UserId}'", factId, userId);
                await SaveToFileAsync();
            }
        }
    }

    /// <inheritdoc/>
    public async Task<List<MemoryFact>> GetAllFactsAsync(string userId, MemoryScope? scope = null)
    {
        await EnsureInitializedAsync();

        if (!_index.TryGetValue(userId, out var userFacts))
            return [];

        return scope.HasValue
            ? userFacts.Where(f => f.Scope == scope.Value).ToList()
            : userFacts.ToList();
    }

    private async Task UpsertFactsAsync(string userId, List<ExtractedFact> extractedFacts)
    {
        var factTexts = extractedFacts.Select(f => f.Fact).ToList();
        var embeddings = await _embedder.EmbedBatchAsync(factTexts);

        await _lock.WaitAsync();
        try
        {
            var userFacts = _index.GetOrAdd(userId, _ => []);

            for (int i = 0; i < extractedFacts.Count; i++)
            {
                var extracted = extractedFacts[i];
                var embedding = embeddings[i];

                // Only search within same scope for conflicts
                var scopedFacts = userFacts.Where(f => f.Scope == extracted.Scope).ToList();
                var bestMatch = FindBestMatch(scopedFacts, embedding);

                if (bestMatch.score >= _conflictThreshold && bestMatch.fact is not null)
                {
                    // Deterministic guard: different entity IDs are always separate entities.
                    // Skip the LLM resolver — prevents "Walmart Inc." (TPID:784852) from being
                    // merged with "Walmart Canada Bank" (TPID:902415), or "Azure Data Box" from
                    // being merged with "Azure Data Explorer".
                    if (HasDifferentEntityIds(extracted, bestMatch.fact))
                    {
                        userFacts.Add(CreateFact(userId, extracted, embedding));
                        _logger.LogInformation("[Memory:ADD] [{Scope}] '{Fact}' (different entity ID from nearest match)",
                            extracted.Scope, extracted.Fact);
                        continue;
                    }

                    var resolution = await _resolver.ResolveAsync(extracted.Fact, bestMatch.fact.Fact);

                    switch (resolution.Operation)
                    {
                        case MemoryOperationType.Update:
                            bestMatch.fact.Fact = resolution.MergedFact;
                            bestMatch.fact.Category = resolution.Category;
                            bestMatch.fact.Embedding = embedding;
                            bestMatch.fact.UpdatedAt = DateTimeOffset.UtcNow;
                            // Preserve original expiry: compute remaining TTL from CreatedAt
                            if (resolution.Category == FactCategory.Temporal)
                            {
                                var elapsed = (int)(DateTimeOffset.UtcNow - bestMatch.fact.CreatedAt).TotalSeconds;
                                bestMatch.fact.TimeToLive = Math.Max(60, TemporalTtlSeconds - elapsed);
                            }
                            _logger.LogInformation("[Memory:UPDATE] [{Scope}] '{Old}' → '{New}'",
                                extracted.Scope, bestMatch.fact.Fact, resolution.MergedFact);
                            break;

                        case MemoryOperationType.Add:
                            userFacts.Add(CreateFact(userId, extracted, embedding));
                            _logger.LogInformation("[Memory:ADD] [{Scope}] '{Fact}' (conflict resolved as separate)",
                                extracted.Scope, extracted.Fact);
                            break;

                        case MemoryOperationType.Noop:
                            _logger.LogDebug("[Memory:NOOP] [{Scope}] '{Fact}'", extracted.Scope, extracted.Fact);
                            break;
                    }
                }
                else
                {
                    userFacts.Add(CreateFact(userId, extracted, embedding));
                    _logger.LogInformation("[Memory:ADD] [{Scope}] '{Fact}'", extracted.Scope, extracted.Fact);
                }
            }

            await SaveToFileAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
            await InitializeAsync();
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
            EntityId = extracted.EntityId
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

    private static (MemoryFact? fact, float score) FindBestMatch(List<MemoryFact> facts, float[] embedding)
    {
        MemoryFact? best = null;
        float bestScore = 0f;

        foreach (var fact in facts)
        {
            if (fact.Embedding.Length == 0)
                continue;

            var score = MemoryEmbedder.CosineSimilarity(embedding, fact.Embedding);
            if (score > bestScore)
            {
                bestScore = score;
                best = fact;
            }
        }

        return (best, bestScore);
    }

    private void LoadFromFile()
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogInformation("No memory file found at '{Path}', starting with empty store", _filePath);
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var facts = JsonSerializer.Deserialize<List<MemoryFact>>(json, s_jsonOptions) ?? [];
            foreach (var group in facts.GroupBy(f => f.UserId, StringComparer.OrdinalIgnoreCase))
            {
                _index[group.Key] = group.ToList();
            }
            _logger.LogInformation("Loaded {Count} facts from '{Path}'", facts.Count, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load memory from '{Path}'", _filePath);
        }
    }

    /// <summary>
    /// Rebuild embeddings for any facts loaded from file that have empty embedding vectors.
    /// </summary>
    private async Task RebuildEmbeddingsAsync()
    {
        var factsNeedingEmbeddings = _index.Values
            .SelectMany(facts => facts)
            .Where(f => f.Embedding.Length == 0)
            .ToList();

        if (factsNeedingEmbeddings.Count == 0)
        {
            _logger.LogDebug("All facts have embeddings, no rebuild needed");
            return;
        }

        _logger.LogInformation("Rebuilding embeddings for {Count} facts", factsNeedingEmbeddings.Count);

        // Batch in chunks of 100 to avoid token limits
        const int batchSize = 100;
        for (int i = 0; i < factsNeedingEmbeddings.Count; i += batchSize)
        {
            var batch = factsNeedingEmbeddings.Skip(i).Take(batchSize).ToList();
            var texts = batch.Select(f => f.Fact).ToList();
            var embeddings = await _embedder.EmbedBatchAsync(texts);

            for (int j = 0; j < batch.Count; j++)
            {
                batch[j].Embedding = embeddings[j];
            }
        }

        await SaveToFileAsync();
        _logger.LogInformation("Embedding rebuild complete");
    }

    private async Task SaveToFileAsync()
    {
        var allFacts = _index.Values.SelectMany(f => f).ToList();
        var json = JsonSerializer.Serialize(allFacts, s_jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
