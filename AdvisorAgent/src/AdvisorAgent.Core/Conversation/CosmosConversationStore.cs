using System.Net;
using System.Text.Json;
using AdvisorAgent.Core.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace AdvisorAgent.Core.Conversation;

/// <summary>
/// Cosmos DB-backed conversation store.
/// Stores one document per session (id={sessionId}_history, partitionKey=/SessionionId)
/// containing an append-only array of conversation turns.
/// </summary>
public sealed class CosmosConversationStore : IConversationStore
{
    private readonly Container _container;
    private readonly ILogger<CosmosConversationStore> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public CosmosConversationStore(Container container, ILogger<CosmosConversationStore> logger)
    {
        _container = container;
        _logger = logger;
    }

    public async Task AppendTurnAsync(string userId, string sessionId, ConversationTurn turn)
    {
        var docId = $"{sessionId}_history";
        var partitionKey = new PartitionKey(sessionId);

        try
        {
            // Try to read existing document
            var response = await _container.ReadItemAsync<ConversationHistoryDocument>(docId, partitionKey);
            var doc = response.Resource;

            doc.Turns.Add(new ConversationTurnRecord
            {
                Prompt = turn.Prompt,
                Response = turn.Response,
                Timestamp = turn.Timestamp.ToString("o"),
                RequestId = turn.RequestId
            });
            doc.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");

            await _container.ReplaceItemAsync(doc, docId, partitionKey);
            _logger.LogInformation("[CosmosConversation] Appended turn to session '{SessionId}' for user '{UserId}' ({TurnCount} turns)",
                sessionId, userId, doc.Turns.Count);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // First turn — create new document
            var doc = new ConversationHistoryDocument
            {
                Id = docId,
                UserId = userId,
                SessionId = sessionId,
                Turns =
                [
                    new ConversationTurnRecord
                    {
                        Prompt = turn.Prompt,
                        Response = turn.Response,
                        Timestamp = turn.Timestamp.ToString("o"),
                        RequestId = turn.RequestId
                    }
                ],
                UpdatedAt = DateTimeOffset.UtcNow.ToString("o")
            };

            await _container.CreateItemAsync(doc, partitionKey);
            _logger.LogInformation("[CosmosConversation] Created history document for session '{SessionId}', user '{UserId}'",
                sessionId, userId);
        }
    }

    public async Task<List<ConversationTurn>> GetRecentTurnsAsync(string userId, string sessionId, int count = 5)
    {
        var docId = $"{sessionId}_history";
        var partitionKey = new PartitionKey(sessionId);

        try
        {
            var response = await _container.ReadItemAsync<ConversationHistoryDocument>(docId, partitionKey);
            var doc = response.Resource;

            var turns = doc.Turns
                .Skip(Math.Max(0, doc.Turns.Count - count))
                .Select(t => new ConversationTurn
                {
                    Prompt = t.Prompt,
                    Response = t.Response,
                    RequestId = t.RequestId,
                    Timestamp = DateTimeOffset.TryParse(t.Timestamp, out var ts) ? ts : DateTimeOffset.UtcNow
                })
                .ToList();

            _logger.LogDebug("[CosmosConversation] Retrieved {Count}/{Total} turns for session '{SessionId}'",
                turns.Count, doc.Turns.Count, sessionId);
            return turns;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("[CosmosConversation] No history found for session '{SessionId}' (first turn)", sessionId);
            return [];
        }
    }

    // ── Internal document models ─────────────────────────

    private sealed class ConversationHistoryDocument
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("SessionId")]
        public string SessionId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("turns")]
        public List<ConversationTurnRecord> Turns { get; set; } = [];

        [System.Text.Json.Serialization.JsonPropertyName("updatedAt")]
        public string UpdatedAt { get; set; } = string.Empty;
    }

    private sealed class ConversationTurnRecord
    {
        [System.Text.Json.Serialization.JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("requestId")]
        public string RequestId { get; set; } = string.Empty;
    }
}
