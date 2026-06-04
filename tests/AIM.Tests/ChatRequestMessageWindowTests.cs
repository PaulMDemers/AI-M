using AIM.Core.Chat;

namespace AIM.Tests;

public sealed class ChatRequestMessageWindowTests
{
    [Fact]
    public void KeepsAllMessagesWhenNoConversationSummaryExists()
    {
        var messages = CreateMessages(10);

        var selected = ChatRequestMessageWindow.Select(messages, ChatContext.Empty, maxVisibleMessagesWithSummary: 3);

        Assert.Equal(messages.Count, selected.Count);
    }

    [Fact]
    public void KeepsRecentVisibleMessagesWhenConversationSummaryExists()
    {
        var messages = CreateMessages(8);
        var context = new ChatContext([], conversationSummary: "Earlier conversation summary.");

        var selected = ChatRequestMessageWindow.Select(messages, context, maxVisibleMessagesWithSummary: 3);

        Assert.Equal(3, selected.Count(message => message.Role is ChatRole.User or ChatRole.Assistant));
        Assert.DoesNotContain(selected, message => message.Content == "message-0");
        Assert.Contains(selected, message => message.Content == "message-7");
    }

    [Fact]
    public void KeepsRecentToolMessagesInsideSelectedWindow()
    {
        var conversationId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Now;
        var messages = new List<ChatMessage>
        {
            new(Guid.NewGuid(), conversationId, ChatRole.User, "old", createdAt),
            new(Guid.NewGuid(), conversationId, ChatRole.Assistant, "recent-1", createdAt.AddMinutes(1)),
            new(Guid.NewGuid(), conversationId, ChatRole.Tool, "recent-tool", createdAt.AddMinutes(2)),
            new(Guid.NewGuid(), conversationId, ChatRole.User, "recent-2", createdAt.AddMinutes(3))
        };
        var context = new ChatContext([], conversationSummary: "Earlier conversation summary.");

        var selected = ChatRequestMessageWindow.Select(messages, context, maxVisibleMessagesWithSummary: 2);

        Assert.DoesNotContain(selected, message => message.Content == "old");
        Assert.Contains(selected, message => message.Content == "recent-tool");
    }

    private static IReadOnlyList<ChatMessage> CreateMessages(int count)
    {
        var conversationId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Now;

        return Enumerable
            .Range(0, count)
            .Select(index => new ChatMessage(
                Guid.NewGuid(),
                conversationId,
                index % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
                $"message-{index}",
                createdAt.AddMinutes(index)))
            .ToArray();
    }
}
