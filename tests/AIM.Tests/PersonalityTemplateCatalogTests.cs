using AIM.Core.Personalities;

namespace AIM.Tests;

public sealed class PersonalityTemplateCatalogTests
{
    [Fact]
    public void ArchetypeTemplatesAreUsableForNewContacts()
    {
        Assert.Equal(8, PersonalityTemplateCatalog.Archetypes.Count);

        foreach (var template in PersonalityTemplateCatalog.Archetypes)
        {
            Assert.Equal("Archetypes", template.Category);
            Assert.Equal("openai", template.ProviderKey);
            Assert.Equal("gpt-4.1-mini", template.ModelId);
            Assert.StartsWith("Assets/Avatars/", template.AvatarImagePath, StringComparison.Ordinal);
            Assert.Contains(template.DisplayName, template.SystemPrompt);
            Assert.Contains("fictional archetypal AI contact", template.SystemPrompt);
        }
    }
}
