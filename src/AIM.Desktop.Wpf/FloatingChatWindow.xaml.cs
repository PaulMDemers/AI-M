using System.Windows;
using AIM.Desktop.Wpf.ViewModels;

namespace AIM.Desktop.Wpf;

public partial class FloatingChatWindow : Window
{
    public FloatingChatWindow(ChatSessionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
