using AdvisorAgent.Core.Models;

namespace AdvisorAgent.Core.Conversation;

public interface IConversationStore
{
    Task AppendTurnAsync(string userId, string sessionId, ConversationTurn turn);
    Task<List<ConversationTurn>> GetRecentTurnsAsync(string userId, string sessionId, int count = 5);
}
