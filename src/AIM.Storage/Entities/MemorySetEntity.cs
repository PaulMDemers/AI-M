namespace AIM.Storage.Entities;

public sealed class MemorySetEntity
{
    public Guid Id { get; set; }

    public Guid PersonalityId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string VectorCollectionName { get; set; } = string.Empty;
}
