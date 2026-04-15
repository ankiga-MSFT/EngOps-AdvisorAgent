using System.Collections.Concurrent;
using AdvisorAgent.Core.Models;

namespace AdvisorAgent.Core.Conversation;

/// <summary>
/// In-memory conversation store suitable for local development and skeleton testing.
/// Replace with CosmosDB or Redis-backed implementation for production.
/// </summary>
public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, List<ConversationTurn>> _sessions = new();

    public Task AppendTurnAsync(string userId, string sessionId, ConversationTurn turn)
    {
        var key = BuildKey(userId, sessionId);
        _sessions.AddOrUpdate(
            key,
            _ => [turn],
            (_, existing) => { lock (existing) { existing.Add(turn); } return existing; });
        return Task.CompletedTask;
    }

    public Task<List<ConversationTurn>> GetRecentTurnsAsync(string userId, string sessionId, int count = 5)
    {
        var key = BuildKey(userId, sessionId);
        if (_sessions.TryGetValue(key, out var turns))
        {
            lock (turns)
            {
                var recent = turns.Count <= count ? new List<ConversationTurn>(turns) : turns.GetRange(turns.Count - count, count);
                return Task.FromResult(recent);
            }
        }
        return Task.FromResult(new List<ConversationTurn>());
    }

    private static string BuildKey(string userId, string sessionId) => $"{userId}:{sessionId}";
}
