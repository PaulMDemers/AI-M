using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using AIM.Core.Chat;
using AIM.Core.PendingActions;
using AIM.Core.Personalities;
using AIM.Core.Services;
using AIM.Core.Tools;
using AIM.Desktop.Wpf.Services;
using AIM.Desktop.Wpf.ViewModels;

namespace AIM.Tests;

public sealed class PendingAgentActionReviewTests
{
    [Fact]
    public void QueueAddsOnceAndPublishesChanges()
    {
        var service = new PendingAgentActionService(TempPath());
        var action = Action("Review memory");
        var changeCount = 0;
        service.ActionsChanged += (_, _) => changeCount++;

        service.Add(action);
        service.Add(action);
        service.Remove(action.Id);

        Assert.Empty(service.Actions);
        Assert.Equal(2, changeCount);
    }

    [Fact]
    public async Task ReviewApproveRoutesThroughAttachedOwnerHandler()
    {
        var service = new PendingAgentActionService(TempPath());
        var viewModel = new PendingActionsReviewViewModel(service);
        var approved = false;
        var fallbackCalled = false;
        var action = new PendingAgentActionViewModel(
            "Run tool",
            "The assistant wants to run a tool.",
            _ =>
            {
                fallbackCalled = true;
                return Task.FromResult(new PendingAgentActionResult("Fallback approved."));
            });
        action.AttachHandlers(
            () =>
            {
                approved = true;
                service.Remove(action.Id);
                return Task.CompletedTask;
            },
            () => { });
        service.Add(action);

        await viewModel.ApproveCommand.ExecuteAsync(action);

        Assert.True(approved);
        Assert.False(fallbackCalled);
        Assert.Empty(service.Actions);
        Assert.False(viewModel.HasActions);
    }

    [Fact]
    public void ReviewDenyRoutesThroughAttachedOwnerHandler()
    {
        var service = new PendingAgentActionService(TempPath());
        var viewModel = new PendingActionsReviewViewModel(service);
        var denied = false;
        var action = Action("Update personality");
        action.AttachHandlers(
            () => Task.CompletedTask,
            () =>
            {
                denied = true;
                service.Remove(action.Id);
            });
        service.Add(action);

        viewModel.DenyCommand.Execute(action);

        Assert.True(denied);
        Assert.Empty(service.Actions);
        Assert.False(viewModel.HasActions);
    }

    [Fact]
    public void SourceLabelIncludesActionPersonalityAndConversation()
    {
        var action = Action("Remember");

        action.AttachSource("Einstein", "Relativity", "Memory");

        Assert.Equal("Memory from Einstein / Relativity", action.SourceLabel);
    }

    [Fact]
    public void QueueRestoresPendingActionSnapshotsAsDismissibleRecords()
    {
        var path = TempPath();
        var original = new PendingAgentActionService(path);
        var action = Action("AI wants to remember");
        action.AttachSource("Coach", "Planning", "Memory");

        original.Add(action);
        var restored = new PendingAgentActionService(path);

        var restoredAction = Assert.Single(restored.Actions);
        Assert.Equal(action.Id, restoredAction.Id);
        Assert.Equal("AI wants to remember", restoredAction.Title);
        Assert.Equal("Memory from Coach / Planning", restoredAction.SourceLabel);
        Assert.False(restoredAction.CanApprove);
        Assert.True(restoredAction.IsApprovalUnavailable);
    }

    [Fact]
    public async Task QueueRestoresDurableToolApprovalsAsActionableRecords()
    {
        var path = TempPath();
        var personality = new Personality(
            Guid.NewGuid(),
            "Coach",
            "Planning",
            "CO",
            "Help plan work.",
            Guid.NewGuid(),
            "fake",
            "fake-model");
        var conversation = new Conversation(
            Guid.NewGuid(),
            personality.Id,
            Guid.NewGuid(),
            "Planning",
            DateTimeOffset.Now);
        var original = new PendingAgentActionService(path);
        var call = new AgentToolCall(
            "remember",
            "memory.remember",
            new JsonObject { ["content"] = "Paul likes narrow buddy lists." });
        var action = new PendingAgentActionViewModel(
            "AI wants to remember",
            "Paul likes narrow buddy lists.",
            _ => Task.FromResult(new PendingAgentActionResult("Approved.")),
            durableToolCall: new PendingAgentActionDurableToolCall(personality.Id, conversation.Id, call));
        action.AttachSource(personality.DisplayName, conversation.Title, "Memory");
        original.Add(action);
        var toolRegistry = new FakeToolRegistry();
        var conversations = new FakeConversationService(conversation);
        var restored = new PendingAgentActionService(
            path,
            new FakePersonalityService(personality),
            conversations,
            toolRegistry);
        var review = new PendingActionsReviewViewModel(restored);

        var restoredAction = Assert.Single(restored.Actions);
        await review.ApproveCommand.ExecuteAsync(restoredAction);

        Assert.Empty(restored.Actions);
        Assert.NotNull(toolRegistry.LastCall);
        Assert.Equal("memory.remember", toolRegistry.LastCall?.Name);
        Assert.Equal(conversation.Id, toolRegistry.LastContext?.Conversation.Id);
        Assert.Equal(2, conversations.Messages.Count);
        Assert.Equal(ChatRole.System, conversations.Messages[0].Role);
        Assert.Contains("approved after restart", conversations.Messages[0].Content);
        Assert.Equal(ChatRole.Tool, conversations.Messages[1].Role);
        Assert.Contains("memory.remember", conversations.Messages[1].Content);
    }

    private static PendingAgentActionViewModel Action(string title)
    {
        return new PendingAgentActionViewModel(
            title,
            "Detail",
            _ => Task.FromResult(new PendingAgentActionResult("Approved.")));
    }

    private static string TempPath()
    {
        return Path.Combine(Path.GetTempPath(), $"aim-pending-actions-{Guid.NewGuid():N}.json");
    }

    private sealed class FakePersonalityService : IPersonalityService
    {
        private readonly Personality _personality;

        public FakePersonalityService(Personality personality)
        {
            _personality = personality;
        }

        public Task<IReadOnlyList<Personality>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Personality>>([_personality]);
        }

        public Task<Personality?> GetAsync(Guid personalityId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Personality?>(_personality.Id == personalityId ? _personality : null);
        }

        public Task<Personality> SaveAsync(PersonalityDraft draft, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid personalityId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeConversationService : IConversationService
    {
        private readonly Conversation _conversation;

        public FakeConversationService(Conversation conversation)
        {
            _conversation = conversation;
        }

        public List<ChatMessage> Messages { get; } = [];

        public Task<ConversationGroup> GetOrCreateConversationGroupAsync(
            Guid personalityId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ConversationGroup>> ListConversationGroupsAsync(
            Guid personalityId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ConversationGroup> CreateConversationGroupAsync(
            Guid personalityId,
            string title,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task RenameConversationGroupAsync(
            Guid groupId,
            string title,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task ArchiveConversationGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Conversation> GetOrCreateConversationAsync(
            Guid personalityId,
            Guid? groupId = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<Conversation>> ListConversationsAsync(
            Guid personalityId,
            Guid? groupId = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Conversation> CreateConversationAsync(
            Guid personalityId,
            string title,
            Guid? groupId = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task RenameConversationAsync(
            Guid conversationId,
            string title,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task ArchiveConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Conversation?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Conversation?>(_conversation.Id == conversationId ? _conversation : null);
        }

        public Task UpdateConversationSummaryAsync(
            Guid conversationId,
            string summary,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(Messages);
        }

        public Task<ChatMessage> AddMessageAsync(
            Guid conversationId,
            ChatRole role,
            string content,
            CancellationToken cancellationToken = default)
        {
            var message = new ChatMessage(Guid.NewGuid(), conversationId, role, content, DateTimeOffset.Now);
            Messages.Add(message);
            return Task.FromResult(message);
        }
    }

    private sealed class FakeToolRegistry : IAgentToolRegistry
    {
        public AgentToolCall? LastCall { get; private set; }

        public AgentToolContext? LastContext { get; private set; }

        public IReadOnlyList<AgentToolDefinition> ListTools()
        {
            return [];
        }

        public Task<AgentToolResult> ExecuteAsync(
            AgentToolCall call,
            AgentToolContext context,
            CancellationToken cancellationToken = default)
        {
            LastCall = call;
            LastContext = context;
            return Task.FromResult(new AgentToolResult(call.Id, call.Name, true, "Remembered."));
        }
    }
}
