using System.Security.Cryptography;
using System.Text;
using AIM.Core.Providers;
using AIM.Core.Services;
using AIM.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIM.Storage.Services;

public sealed class SqliteProviderAccountService : IProviderAccountService
{
    private static readonly byte[] Entropy = "AI-M provider account v1"u8.ToArray();
    private readonly IDbContextFactory<AimDbContext> _dbContextFactory;

    public SqliteProviderAccountService(IDbContextFactory<AimDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<ProviderAccount?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.ProviderAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.Key == key, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.ProviderAccounts
            .AsNoTracking()
            .OrderBy(account => account.DisplayName)
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToArray();
    }

    public async Task SaveAsync(
        string key,
        string displayName,
        string providerKind,
        string? endpoint,
        string? defaultModelId,
        string? credential,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.ProviderAccounts
            .FirstOrDefaultAsync(account => account.Key == key, cancellationToken);

        if (entity is null)
        {
            entity = new ProviderAccountEntity
            {
                Id = Guid.NewGuid(),
                Key = key
            };
            dbContext.ProviderAccounts.Add(entity);
        }

        entity.DisplayName = displayName.Trim();
        entity.ProviderKind = providerKind.Trim();
        entity.Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim();
        entity.DefaultModelId = string.IsNullOrWhiteSpace(defaultModelId) ? string.Empty : defaultModelId.Trim();
        entity.IsEnabled = isEnabled;

        if (credential is not null)
        {
            entity.ProtectedCredential = string.IsNullOrWhiteSpace(credential)
                ? null
                : Protect(credential.Trim());
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ProviderAccount Map(ProviderAccountEntity entity)
    {
        return new ProviderAccount(
            entity.Id,
            entity.Key,
            entity.DisplayName,
            entity.ProviderKind,
            entity.Endpoint,
            string.IsNullOrWhiteSpace(entity.DefaultModelId) ? null : entity.DefaultModelId,
            Unprotect(entity.ProtectedCredential),
            entity.IsEnabled);
    }

    private static byte[] Protect(string value)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Provider credential encryption uses Windows DPAPI.");
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        return ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
    }

    private static string? Unprotect(byte[]? protectedValue)
    {
        if (protectedValue is null || protectedValue.Length == 0)
        {
            return null;
        }

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            var bytes = ProtectedData.Unprotect(protectedValue, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
