namespace AIM.Core.Chat;

public sealed class ConversationGroup
{
    public ConversationGroup(Guid id, Guid personalityId, string title, DateTimeOffset createdAt)
    {
        Id = id;
        PersonalityId = personalityId;
        Title = title;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid PersonalityId { get; }

    public string Title { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public void Rename(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }
    }
}
