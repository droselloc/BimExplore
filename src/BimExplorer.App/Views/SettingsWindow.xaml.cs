using System.Windows;
using BimExplorer.App.ViewModels;

namespace BimExplorer.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        DataContext = vm;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = ViewModel.IndexChanged;
        Close();
    }
}
