using AIM.Core.Providers;

namespace AIM.Core.Services;

public interface IProviderAccountService
{
    Task<ProviderAccount?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        string key,
        string displayName,
        string providerKind,
        string? endpoint,
        string? defaultModelId,
        string? credential,
        bool isEnabled,
        CancellationToken cancellationToken = default);
}
