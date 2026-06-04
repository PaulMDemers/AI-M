namespace AIM.Core.Personalities;

public sealed record PersonalityTemplate(
    string Key,
    string DisplayName,
    string Description,
    string Status,
    string AvatarText,
    string AvatarImagePath,
    string Category,
    string SystemPrompt,
    string ProviderKey = "openai",
    string ModelId = "gpt-4.1-mini");
