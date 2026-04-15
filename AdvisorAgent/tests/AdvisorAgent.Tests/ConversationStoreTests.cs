using AdvisorAgent.Core.Conversation;
using AdvisorAgent.Core.Models;
using Xunit;

namespace AdvisorAgent.Tests;

public class ConversationStoreTests
{
    private const string UserId = "user1";

    [Fact]
    public async Task AppendAndRetrieve_ReturnsCorrectTurns()
    {
        var store = new InMemoryConversationStore();
        var sessionId = "test-session";

        await store.AppendTurnAsync(UserId, sessionId, new ConversationTurn
        {
            Prompt = "Hello",
            Response = "Hi there",
            RequestId = "req1"
        });

        await store.AppendTurnAsync(UserId, sessionId, new ConversationTurn
        {
            Prompt = "Help me with costs",
            Response = "Here are cost recommendations...",
            RequestId = "req2"
        });

        var turns = await store.GetRecentTurnsAsync(UserId, sessionId, 10);

        Assert.Equal(2, turns.Count);
        Assert.Equal("Hello", turns[0].Prompt);
        Assert.Equal("Help me with costs", turns[1].Prompt);
    }

    [Fact]
    public async Task GetRecentTurns_RespectsMaxCount()
    {
        var store = new InMemoryConversationStore();
        var sessionId = "test-session";

        for (int i = 0; i < 5; i++)
        {
            await store.AppendTurnAsync(UserId, sessionId, new ConversationTurn
            {
                Prompt = $"Prompt {i}",
                Response = $"Response {i}",
                RequestId = $"req{i}"
            });
        }

        var turns = await store.GetRecentTurnsAsync(UserId, sessionId, 3);

        Assert.Equal(3, turns.Count);
        // Should return most recent 3
        Assert.Equal("Prompt 2", turns[0].Prompt);
        Assert.Equal("Prompt 4", turns[2].Prompt);
    }

    [Fact]
    public async Task GetRecentTurns_EmptySession_ReturnsEmpty()
    {
        var store = new InMemoryConversationStore();
        var turns = await store.GetRecentTurnsAsync(UserId, "nonexistent", 5);

        Assert.Empty(turns);
    }
}
