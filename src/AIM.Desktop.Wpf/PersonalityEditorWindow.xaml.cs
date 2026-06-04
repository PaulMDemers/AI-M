using System.Windows;
using AIM.Desktop.Wpf.ViewModels;

namespace AIM.Desktop.Wpf;

public partial class PersonalityEditorWindow : Window
{
    private readonly PersonalityEditorViewModel _viewModel;

    public PersonalityEditorWindow(PersonalityEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _viewModel.WasSavedOrDeleted;
        Close();
    }
}
