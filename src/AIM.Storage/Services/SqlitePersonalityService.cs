using AIM.Core.Personalities;
using AIM.Core.Services;
using AIM.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIM.Storage.Services;

public sealed class SqlitePersonalityService : IPersonalityService
{
    private readonly IDbContextFactory<AimDbContext> _dbContextFactory;

    public SqlitePersonalityService(IDbContextFactory<AimDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<Personality>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var personalities = await (
            from personality in dbContext.Personalities.AsNoTracking()
            join provider in dbContext.ProviderAccounts.AsNoTracking()
                on personality.DefaultProviderAccountId equals provider.Id
            orderby personality.Category, personality.DisplayName
            select Map(personality, provider.Key))
            .ToListAsync(cancellationToken);

        return personalities;
    }

    public async Task<Personality?> GetAsync(Guid personalityId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await (
            from personality in dbContext.Personalities.AsNoTracking()
            join provider in dbContext.ProviderAccounts.AsNoTracking()
                on personality.DefaultProviderAccountId equals provider.Id
            where personality.Id == personalityId
            select Map(personality, provider.Key))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Personality> SaveAsync(PersonalityDraft draft, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await dbContext.ProviderAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.Key == draft.ProviderKey, cancellationToken);

        if (provider is null)
        {
            throw new InvalidOperationException($"Provider '{draft.ProviderKey}' does not exist.");
        }

        var entity = draft.Id is null
            ? null
            : await dbContext.Personalities.FirstOrDefaultAsync(personality => personality.Id == draft.Id, cancellationToken);

        if (entity is null)
        {
            entity = new PersonalityEntity
            {
                Id = draft.Id ?? Guid.NewGuid(),
                MemorySetId = Guid.NewGuid()
            };
            dbContext.Personalities.Add(entity);
            dbContext.MemorySets.Add(new MemorySetEntity
            {
                Id = entity.MemorySetId,
                PersonalityId = entity.Id,
                Name = $"{draft.DisplayName.Trim()} memory",
                VectorCollectionName = $"personality_{entity.Id:N}"
            });
            dbContext.ConversationGroups.Add(new ConversationGroupEntity
            {
                Id = Guid.NewGuid(),
                PersonalityId = entity.Id,
                Title = "General",
                CreatedAt = DateTimeOffset.Now
            });
        }

        entity.DisplayName = draft.DisplayName.Trim();
        entity.Status = draft.Status.Trim();
        entity.AvatarText = string.IsNullOrWhiteSpace(draft.AvatarText)
            ? draft.DisplayName.Trim()[..1].ToUpperInvariant()
            : draft.AvatarText.Trim()[..1].ToUpperInvariant();
        entity.AvatarImagePath = draft.AvatarImagePath.Trim();
        entity.Category = NormalizeCategory(draft.Category);
        entity.SystemPrompt = draft.SystemPrompt.Trim();
        entity.DefaultProviderAccountId = provider.Id;
        entity.DefaultModelId = draft.ModelId.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity, provider.Key);
    }

    public async Task DeleteAsync(Guid personalityId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.Messages
            .Where(message => dbContext.Conversations
                .Where(conversation => conversation.PersonalityId == personalityId)
                .Select(conversation => conversation.Id)
                .Contains(message.ConversationId))
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.Conversations
            .Where(conversation => conversation.PersonalityId == personalityId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ConversationGroups
            .Where(group => group.PersonalityId == personalityId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.MemoryRecords
            .Where(memory => memory.PersonalityId == personalityId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.MemorySets
            .Where(memorySet => memorySet.PersonalityId == personalityId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.Personalities
            .Where(personality => personality.Id == personalityId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static Personality Map(Entities.PersonalityEntity entity, string providerKey)
    {
        return new Personality(
            entity.Id,
            entity.DisplayName,
            entity.Status,
            entity.AvatarText,
            entity.SystemPrompt,
            entity.MemorySetId,
            providerKey,
            entity.DefaultModelId,
            entity.AvatarImagePath,
            entity.Category);
    }

    private static string NormalizeCategory(string? category)
    {
        return string.IsNullOrWhiteSpace(category) ? "My Contacts" : category.Trim();
    }
}
