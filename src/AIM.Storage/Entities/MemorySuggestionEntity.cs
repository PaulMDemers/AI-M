namespace AIM.Storage.Entities;

public sealed class MemorySuggestionEntity
{
    public Guid Id { get; set; }

    public Guid PersonalityId { get; set; }

    public Guid ConversationId { get; set; }

    public string Content { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}
