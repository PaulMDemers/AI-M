using AIM.Core.Personalities;

namespace AIM.Desktop.Wpf.ViewModels;

public sealed class PersonalityTemplateViewModel
{
    public PersonalityTemplateViewModel(PersonalityTemplate template)
    {
        Template = template;
    }

    public PersonalityTemplate Template { get; }

    public string DisplayName => Template.DisplayName;

    public string Description => Template.Description;

    public string Status => Template.Status;

    public string Category => Template.Category;

    public string AvatarText => Template.AvatarText;

    public string AvatarImagePath => Template.AvatarImagePath;

    public string? AvatarImageUri => AvatarAssetResolver.Resolve(Template.AvatarImagePath);

    public string SystemPrompt => Template.SystemPrompt;
}
