using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HorosSaver.Models;
using HorosSaver.Services;

namespace HorosSaver.ViewModels;

public partial class SnapshotCaptureTargetViewModel : ViewModelBase
{
    private readonly IStoragePathResolver _paths;
    private readonly AppSettings _settings;
    private readonly ProgramProfile _profile;

    public SnapshotCaptureTargetViewModel(
        IStoragePathResolver paths,
        AppSettings settings,
        ProgramProfile profile)
    {
        _paths = paths;
        _settings = settings;
        _profile = profile;

        StandardInternalLabel = $"Standard (intern): {_paths.DataRoot}\\snapshots\\{profile.Id}";
        ProfileDefaultPath = ResolveProfileDefaultPath();
        HasProfileDefault = !string.IsNullOrWhiteSpace(ProfileDefaultPath);
        ProfileDefaultLabel = HasProfileDefault
            ? $"Profil-Standard: {ProfileDefaultPath}"
            : "Profil-Standard (nicht konfiguriert)";

        SelectedMode = HasProfileDefault
            ? SnapshotCaptureTargetMode.ProfileDefault
            : SnapshotCaptureTargetMode.StandardInternal;
    }

    public bool IsStandardInternalMode => SelectedMode == SnapshotCaptureTargetMode.StandardInternal;
    public bool IsProfileDefaultMode => SelectedMode == SnapshotCaptureTargetMode.ProfileDefault;
    public bool IsCustomFolderMode => SelectedMode == SnapshotCaptureTargetMode.CustomFolder;

    public string StandardInternalLabel { get; }
    public string ProfileDefaultLabel { get; }
    public string? ProfileDefaultPath { get; }
    public bool HasProfileDefault { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStandardInternalMode))]
    [NotifyPropertyChangedFor(nameof(IsProfileDefaultMode))]
    [NotifyPropertyChangedFor(nameof(IsCustomFolderMode))]
    private SnapshotCaptureTargetMode _selectedMode;

    [ObservableProperty]
    private string _customFolderPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Zielort für den neuen Snapshot auswählen.";

    public SnapshotCaptureTargetChoice? Result { get; private set; }

    public event Action? CloseRequested;

    [RelayCommand]
    private void SetMode(string? modeName)
    {
        SelectedMode = modeName switch
        {
            nameof(SnapshotCaptureTargetMode.ProfileDefault) => SnapshotCaptureTargetMode.ProfileDefault,
            nameof(SnapshotCaptureTargetMode.CustomFolder) => SnapshotCaptureTargetMode.CustomFolder,
            _ => SnapshotCaptureTargetMode.StandardInternal
        };
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    [RelayCommand]
    private void Confirm()
    {
        if (SelectedMode == SnapshotCaptureTargetMode.CustomFolder
            && string.IsNullOrWhiteSpace(CustomFolderPath))
        {
            StatusMessage = "Bitte einen Zielordner auswählen oder eine andere Option wählen.";
            return;
        }

        if (SelectedMode == SnapshotCaptureTargetMode.ProfileDefault && !HasProfileDefault)
        {
            StatusMessage = "Kein Profil-Standard konfiguriert — bitte andere Option wählen.";
            return;
        }

        Result = new SnapshotCaptureTargetChoice
        {
            Mode = SelectedMode,
            CustomFolderPath = SelectedMode == SnapshotCaptureTargetMode.CustomFolder
                ? CustomFolderPath.Trim()
                : null
        };

        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private async Task BrowseCustomFolderAsync()
    {
        if (HostWindow is null)
        {
            return;
        }

        var folders = await HostWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Snapshot-Zielordner auswählen",
            AllowMultiple = false
        }).ConfigureAwait(true);

        var folder = folders.FirstOrDefault();
        if (folder is not null)
        {
            CustomFolderPath = folder.Path.LocalPath;
            SelectedMode = SnapshotCaptureTargetMode.CustomFolder;
            StatusMessage = $"Zielordner: {CustomFolderPath}";
        }
    }

    private string? ResolveProfileDefaultPath()
    {
        if (!string.IsNullOrWhiteSpace(_profile.CustomSnapshotRoot))
        {
            return _profile.CustomSnapshotRoot.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_settings.DefaultSnapshotRoot))
        {
            return _settings.DefaultSnapshotRoot.Trim();
        }

        return null;
    }

    public Window? HostWindow { get; set; }
}
