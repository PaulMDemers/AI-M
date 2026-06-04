namespace AIM.Storage.Entities;

public sealed class ConversationGroupEntity
{
    public Guid Id { get; set; }

    public Guid PersonalityId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
}
