namespace AIM.Storage.Entities;

public sealed class MemoryRecordEntity
{
    public Guid Id { get; set; }

    public Guid PersonalityId { get; set; }

    public Guid MemorySetId { get; set; }

    public string Content { get; set; } = string.Empty;

    public string Source { get; set; } = "manual";

    public DateTimeOffset CreatedAt { get; set; }
}
