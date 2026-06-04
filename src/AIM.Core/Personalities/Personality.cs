namespace AIM.Core.Personalities;

public sealed class Personality
{
    public Personality(
        Guid id,
        string displayName,
        string status,
        string avatarText,
        string systemPrompt,
        Guid memorySetId,
        string providerKey,
        string modelId,
        string avatarImagePath = "",
        string category = "My Contacts")
    {
        Id = id;
        DisplayName = displayName;
        Status = status;
        AvatarText = avatarText;
        SystemPrompt = systemPrompt;
        MemorySetId = memorySetId;
        ProviderKey = providerKey;
        ModelId = modelId;
        AvatarImagePath = avatarImagePath;
        Category = string.IsNullOrWhiteSpace(category) ? "My Contacts" : category;
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public string Status { get; }

    public string AvatarText { get; }

    public string SystemPrompt { get; }

    public Guid MemorySetId { get; }

    public string ProviderKey { get; }

    public string ModelId { get; }

    public string AvatarImagePath { get; }

    public string Category { get; }
}
