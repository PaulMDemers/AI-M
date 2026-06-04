using System.Windows;
using AIM.Core.Personalities;
using AIM.Desktop.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Desktop.Wpf.Services;

public interface IMemoryReviewWindowService
{
    Task OpenAsync(Personality personality);
}

public sealed class MemoryReviewWindowService : IMemoryReviewWindowService
{
    private readonly IServiceProvider _serviceProvider;

    public MemoryReviewWindowService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OpenAsync(Personality personality)
    {
        var viewModel = _serviceProvider.GetRequiredService<MemoryReviewViewModel>();
        await viewModel.LoadAsync(personality);

        var window = new MemoryReviewWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.ShowDialog();
    }
}
