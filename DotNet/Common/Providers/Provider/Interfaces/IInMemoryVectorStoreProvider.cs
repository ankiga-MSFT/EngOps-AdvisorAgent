// Copyright (c) Microsoft. All rights reserved.

namespace Provider.Interfaces
{
    /// <summary>
    /// Manager that uses <see cref="InMemoryVectorStore"/> and an embedded
    /// embedding generator (supplied via <see cref="InMemoryVectorStoreOptions"/>)
    /// to index text and run similarity searches.
    /// - The InMemory store will be responsible for text-&gt;vector conversion.
    /// - Store property "Embedding" is kept as string so the provider's embedding
    ///   generator can be used when indexing and searching.
    /// </summary>
    public interface IInMemoryVectorStoreProvider<TVectorStoreModel> : IDisposable
        where TVectorStoreModel : class
    {
        Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default);

        Task IndexRecordsAsync(IEnumerable<TVectorStoreModel> records, int batchSize = 500, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<(TVectorStoreModel Record, double Score)>> SearchByTextAsync<TQuery>(TQuery query, int top = 5, CancellationToken cancellationToken = default) where TQuery : notnull;

        Task DeleteAsync(string key, CancellationToken cancellationToken = default);

        Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

        Task DeleteAllAsync(CancellationToken cancellationToken = default);
    }
}
