using System.Windows;
using AIM.Core.Personalities;
using AIM.Desktop.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AIM.Desktop.Wpf.Services;

public interface IPersonalityEditorWindowService
{
    Task<bool> OpenAsync(Personality? personality);
}

public sealed class PersonalityEditorWindowService : IPersonalityEditorWindowService
{
    private readonly IServiceProvider _serviceProvider;

    public PersonalityEditorWindowService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> OpenAsync(Personality? personality)
    {
        var viewModel = _serviceProvider.GetRequiredService<PersonalityEditorViewModel>();
        await viewModel.LoadAsync(personality);

        var window = new PersonalityEditorWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        return window.ShowDialog() == true || viewModel.WasSavedOrDeleted;
    }
}
