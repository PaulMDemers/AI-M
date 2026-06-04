using AIM.Core.Memory;
using AIM.Core.Services;
using AIM.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIM.Storage.Services;

public sealed class SqliteMemoryService : IMemoryService
{
    private readonly IDbContextFactory<AimDbContext> _dbContextFactory;

    public SqliteMemoryService(IDbContextFactory<AimDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<MemoryRecord>> GetMemoriesAsync(
        Guid personalityId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var memories = await dbContext.MemoryRecords
            .AsNoTracking()
            .Where(memory => memory.PersonalityId == personalityId)
            .ToListAsync(cancellationToken);

        return memories
            .OrderByDescending(memory => memory.CreatedAt)
            .Select(memory => new MemoryRecord(memory.Id, memory.PersonalityId, memory.Content, memory.CreatedAt))
            .ToArray();
    }

    public async Task<MemoryRecord> RememberAsync(
        Guid personalityId,
        string content,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var memorySetId = await dbContext.MemorySets
            .AsNoTracking()
            .Where(memorySet => memorySet.PersonalityId == personalityId)
            .Select(memorySet => memorySet.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (memorySetId == Guid.Empty)
        {
            memorySetId = Guid.NewGuid();
            dbContext.MemorySets.Add(new MemorySetEntity
            {
                Id = memorySetId,
                PersonalityId = personalityId,
                Name = "Default memory",
                VectorCollectionName = $"personality_{personalityId:N}"
            });
        }

        var entity = new MemoryRecordEntity
        {
            Id = Guid.NewGuid(),
            PersonalityId = personalityId,
            MemorySetId = memorySetId,
            Content = content.Trim(),
            Source = "manual",
            CreatedAt = DateTimeOffset.Now
        };

        dbContext.MemoryRecords.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MemoryRecord(entity.Id, entity.PersonalityId, entity.Content, entity.CreatedAt);
    }

    public async Task DeleteAsync(Guid memoryId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.MemoryRecords
            .Where(memory => memory.Id == memoryId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
