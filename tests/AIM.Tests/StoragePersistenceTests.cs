using AIM.Core.Chat;
using AIM.Core.Services;
using AIM.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Tests;

public sealed class StoragePersistenceTests
{
    [Fact]
    public async Task StoragePersistsSeededPersonalitiesConversationsAndMemories()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-tests-{Guid.NewGuid():N}.db");

        try
        {
            await using (var provider = BuildProvider(databasePath))
            {
                await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

                var personalities = await provider.GetRequiredService<IPersonalityService>().ListAsync();
                var personality = Assert.Single(personalities, item => item.DisplayName == "Ada");
                Assert.Equal("Core", personality.Category);
                Assert.Contains(personalities, item => item.DisplayName == "Architect" && item.Category == "Archetypes");
                var conversationService = provider.GetRequiredService<IConversationService>();
                var memoryService = provider.GetRequiredService<IMemoryService>();

                var conversation = await conversationService.GetOrCreateConversationAsync(personality.Id);
                await conversationService.AddMessageAsync(conversation.Id, ChatRole.User, "Persist this message");
                await memoryService.RememberAsync(personality.Id, "Persist this memory");
            }

            await using (var provider = BuildProvider(databasePath))
            {
                await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

                var personality = (await provider.GetRequiredService<IPersonalityService>().ListAsync())
                    .Single(item => item.DisplayName == "Ada");
                var conversation = await provider.GetRequiredService<IConversationService>()
                    .GetOrCreateConversationAsync(personality.Id);
                var messages = await provider.GetRequiredService<IConversationService>().GetMessagesAsync(conversation.Id);
                var memories = await provider.GetRequiredService<IMemoryService>().GetMemoriesAsync(personality.Id);

                Assert.Contains(messages, message => message.Content == "Persist this message");
                Assert.Contains(memories, memory => memory.Content == "Persist this memory");
            }
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
