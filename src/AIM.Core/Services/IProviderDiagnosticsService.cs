using AIM.Core.Providers;

namespace AIM.Core.Services;

public interface IProviderDiagnosticsService
{
    Task<ProviderDiagnosticResult> CheckAsync(
        ProviderAccount account,
        bool providerRegistered,
        CancellationToken cancellationToken = default);
}
