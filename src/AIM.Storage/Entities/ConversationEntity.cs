namespace AIM.Storage.Entities;

public sealed class ConversationEntity
{
    public Guid Id { get; set; }

    public Guid PersonalityId { get; set; }

    public Guid GroupId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset? SummaryUpdatedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
}
