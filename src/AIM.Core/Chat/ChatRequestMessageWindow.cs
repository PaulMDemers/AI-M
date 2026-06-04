namespace AIM.Core.Chat;

public static class ChatRequestMessageWindow
{
    public const int DefaultMaxVisibleMessagesWithSummary = 40;

    public static IReadOnlyList<ChatMessage> Select(
        IReadOnlyList<ChatMessage> messages,
        ChatContext context,
        int maxVisibleMessagesWithSummary = DefaultMaxVisibleMessagesWithSummary)
    {
        if (!context.HasConversationSummary || maxVisibleMessagesWithSummary <= 0)
        {
            return messages;
        }

        var visibleMessages = messages
            .Where(IsVisibleMessage)
            .TakeLast(maxVisibleMessagesWithSummary)
            .ToArray();

        if (visibleMessages.Length == 0)
        {
            return messages.Where(message => message.Role == ChatRole.Tool).ToArray();
        }

        var firstVisibleMessage = visibleMessages[0];
        var visibleIds = visibleMessages
            .Select(message => message.Id)
            .ToHashSet();

        return messages
            .Where(message =>
                visibleIds.Contains(message.Id) ||
                (message.Role == ChatRole.Tool && message.CreatedAt >= firstVisibleMessage.CreatedAt))
            .ToArray();
    }

    private static bool IsVisibleMessage(ChatMessage message)
    {
        return message.Role is ChatRole.User or ChatRole.Assistant;
    }
}
