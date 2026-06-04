using System.Text.Json.Nodes;
using AIM.Core.Services;
using AIM.Core.Tools;
using AIM.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Tests;

public sealed class BuiltInAgentToolRegistryTests
{
    [Fact]
    public void DurableMemoryToolsRequireApproval()
    {
        using var provider = BuildProvider(Path.Combine(Path.GetTempPath(), $"aim-tools-{Guid.NewGuid():N}.db"));
        var registry = provider.GetRequiredService<IAgentToolRegistry>();
        var tools = registry.ListTools();

        Assert.False(tools.Single(tool => tool.Name == "memory.list").RequiresApproval);
        Assert.False(tools.Single(tool => tool.Name == "memory.search").RequiresApproval);
        Assert.True(tools.Single(tool => tool.Name == "memory.remember").RequiresApproval);
        Assert.True(tools.Single(tool => tool.Name == "memory.forget").RequiresApproval);
        Assert.True(tools.Single(tool => tool.Name == "personality.update_status").RequiresApproval);
        Assert.True(tools.Single(tool => tool.Name == "personality.append_system_note").RequiresApproval);
        Assert.False(tools.Single(tool => tool.Name == "conversation.recent").RequiresApproval);
        Assert.False(tools.Single(tool => tool.Name == "conversation.search").RequiresApproval);
        Assert.False(tools.Single(tool => tool.Name == "conversation.summary.get").RequiresApproval);
        Assert.True(tools.Single(tool => tool.Name == "conversation.summary.update").RequiresApproval);
    }

    [Fact]
    public async Task MemoryToolsCanRememberListAndForget()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-tools-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personality = (await provider.GetRequiredService<IPersonalityService>().ListAsync()).First();
            var conversation = await provider.GetRequiredService<IConversationService>()
                .GetOrCreateConversationAsync(personality.Id);
            var registry = provider.GetRequiredService<IAgentToolRegistry>();
            var context = new AgentToolContext(personality, conversation);

            var remember = await registry.ExecuteAsync(
                new AgentToolCall(
                    "remember",
                    "memory.remember",
                    new JsonObject { ["content"] = "Paul prefers compact tool summaries." }),
                context);
            var list = await registry.ExecuteAsync(new AgentToolCall("list", "memory.list", []), context);
            var search = await registry.ExecuteAsync(
                new AgentToolCall(
                    "search",
                    "memory.search",
                    new JsonObject { ["query"] = "compact", ["limit"] = 3 }),
                context);
            var forget = await registry.ExecuteAsync(
                new AgentToolCall(
                    "forget",
                    "memory.forget",
                    new JsonObject { ["match"] = "compact tool summaries" }),
                context);
            var afterForget = await registry.ExecuteAsync(new AgentToolCall("after", "memory.list", []), context);

            Assert.True(remember.Success);
            Assert.Contains("compact tool summaries", list.Content);
            Assert.True(search.Success);
            Assert.Contains("compact tool summaries", search.Content);
            Assert.Contains("\"count\":1", search.Content);
            Assert.True(forget.Success);
            Assert.Contains("\"deleted\":1", forget.Content);
            Assert.DoesNotContain("compact tool summaries", afterForget.Content);
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
    public async Task ConversationToolsReturnActiveConversationVisibleMessagesOnly()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-tools-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personality = (await provider.GetRequiredService<IPersonalityService>().ListAsync()).First();
            var conversations = provider.GetRequiredService<IConversationService>();
            var first = await conversations.CreateConversationAsync(personality.Id, "First");
            var second = await conversations.CreateConversationAsync(personality.Id, "Second");
            var registry = provider.GetRequiredService<IAgentToolRegistry>();
            var context = new AgentToolContext(personality, first);

            await conversations.AddMessageAsync(first.Id, AIM.Core.Chat.ChatRole.User, "We discussed compact windows.");
            await conversations.AddMessageAsync(first.Id, AIM.Core.Chat.ChatRole.Tool, "Internal tool result should stay hidden.");
            await conversations.AddMessageAsync(first.Id, AIM.Core.Chat.ChatRole.Assistant, "The contact list should stay narrow.");
            await conversations.AddMessageAsync(second.Id, AIM.Core.Chat.ChatRole.User, "Only in second conversation.");

            var recent = await registry.ExecuteAsync(
                new AgentToolCall("recent", "conversation.recent", new JsonObject { ["limit"] = 10 }),
                context);
            var search = await registry.ExecuteAsync(
                new AgentToolCall("search", "conversation.search", new JsonObject { ["query"] = "compact", ["limit"] = 10 }),
                context);

            Assert.True(recent.Success);
            Assert.Contains("compact windows", recent.Content);
            Assert.Contains("contact list should stay narrow", recent.Content);
            Assert.DoesNotContain("Internal tool result", recent.Content);
            Assert.DoesNotContain("Only in second conversation", recent.Content);

            Assert.True(search.Success);
            Assert.Contains("compact windows", search.Content);
            Assert.Contains("\"count\":1", search.Content);
            Assert.DoesNotContain("contact list should stay narrow", search.Content);
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
    public async Task ConversationSummaryToolsCanReadAndUpdateActiveConversationSummary()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-tools-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personality = (await provider.GetRequiredService<IPersonalityService>().ListAsync()).First();
            var conversation = await provider.GetRequiredService<IConversationService>()
                .CreateConversationAsync(personality.Id, "Summary tools");
            var registry = provider.GetRequiredService<IAgentToolRegistry>();
            var context = new AgentToolContext(personality, conversation);

            var update = await registry.ExecuteAsync(
                new AgentToolCall(
                    "summary-update",
                    "conversation.summary.update",
                    new JsonObject { ["summary"] = "The chat covered memory approvals." }),
                context);
            var get = await registry.ExecuteAsync(
                new AgentToolCall("summary-get", "conversation.summary.get", []),
                context);

            Assert.True(update.Success);
            Assert.True(get.Success);
            Assert.Contains("memory approvals", get.Content);
            Assert.Contains("SummaryUpdatedAt", get.Content);
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
    public async Task PersonalityToolsCanUpdateStatusAndAppendSystemNote()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aim-tools-{Guid.NewGuid():N}.db");

        try
        {
            await using var provider = BuildProvider(databasePath);
            await provider.GetRequiredService<AimStorageInitializer>().InitializeAsync();

            var personalityService = provider.GetRequiredService<IPersonalityService>();
            var personality = (await personalityService.ListAsync()).First();
            var conversation = await provider.GetRequiredService<IConversationService>()
                .GetOrCreateConversationAsync(personality.Id);
            var registry = provider.GetRequiredService<IAgentToolRegistry>();
            var context = new AgentToolContext(personality, conversation);

            var status = await registry.ExecuteAsync(
                new AgentToolCall(
                    "status",
                    "personality.update_status",
                    new JsonObject { ["status"] = "Focused" }),
                context);
            var note = await registry.ExecuteAsync(
                new AgentToolCall(
                    "note",
                    "personality.append_system_note",
                    new JsonObject { ["note"] = "Prefer concise responses." }),
                context);
            var updated = await personalityService.GetAsync(personality.Id);

            Assert.True(status.Success);
            Assert.True(note.Success);
            Assert.Equal("Focused", updated?.Status);
            Assert.Contains("Prefer concise responses.", updated?.SystemPrompt);
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
