using AIM.Core.Chat;
using AIM.Core.Services;
using AIM.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Tests;

public sealed class ConversationManagementTests
{
    [Fact]
    public async Task ConversationsCanBeCreatedRenamedArchivedAndKeepMessagesIsolated()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-conversations-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personality = (await provider.GetRequiredService<IPersonalityService>().ListAsync()).First();
            var conversations = provider.GetRequiredService<IConversationService>();
            var first = await conversations.CreateConversationAsync(personality.Id, "First");
            var second = await conversations.CreateConversationAsync(personality.Id, "Second");

            await conversations.AddMessageAsync(first.Id, ChatRole.User, "Only in first");
            await conversations.AddMessageAsync(second.Id, ChatRole.User, "Only in second");
            await conversations.RenameConversationAsync(first.Id, "Renamed First");

            var listed = await conversations.ListConversationsAsync(personality.Id);

            Assert.Contains(listed, conversation => conversation.Id == first.Id && conversation.Title == "Renamed First");
            Assert.Contains(await conversations.GetMessagesAsync(first.Id), message => message.Content == "Only in first");
            Assert.DoesNotContain(await conversations.GetMessagesAsync(first.Id), message => message.Content == "Only in second");

            await conversations.ArchiveConversationAsync(first.Id);

            listed = await conversations.ListConversationsAsync(personality.Id);

            Assert.DoesNotContain(listed, conversation => conversation.Id == first.Id);
            Assert.Contains(listed, conversation => conversation.Id == second.Id);
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
    public async Task ConversationsCanBeOrganizedByPersonalityGroup()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-conversation-groups-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personality = (await provider.GetRequiredService<IPersonalityService>().ListAsync()).First();
            var conversations = provider.GetRequiredService<IConversationService>();
            var planning = await conversations.CreateConversationGroupAsync(personality.Id, "Planning");
            var research = await conversations.CreateConversationGroupAsync(personality.Id, "Research");
            var roadmap = await conversations.CreateConversationAsync(personality.Id, "Roadmap", planning.Id);
            var notes = await conversations.CreateConversationAsync(personality.Id, "Notes", research.Id);

            await conversations.RenameConversationGroupAsync(planning.Id, "Product planning");

            var groups = await conversations.ListConversationGroupsAsync(personality.Id);
            var planningConversations = await conversations.ListConversationsAsync(personality.Id, planning.Id);
            var researchConversations = await conversations.ListConversationsAsync(personality.Id, research.Id);

            Assert.Contains(groups, group => group.Id == planning.Id && group.Title == "Product planning");
            Assert.Contains(planningConversations, conversation => conversation.Id == roadmap.Id);
            Assert.DoesNotContain(planningConversations, conversation => conversation.Id == notes.Id);
            Assert.Contains(researchConversations, conversation => conversation.Id == notes.Id);

            await conversations.ArchiveConversationGroupAsync(planning.Id);

            groups = await conversations.ListConversationGroupsAsync(personality.Id);
            planningConversations = await conversations.ListConversationsAsync(personality.Id, planning.Id);

            Assert.DoesNotContain(groups, group => group.Id == planning.Id);
            Assert.Empty(planningConversations);
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
    public async Task ConversationSummaryCanBeStoredAndLoaded()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-conversation-summary-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personality = (await provider.GetRequiredService<IPersonalityService>().ListAsync()).First();
            var conversations = provider.GetRequiredService<IConversationService>();
            var conversation = await conversations.CreateConversationAsync(personality.Id, "Summary test");

            await conversations.UpdateConversationSummaryAsync(conversation.Id, "Paul wants an AIM-style layout.");

            var updated = await conversations.GetConversationAsync(conversation.Id);

            Assert.Equal("Paul wants an AIM-style layout.", updated?.Summary);
            Assert.NotNull(updated?.SummaryUpdatedAt);
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
