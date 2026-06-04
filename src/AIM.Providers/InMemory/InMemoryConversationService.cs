using AIM.Core.Chat;
using AIM.Core.Services;

namespace AIM.Providers.InMemory;

public sealed class InMemoryConversationService : IConversationService
{
    private const string DefaultGroupTitle = "General";
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, List<ConversationGroup>> _groupsByPersonality = [];
    private readonly HashSet<Guid> _archivedGroupIds = [];
    private readonly Dictionary<Guid, List<Conversation>> _conversationsByPersonality = [];
    private readonly HashSet<Guid> _archivedConversationIds = [];
    private readonly Dictionary<Guid, List<ChatMessage>> _messagesByConversation = [];

    public Task<ConversationGroup> GetOrCreateConversationGroupAsync(
        Guid personalityId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(GetOrCreateGroup(personalityId));
        }
    }

    public Task<IReadOnlyList<ConversationGroup>> ListConversationGroupsAsync(
        Guid personalityId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_groupsByPersonality.TryGetValue(personalityId, out var groups))
            {
                return Task.FromResult<IReadOnlyList<ConversationGroup>>([]);
            }

            return Task.FromResult<IReadOnlyList<ConversationGroup>>(
                groups
                    .Where(group => !_archivedGroupIds.Contains(group.Id))
                    .OrderBy(group => group.CreatedAt)
                    .ToArray());
        }
    }

    public Task<ConversationGroup> CreateConversationGroupAsync(
        Guid personalityId,
        string title,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(CreateGroup(personalityId, title));
        }
    }

    public Task RenameConversationGroupAsync(
        Guid groupId,
        string title,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            foreach (var groups in _groupsByPersonality.Values)
            {
                var group = groups.FirstOrDefault(item => item.Id == groupId);

                if (group is not null)
                {
                    group.Rename(title);
                    break;
                }
            }

            return Task.CompletedTask;
        }
    }

    public Task ArchiveConversationGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _archivedGroupIds.Add(groupId);

            foreach (var conversations in _conversationsByPersonality.Values)
            {
                foreach (var conversation in conversations.Where(item => item.GroupId == groupId))
                {
                    _archivedConversationIds.Add(conversation.Id);
                }
            }

            return Task.CompletedTask;
        }
    }

    public Task<Conversation> GetOrCreateConversationAsync(
        Guid personalityId,
        Guid? groupId = null,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            groupId ??= GetOrCreateGroup(personalityId).Id;

            if (_conversationsByPersonality.TryGetValue(personalityId, out var conversations))
            {
                var existing = conversations
                    .Where(conversation =>
                        conversation.GroupId == groupId &&
                        !_archivedConversationIds.Contains(conversation.Id))
                    .OrderByDescending(conversation => conversation.CreatedAt)
                    .FirstOrDefault();

                if (existing is not null)
                {
                    return Task.FromResult(existing);
                }
            }

            return Task.FromResult(CreateConversation(personalityId, groupId.Value, "New conversation"));
        }
    }

    public Task<IReadOnlyList<Conversation>> ListConversationsAsync(
        Guid personalityId,
        Guid? groupId = null,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_conversationsByPersonality.TryGetValue(personalityId, out var conversations))
            {
                return Task.FromResult<IReadOnlyList<Conversation>>([]);
            }

            return Task.FromResult<IReadOnlyList<Conversation>>(
                conversations
                    .Where(conversation =>
                        (groupId is null || conversation.GroupId == groupId) &&
                        !_archivedConversationIds.Contains(conversation.Id))
                    .OrderByDescending(conversation => conversation.CreatedAt)
                    .ToArray());
        }
    }

    public Task<Conversation> CreateConversationAsync(
        Guid personalityId,
        string title,
        Guid? groupId = null,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            groupId ??= GetOrCreateGroup(personalityId).Id;
            return Task.FromResult(CreateConversation(personalityId, groupId.Value, title));
        }
    }

    public Task RenameConversationAsync(
        Guid conversationId,
        string title,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            foreach (var conversations in _conversationsByPersonality.Values)
            {
                var conversation = conversations.FirstOrDefault(item => item.Id == conversationId);

                if (conversation is not null)
                {
                    conversation.Rename(title);
                    break;
                }
            }

            return Task.CompletedTask;
        }
    }

    public Task ArchiveConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _archivedConversationIds.Add(conversationId);
            return Task.CompletedTask;
        }
    }

    public Task<Conversation?> GetConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var conversation = _conversationsByPersonality.Values
                .SelectMany(conversations => conversations)
                .FirstOrDefault(item => item.Id == conversationId);

            return Task.FromResult(conversation);
        }
    }

    public Task UpdateConversationSummaryAsync(
        Guid conversationId,
        string summary,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var conversation = _conversationsByPersonality.Values
                .SelectMany(conversations => conversations)
                .FirstOrDefault(item => item.Id == conversationId);

            conversation?.UpdateSummary(summary, DateTimeOffset.Now);

            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_messagesByConversation.TryGetValue(conversationId, out var messages))
            {
                return Task.FromResult<IReadOnlyList<ChatMessage>>([]);
            }

            return Task.FromResult<IReadOnlyList<ChatMessage>>(messages.ToArray());
        }
    }

    public Task<ChatMessage> AddMessageAsync(
        Guid conversationId,
        ChatRole role,
        string content,
        CancellationToken cancellationToken = default)
    {
        var message = new ChatMessage(Guid.NewGuid(), conversationId, role, content.Trim(), DateTimeOffset.Now);

        lock (_gate)
        {
            if (!_messagesByConversation.TryGetValue(conversationId, out var messages))
            {
                messages = [];
                _messagesByConversation[conversationId] = messages;
            }

            messages.Add(message);
        }

        return Task.FromResult(message);
    }

    private ConversationGroup GetOrCreateGroup(Guid personalityId)
    {
        if (_groupsByPersonality.TryGetValue(personalityId, out var groups))
        {
            var existing = groups
                .Where(group => !_archivedGroupIds.Contains(group.Id))
                .OrderBy(group => group.CreatedAt)
                .FirstOrDefault();

            if (existing is not null)
            {
                return existing;
            }
        }

        return CreateGroup(personalityId, DefaultGroupTitle);
    }

    private ConversationGroup CreateGroup(Guid personalityId, string title)
    {
        var group = new ConversationGroup(
            Guid.NewGuid(),
            personalityId,
            string.IsNullOrWhiteSpace(title) ? DefaultGroupTitle : title.Trim(),
            DateTimeOffset.Now);

        if (!_groupsByPersonality.TryGetValue(personalityId, out var groups))
        {
            groups = [];
            _groupsByPersonality[personalityId] = groups;
        }

        groups.Add(group);

        return group;
    }

    private Conversation CreateConversation(Guid personalityId, Guid groupId, string title)
    {
        var conversation = new Conversation(
            Guid.NewGuid(),
            personalityId,
            groupId,
            string.IsNullOrWhiteSpace(title) ? "New conversation" : title.Trim(),
            DateTimeOffset.Now);

        if (!_conversationsByPersonality.TryGetValue(personalityId, out var conversations))
        {
            conversations = [];
            _conversationsByPersonality[personalityId] = conversations;
        }

        conversations.Add(conversation);
        _messagesByConversation[conversation.Id] = [];

        return conversation;
    }
}
