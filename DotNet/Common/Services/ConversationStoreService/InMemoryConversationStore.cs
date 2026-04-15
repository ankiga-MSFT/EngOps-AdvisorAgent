namespace CXOAI.ConversationStore;

/// <summary>
/// In-memory implementation of session-scoped conversation store.
/// Used for local development and testing. Data is lost on restart.
/// </summary>
public class InMemoryConversationStore : IConversationStore
{
    private readonly Dictionary<string, string> _summaryStore = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ConversationTurnEntry>> _historyStore = new(StringComparer.OrdinalIgnoreCase);

    public Task<string?> GetSessionSummaryAsync(string userId, string sessionId)
    {
        return _summaryStore.TryGetValue(BuildKey(userId, sessionId), out var summary)
            ? Task.FromResult<string?>(summary)
            : Task.FromResult<string?>(null);
    }

    public Task UpsertSessionSummaryAsync(string userId, string sessionId, string summary)
    {
        _summaryStore[BuildKey(userId, sessionId)] = summary;
        return Task.CompletedTask;
    }

    public Task AppendToHistoryAsync(string userId, string sessionId, string prompt, string response, string? requestId = null)
    {
        var key = BuildKey(userId, sessionId);
        if (!_historyStore.TryGetValue(key, out var history))
        {
            history = [];
            _historyStore[key] = history;
        }

        history.Add(new ConversationTurnEntry
        {
            Prompt = prompt,
            Response = response,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            RequestId = requestId ?? string.Empty
        });
        return Task.CompletedTask;
    }

    public Task<List<ConversationTurnEntry>?> GetSessionHistoryAsync(string userId, string sessionId, int? lastN = null)
    {
        if (!_historyStore.TryGetValue(BuildKey(userId, sessionId), out var history) || history.Count == 0)
            return Task.FromResult<List<ConversationTurnEntry>?>(null);

        var result = lastN.HasValue && lastN.Value > 0 && lastN.Value < history.Count
            ? history.Skip(history.Count - lastN.Value).ToList()
            : history;

        return Task.FromResult<List<ConversationTurnEntry>?>(result);
    }

    private static string BuildKey(string userId, string sessionId) => $"{userId}::{sessionId}";
}
