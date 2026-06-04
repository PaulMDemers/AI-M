using AIM.Core.Providers;

namespace AIM.Core.Services;

public sealed class ProviderStatusChangedEventArgs : EventArgs
{
    public ProviderStatusChangedEventArgs(string providerKey, ProviderDiagnosticResult? diagnostic)
    {
        ProviderKey = providerKey;
        Diagnostic = diagnostic;
    }

    public string ProviderKey { get; }

    public ProviderDiagnosticResult? Diagnostic { get; }
}

public interface IProviderStatusService
{
    event EventHandler<ProviderStatusChangedEventArgs>? ProviderStatusChanged;

    ProviderDiagnosticResult? GetCached(string providerKey);

    IReadOnlyDictionary<string, ProviderDiagnosticResult> Snapshot();

    void Clear(string providerKey);

    Task<ProviderDiagnosticResult?> RefreshAsync(
        string providerKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, ProviderDiagnosticResult>> RefreshAllAsync(
        CancellationToken cancellationToken = default);
}
