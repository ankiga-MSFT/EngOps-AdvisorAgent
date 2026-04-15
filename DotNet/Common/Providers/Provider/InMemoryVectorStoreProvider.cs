// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Provider.Interfaces;
using Provider.Model;

namespace Provider;


public sealed class InMemoryVectorStoreProvider<TVectorStoreModel> : IInMemoryVectorStoreProvider<TVectorStoreModel>
    where TVectorStoreModel : class
{
    private readonly InMemoryVectorStore _vectorStore;
    private readonly InMemoryCollection<string, TVectorStoreModel> _collection;
    private readonly ILogger<IInMemoryVectorStoreProvider<TVectorStoreModel>> _logger;
    private readonly string _collectionName;
    private bool _disposed;

    public InMemoryVectorStoreProvider(InMemoryVectorStoreOptions options, string collectionName, ILogger<IInMemoryVectorStoreProvider<TVectorStoreModel>> logger)
        : this(new InMemoryVectorStore(options), collectionName, logger)
    {
    }

    public InMemoryVectorStoreProvider(InMemoryVectorStore vectorStore, string collectionName, ILogger<IInMemoryVectorStoreProvider<TVectorStoreModel>> logger)
    {
        this._vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        if (string.IsNullOrWhiteSpace(collectionName)) throw new ArgumentException("collectionName required", nameof(collectionName));

        this._logger = logger;
        this._collectionName = collectionName;

        this._collection = _vectorStore.GetCollection<string, TVectorStoreModel>(collectionName);
    }

    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        await this._collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Indexes (upserts) records providing raw text that the store will convert into vectors
    /// using the configured embedding generator.
    /// </summary>
    public async Task IndexRecordsAsync(IEnumerable<TVectorStoreModel> records, int batchSize = 500, CancellationToken ct = default)
    {
        this.ThrowIfDisposed();
        if (records is null) throw new ArgumentNullException(nameof(records));

        int total = 0;

        this._logger.LogInformation($"Indexing records into in-memory collection '{this._collectionName}'...");

        Stopwatch sw = Stopwatch.StartNew();

        await this._collection.EnsureCollectionExistsAsync(ct).ConfigureAwait(false);

        var batch = new List<TVectorStoreModel>(batchSize);
        foreach (var item in records)
        {
            ct.ThrowIfCancellationRequested();
            if (item is null) continue;

            batch.Add(item);
            if (batch.Count >= batchSize)
            {
                await this.ProcessWithRetryAsync(
                    (ct) => this._collection.UpsertAsync(batch, ct),
                    "In-memory vector store upsert operation",
                    ct: ct).ConfigureAwait(false);

                total += batch.Count;
                this._logger.LogInformation($"Upserted {batch.Count} records (total {total}) into in-memory '{this._collectionName}' collection .");
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await this.ProcessWithRetryAsync(
                    (ct) => this._collection.UpsertAsync(batch, ct),
                    "In-memory vector store upsert operation",
                    ct: ct).ConfigureAwait(false);

            total += batch.Count;
            this._logger.LogInformation($"Upserted {batch.Count} records (total {total}) into in-memory collection '{this._collectionName}'.");
        }

        this._logger.LogInformation($"Indexed {total} records into in-memory '{this._collectionName}' collection  in {sw.ElapsedMilliseconds}ms.");
    }

    /// <summary>
    /// Search by vector or free-text query. Returns ordered id + score pairs (highest relevance first).
    /// When a string query is provided, the InMemoryVectorStore will vectorize it using the configured embedding generator.
    /// When a vector is provided, it will be used directly for similarity search.
    /// </summary>
    public async Task<IReadOnlyList<(TVectorStoreModel Record, double Score)>> SearchByTextAsync<TQuery>(TQuery query, int top = 5, CancellationToken ct = default)
        where TQuery : notnull
    {
        this.ThrowIfDisposed();
        if (query is null) return [];
        
        // Handle string queries that are empty or whitespace
        if (query is string stringQuery && string.IsNullOrWhiteSpace(stringQuery)) return [];

        this._logger.LogInformation($"Searching in-memory collection '{this._collectionName}' for top {top} results matching query: {query}");

        Stopwatch sw = Stopwatch.StartNew();

        var results = await ProcessWithRetryAsync((ct) => 
                        _collection.SearchAsync(query, top, null, ct)
                        .ToListAsync(ct)
                        .AsTask(),
                        "In-memory vector store search operation",
                        ct: ct).ConfigureAwait(false);

        var outList = new List<(TVectorStoreModel Record, double Score)>(results.Count);
        foreach (var r in results)
        {
            // r.Record is expected to be compatible with TVectorStoreModel
            outList.Add((r.Record, r.Score ?? 0));
        }

        this._logger.LogInformation($"Search for '{query}' returned {outList.Count} results in {sw.ElapsedMilliseconds}ms.");

        return outList;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        this.ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

        this._logger.LogInformation($"Deleting record with key '{key}' from in-memory collection '{this._collectionName}'...");

        await this._collection.EnsureCollectionExistsAsync(ct).ConfigureAwait(false);
        await this.ProcessWithRetryAsync(
            (ct) => this._collection.DeleteAsync(key, ct),
            "In-memory vector store delete operation",
            ct: ct).ConfigureAwait(false);

        this._logger.LogInformation($"Deleted record with key '{key}' from in-memory collection '{this._collectionName}'.");
    }

    public async Task DeleteAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        this.ThrowIfDisposed();
        if (keys is null) throw new ArgumentNullException(nameof(keys));

        var keyList = keys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        if (keyList.Count == 0) return;

        var keyslog = string.Join(", ", keyList);
        this._logger.LogInformation($"Deleting {keyList.Count}: {keyslog} records from in-memory collection '{this._collectionName}'...");

        await this._collection.EnsureCollectionExistsAsync(ct).ConfigureAwait(false);
        await this.ProcessWithRetryAsync(
            (ct) => this._collection.DeleteAsync(keyList, ct),
            "In-memory vector store batch delete operation",
            ct: ct).ConfigureAwait(false);

        this._logger.LogInformation($"Deleted {keyList.Count}: {keyslog} records from in-memory collection '{this._collectionName}'.");
    }

    /// <summary>
    /// Deletes all records from the in-memory collection.
    /// This method enumerates the collection to extract record ids and deletes them in batches.
    /// </summary>
    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        this.ThrowIfDisposed();

        await this._collection.EnsureCollectionExistsAsync(ct).ConfigureAwait(false);

        // Try to fetch all records. Use a generous page size and rely on the collection's streaming implementation.
        const int fetchTop = 10000; // should be large enough for most collections; adjust if necessary

        var results = await this.SearchByTextAsync("*", fetchTop, ct).ConfigureAwait(false);

        var ids = new List<string>(results.Count);
        foreach (var r in results)
        {
            if (r.Record == null) continue;

            string? id = null;

            // Try dictionary-like access first
            if (r.Record is System.Collections.IDictionary dict)
            {
                if (dict.Contains("Id"))
                {
                    var val = dict["Id"];
                    id = val?.ToString();
                }
            }
            else if (r.Record is InMemoryVectorStoreRecord inMemoryVectorStoreRecord)
            {
                id = inMemoryVectorStoreRecord.Id;
            }
            else
            {
                // Fallback to reflection to find an `Id` property
                var prop = r.Record.GetType().GetProperty("Id");
                if (prop != null)
                {
                    id = prop.GetValue(r.Record)?.ToString();
                }
            }

            if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
        }

        if (ids.Count == 0)
        {
            this._logger.LogInformation($"No records found in in-memory collection '{this._collectionName}' to delete.");
            return;
        }

        // Delete in batches using the existing batch delete method
        const int batchSize = 500;
        int totalDeleted = 0;
        for (int i = 0; i < ids.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = ids.Skip(i).Take(batchSize).ToList();
            await this.DeleteAsync(batch, ct).ConfigureAwait(false);
            totalDeleted += batch.Count;
            this._logger.LogInformation($"Deleted batch of {batch.Count} records (total deleted {totalDeleted}) from in-memory collection '{this._collectionName}'.");
        }

        this._logger.LogInformation($"Deleted all {totalDeleted} records from in-memory collection '{this._collectionName}'.");
    }

    public void Dispose()
    {
        if (this._disposed) return;
        // nothing to dispose on InMemoryVectorStore itself; clear references
        this._disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Executes the provided async operation with retry and exponential backoff.
    /// The operation receives the current <see cref="CancellationToken"/> and should honour it.
    /// On transient failures the operation is retried up to <paramref name="maxRetries"/> times.
    /// </summary>
    private async Task ProcessWithRetryAsync(Func<CancellationToken, Task> operation, string operationDescription, int maxRetries = 3, CancellationToken ct = default)
    {
        if (operation is null) throw new ArgumentNullException(nameof(operation));

        const int baseDelayMs = 200;
        const int maxJitterMs = 100;

        int attempt = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var sw = Stopwatch.StartNew();
                await operation(ct).ConfigureAwait(false);
                sw.Stop();
                _logger.LogInformation($"{operationDescription} completed in {sw.ElapsedMilliseconds}ms.");
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                attempt++;
                if (attempt > maxRetries)
                {
                    _logger.LogError(ex, $"{operationDescription} failed after {attempt - 1} attempts.");
                    throw;
                }

                var backoffMs = (int)(baseDelayMs * Math.Pow(2, attempt - 1));
                var jitter = (int)(Random.Shared.NextDouble() * maxJitterMs);
                var delay = Math.Min(backoffMs + jitter, 8000);

                _logger.LogWarning(ex, $"Transient error executing {operationDescription} (attempt {attempt}/{maxRetries}). Retrying after {delay}ms...");
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Executes the provided async operation that returns a value with retry and exponential backoff.
    /// </summary>
    private async Task<T> ProcessWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation, string operationDescription, int maxRetries = 3, CancellationToken ct = default)
    {
        if (operation is null) throw new ArgumentNullException(nameof(operation));

        const int baseDelayMs = 200;
        const int maxJitterMs = 100;

        int attempt = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await operation(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                attempt++;
                if (attempt > maxRetries)
                {
                    _logger.LogError(ex, $"{operationDescription} failed after {attempt - 1} attempts.");
                    throw;
                }

                var backoffMs = (int)(baseDelayMs * Math.Pow(2, attempt - 1));
                var jitter = (int)(Random.Shared.NextDouble() * maxJitterMs);
                var delay = Math.Min(backoffMs + jitter, 8000);

                _logger.LogWarning(ex, $"Transient error executing {operationDescription} (attempt {attempt}/{maxRetries}). Retrying after {delay}ms...");
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InMemoryVectorStoreProvider<TVectorStoreModel>));
    }
}
