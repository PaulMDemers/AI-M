namespace AIM.Core.Chat;

public sealed class ChatMessage
{
    public ChatMessage(Guid id, Guid conversationId, ChatRole role, string content, DateTimeOffset createdAt)
    {
        Id = id;
        ConversationId = conversationId;
        Role = role;
        Content = content;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid ConversationId { get; }

    public ChatRole Role { get; }

    public string Content { get; }

    public DateTimeOffset CreatedAt { get; }
}
