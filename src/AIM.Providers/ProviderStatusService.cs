using AIM.Core.Providers;
using AIM.Core.Services;

namespace AIM.Providers;

public sealed class ProviderStatusService : IProviderStatusService
{
    private readonly IProviderAccountService _providerAccountService;
    private readonly IProviderDiagnosticsService _providerDiagnosticsService;
    private readonly IReadOnlySet<string> _registeredProviderKeys;
    private readonly object _gate = new();
    private Dictionary<string, ProviderDiagnosticResult> _diagnostics =
        new(StringComparer.OrdinalIgnoreCase);

    public ProviderStatusService(
        IProviderAccountService providerAccountService,
        IProviderDiagnosticsService providerDiagnosticsService,
        IEnumerable<IAiProvider> providers)
    {
        _providerAccountService = providerAccountService;
        _providerDiagnosticsService = providerDiagnosticsService;
        _registeredProviderKeys = providers
            .Select(provider => provider.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public event EventHandler<ProviderStatusChangedEventArgs>? ProviderStatusChanged;

    public ProviderDiagnosticResult? GetCached(string providerKey)
    {
        lock (_gate)
        {
            return _diagnostics.TryGetValue(providerKey, out var diagnostic)
                ? diagnostic
                : null;
        }
    }

    public IReadOnlyDictionary<string, ProviderDiagnosticResult> Snapshot()
    {
        lock (_gate)
        {
            return new Dictionary<string, ProviderDiagnosticResult>(_diagnostics, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Clear(string providerKey)
    {
        var removed = false;

        lock (_gate)
        {
            removed = _diagnostics.Remove(providerKey);
        }

        if (removed)
        {
            ProviderStatusChanged?.Invoke(this, new ProviderStatusChangedEventArgs(providerKey, null));
        }
    }

    public async Task<ProviderDiagnosticResult?> RefreshAsync(
        string providerKey,
        CancellationToken cancellationToken = default)
    {
        var account = await _providerAccountService.GetAsync(providerKey, cancellationToken);

        if (account is null)
        {
            Clear(providerKey);
            return null;
        }

        var diagnostic = await _providerDiagnosticsService.CheckAsync(
            account,
            _registeredProviderKeys.Contains(account.Key),
            cancellationToken);
        Set(diagnostic);
        return diagnostic;
    }

    public async Task<IReadOnlyDictionary<string, ProviderDiagnosticResult>> RefreshAllAsync(
        CancellationToken cancellationToken = default)
    {
        var accounts = await _providerAccountService.ListAsync(cancellationToken);
        var results = new Dictionary<string, ProviderDiagnosticResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var account in accounts)
        {
            var diagnostic = await _providerDiagnosticsService.CheckAsync(
                account,
                _registeredProviderKeys.Contains(account.Key),
                cancellationToken);
            results[account.Key] = diagnostic;
        }

        lock (_gate)
        {
            _diagnostics = results;
        }

        foreach (var result in results.Values)
        {
            ProviderStatusChanged?.Invoke(
                this,
                new ProviderStatusChangedEventArgs(result.ProviderKey, result));
        }

        return Snapshot();
    }

    private void Set(ProviderDiagnosticResult diagnostic)
    {
        lock (_gate)
        {
            _diagnostics[diagnostic.ProviderKey] = diagnostic;
        }

        ProviderStatusChanged?.Invoke(
            this,
            new ProviderStatusChangedEventArgs(diagnostic.ProviderKey, diagnostic));
    }
}
