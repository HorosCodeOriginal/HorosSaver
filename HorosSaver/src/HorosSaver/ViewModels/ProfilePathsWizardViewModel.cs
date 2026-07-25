using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HorosSaver.Models;
using HorosSaver.Services;

namespace HorosSaver.ViewModels;

public partial class ProfilePathsWizardViewModel : ViewModelBase
{
    private readonly ProfilePathsWizardMode _mode;
    private readonly ProgramProfile? _existingProfile;
    private readonly IReadOnlyCollection<string> _existingIds;
    private readonly int _sortOrder;
    private bool _isApplyingCursorLevel;

    public ProfilePathsWizardViewModel(
        ProfilePathsWizardMode mode,
        IReadOnlyList<ProgramProfile> existingProfiles,
        ProgramProfile? existingProfile = null,
        CursorSnapshotLevel? defaultCursorSnapshotLevel = null)
    {
        _mode = mode;
        _existingProfile = existingProfile;
        _existingIds = existingProfiles.Select(profile => profile.Id).ToHashSet();
        _sortOrder = existingProfiles.Count;
        CursorSnapshotLevelOptions = CursorSnapshotLevelOptionViewModel.CreateAll();

        PathHint = mode == ProfilePathsWizardMode.EditExisting
            ? "Pfade ergänzen oder entfernen. Fehlende Quellen werden beim Snapshot übersprungen."
            : "Benennen Sie das Profil und wählen Sie Dateien und/oder Ordner für Snapshots.";

        if (mode == ProfilePathsWizardMode.EditExisting && existingProfile is not null)
        {
            ProfileName = existingProfile.Name;
            CustomSnapshotRoot = existingProfile.CustomSnapshotRoot ?? string.Empty;
            IsCursorProfile = CursorSnapshotPaths.IsCursorProfile(existingProfile);
            if (IsCursorProfile)
            {
                PathHint = "Cursor Snapshot-Level wählen — Pfade werden automatisch gesetzt. " +
                           "Einzelne Pfade können danach ergänzt oder entfernt werden.";
                SelectedCursorSnapshotLevelOption = ResolveCursorSnapshotLevelOption(
                    CursorSnapshotPaths.NormalizeLevel(existingProfile.CursorSnapshotLevel));
                CursorSnapshotLevelDescription = CursorSnapshotPaths.GetLevelDescription(
                    SelectedCursorSnapshotLevelOption.Level);
                CursorSnapshotSecretsHint = CursorSnapshotPaths.GetSecretsHint(SelectedCursorSnapshotLevelOption.Level);
                ShowCursorSecretsHint = !string.IsNullOrWhiteSpace(CursorSnapshotSecretsHint);
            }

            foreach (var path in existingProfile.Paths)
            {
                PathEntries.Add(BindPathEntryViewModel.FromProfilePath(path));
            }

            StatusMessage = $"{existingProfile.Name}: {existingProfile.Paths.Count} Pfad(e) geladen.";
        }
        else
        {
            StatusMessage = "Mindestens ein Pfad erforderlich.";
        }

        PathEntries.CollectionChanged += (_, _) => FinishCommand.NotifyCanExecuteChanged();
    }

    public event Action<ProgramProfile>? ProfileSaved;
    public event Action? CloseRequested;

    public ProfilePathsWizardMode Mode => _mode;
    public bool IsCreateMode => _mode == ProfilePathsWizardMode.CreateCustom;
    public bool IsEditMode => _mode == ProfilePathsWizardMode.EditExisting;

    public string WindowTitle => IsEditMode
        ? "Speicherpfade bearbeiten"
        : "Dateien & Ordner einbinden";

    public string FinishButtonLabel => IsEditMode ? "Speichern" : "Einbinden";

    public ObservableCollection<BindPathEntryViewModel> PathEntries { get; } = [];

    public IReadOnlyList<CursorSnapshotLevelOptionViewModel> CursorSnapshotLevelOptions { get; }

    [ObservableProperty]
    private bool _isCursorProfile;

    [ObservableProperty]
    private CursorSnapshotLevelOptionViewModel? _selectedCursorSnapshotLevelOption;

    [ObservableProperty]
    private string _cursorSnapshotLevelDescription = string.Empty;

    [ObservableProperty]
    private string _cursorSnapshotSecretsHint = string.Empty;

    [ObservableProperty]
    private bool _showCursorSecretsHint;

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _pathHint = string.Empty;

    [ObservableProperty]
    private string _customSnapshotRoot = string.Empty;

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private void Finish()
    {
        var validPaths = PathEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.SourcePath))
            .Select(entry => entry.ToProfilePathEntry())
            .ToList();

        if (validPaths.Count == 0)
        {
            StatusMessage = "Mindestens ein Speicherpfad ist erforderlich.";
            return;
        }

        if (IsCreateMode)
        {
            var trimmedName = ProfileName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                StatusMessage = "Bitte einen Profilnamen eingeben.";
                return;
            }

            var profile = ProfileBindingFactory.CreateCustomPathsProfile(
                trimmedName,
                validPaths,
                _sortOrder,
                _existingIds);
            profile.CustomSnapshotRoot = NormalizeOptionalPath(CustomSnapshotRoot);

            ProfileSaved?.Invoke(profile);
            CloseRequested?.Invoke();
            return;
        }

        if (_existingProfile is null)
        {
            StatusMessage = "Profil konnte nicht geladen werden.";
            return;
        }

        if (IsCursorProfile && SelectedCursorSnapshotLevelOption is not null)
        {
            _existingProfile.CursorSnapshotLevel = SelectedCursorSnapshotLevelOption.Level;
        }

        ProfileBindingFactory.ApplyPathsToProfile(_existingProfile, validPaths);
        _existingProfile.CustomSnapshotRoot = NormalizeOptionalPath(CustomSnapshotRoot);
        ProfileSaved?.Invoke(_existingProfile);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private async Task AddFilePathAsync()
    {
        var entry = new BindPathEntryViewModel
        {
            Label = "Neue Datei",
            SourcePath = string.Empty,
            RelativeTarget = $"file-{PathEntries.Count + 1}",
            IsDirectory = false
        };
        PathEntries.Add(entry);
        await BrowsePathAsync(entry).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddFolderPathAsync()
    {
        var entry = new BindPathEntryViewModel
        {
            Label = "Neuer Ordner",
            SourcePath = string.Empty,
            RelativeTarget = $"folder-{PathEntries.Count + 1}",
            IsDirectory = true
        };
        PathEntries.Add(entry);
        await BrowsePathAsync(entry).ConfigureAwait(true);
    }

    [RelayCommand]
    private void RemovePath(BindPathEntryViewModel? entry)
    {
        if (entry is not null)
        {
            PathEntries.Remove(entry);
        }
    }

    [RelayCommand]
    private async Task BrowsePathAsync(BindPathEntryViewModel? entry)
    {
        if (entry is null || HostWindow is null)
        {
            return;
        }

        if (entry.IsDirectory)
        {
            var folders = await HostWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Ordner auswählen",
                AllowMultiple = false
            }).ConfigureAwait(true);

            var folder = folders.FirstOrDefault();
            if (folder is not null)
            {
                entry.SourcePath = folder.Path.LocalPath;
                if (string.IsNullOrWhiteSpace(entry.Label) || entry.Label is "Neuer Ordner")
                {
                    entry.Label = Path.GetFileName(folder.Path.LocalPath.TrimEnd('\\', '/'));
                }

                if (string.IsNullOrWhiteSpace(entry.RelativeTarget)
                    || entry.RelativeTarget.StartsWith("folder-", StringComparison.Ordinal))
                {
                    entry.RelativeTarget = Path.GetFileName(folder.Path.LocalPath.TrimEnd('\\', '/'));
                }
            }
        }
        else
        {
            var files = await HostWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Datei auswählen",
                AllowMultiple = false
            }).ConfigureAwait(true);

            var file = files.FirstOrDefault();
            if (file is not null)
            {
                entry.SourcePath = file.Path.LocalPath;
                if (string.IsNullOrWhiteSpace(entry.Label) || entry.Label is "Neue Datei")
                {
                    entry.Label = Path.GetFileName(file.Path.LocalPath);
                }

                if (string.IsNullOrWhiteSpace(entry.RelativeTarget)
                    || entry.RelativeTarget.StartsWith("file-", StringComparison.Ordinal))
                {
                    entry.RelativeTarget = Path.GetFileName(file.Path.LocalPath);
                }
            }
        }
    }

    [RelayCommand]
    private async Task BrowseCustomSnapshotRootAsync()
    {
        if (HostWindow is null)
        {
            return;
        }

        var folders = await HostWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Standard-Snapshot-Zielordner auswählen",
            AllowMultiple = false
        }).ConfigureAwait(true);

        var folder = folders.FirstOrDefault();
        if (folder is not null)
        {
            CustomSnapshotRoot = folder.Path.LocalPath;
            StatusMessage = $"Snapshot-Zielordner: {CustomSnapshotRoot}";
        }
    }

    partial void OnProfileNameChanged(string value) => FinishCommand.NotifyCanExecuteChanged();

    partial void OnSelectedCursorSnapshotLevelOptionChanged(CursorSnapshotLevelOptionViewModel? value)
    {
        if (!IsCursorProfile || value is null || _isApplyingCursorLevel)
        {
            return;
        }

        _isApplyingCursorLevel = true;
        try
        {
            CursorSnapshotLevelDescription = CursorSnapshotPaths.GetLevelDescription(value.Level);
            CursorSnapshotSecretsHint = CursorSnapshotPaths.GetSecretsHint(value.Level);
            ShowCursorSecretsHint = !string.IsNullOrWhiteSpace(CursorSnapshotSecretsHint);
            ApplyCursorSnapshotPaths(value.Level);
            StatusMessage = $"Cursor {value.Label}: {PathEntries.Count} Pfad(e).";
        }
        finally
        {
            _isApplyingCursorLevel = false;
        }
    }

    private void ApplyCursorSnapshotPaths(CursorSnapshotLevel level)
    {
        PathEntries.Clear();
        foreach (var path in CursorSnapshotPaths.Resolve(level))
        {
            PathEntries.Add(BindPathEntryViewModel.FromProfilePath(path));
        }
    }

    private CursorSnapshotLevelOptionViewModel ResolveCursorSnapshotLevelOption(CursorSnapshotLevel level)
        => CursorSnapshotLevelOptions.First(option => option.Level == level);

    private bool CanFinish()
    {
        if (PathEntries.Count == 0)
        {
            return false;
        }

        return IsEditMode || !string.IsNullOrWhiteSpace(ProfileName);
    }

    private static string? NormalizeOptionalPath(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    public Window? HostWindow { get; set; }
}
