using AIM.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIM.Storage;

public sealed class AimDbContext : DbContext
{
    public AimDbContext(DbContextOptions<AimDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProviderAccountEntity> ProviderAccounts => Set<ProviderAccountEntity>();

    public DbSet<PersonalityEntity> Personalities => Set<PersonalityEntity>();

    public DbSet<MemorySetEntity> MemorySets => Set<MemorySetEntity>();

    public DbSet<ConversationGroupEntity> ConversationGroups => Set<ConversationGroupEntity>();

    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<MemoryRecordEntity> MemoryRecords => Set<MemoryRecordEntity>();

    public DbSet<MemorySuggestionEntity> MemorySuggestions => Set<MemorySuggestionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProviderAccountEntity>().HasIndex(account => account.Key).IsUnique();
        modelBuilder.Entity<ProviderAccountEntity>().Property(account => account.DefaultModelId).HasDefaultValue(string.Empty);
        modelBuilder.Entity<PersonalityEntity>().HasIndex(personality => personality.DisplayName);
        modelBuilder.Entity<ConversationGroupEntity>().HasIndex(group => group.PersonalityId);
        modelBuilder.Entity<ConversationEntity>().HasIndex(conversation => conversation.PersonalityId);
        modelBuilder.Entity<ConversationEntity>().HasIndex(conversation => conversation.GroupId);
        modelBuilder.Entity<MessageEntity>().HasIndex(message => new { message.ConversationId, message.CreatedAt });
        modelBuilder.Entity<MemoryRecordEntity>().HasIndex(memory => memory.PersonalityId);
        modelBuilder.Entity<MemorySuggestionEntity>().HasIndex(suggestion => new { suggestion.PersonalityId, suggestion.Status });
    }
}
