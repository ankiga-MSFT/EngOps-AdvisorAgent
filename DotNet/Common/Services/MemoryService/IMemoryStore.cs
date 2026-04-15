namespace CXOAI.Memory;

/// <summary>
/// Long-term memory store using mem0-style fact extraction, embedding, and semantic recall.
/// Supports scoped facts: User (preferences), Org (shared data cache), System (reserved).
/// </summary>
public interface IMemoryStore
{
    /// <summary>
    /// Extract facts from conversation content and upsert into the store.
    /// Uses LLM to extract facts, embeds each, and resolves conflicts (ADD/UPDATE/NOOP).
    /// </summary>
    Task ExtractAndStoreAsync(string userId, string conversationContent, MemoryScope scope = MemoryScope.User);

    /// <summary>
    /// Store pre-built facts directly (e.g., system configurations parsed at startup).
    /// Skips LLM extraction — caller provides the facts.
    /// </summary>
    Task StoreFactsAsync(string userId, List<ExtractedFact> facts);

    /// <summary>
    /// Recall relevant facts for a user given a query.
    /// Embeds the query and performs vector similarity search filtered by userId and optional scope.
    /// </summary>
    Task<List<MemoryFact>> RecallAsync(string userId, string query, int topK = 10, float minScore = 0.7f, MemoryScope? scope = null);

    /// <summary>
    /// Delete a specific fact by ID.
    /// </summary>
    Task ForgetAsync(string userId, string factId);

    /// <summary>
    /// Get all facts for a user, optionally filtered by scope.
    /// </summary>
    Task<List<MemoryFact>> GetAllFactsAsync(string userId, MemoryScope? scope = null);
}
