using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using BimExplorer.App.Services;
using BimExplorer.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BimExplorer.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedFile))
            UpdateDetailPreview();
    }

    private async void UpdateDetailPreview()
    {
        var selected = _viewModel?.SelectedFile;
        if (selected == null)
        {
            DetailPreviewImage.Source = null;
            DetailExtLabel.Text = "";
            return;
        }

        var preview = LoadDetailPreview(selected);
        if (preview != null)
        {
            DetailPreviewImage.Source = preview;
            DetailExtLabel.Text = "";
            return;
        }

        // FBX: generate on background thread to avoid freezing UI
        if (selected.IsFbx)
        {
            DetailPreviewImage.Source = null;
            DetailExtLabel.Text = "Cargando...";
            var pngData = await Task.Run(() => FbxThumbnailGenerator.GeneratePreview(selected.FilePath, 400));
            // Verify selection hasn't changed
            if (_viewModel?.SelectedFile == selected && pngData is { Length: > 8 })
            {
                DetailPreviewImage.Source = LoadBitmapFromBytes(pngData);
                DetailExtLabel.Text = "";
                return;
            }
        }

        DetailPreviewImage.Source = null;
        DetailExtLabel.Text = selected.Extension;
    }

    private void Gallery_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _viewModel?.OpenSelectedFile();
    }

    private async void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var vm = App.Services.GetRequiredService<SettingsViewModel>();
        var window = new SettingsWindow(vm) { Owner = this };
        window.ShowDialog();

        if (vm.IndexChanged && _viewModel != null)
        {
            await _viewModel.RefreshAfterIndexChangeAsync();
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string filePath)
            OpenFolderForFile(filePath);
    }

    private void ContextOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string filePath)
            OpenFolderForFile(filePath);
    }

    private static void OpenFolderForFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (dir != null && Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
    }

    private static BitmapImage? LoadDetailPreview(BimFileViewModel file)
    {
        // Cached thumbnail (full size)
        if (!string.IsNullOrEmpty(file.ThumbnailPath) && File.Exists(file.ThumbnailPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(file.ThumbnailPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { /* fall through */ }
        }

        // Live extraction for Revit
        if (file.IsRevit)
        {
            var pngData = RevitThumbnailExtractor.ExtractPreviewImage(file.FilePath);
            if (pngData is { Length: > 8 })
                return LoadBitmapFromBytes(pngData);
        }

        // Live generation for DXF
        if (file.IsDxf)
        {
            var pngData = DxfThumbnailGenerator.GeneratePreview(file.FilePath, 400);
            if (pngData is { Length: > 8 })
                return LoadBitmapFromBytes(pngData);
        }

        // FBX is handled async in UpdateDetailPreview to avoid UI freeze

        return null;
    }

    private static BitmapImage? LoadBitmapFromBytes(byte[] data)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(data);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
