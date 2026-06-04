using System.Windows;
using AIM.Desktop.Wpf.ViewModels;

namespace AIM.Desktop.Wpf;

public partial class PendingActionsReviewWindow : Window
{
    public PendingActionsReviewWindow(PendingActionsReviewViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
