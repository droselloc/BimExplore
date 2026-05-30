using System.ComponentModel;
using System.IO;
using System.Windows;

namespace BimExplorer.App.Views;

public partial class CleanVersionsDialog : Window
{
    public List<VersionFileItem> Files { get; }
    public int DeletedCount { get; private set; }
    public int ErrorCount { get; private set; }
    public long FreedBytes { get; private set; }
    public List<string> DeletedPaths { get; } = [];

    public CleanVersionsDialog(List<FileInfo> files)
    {
        InitializeComponent();

        Files = files.Select(f => new VersionFileItem
        {
            IsSelected = true,
            FileName = f.Name,
            FullPath = f.FullName,
            Folder = Path.GetDirectoryName(f.FullName) ?? "",
            SizeBytes = f.Length,
            SizeText = FormatSize(f.Length)
        }).ToList();

        FileListView.ItemsSource = Files;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var selected = Files.Count(f => f.IsSelected);
        var totalSize = Files.Where(f => f.IsSelected).Sum(f => f.SizeBytes);
        SummaryText.Text = $"{Files.Count} archivos encontrados · {FormatSize(Files.Sum(f => f.SizeBytes))} en total";
        SelectionCountText.Text = $"{selected} seleccionado(s) · {FormatSize(totalSize)}";
        DeleteButton.IsEnabled = selected > 0;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var f in Files) f.IsSelected = true;
        FileListView.Items.Refresh();
        UpdateSummary();
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var f in Files) f.IsSelected = false;
        FileListView.Items.Refresh();
        UpdateSummary();
    }

    private void CheckChanged(object sender, RoutedEventArgs e) => UpdateSummary();

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var toDelete = Files.Where(f => f.IsSelected).ToList();
        if (toDelete.Count == 0) return;

        // Disable UI during deletion
        DeleteButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        FileListView.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressBar.Maximum = toDelete.Count;
        ProgressBar.Value = 0;

        DeletedCount = 0;
        ErrorCount = 0;
        FreedBytes = 0;

        foreach (var file in toDelete)
        {
            try
            {
                var size = file.SizeBytes;
                await Task.Run(() =>
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        file.FullPath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin));

                DeletedCount++;
                FreedBytes += size;
                file.Status = "Eliminado";
                DeletedPaths.Add(file.FullPath);
            }
            catch
            {
                ErrorCount++;
                file.Status = "Error";
            }

            ProgressBar.Value = DeletedCount + ErrorCount;
            ProgressText.Text = $"Procesando... {DeletedCount + ErrorCount}/{toDelete.Count}";
        }

        // Show completion message
        var msg = $"Proceso completado:\n\n" +
                  $"   Enviados a papelera: {DeletedCount}\n" +
                  $"   Errores: {ErrorCount}\n" +
                  $"   Espacio liberado: {FormatSize(FreedBytes)}";

        MessageBox.Show(this, msg, "Limpieza completada",
            MessageBoxButton.OK, MessageBoxImage.Information);

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1_024 => $"{bytes / 1_024.0:F0} KB",
        _ => $"{bytes} B"
    };
}

public class VersionFileItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }

    public required string FileName { get; set; }
    public required string FullPath { get; set; }
    public required string Folder { get; set; }
    public long SizeBytes { get; set; }
    public required string SizeText { get; set; }
    public string? Status { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
