namespace CXOAI.ConversationStore;

public class ConversationSummaryEntry
{
    public required string UserId { get; init; }
    public required string Summary { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// A single turn in the conversation history (user prompt + assistant response).
/// </summary>
public class ConversationTurnEntry
{
    public string Prompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    /// <summary>Unique identifier for the request that produced this turn. Enables per-request log correlation.</summary>
    public string RequestId { get; set; } = string.Empty;
}

/// <summary>
/// Short-term conversation memory scoped by session.
/// Stores a rolling summary (overwritten each turn) and an append-only raw history.
/// </summary>
public interface IConversationStore
{
    /// <summary>
    /// Get the rolling summary for a specific session.
    /// Returns null if no summary exists (first turn).
    /// </summary>
    Task<string?> GetSessionSummaryAsync(string userId, string sessionId);

    /// <summary>
    /// Create or replace the rolling summary for a session.
    /// </summary>
    Task UpsertSessionSummaryAsync(string userId, string sessionId, string summary);

    /// <summary>
    /// Append a user prompt + assistant response to the raw conversation history.
    /// </summary>
    Task AppendToHistoryAsync(string userId, string sessionId, string prompt, string response, string? requestId = null);

    /// <summary>
    /// Get the raw conversation history for a session.
    /// When <paramref name="lastN"/> is specified, returns only the most recent N turns.
    /// Returns null if no history exists (first turn).
    /// </summary>
    Task<List<ConversationTurnEntry>?> GetSessionHistoryAsync(string userId, string sessionId, int? lastN = null);
}
