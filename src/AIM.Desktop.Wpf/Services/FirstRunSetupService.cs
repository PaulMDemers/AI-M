using System.Windows;
using AIM.Core.Providers;
using AIM.Core.Services;
using AIM.Desktop.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Desktop.Wpf.Services;

public interface IFirstRunSetupService
{
    Task ShowIfNeededAsync(Window owner, CancellationToken cancellationToken = default);
}

public sealed class FirstRunSetupService : IFirstRunSetupService
{
    private readonly IProviderAccountService _providerAccountService;
    private readonly IReadOnlySet<string> _registeredProviderKeys;
    private readonly FirstRunSetupPreferenceService _preferenceService;
    private readonly IServiceProvider _serviceProvider;

    public FirstRunSetupService(
        IProviderAccountService providerAccountService,
        IEnumerable<IAiProvider> providers,
        FirstRunSetupPreferenceService preferenceService,
        IServiceProvider serviceProvider)
    {
        _providerAccountService = providerAccountService;
        _registeredProviderKeys = providers
            .Select(provider => provider.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _preferenceService = preferenceService;
        _serviceProvider = serviceProvider;
    }

    public async Task ShowIfNeededAsync(Window owner, CancellationToken cancellationToken = default)
    {
        if (await _preferenceService.GetUseDemoModeAsync(cancellationToken) ||
            await HasReadyRealProviderAsync(cancellationToken))
        {
            return;
        }

        var viewModel = _serviceProvider.GetRequiredService<FirstRunSetupViewModel>();
        await viewModel.LoadAsync(cancellationToken);

        var window = new FirstRunSetupWindow(viewModel)
        {
            Owner = owner
        };

        window.ShowDialog();
    }

    private async Task<bool> HasReadyRealProviderAsync(CancellationToken cancellationToken)
    {
        var accounts = await _providerAccountService.ListAsync(cancellationToken);

        return accounts
            .Where(account => !string.Equals(account.ProviderKind, "fake", StringComparison.OrdinalIgnoreCase))
            .Any(account => ProviderHealthEvaluator
                .EvaluateAccount(account, _registeredProviderKeys.Contains(account.Key))
                .IsReady);
    }
}
