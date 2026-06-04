namespace AIM.Storage.Entities;

public sealed class MessageEntity
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? ProviderMessageId { get; set; }

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
