using System.Windows;
using System.Windows.Input;
using AIM.Desktop.Wpf.ViewModels;

namespace AIM.Desktop.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ApplyShellSize(viewModel);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainWindowViewModel.ShellWidth) or nameof(MainWindowViewModel.ShellMinWidth))
            {
                ApplyShellSize(viewModel);
            }
        };
        Loaded += (_, _) => ApplyShellSize(viewModel);
    }

    private void ApplyShellSize(MainWindowViewModel viewModel)
    {
        MinWidth = viewModel.ShellMinWidth;
        Width = viewModel.ShellWidth;
    }

    private async void FriendsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.OpenFloatingChatCommand.CanExecute(null))
        {
            e.Handled = true;
            await viewModel.OpenFloatingChatCommand.ExecuteAsync(null);
        }
    }
}
