using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HorosSaver.Models;

namespace HorosSaver.ViewModels;

public partial class EditSnapshotViewModel : ViewModelBase
{
    public EditSnapshotViewModel(
        ProgramProfile profile,
        SnapshotInfo snapshot,
        string currentDirectory)
    {
        Profile = profile;
        Snapshot = snapshot;
        DisplayName = snapshot.Name;
        StorageRoot = snapshot.IsExternal
            ? TryGetExternalStorageRoot(currentDirectory) ?? string.Empty
            : string.Empty;
        CurrentStoragePath = currentDirectory;
        StoragePathLabel = currentDirectory;
    }

    public ProgramProfile Profile { get; }
    public SnapshotInfo Snapshot { get; }
    public string CurrentStoragePath { get; }
    public string StoragePathLabel { get; }

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _storageRoot;

    [ObservableProperty]
    private string _statusMessage = "Name und optional neuen Speicherort festlegen.";

    public EditSnapshotResult? Result { get; private set; }

    public event Action? CloseRequested;

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    [RelayCommand]
    private void Confirm()
    {
        var trimmedName = DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            StatusMessage = "Bitte einen Snapshot-Namen eingeben.";
            return;
        }

        var trimmedRoot = StorageRoot.Trim();
        Result = new EditSnapshotResult(trimmedName, string.IsNullOrWhiteSpace(trimmedRoot) ? null : trimmedRoot);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private async Task BrowseStorageRootAsync()
    {
        if (HostWindow is null)
        {
            return;
        }

        var folders = await HostWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Neuen Snapshot-Speicherort auswählen",
            AllowMultiple = false
        }).ConfigureAwait(true);

        var folder = folders.FirstOrDefault();
        if (folder is not null)
        {
            StorageRoot = folder.Path.LocalPath;
            StatusMessage = $"Neuer Speicherort: {StorageRoot}";
        }
    }

    public Window? HostWindow { get; set; }

    private static string? TryGetExternalStorageRoot(string snapshotDir)
    {
        var programFolder = Path.GetDirectoryName(snapshotDir);
        var snapshotRoot = programFolder is null ? null : Path.GetDirectoryName(programFolder);
        return snapshotRoot is null ? null : Path.GetDirectoryName(snapshotRoot);
    }
}

public sealed class EditSnapshotResult
{
    public EditSnapshotResult(string displayName, string? newStorageRoot)
    {
        DisplayName = displayName;
        NewStorageRoot = newStorageRoot;
    }

    public string DisplayName { get; }
    public string? NewStorageRoot { get; }
}
