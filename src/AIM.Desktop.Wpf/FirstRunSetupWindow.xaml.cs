using System.Windows;
using AIM.Desktop.Wpf.ViewModels;

namespace AIM.Desktop.Wpf;

public partial class FirstRunSetupWindow : Window
{
    public FirstRunSetupWindow(FirstRunSetupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }
}
