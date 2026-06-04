using AIM.Core.Chat;
using AIM.Core.Services;
using AIM.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIM.Storage.Services;

public sealed class SqliteConversationService : IConversationService
{
    private const string DefaultGroupTitle = "General";
    private readonly IDbContextFactory<AimDbContext> _dbContextFactory;

    public SqliteConversationService(IDbContextFactory<AimDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<ConversationGroup> GetOrCreateConversationGroupAsync(
        Guid personalityId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.ConversationGroups
            .AsNoTracking()
            .Where(group => group.PersonalityId == personalityId && group.ArchivedAt == null)
            .ToListAsync(cancellationToken);
        var firstGroup = existing
            .OrderBy(group => group.CreatedAt)
            .FirstOrDefault();

        if (firstGroup is not null)
        {
            return MapConversationGroup(firstGroup);
        }

        var entity = CreateGroupEntity(personalityId, DefaultGroupTitle);

        dbContext.ConversationGroups.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapConversationGroup(entity);
    }

    public async Task<IReadOnlyList<ConversationGroup>> ListConversationGroupsAsync(
        Guid personalityId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var groups = await dbContext.ConversationGroups
            .AsNoTracking()
            .Where(group => group.PersonalityId == personalityId && group.ArchivedAt == null)
            .ToListAsync(cancellationToken);

        return groups
            .OrderBy(group => group.CreatedAt)
            .Select(MapConversationGroup)
            .ToArray();
    }

    public async Task<ConversationGroup> CreateConversationGroupAsync(
        Guid personalityId,
        string title,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = CreateGroupEntity(personalityId, title);

        dbContext.ConversationGroups.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapConversationGroup(entity);
    }

    public async Task RenameConversationGroupAsync(
        Guid groupId,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var group = await dbContext.ConversationGroups
            .FirstOrDefaultAsync(item => item.Id == groupId, cancellationToken);

        if (group is null)
        {
            return;
        }

        group.Title = title.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveConversationGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var group = await dbContext.ConversationGroups
            .FirstOrDefaultAsync(item => item.Id == groupId, cancellationToken);

        if (group is null)
        {
            return;
        }

        group.ArchivedAt = DateTimeOffset.Now;

        await dbContext.Conversations
            .Where(conversation => conversation.GroupId == groupId && conversation.ArchivedAt == null)
            .ExecuteUpdateAsync(
                calls => calls.SetProperty(conversation => conversation.ArchivedAt, DateTimeOffset.Now),
                cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Conversation> GetOrCreateConversationAsync(
        Guid personalityId,
        Guid? groupId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        groupId ??= await GetOrCreateConversationGroupIdAsync(dbContext, personalityId, cancellationToken);

        var conversations = await dbContext.Conversations
            .AsNoTracking()
            .Where(conversation =>
                conversation.PersonalityId == personalityId &&
                conversation.GroupId == groupId &&
                conversation.ArchivedAt == null)
            .ToListAsync(cancellationToken);
        var existing = conversations
            .OrderByDescending(conversation => conversation.CreatedAt)
            .FirstOrDefault();

        if (existing is not null)
        {
            return MapConversation(existing);
        }

        var entity = new ConversationEntity
        {
            Id = Guid.NewGuid(),
            PersonalityId = personalityId,
            GroupId = groupId.Value,
            Title = "New conversation",
            CreatedAt = DateTimeOffset.Now
        };

        dbContext.Conversations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapConversation(entity);
    }

    public async Task<IReadOnlyList<Conversation>> ListConversationsAsync(
        Guid personalityId,
        Guid? groupId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.PersonalityId == personalityId && conversation.ArchivedAt == null);

        if (groupId is not null)
        {
            query = query.Where(conversation => conversation.GroupId == groupId);
        }

        var conversations = await query.ToListAsync(cancellationToken);

        return conversations
            .OrderByDescending(conversation => conversation.CreatedAt)
            .Select(MapConversation)
            .ToArray();
    }

    public async Task<Conversation> CreateConversationAsync(
        Guid personalityId,
        string title,
        Guid? groupId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        groupId ??= await GetOrCreateConversationGroupIdAsync(dbContext, personalityId, cancellationToken);
        var entity = new ConversationEntity
        {
            Id = Guid.NewGuid(),
            PersonalityId = personalityId,
            GroupId = groupId.Value,
            Title = string.IsNullOrWhiteSpace(title) ? "New conversation" : title.Trim(),
            CreatedAt = DateTimeOffset.Now
        };

        dbContext.Conversations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapConversation(entity);
    }

    public async Task RenameConversationAsync(
        Guid conversationId,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            return;
        }

        conversation.Title = title.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            return;
        }

        conversation.ArchivedAt = DateTimeOffset.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Conversation?> GetConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken);

        return conversation is null ? null : MapConversation(conversation);
    }

    public async Task UpdateConversationSummaryAsync(
        Guid conversationId,
        string summary,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            return;
        }

        conversation.Summary = summary.Trim();
        conversation.SummaryUpdatedAt = DateTimeOffset.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var messages = await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .ToListAsync(cancellationToken);

        return messages
            .OrderBy(message => message.CreatedAt)
            .Select(message => new ChatMessage(
                message.Id,
                message.ConversationId,
                Enum.Parse<ChatRole>(message.Role),
                message.Content,
                message.CreatedAt))
            .ToArray();
    }

    public async Task<ChatMessage> AddMessageAsync(
        Guid conversationId,
        ChatRole role,
        string content,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new MessageEntity
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role.ToString(),
            Content = content.Trim(),
            CreatedAt = DateTimeOffset.Now
        };

        dbContext.Messages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ChatMessage(entity.Id, entity.ConversationId, role, entity.Content, entity.CreatedAt);
    }

    private static Conversation MapConversation(ConversationEntity entity)
    {
        return new Conversation(
            entity.Id,
            entity.PersonalityId,
            entity.GroupId,
            entity.Title,
            entity.CreatedAt,
            entity.Summary,
            entity.SummaryUpdatedAt);
    }

    private static ConversationGroup MapConversationGroup(ConversationGroupEntity entity)
    {
        return new ConversationGroup(entity.Id, entity.PersonalityId, entity.Title, entity.CreatedAt);
    }

    private static ConversationGroupEntity CreateGroupEntity(Guid personalityId, string title)
    {
        return new ConversationGroupEntity
        {
            Id = Guid.NewGuid(),
            PersonalityId = personalityId,
            Title = string.IsNullOrWhiteSpace(title) ? DefaultGroupTitle : title.Trim(),
            CreatedAt = DateTimeOffset.Now
        };
    }

    private static async Task<Guid> GetOrCreateConversationGroupIdAsync(
        AimDbContext dbContext,
        Guid personalityId,
        CancellationToken cancellationToken)
    {
        var existingGroupId = await dbContext.ConversationGroups
            .AsNoTracking()
            .Where(group => group.PersonalityId == personalityId && group.ArchivedAt == null)
            .ToListAsync(cancellationToken);
        var firstGroup = existingGroupId
            .OrderBy(group => group.CreatedAt)
            .FirstOrDefault();

        if (firstGroup is not null)
        {
            return firstGroup.Id;
        }

        var entity = CreateGroupEntity(personalityId, DefaultGroupTitle);
        dbContext.ConversationGroups.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
