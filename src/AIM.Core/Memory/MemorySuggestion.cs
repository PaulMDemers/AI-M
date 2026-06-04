namespace AIM.Core.Memory;

public sealed class MemorySuggestion
{
    public MemorySuggestion(
        Guid id,
        Guid personalityId,
        Guid conversationId,
        string content,
        string status,
        DateTimeOffset createdAt)
    {
        Id = id;
        PersonalityId = personalityId;
        ConversationId = conversationId;
        Content = content;
        Status = status;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid PersonalityId { get; }

    public Guid ConversationId { get; }

    public string Content { get; }

    public string Status { get; }

    public DateTimeOffset CreatedAt { get; }
}
