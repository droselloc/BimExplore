using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BimExplorer.Data;
using BimExplorer.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BimExplorer.App.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly BimDbContext _db;
    private string _statusText = string.Empty;
    private bool _indexChanged;

    public SettingsViewModel(BimDbContext db)
    {
        _db = db;

        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        RemoveFolderCommand = new RelayCommand(async p => await RemoveFolderAsync(p as IndexedFolder),
            p => p is IndexedFolder);

        _ = LoadAsync();
    }

    public ObservableCollection<IndexedFolder> Folders { get; } = [];

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    /// <summary>
    /// True when the user removed at least one folder. The caller
    /// (main window) uses this to know whether to refresh its gallery.
    /// </summary>
    public bool IndexChanged
    {
        get => _indexChanged;
        private set => SetField(ref _indexChanged, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand RemoveFolderCommand { get; }

    public async Task LoadAsync()
    {
        Folders.Clear();
        var list = await _db.IndexedFolders.OrderBy(f => f.Path).ToListAsync();
        foreach (var f in list)
            Folders.Add(f);
        StatusText = $"{Folders.Count} carpeta(s) indexada(s)";
    }

    private async Task RemoveFolderAsync(IndexedFolder? folder)
    {
        if (folder == null) return;

        var filesCount = await _db.BimFiles.CountAsync(f => f.FilePath.StartsWith(folder.Path));

        var result = MessageBox.Show(
            $"¿Dejar de indexar esta carpeta?\n\n{folder.Path}\n\n" +
            $"Se eliminaran del indice {filesCount} archivo(s). Los archivos NO se borran del disco.",
            "Confirmar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var files = await _db.BimFiles
            .Where(f => f.FilePath.StartsWith(folder.Path))
            .ToListAsync();

        foreach (var file in files)
        {
            if (!string.IsNullOrEmpty(file.ThumbnailPath) && System.IO.File.Exists(file.ThumbnailPath))
            {
                try { System.IO.File.Delete(file.ThumbnailPath); } catch { /* ignore */ }
            }
        }

        _db.BimFiles.RemoveRange(files);
        _db.IndexedFolders.Remove(folder);
        await _db.SaveChangesAsync();

        IndexChanged = true;
        await LoadAsync();
        StatusText = $"Carpeta eliminada del indice ({files.Count} archivo(s))";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
