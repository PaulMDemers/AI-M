namespace AIM.Core.Personalities;

public sealed record PersonalityDraft(
    Guid? Id,
    string DisplayName,
    string Status,
    string AvatarText,
    string SystemPrompt,
    string ProviderKey,
    string ModelId,
    string AvatarImagePath = "",
    string Category = "My Contacts");
