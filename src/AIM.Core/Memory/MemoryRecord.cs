namespace AIM.Core.Memory;

public sealed class MemoryRecord
{
    public MemoryRecord(Guid id, Guid personalityId, string content, DateTimeOffset createdAt)
    {
        Id = id;
        PersonalityId = personalityId;
        Content = content;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid PersonalityId { get; }

    public string Content { get; }

    public DateTimeOffset CreatedAt { get; }
}
