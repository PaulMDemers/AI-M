using AIM.Core.Services;
using AIM.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Tests;

public sealed class MemorySuggestionStorageTests
{
    [Fact]
    public async Task SuggestionsCanBeApprovedOrRejected()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-memory-suggestions-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personality = (await provider.GetRequiredService<IPersonalityService>().ListAsync()).First();
            var conversation = await provider.GetRequiredService<IConversationService>()
                .GetOrCreateConversationAsync(personality.Id);
            var suggestions = provider.GetRequiredService<IMemorySuggestionService>();
            var memory = provider.GetRequiredService<IMemoryService>();

            await suggestions.SuggestFromTurnAsync(
                personality.Id,
                conversation.Id,
                "I prefer concise status updates.",
                "Got it.");

            var pending = await suggestions.ListPendingAsync(personality.Id);
            var suggestion = Assert.Single(pending);

            await suggestions.ApproveAsync(suggestion.Id);

            Assert.Empty(await suggestions.ListPendingAsync(personality.Id));
            Assert.Contains(
                await memory.GetMemoriesAsync(personality.Id),
                item => item.Content == "I prefer concise status updates.");

            await suggestions.SuggestFromTurnAsync(
                personality.Id,
                conversation.Id,
                "I like dark mode.",
                "Noted.");
            pending = await suggestions.ListPendingAsync(personality.Id);

            await suggestions.RejectAsync(Assert.Single(pending).Id);

            Assert.Empty(await suggestions.ListPendingAsync(personality.Id));
            Assert.DoesNotContain(
                await memory.GetMemoriesAsync(personality.Id),
                item => item.Content == "I like dark mode.");
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task ExplicitRememberTurnsAreStoredImmediately()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-explicit-memory-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personality = (await provider.GetRequiredService<IPersonalityService>().ListAsync()).First();
            var conversation = await provider.GetRequiredService<IConversationService>()
                .GetOrCreateConversationAsync(personality.Id);
            var suggestions = provider.GetRequiredService<IMemorySuggestionService>();
            var memory = provider.GetRequiredService<IMemoryService>();

            await suggestions.SuggestFromTurnAsync(
                personality.Id,
                conversation.Id,
                "Remember that my project is AI-M.",
                "Got it.");

            Assert.Empty(await suggestions.ListPendingAsync(personality.Id));
            Assert.Contains(
                await memory.GetMemoriesAsync(personality.Id),
                item => item.Content == "my project is AI-M.");
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task SuggestionsAreIsolatedPerPersonality()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-memory-isolation-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personalities = await provider.GetRequiredService<IPersonalityService>().ListAsync();
            var first = personalities[0];
            var second = personalities[1];
            var conversation = await provider.GetRequiredService<IConversationService>()
                .GetOrCreateConversationAsync(first.Id);
            var suggestions = provider.GetRequiredService<IMemorySuggestionService>();

            await suggestions.SuggestFromTurnAsync(
                first.Id,
                conversation.Id,
                "I prefer compact answers.",
                "Stored for review.");

            Assert.Single(await suggestions.ListPendingAsync(first.Id));
            Assert.Empty(await suggestions.ListPendingAsync(second.Id));
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
