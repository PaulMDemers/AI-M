using AIM.Core.Services;
using AIM.Storage;
using AIM.Storage.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Tests;

public sealed class ChatContextBuilderTests
{
    [Fact]
    public async Task BuildsContextFromApprovedMemoriesOnly()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-context-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personality = (await provider.GetRequiredService<IPersonalityService>().ListAsync()).First();
            var conversation = await provider.GetRequiredService<IConversationService>()
                .GetOrCreateConversationAsync(personality.Id);
            var memoryService = provider.GetRequiredService<IMemoryService>();
            var suggestions = provider.GetRequiredService<IMemorySuggestionService>();
            var builder = provider.GetRequiredService<IChatContextBuilder>();

            await memoryService.RememberAsync(personality.Id, "Approved manual memory");
            await suggestions.SuggestFromTurnAsync(
                personality.Id,
                conversation.Id,
                "I prefer this to stay pending until review.",
                "Okay.");
            await provider.GetRequiredService<IConversationService>()
                .AddMessageAsync(conversation.Id, AIM.Core.Chat.ChatRole.User, "Please keep the contacts narrow.");
            await provider.GetRequiredService<IConversationService>()
                .UpdateConversationSummaryAsync(conversation.Id, "The chat is about compact messenger UI.");
            await provider.GetRequiredService<IConversationService>()
                .AddMessageAsync(conversation.Id, AIM.Core.Chat.ChatRole.Assistant, "I will keep that in mind.");

            var context = await builder.BuildAsync(personality, conversation);

            Assert.Contains(context.Memories, memory => memory.Content == "Approved manual memory");
            Assert.DoesNotContain(context.Memories, memory => memory.Content == "I prefer this to stay pending until review.");
            Assert.True(context.HasToolDefinitions);
            Assert.Contains(context.ToolDefinitions, tool => tool.Name == "memory.search");
            Assert.True(context.HasConversationSummary);
            Assert.Equal("The chat is about compact messenger UI.", context.ConversationSummary);
            Assert.True(context.HasConversationSummaryInstructions);
            Assert.Contains("conversation.summary.update", context.ConversationSummaryInstructions);
            Assert.Contains("There are 1 visible user/assistant messages since then.", context.ConversationSummaryInstructions);
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
