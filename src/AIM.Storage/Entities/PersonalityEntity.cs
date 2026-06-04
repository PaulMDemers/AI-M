namespace AIM.Storage.Entities;

public sealed class PersonalityEntity
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string AvatarText { get; set; } = string.Empty;

    public string AvatarImagePath { get; set; } = string.Empty;

    public string Category { get; set; } = "My Contacts";

    public string SystemPrompt { get; set; } = string.Empty;

    public Guid MemorySetId { get; set; }

    public Guid DefaultProviderAccountId { get; set; }

    public string DefaultModelId { get; set; } = string.Empty;
}
