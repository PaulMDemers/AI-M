using AIM.Core.Personalities;
using AIM.Core.Services;
using AIM.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Tests;

public sealed class PersonalityStorageTests
{
    [Fact]
    public async Task CanCreateUpdateDeletePersonalityAndKeepMemoriesIsolated()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-personality-tests-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personalityService = provider.GetRequiredService<IPersonalityService>();
            var memoryService = provider.GetRequiredService<IMemoryService>();
            var existing = (await personalityService.ListAsync()).First();

            var created = await personalityService.SaveAsync(new PersonalityDraft(
                Id: null,
                DisplayName: "Casey",
                Status: "Test contact",
                AvatarText: "C",
                SystemPrompt: "You are Casey.",
                ProviderKey: "fake",
                ModelId: "fake-preview"));

            await memoryService.RememberAsync(created.Id, "Casey memory");
            await memoryService.RememberAsync(existing.Id, "Existing memory");

            var updated = await personalityService.SaveAsync(new PersonalityDraft(
                created.Id,
                "Casey Updated",
                "Still testing",
                "U",
                "You are still Casey.",
                "fake",
                "fake-preview"));

            var createdMemories = await memoryService.GetMemoriesAsync(created.Id);
            var existingMemories = await memoryService.GetMemoriesAsync(existing.Id);

            Assert.Equal("Casey Updated", updated.DisplayName);
            Assert.Contains(createdMemories, memory => memory.Content == "Casey memory");
            Assert.DoesNotContain(createdMemories, memory => memory.Content == "Existing memory");
            Assert.Contains(existingMemories, memory => memory.Content == "Existing memory");

            await personalityService.DeleteAsync(created.Id);

            Assert.Null(await personalityService.GetAsync(created.Id));
            Assert.Empty(await memoryService.GetMemoriesAsync(created.Id));
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static ServiceProvider BuildProvider(string databasePath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AIM:Storage:SqlitePath"] = databasePath
            })
            .Build();
        var services = new ServiceCollection();

        services.AddAimStorage(configuration);

        return services.BuildServiceProvider();
    }
}
