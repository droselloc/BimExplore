using System.Windows;

namespace BimExplorer.App.Views;

public partial class RenameDialog : Window
{
    public string NewName => NameTextBox.Text.Trim();

    public RenameDialog(string prompt, string currentName)
    {
        InitializeComponent();
        PromptLabel.Text = prompt;
        NameTextBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NameTextBox.Text))
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
