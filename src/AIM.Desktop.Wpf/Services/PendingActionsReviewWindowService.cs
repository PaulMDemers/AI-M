using System.Windows;
using AIM.Desktop.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Desktop.Wpf.Services;

public interface IPendingActionsReviewWindowService
{
    Task OpenAsync();
}

public sealed class PendingActionsReviewWindowService : IPendingActionsReviewWindowService
{
    private readonly IServiceProvider _serviceProvider;

    public PendingActionsReviewWindowService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task OpenAsync()
    {
        var viewModel = _serviceProvider.GetRequiredService<PendingActionsReviewViewModel>();
        viewModel.Refresh();

        var window = new PendingActionsReviewWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.ShowDialog();
        return Task.CompletedTask;
    }
}
