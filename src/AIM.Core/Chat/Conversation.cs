namespace AIM.Core.Chat;

public sealed class Conversation
{
    public Conversation(
        Guid id,
        Guid personalityId,
        Guid groupId,
        string title,
        DateTimeOffset createdAt,
        string summary = "",
        DateTimeOffset? summaryUpdatedAt = null)
    {
        Id = id;
        PersonalityId = personalityId;
        GroupId = groupId;
        Title = title;
        CreatedAt = createdAt;
        Summary = summary;
        SummaryUpdatedAt = summaryUpdatedAt;
    }

    public Guid Id { get; }

    public Guid PersonalityId { get; }

    public Guid GroupId { get; }

    public string Title { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public string Summary { get; private set; }

    public DateTimeOffset? SummaryUpdatedAt { get; private set; }

    public string CreatedAtLabel => CreatedAt.ToString("g");

    public void Rename(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }
    }

    public void UpdateSummary(string summary, DateTimeOffset updatedAt)
    {
        Summary = summary.Trim();
        SummaryUpdatedAt = updatedAt;
    }
}
