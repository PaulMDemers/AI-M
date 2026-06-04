using System.Windows;
using AIM.Desktop.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Desktop.Wpf.Services;

public interface IProviderSettingsWindowService
{
    Task OpenAsync(string? focusedProviderKey = null);
}

public sealed class ProviderSettingsWindowService : IProviderSettingsWindowService
{
    private readonly IServiceProvider _serviceProvider;

    public ProviderSettingsWindowService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OpenAsync(string? focusedProviderKey = null)
    {
        var viewModel = _serviceProvider.GetRequiredService<ProviderSettingsViewModel>();
        await viewModel.LoadAsync(focusedProviderKey);

        var window = new ProviderSettingsWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.ShowDialog();
    }
}
