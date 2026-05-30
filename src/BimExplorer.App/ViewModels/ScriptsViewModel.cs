using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BimExplorer.Data;
using BimExplorer.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BimExplorer.App.ViewModels;

public class ScriptsViewModel : INotifyPropertyChanged
{
    private readonly BimDbContext _db;
    private Script? _selectedScript;
    private string _statusText = string.Empty;

    public ScriptsViewModel(BimDbContext db)
    {
        _db = db;

        NewScriptCommand = new RelayCommand(_ => NewScript());
        SaveScriptCommand = new RelayCommand(async _ => await SaveScriptAsync(), _ => SelectedScript != null && !string.IsNullOrWhiteSpace(SelectedScript.Name));
        DeleteScriptCommand = new RelayCommand(async _ => await DeleteScriptAsync(), _ => SelectedScript != null && SelectedScript.Id > 0);
        PasteCodeCommand = new RelayCommand(_ => PasteCode(), _ => SelectedScript != null);
        CopyCodeCommand = new RelayCommand(_ => CopyCode(), _ => SelectedScript != null && !string.IsNullOrEmpty(SelectedScript.Code));

        _ = LoadAsync();
    }

    public ObservableCollection<Script> Scripts { get; } = [];

    public IReadOnlyList<string> AvailableTargets { get; } =
        ["Blender", "Revit", "Dynamo", "Python", "PowerShell", "Otro"];

    public Script? SelectedScript
    {
        get => _selectedScript;
        set
        {
            if (SetField(ref _selectedScript, value))
            {
                OnPropertyChanged(nameof(IsScriptSelected));
            }
        }
    }

    public bool IsScriptSelected => SelectedScript != null;

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public ICommand NewScriptCommand { get; }
    public ICommand SaveScriptCommand { get; }
    public ICommand DeleteScriptCommand { get; }
    public ICommand PasteCodeCommand { get; }
    public ICommand CopyCodeCommand { get; }

    public async Task LoadAsync()
    {
        Scripts.Clear();
        var items = await _db.Scripts.OrderBy(s => s.Target).ThenBy(s => s.Name).ToListAsync();
        foreach (var s in items)
            Scripts.Add(s);
    }

    private void NewScript()
    {
        var draft = new Script
        {
            Name = "Nuevo script",
            Target = "Otro",
            Description = string.Empty,
            Code = string.Empty
        };
        Scripts.Add(draft);
        SelectedScript = draft;
        StatusText = "Nuevo script (sin guardar)";
    }

    private async Task SaveScriptAsync()
    {
        if (SelectedScript == null || string.IsNullOrWhiteSpace(SelectedScript.Name)) return;

        SelectedScript.UpdatedAtUtc = DateTime.UtcNow;
        if (SelectedScript.Id == 0)
        {
            SelectedScript.CreatedAtUtc = DateTime.UtcNow;
            _db.Scripts.Add(SelectedScript);
        }

        await _db.SaveChangesAsync();
        var savedId = SelectedScript.Id;
        StatusText = $"Guardado: {SelectedScript.Name}";

        await LoadAsync();
        SelectedScript = Scripts.FirstOrDefault(s => s.Id == savedId);
    }

    private async Task DeleteScriptAsync()
    {
        if (SelectedScript == null || SelectedScript.Id == 0) return;

        var result = MessageBox.Show(
            $"¿Eliminar el script \"{SelectedScript.Name}\"?",
            "Confirmar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        _db.Scripts.Remove(SelectedScript);
        await _db.SaveChangesAsync();
        SelectedScript = null;
        await LoadAsync();
        StatusText = "Script eliminado";
    }

    private void PasteCode()
    {
        if (SelectedScript == null) return;
        try
        {
            if (Clipboard.ContainsText())
            {
                SelectedScript.Code = Clipboard.GetText();
                OnPropertyChanged(nameof(SelectedScript));
                StatusText = "Codigo pegado desde el portapapeles";
            }
            else
            {
                StatusText = "El portapapeles no contiene texto";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error al pegar: {ex.Message}";
        }
    }

    private void CopyCode()
    {
        if (SelectedScript == null || string.IsNullOrEmpty(SelectedScript.Code)) return;
        try
        {
            Clipboard.SetText(SelectedScript.Code);
            StatusText = "Codigo copiado al portapapeles";
        }
        catch (Exception ex)
        {
            StatusText = $"Error al copiar: {ex.Message}";
        }
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
