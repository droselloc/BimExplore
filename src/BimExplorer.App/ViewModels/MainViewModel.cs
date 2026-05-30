using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BimExplorer.App.Services;
using BimExplorer.Data;
using BimExplorer.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;

namespace BimExplorer.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly BimDbContext _db;
    private readonly FileIndexerService _indexer;
    private readonly ThumbnailService _thumbnailService;
    private readonly DispatcherTimer _searchDebounce;

    private string _searchText = string.Empty;
    private BimFileViewModel? _selectedFile;
    private string _statusText = "Listo";
    private bool _isBusy;
    private string _newTagText = string.Empty;
    private bool _showFbxFiles;
    private bool _showRvtFiles;
    private double _progressValue;
    private double _progressMax = 100;
    private bool _isProgressVisible;

    // Editable fields
    private string _editFileName = string.Empty;
    private string _editProjectName = string.Empty;
    private string _editDiscipline = string.Empty;
    private string _editAuthorName = string.Empty;

    public MainViewModel(BimDbContext db, FileIndexerService indexer, ThumbnailService thumbnailService, ScriptsViewModel scriptsViewModel)
    {
        _db = db;
        _indexer = indexer;
        _thumbnailService = thumbnailService;
        Scripts = scriptsViewModel;

        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce.Stop();
            ApplyFilter();
        };

        SearchCommand = new RelayCommand(_ => ApplyFilter(), _ => !IsBusy);
        IndexDirectoryCommand = new RelayCommand(async _ => await IndexDirectoryAsync(), _ => !IsBusy);
        ClearCommand = new RelayCommand(_ => Clear(), _ => !IsBusy);
        LoadAllCommand = new RelayCommand(async _ => await LoadAllAsync(), _ => !IsBusy);
        OpenFileCommand = new RelayCommand(_ => OpenSelectedFile(), _ => SelectedFile != null);
        AddTagCommand = new RelayCommand(async _ => await AddTagAsync(), _ => SelectedFile != null && !string.IsNullOrWhiteSpace(NewTagText));
        RemoveTagCommand = new RelayCommand(async p => await RemoveTagAsync(p as string), _ => SelectedFile != null);
        SavePropertiesCommand = new RelayCommand(async _ => await SavePropertiesAsync(), _ => SelectedFile != null);
        CleanRevitVersionsCommand = new RelayCommand(async _ => await CleanRevitVersionsAsync(), _ => !IsBusy);
        RefreshCommand = new RelayCommand(async _ => await RefreshGalleryAsync(), _ => !IsBusy);
        RenameFileCommand = new RelayCommand(async p => await RenameFileAsync(p as BimFileViewModel), p => p is BimFileViewModel);
        DeleteFileFromIndexCommand = new RelayCommand(async p => await DeleteFileFromIndexAsync(p as BimFileViewModel), p => p is BimFileViewModel);
        DeleteFileFromDiskCommand = new RelayCommand(async p => await DeleteFileFromDiskAsync(p as BimFileViewModel), p => p is BimFileViewModel);

        _ = LoadAllAsync();
    }

    public ScriptsViewModel Scripts { get; }

    // All loaded files (unfiltered)
    private List<BimFileViewModel> _allFiles = [];

    // Filtered files shown in gallery
    public ObservableCollection<BimFileViewModel> BimFiles { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                _searchDebounce.Stop();
                _searchDebounce.Start();
            }
        }
    }

    public bool ShowFbxFiles
    {
        get => _showFbxFiles;
        set
        {
            if (SetField(ref _showFbxFiles, value))
                ApplyFilter();
        }
    }

    public bool ShowRvtFiles
    {
        get => _showRvtFiles;
        set
        {
            if (SetField(ref _showRvtFiles, value))
                ApplyFilter();
        }
    }

    public BimFileViewModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetField(ref _selectedFile, value))
            {
                OnPropertyChanged(nameof(SelectedFileTags));
                LoadEditableFields();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetField(ref _progressValue, value);
    }

    public double ProgressMax
    {
        get => _progressMax;
        set => SetField(ref _progressMax, value);
    }

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        set => SetField(ref _isProgressVisible, value);
    }

    public string NewTagText
    {
        get => _newTagText;
        set => SetField(ref _newTagText, value);
    }

    // Editable properties
    public string EditFileName
    {
        get => _editFileName;
        set => SetField(ref _editFileName, value);
    }

    public string EditProjectName
    {
        get => _editProjectName;
        set => SetField(ref _editProjectName, value);
    }

    public string EditDiscipline
    {
        get => _editDiscipline;
        set => SetField(ref _editDiscipline, value);
    }

    public string EditAuthorName
    {
        get => _editAuthorName;
        set => SetField(ref _editAuthorName, value);
    }

    public ObservableCollection<string> SelectedFileTags
    {
        get
        {
            var tags = new ObservableCollection<string>();
            if (SelectedFile == null) return tags;

            var fileTags = _db.BimFileTags
                .Where(ft => ft.BimFileId == SelectedFile.Id)
                .Include(ft => ft.Tag)
                .Select(ft => ft.Tag.Name)
                .ToList();
            foreach (var t in fileTags) tags.Add(t);
            return tags;
        }
    }

    public ICommand SearchCommand { get; }
    public ICommand IndexDirectoryCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand LoadAllCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand AddTagCommand { get; }
    public ICommand RemoveTagCommand { get; }
    public ICommand SavePropertiesCommand { get; }
    public ICommand CleanRevitVersionsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand RenameFileCommand { get; }
    public ICommand DeleteFileFromIndexCommand { get; }
    public ICommand DeleteFileFromDiskCommand { get; }

    private void LoadEditableFields()
    {
        if (SelectedFile == null)
        {
            EditFileName = string.Empty;
            EditProjectName = string.Empty;
            EditDiscipline = string.Empty;
            EditAuthorName = string.Empty;
            return;
        }

        EditFileName = SelectedFile.FileName;
        EditProjectName = SelectedFile.ProjectName ?? string.Empty;
        EditDiscipline = SelectedFile.Discipline ?? string.Empty;
        EditAuthorName = SelectedFile.AuthorName ?? string.Empty;
    }

    private async Task SavePropertiesAsync()
    {
        if (SelectedFile == null) return;

        var entity = await _db.BimFiles.FindAsync(SelectedFile.Id);
        if (entity == null) return;

        var oldPath = entity.FilePath;
        var nameChanged = !string.IsNullOrWhiteSpace(EditFileName) && EditFileName != entity.FileName;

        // Rename actual file if name changed
        if (nameChanged)
        {
            var dir = Path.GetDirectoryName(oldPath)!;
            var newPath = Path.Combine(dir, EditFileName);
            if (File.Exists(oldPath) && !File.Exists(newPath))
            {
                File.Move(oldPath, newPath);
                entity.FileName = EditFileName;
                entity.FilePath = newPath;
            }
            else if (!File.Exists(oldPath))
            {
                // File doesn't exist at old path, just update DB
                entity.FileName = EditFileName;
            }
            else
            {
                StatusText = "Ya existe un archivo con ese nombre";
                return;
            }
        }

        entity.ProjectName = string.IsNullOrWhiteSpace(EditProjectName) ? null : EditProjectName;
        entity.Discipline = string.IsNullOrWhiteSpace(EditDiscipline) ? null : EditDiscipline;
        entity.AuthorName = string.IsNullOrWhiteSpace(EditAuthorName) ? null : EditAuthorName;

        await _db.SaveChangesAsync();
        StatusText = "Propiedades guardadas";

        // Refresh the list
        await LoadAllAsync();
    }

    private void ApplyFilter()
    {
        var query = SearchText?.Trim();
        var hasSearch = !string.IsNullOrEmpty(query);
        var queryLower = hasSearch ? query!.ToLowerInvariant() : string.Empty;

        // Fast path: precomputed lowercase fields + no StringComparison overhead
        var showFbx = ShowFbxFiles;
        var showRvt = ShowRvtFiles;
        var source = _allFiles;
        var results = new List<BimFileViewModel>(source.Count);

        for (var i = 0; i < source.Count; i++)
        {
            var f = source[i];
            if (!(f.IsRfa || (showFbx && f.IsFbx) || (showRvt && f.IsRvt)))
                continue;

            if (hasSearch && !f.MatchesSearch(queryLower))
                continue;

            results.Add(f);
        }

        BimFiles.Clear();
        foreach (var file in results)
            BimFiles.Add(file);

        StatusText = hasSearch
            ? $"{BimFiles.Count} resultado(s) — \"{SearchText}\""
            : $"{BimFiles.Count} archivo(s) indexado(s)";
    }

    private async Task LoadAllAsync()
    {
        var files = await _db.BimFiles
            .AsNoTracking()
            .Include(f => f.BimFileTags).ThenInclude(ft => ft.Tag)
            .OrderBy(f => f.FilePath)
            .ToListAsync();

        _allFiles = files.Select(f => new BimFileViewModel(f)).ToList();

        ApplyFilter();
    }

    /// <summary>
    /// Called by Settings window after folders were removed so the
    /// gallery reflects the new index state.
    /// </summary>
    public async Task RefreshAfterIndexChangeAsync()
    {
        await LoadAllAsync();
    }

    private async Task IndexDirectoryAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleccionar carpeta con archivos RFA"
        };

        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        IsProgressVisible = true;
        ProgressValue = 0;
        StatusText = "Indexando...";

        try
        {
            var progress = new Progress<(string message, int current, int total)>(p =>
            {
                StatusText = p.message;
                ProgressMax = p.total;
                ProgressValue = p.current;
            });
            var count = await _indexer.IndexDirectoryAsync(dialog.FolderName, progress, includeFbx: ShowFbxFiles, includeRvt: ShowRvtFiles);
            await LoadAllAsync();
            StatusText = $"Indexados {count} archivo(s) nuevo(s). Total: {BimFiles.Count}";
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            StatusText = $"Error: {msg}";
        }
        finally
        {
            IsBusy = false;
            IsProgressVisible = false;
        }
    }

    private void Clear()
    {
        SearchText = string.Empty;
    }

    public void OpenSelectedFile()
    {
        if (SelectedFile == null) return;
        try
        {
            Process.Start(new ProcessStartInfo(SelectedFile.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo abrir: {ex.Message}";
        }
    }

    private async Task AddTagAsync()
    {
        if (SelectedFile == null || string.IsNullOrWhiteSpace(NewTagText)) return;

        var tagName = NewTagText.Trim();
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
        if (tag == null)
        {
            tag = new Tag { Name = tagName };
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();
        }

        var exists = await _db.BimFileTags.AnyAsync(ft =>
            ft.BimFileId == SelectedFile.Id && ft.TagId == tag.Id);

        if (!exists)
        {
            _db.BimFileTags.Add(new BimFileTag
            {
                BimFileId = SelectedFile.Id,
                TagId = tag.Id
            });
            await _db.SaveChangesAsync();
        }

        NewTagText = string.Empty;
        OnPropertyChanged(nameof(SelectedFileTags));
    }

    private async Task RemoveTagAsync(string? tagName)
    {
        if (SelectedFile == null || string.IsNullOrEmpty(tagName)) return;

        var fileTag = await _db.BimFileTags
            .Include(ft => ft.Tag)
            .FirstOrDefaultAsync(ft =>
                ft.BimFileId == SelectedFile.Id && ft.Tag.Name == tagName);

        if (fileTag != null)
        {
            _db.BimFileTags.Remove(fileTag);
            await _db.SaveChangesAsync();
            OnPropertyChanged(nameof(SelectedFileTags));
        }
    }

    private async Task RefreshGalleryAsync()
    {
        IsBusy = true;
        IsProgressVisible = true;
        ProgressValue = 0;
        StatusText = "Actualizando galeria...";

        try
        {
            var allFiles = await _db.BimFiles.ToListAsync();
            var removed = 0;
            var regenerated = 0;
            var skippedLarge = 0;

            // Ask about large files (>50 MB)
            const long largeFileThreshold = 50 * 1024 * 1024; // 50 MB
            var hasLargeFiles = allFiles.Any(f => f.FileSizeBytes > largeFileThreshold);
            var includeLargeFiles = true;

            if (hasLargeFiles)
            {
                var result = MessageBox.Show(
                    "Hay archivos mayores de 50 MB en la galeria.\n\n¿Desea incluirlos en la actualizacion?\n\n(Puede tardar bastante mas tiempo)",
                    "Archivos grandes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                includeLargeFiles = result == MessageBoxResult.Yes;
            }

            // 1. Remove entries for files that no longer exist on disk
            var stale = allFiles.Where(f => !File.Exists(f.FilePath)).ToList();
            if (stale.Count > 0)
            {
                _db.BimFiles.RemoveRange(stale);
                await _db.SaveChangesAsync();
                removed = stale.Count;
            }

            // 2. Regenerate thumbnails (missing or forced refresh)
            var remaining = await _db.BimFiles.ToListAsync();
            ProgressMax = remaining.Count;

            for (var i = 0; i < remaining.Count; i++)
            {
                var file = remaining[i];
                ProgressValue = i + 1;

                // Skip large files if user chose not to include them
                if (!includeLargeFiles && file.FileSizeBytes > largeFileThreshold)
                {
                    StatusText = $"Saltando archivo grande: {file.FileName} ({i + 1}/{remaining.Count})";
                    skippedLarge++;
                    continue;
                }

                StatusText = $"Regenerando preview: {file.FileName} ({i + 1}/{remaining.Count})";

                // Delete old cached thumbnail to force regeneration
                if (!string.IsNullOrEmpty(file.ThumbnailPath) && File.Exists(file.ThumbnailPath))
                    File.Delete(file.ThumbnailPath);

                var newThumb = await Task.Run(() => _thumbnailService.GenerateThumbnail(file.FilePath, file.FileHash));
                if (newThumb != file.ThumbnailPath)
                {
                    file.ThumbnailPath = newThumb;
                    regenerated++;
                }
            }

            await _db.SaveChangesAsync();
            await LoadAllAsync();

            var statusParts = new List<string>
            {
                $"{regenerated} previews regenerados",
                $"{removed} eliminados"
            };
            if (skippedLarge > 0)
                statusParts.Add($"{skippedLarge} grandes omitidos");

            StatusText = $"Galeria actualizada: {string.Join(", ", statusParts)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsProgressVisible = false;
        }
    }

    private async Task RenameFileAsync(BimFileViewModel? fileVm)
    {
        if (fileVm == null) return;

        var entity = await _db.BimFiles.FindAsync(fileVm.Id);
        if (entity == null) return;

        var dialog = new Views.RenameDialog("Nuevo nombre de archivo:", entity.FileName)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true) return;

        var newName = dialog.NewName;
        if (newName == entity.FileName) return;

        var dir = Path.GetDirectoryName(entity.FilePath)!;
        var newPath = Path.Combine(dir, newName);

        if (File.Exists(entity.FilePath) && !File.Exists(newPath))
        {
            File.Move(entity.FilePath, newPath);
            entity.FileName = newName;
            entity.FilePath = newPath;
            await _db.SaveChangesAsync();
            await LoadAllAsync();
            StatusText = $"Archivo renombrado a: {newName}";
        }
        else if (File.Exists(newPath))
        {
            StatusText = "Ya existe un archivo con ese nombre";
        }
        else
        {
            entity.FileName = newName;
            entity.FilePath = newPath;
            await _db.SaveChangesAsync();
            await LoadAllAsync();
            StatusText = $"Registro actualizado: {newName}";
        }
    }

    private async Task DeleteFileFromIndexAsync(BimFileViewModel? fileVm)
    {
        if (fileVm == null) return;

        var result = MessageBox.Show(
            $"¿Eliminar \"{fileVm.FileName}\" del indice?\n\nEl archivo NO se borrara del disco.",
            "Confirmar eliminacion del indice",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var entity = await _db.BimFiles.FindAsync(fileVm.Id);
        if (entity == null) return;

        // Delete thumbnail only
        if (!string.IsNullOrEmpty(entity.ThumbnailPath) && File.Exists(entity.ThumbnailPath))
            File.Delete(entity.ThumbnailPath);

        _db.BimFiles.Remove(entity);
        await _db.SaveChangesAsync();
        await LoadAllAsync();
        StatusText = $"Eliminado del indice: {fileVm.FileName}";
    }

    private async Task DeleteFileFromDiskAsync(BimFileViewModel? fileVm)
    {
        if (fileVm == null) return;

        var result = MessageBox.Show(
            $"¿Eliminar \"{fileVm.FileName}\"?\n\nSe movera a la papelera de reciclaje.",
            "Confirmar eliminacion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var entity = await _db.BimFiles.FindAsync(fileVm.Id);
        if (entity == null) return;

        // Move to recycle bin
        if (File.Exists(entity.FilePath))
        {
            try
            {
                FileSystem.DeleteFile(entity.FilePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            catch (Exception ex)
            {
                StatusText = $"Error al borrar archivo: {ex.Message}";
                return;
            }
        }

        // Delete thumbnail
        if (!string.IsNullOrEmpty(entity.ThumbnailPath) && File.Exists(entity.ThumbnailPath))
            File.Delete(entity.ThumbnailPath);

        _db.BimFiles.Remove(entity);
        await _db.SaveChangesAsync();
        await LoadAllAsync();
        StatusText = $"Archivo movido a la papelera: {fileVm.FileName}";
    }

    private static readonly Regex RevitVersionPattern = new(@"\.\d{3,4}\.(rvt|rfa|rte)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private async Task CleanRevitVersionsAsync()
    {
        var folderDialog = new OpenFolderDialog
        {
            Title = "Seleccionar carpeta para buscar versiones de Revit"
        };

        if (folderDialog.ShowDialog() != true)
            return;

        IsBusy = true;
        StatusText = "Buscando versiones de Revit...";

        try
        {
            var versionFiles = await Task.Run(() =>
                Directory.EnumerateFiles(folderDialog.FolderName, "*.*", System.IO.SearchOption.AllDirectories)
                    .Where(f => RevitVersionPattern.IsMatch(f))
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.FullName)
                    .ToList());

            if (versionFiles.Count == 0)
            {
                MessageBox.Show(
                    "No se encontraron archivos de version de Revit\n(patron: *.000?.rvt/.rfa/.rte)",
                    "Sin resultados", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText = "No se encontraron versiones de Revit";
                return;
            }

            // Show selection dialog
            var cleanDialog = new Views.CleanVersionsDialog(versionFiles)
            {
                Owner = Application.Current.MainWindow
            };

            if (cleanDialog.ShowDialog() == true)
            {
                // Remove deleted files from DB
                if (cleanDialog.DeletedPaths.Count > 0)
                {
                    var deletedSet = new HashSet<string>(cleanDialog.DeletedPaths, StringComparer.OrdinalIgnoreCase);
                    var toRemove = await _db.BimFiles
                        .Where(f => deletedSet.Contains(f.FilePath))
                        .ToListAsync();

                    if (toRemove.Count > 0)
                    {
                        _db.BimFiles.RemoveRange(toRemove);
                        await _db.SaveChangesAsync();
                    }

                    await LoadAllAsync();
                }

                StatusText = $"Limpieza completada: {cleanDialog.DeletedCount} eliminados, " +
                             $"{cleanDialog.ErrorCount} errores. Liberados {FormatSize(cleanDialog.FreedBytes)}";
            }
            else
            {
                StatusText = "Limpieza cancelada";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{bytes / 1_024.0:F0} KB",
            _ => $"{bytes} B"
        };
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
