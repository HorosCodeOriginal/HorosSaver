using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HorosSaver.Models;
using HorosSaver.Services;

namespace HorosSaver.ViewModels;

public partial class RestoreBatchItemViewModel : ObservableObject
{
    public RestoreBatchItemViewModel(ProgramProfile profile, SnapshotInfo snapshot)
    {
        Profile = profile;
        Snapshot = snapshot;
        ProgramName = profile.Name;
        Category = profile.Category;
        SnapshotName = snapshot.Name;
        SnapshotDescription = snapshot.Description;
        IsSelected = true;
    }

    public ProgramProfile Profile { get; }

    public SnapshotInfo Snapshot { get; }

    public string ProgramName { get; }

    public string Category { get; }

    public string SnapshotName { get; }

    public string SnapshotDescription { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public partial class RestoreProgramOptionViewModel : ObservableObject
{
    public RestoreProgramOptionViewModel(ProgramProfile profile, int snapshotCount)
    {
        Profile = profile;
        SnapshotCount = snapshotCount;
        Name = profile.Name;
        Category = profile.Category;
        IconGlyph = profile.IconGlyph;
        HasSnapshots = snapshotCount > 0;
        StatusLabel = HasSnapshots ? $"{snapshotCount} Snapshot(s)" : "Keine Snapshots";
    }

    public ProgramProfile Profile { get; }
    public string Name { get; }
    public string Category { get; }
    public string IconGlyph { get; }
    public int SnapshotCount { get; }
    public bool HasSnapshots { get; }
    public string StatusLabel { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public partial class RestorePathOptionViewModel : ObservableObject
{
    public RestorePathOptionViewModel(CapturedItem item)
    {
        Item = item;
        Label = item.Label;
        SourcePath = item.SourcePath;
        RelativePath = item.SnapshotRelativePath.Replace('\\', '/');
        IsDirectory = item.IsDirectory;
        IsAvailable = item.Exists;
        TypeLabel = item.IsDirectory ? "Ordner" : "Datei";
        StatusLabel = item.Exists ? "Im Snapshot" : "Fehlt im Snapshot";
        TargetPath = item.SourcePath;
    }

    public CapturedItem Item { get; }
    public string Label { get; }
    public string SourcePath { get; }
    public string RelativePath { get; }
    public bool IsDirectory { get; }
    public bool IsAvailable { get; }
    public string TypeLabel { get; }
    public string StatusLabel { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _targetPath = string.Empty;

    [ObservableProperty]
    private bool _targetExists;

    [ObservableProperty]
    private bool _isRemapped;
}

public partial class RestoreWizardViewModel : ViewModelBase
{
    private readonly IProgramProfileService _profileService;
    private readonly ISnapshotService _snapshotService;
    private ProgramProfile? _selectedProfile;
    private SnapshotManifest? _currentManifest;

    public RestoreWizardViewModel(
        IProgramProfileService profileService,
        ISnapshotService snapshotService)
    {
        _profileService = profileService;
        _snapshotService = snapshotService;
        CustomRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Restore");
        AlternateUserProfilePath = Path.Combine(
            Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? "C:\\",
            "Users",
            "Restore");
        UpdateModePresentation();
        _ = InitializeAsync();
    }

    public Window? HostWindow { get; set; }

    public ObservableCollection<RestoreProgramOptionViewModel> Programs { get; } = [];
    public ObservableCollection<RestoreBatchItemViewModel> BatchItems { get; } = [];
    public ObservableCollection<SnapshotItemViewModel> Snapshots { get; } = [];
    public ObservableCollection<RestorePathOptionViewModel> PathOptions { get; } = [];
    public ObservableCollection<string> ResultDetails { get; } = [];

    [ObservableProperty]
    private bool _isBatchMode;

    [ObservableProperty]
    private string _batchGroupTitle = string.Empty;

    public bool ShowSingleRestorePanels => !IsBatchMode;
    public bool ShowBatchRestorePanels => IsBatchMode;

    [ObservableProperty]
    private RestoreWizardStep _currentStep = RestoreWizardStep.Auswahl;

    [ObservableProperty]
    private RestoreTargetMode _targetMode = RestoreTargetMode.OriginalPaths;

    [ObservableProperty]
    private SnapshotItemViewModel? _selectedSnapshot;

    [ObservableProperty]
    private string _stepTitle = "Wiederherstellen — Auswahl";

    [ObservableProperty]
    private string _infoText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Wählen Sie Programme, Snapshot und Pfade für die Wiederherstellung.";

    [ObservableProperty]
    private string _progressLabel = "0%";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressDetail = string.Empty;

    [ObservableProperty]
    private string _resultMessage = string.Empty;

    [ObservableProperty]
    private bool _isRestoreSuccessful;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _hasSnapshots;

    [ObservableProperty]
    private bool _hasPathOptions;

    [ObservableProperty]
    private string _snapshotSummary = "Kein Snapshot ausgewählt";

    [ObservableProperty]
    private string _customRootPath = string.Empty;

    [ObservableProperty]
    private string _alternateUserProfilePath = string.Empty;

    [ObservableProperty]
    private bool _overwriteConfirmed;

    [ObservableProperty]
    private bool _reinstallProgram = true;

    [ObservableProperty]
    private bool _showReinstallOption;

    [ObservableProperty]
    private bool _hasTargetConflicts;

    [ObservableProperty]
    private string _previewSummary = string.Empty;

    public bool IsAuswahlStep => CurrentStep == RestoreWizardStep.Auswahl;
    public bool IsFortschrittStep => CurrentStep == RestoreWizardStep.Fortschritt;
    public bool IsErgebnisStep => CurrentStep == RestoreWizardStep.Ergebnis;

    public bool IsOriginalPathsMode => TargetMode == RestoreTargetMode.OriginalPaths;
    public bool IsCustomRootMode => TargetMode == RestoreTargetMode.CustomRoot;
    public bool IsAlternateUserProfileMode => TargetMode == RestoreTargetMode.AlternateUserProfile;

    public bool ShowCustomRootPicker => IsCustomRootMode;
    public bool ShowAlternateProfilePicker => IsAlternateUserProfileMode;
    public bool ShowOverwriteConfirmation => TargetMode != RestoreTargetMode.OriginalPaths;
    public bool ShowTargetPreview => TargetMode != RestoreTargetMode.OriginalPaths && HasPathOptions;

    public async Task PrefillAsync(string? programId, SnapshotInfo? snapshot)
    {
        ExitBatchMode();
        await InitializeAsync().ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(programId))
        {
            var program = Programs.FirstOrDefault(item => item.Profile.Id == programId);
            if (program is not null)
            {
                await SelectProgramAsync(program).ConfigureAwait(true);
            }
        }

        if (snapshot is not null)
        {
            SelectedSnapshot = Snapshots.FirstOrDefault(item => item.Id == snapshot.Id)
                ?? Snapshots.FirstOrDefault();
            if (SelectedSnapshot is not null)
            {
                await LoadPathOptionsAsync().ConfigureAwait(true);
            }
        }
    }

    public async Task PrefillBatchAsync(
        string groupTitle,
        IReadOnlyList<RestoreBatchSelection> selections)
    {
        IsBatchMode = true;
        BatchGroupTitle = groupTitle;
        BatchItems.Clear();
        Snapshots.Clear();
        PathOptions.Clear();
        SelectedSnapshot = null;
        _selectedProfile = null;
        _currentManifest = null;
        HasSnapshots = false;
        HasPathOptions = false;
        ShowReinstallOption = false;
        ReinstallProgram = true;
        SnapshotSummary = $"{selections.Count} Programme in der Gruppe";

        await InitializeAsync().ConfigureAwait(true);

        foreach (var selection in selections)
        {
            var program = Programs.FirstOrDefault(item => item.Profile.Id == selection.ProgramId);
            if (program is null)
            {
                continue;
            }

            BatchItems.Add(new RestoreBatchItemViewModel(program.Profile, selection.Snapshot));
            if (program.Profile.IsBound)
            {
                ShowReinstallOption = true;
            }
        }

        foreach (var item in BatchItems)
        {
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(RestoreBatchItemViewModel.IsSelected))
                {
                    StartRestoreCommand.NotifyCanExecuteChanged();
                }
            };
        }

        StatusMessage = BatchItems.Count > 0
            ? $"Gruppen-Wiederherstellung: {BatchItems.Count} Programme vorausgewählt — abwählen oder starten."
            : "Keine gültigen Programme für die Gruppen-Wiederherstellung gefunden.";
        StartRestoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBatchModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSingleRestorePanels));
        OnPropertyChanged(nameof(ShowBatchRestorePanels));
        StartRestoreCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SetTargetMode(string? mode)
    {
        if (IsRunning || string.IsNullOrWhiteSpace(mode))
        {
            return;
        }

        TargetMode = mode switch
        {
            nameof(RestoreTargetMode.CustomRoot) => RestoreTargetMode.CustomRoot,
            nameof(RestoreTargetMode.AlternateUserProfile) => RestoreTargetMode.AlternateUserProfile,
            _ => RestoreTargetMode.OriginalPaths
        };

        OverwriteConfirmed = false;
        UpdateModePresentation();
        RefreshPathPreview();
        StartRestoreCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task BrowseCustomRootAsync()
    {
        var folder = await PickFolderAsync("Staging-Zielordner wählen").ConfigureAwait(true);
        if (folder is not null)
        {
            CustomRootPath = folder;
        }
    }

    [RelayCommand]
    private async Task BrowseAlternateProfileAsync()
    {
        var folder = await PickFolderAsync("Alternatives Benutzerprofil wählen").ConfigureAwait(true);
        if (folder is not null)
        {
            AlternateUserProfilePath = folder;
        }
    }

    [RelayCommand]
    private async Task SelectProgramAsync(RestoreProgramOptionViewModel? program)
    {
        if (program is null || IsRunning)
        {
            return;
        }

        foreach (var item in Programs)
        {
            item.IsSelected = item == program;
        }

        _selectedProfile = program.Profile;
        ReinstallProgram = program.Profile.IsBound;
        ShowReinstallOption = program.Profile.IsBound;
        await LoadSnapshotsForProgramAsync(program.Profile.Id).ConfigureAwait(true);
        StatusMessage = $"Programm „{program.Name}“ ausgewählt — Snapshot und Pfade wählen.";
    }

    [RelayCommand]
    private async Task SelectSnapshotAsync(SnapshotItemViewModel? snapshot)
    {
        if (snapshot is null || IsRunning)
        {
            return;
        }

        SelectedSnapshot = snapshot;
        await LoadPathOptionsAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void ToggleAllPaths(bool selectAll)
    {
        foreach (var path in PathOptions.Where(path => path.IsAvailable))
        {
            path.IsSelected = selectAll;
        }

        RefreshPathPreview();
        StartRestoreCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ToggleAllBatchItems(bool selectAll)
    {
        foreach (var item in BatchItems)
        {
            item.IsSelected = selectAll;
        }

        StartRestoreCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStartRestore))]
    private async Task StartRestoreAsync()
    {
        if (IsBatchMode)
        {
            await StartBatchRestoreAsync().ConfigureAwait(true);
            return;
        }

        if (_selectedProfile is null || SelectedSnapshot is null)
        {
            StatusMessage = "Bitte Programm und Snapshot auswählen.";
            return;
        }

        var selectedPaths = PathOptions
            .Where(path => path.IsSelected && path.IsAvailable)
            .Select(path => path.RelativePath)
            .ToList();

        if (selectedPaths.Count == 0)
        {
            StatusMessage = "Bitte mindestens einen Pfad für die Wiederherstellung auswählen.";
            return;
        }

        var options = BuildRestoreOptions();
        if (options.Mode == RestoreTargetMode.CustomRoot && string.IsNullOrWhiteSpace(options.CustomRootPath))
        {
            StatusMessage = "Bitte ein Zielverzeichnis (Staging) angeben.";
            return;
        }

        if (options.Mode == RestoreTargetMode.AlternateUserProfile
            && string.IsNullOrWhiteSpace(options.AlternateUserProfilePath))
        {
            StatusMessage = "Bitte ein alternatives Benutzerprofil angeben.";
            return;
        }

        if (options.RequiresExplicitOverwrite && HasTargetConflicts && !OverwriteConfirmed)
        {
            StatusMessage = "Am Ziel existieren bereits Dateien — bitte Überschreiben bestätigen.";
            return;
        }

        IsRunning = true;
        CurrentStep = RestoreWizardStep.Fortschritt;
        UpdateStepVisibility();
        ProgressValue = 0;
        ProgressLabel = "0%";
        ProgressDetail = "Wiederherstellung wird vorbereitet…";
        StartRestoreCommand.NotifyCanExecuteChanged();

        var progress = new Progress<RestoreProgressReport>(report =>
        {
            ProgressValue = report.Percent;
            ProgressLabel = $"{report.Percent:0}%";
            ProgressDetail = $"({report.Current}/{report.Total}) {report.CurrentItemLabel}";
        });

        try
        {
            var result = await _snapshotService.RestoreSnapshotAsync(
                _selectedProfile,
                SelectedSnapshot.Snapshot,
                selectedPaths,
                options,
                progress).ConfigureAwait(true);

            ResultDetails.Clear();
            foreach (var detail in result.ErrorDetails)
            {
                ResultDetails.Add(detail);
            }

            foreach (var warning in result.AclWarnings)
            {
                ResultDetails.Add(warning.StartsWith("ACL/", StringComparison.Ordinal)
                    ? warning
                    : $"ACL/Owner: {warning}");
            }

            foreach (var line in result.InstallLog)
            {
                ResultDetails.Add($"[Install] {line}");
            }

            IsRestoreSuccessful = result.Success;
            ResultMessage = result.Message;
            CurrentStep = RestoreWizardStep.Ergebnis;
            StatusMessage = result.Success
                ? result.AclWarnings.Count > 0
                    ? $"{result.Message} ({result.AclWarnings.Count} ACL-Hinweis(e) — Dateien wurden trotzdem wiederhergestellt.)"
                    : result.Message
                : result.Message;
            UpdateStepVisibility();
        }
        catch (Exception ex)
        {
            IsRestoreSuccessful = false;
            ResultMessage = $"Wiederherstellung fehlgeschlagen: {ex.Message}";
            ResultDetails.Clear();
            ResultDetails.Add(ex.Message);
            CurrentStep = RestoreWizardStep.Ergebnis;
            StatusMessage = ResultMessage;
            UpdateStepVisibility();
        }
        finally
        {
            IsRunning = false;
            StartRestoreCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void BackToAuswahl()
    {
        if (IsRunning)
        {
            return;
        }

        CurrentStep = RestoreWizardStep.Auswahl;
        StepTitle = "Wiederherstellen — Auswahl";
        UpdateStepVisibility();
        CancelWizardCommand.NotifyCanExecuteChanged();
    }

    public event Action? CloseWizardRequested;

    [RelayCommand(CanExecute = nameof(CanCancelWizard))]
    private void CancelWizard()
    {
        if (IsErgebnisStep)
        {
            Finish();
        }

        CloseWizardRequested?.Invoke();
    }

    private bool CanCancelWizard()
        => !IsRunning && (IsAuswahlStep || IsErgebnisStep);

    [RelayCommand]
    private void Finish()
    {
        ExitBatchMode();
        BackToAuswahl();
        ResultMessage = string.Empty;
        IsRestoreSuccessful = false;
        ResultDetails.Clear();
        ProgressValue = 0;
        ProgressLabel = "0%";
        ProgressDetail = string.Empty;
        StatusMessage = "Bereit für eine neue Wiederherstellung.";
    }

    private async Task StartBatchRestoreAsync()
    {
        var selectedItems = BatchItems.Where(item => item.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            StatusMessage = "Bitte mindestens ein Programm für die Gruppen-Wiederherstellung auswählen.";
            return;
        }

        var options = BuildRestoreOptions();
        if (options.Mode == RestoreTargetMode.CustomRoot && string.IsNullOrWhiteSpace(options.CustomRootPath))
        {
            StatusMessage = "Bitte ein Zielverzeichnis (Staging) angeben.";
            return;
        }

        if (options.Mode == RestoreTargetMode.AlternateUserProfile
            && string.IsNullOrWhiteSpace(options.AlternateUserProfilePath))
        {
            StatusMessage = "Bitte ein alternatives Benutzerprofil angeben.";
            return;
        }

        IsRunning = true;
        CurrentStep = RestoreWizardStep.Fortschritt;
        UpdateStepVisibility();
        ProgressValue = 0;
        ProgressLabel = "0%";
        ProgressDetail = "Gruppen-Wiederherstellung wird vorbereitet…";
        StartRestoreCommand.NotifyCanExecuteChanged();

        var successCount = 0;
        var failureCount = 0;
        ResultDetails.Clear();

        try
        {
            for (var index = 0; index < selectedItems.Count; index++)
            {
                var batchItem = selectedItems[index];
                var programIndex = index + 1;
                ProgressDetail = $"({programIndex}/{selectedItems.Count}) „{batchItem.ProgramName}“ wird vorbereitet…";
                ProgressValue = (double)(programIndex - 1) / selectedItems.Count * 100;
                ProgressLabel = $"{ProgressValue:0}%";

                var manifest = await _snapshotService
                    .LoadSnapshotManifestAsync(batchItem.Profile.Id, batchItem.Snapshot.Id)
                    .ConfigureAwait(true);

                if (manifest is null)
                {
                    failureCount++;
                    ResultDetails.Add($"„{batchItem.ProgramName}“: Manifest nicht lesbar.");
                    continue;
                }

                var selectedPaths = manifest.CapturedItems
                    .Where(path => path.Exists)
                    .Select(path => path.SnapshotRelativePath.Replace('\\', '/'))
                    .ToList();

                if (selectedPaths.Count == 0)
                {
                    failureCount++;
                    ResultDetails.Add($"„{batchItem.ProgramName}“: Keine wiederherstellbaren Pfade im Snapshot.");
                    continue;
                }

                var itemOptions = new RestoreOptions
                {
                    Mode = options.Mode,
                    CustomRootPath = options.CustomRootPath,
                    AlternateUserProfilePath = options.AlternateUserProfilePath,
                    OverwriteConfirmed = options.OverwriteConfirmed,
                    ReinstallProgram = options.ReinstallProgram && batchItem.Profile.IsBound
                };

                if (itemOptions.Mode != RestoreTargetMode.OriginalPaths)
                {
                    var previews = RestorePathRemapper.BuildPreview(manifest.CapturedItems, selectedPaths, itemOptions);
                    if (RestorePathRemapper.HasTargetConflicts(previews) && !itemOptions.OverwriteConfirmed)
                    {
                        failureCount++;
                        ResultDetails.Add($"„{batchItem.ProgramName}“: Zielkonflikte — bitte Überschreiben bestätigen.");
                        continue;
                    }
                }

                var progress = new Progress<RestoreProgressReport>(report =>
                {
                    var itemBase = (double)(programIndex - 1) / selectedItems.Count * 100;
                    var itemSpan = 100.0 / selectedItems.Count;
                    ProgressValue = itemBase + report.Percent / 100 * itemSpan;
                    ProgressLabel = $"{ProgressValue:0}%";
                    ProgressDetail = $"({programIndex}/{selectedItems.Count}) {report.CurrentItemLabel}";
                });

                var result = await _snapshotService.RestoreSnapshotAsync(
                    batchItem.Profile,
                    batchItem.Snapshot,
                    selectedPaths,
                    itemOptions,
                    progress).ConfigureAwait(true);

                if (result.Success)
                {
                    successCount++;
                    ResultDetails.Add($"✓ „{batchItem.ProgramName}“: {result.Message}");
                }
                else
                {
                    failureCount++;
                    ResultDetails.Add($"✗ „{batchItem.ProgramName}“: {result.Message}");
                }

                foreach (var detail in result.ErrorDetails)
                {
                    ResultDetails.Add($"  · {batchItem.ProgramName}: {detail}");
                }

                foreach (var warning in result.AclWarnings)
                {
                    ResultDetails.Add($"  · ACL {batchItem.ProgramName}: {warning}");
                }
            }

            ProgressValue = 100;
            ProgressLabel = "100%";
            ProgressDetail = "Gruppen-Wiederherstellung abgeschlossen.";
            IsRestoreSuccessful = failureCount == 0;
            ResultMessage = failureCount == 0
                ? $"Gruppe „{BatchGroupTitle}“: {successCount} Programme erfolgreich wiederhergestellt."
                : $"Gruppe „{BatchGroupTitle}“: {successCount} OK, {failureCount} fehlgeschlagen.";
            CurrentStep = RestoreWizardStep.Ergebnis;
            StatusMessage = ResultMessage;
            UpdateStepVisibility();
        }
        catch (Exception ex)
        {
            IsRestoreSuccessful = false;
            ResultMessage = $"Gruppen-Wiederherstellung fehlgeschlagen: {ex.Message}";
            ResultDetails.Add(ex.Message);
            CurrentStep = RestoreWizardStep.Ergebnis;
            StatusMessage = ResultMessage;
            UpdateStepVisibility();
        }
        finally
        {
            IsRunning = false;
            StartRestoreCommand.NotifyCanExecuteChanged();
        }
    }

    private void ExitBatchMode()
    {
        IsBatchMode = false;
        BatchGroupTitle = string.Empty;
        BatchItems.Clear();
    }

    private bool CanStartRestore()
    {
        if (IsRunning || CurrentStep != RestoreWizardStep.Auswahl)
        {
            return false;
        }

        if (IsBatchMode)
        {
            if (!BatchItems.Any(item => item.IsSelected))
            {
                return false;
            }
        }
        else if (_selectedProfile is null
                 || SelectedSnapshot is null
                 || !PathOptions.Any(path => path.IsSelected && path.IsAvailable))
        {
            return false;
        }

        if (TargetMode == RestoreTargetMode.CustomRoot && string.IsNullOrWhiteSpace(CustomRootPath))
        {
            return false;
        }

        if (TargetMode == RestoreTargetMode.AlternateUserProfile && string.IsNullOrWhiteSpace(AlternateUserProfilePath))
        {
            return false;
        }

        if (!IsBatchMode
            && TargetMode != RestoreTargetMode.OriginalPaths
            && HasTargetConflicts
            && !OverwriteConfirmed)
        {
            return false;
        }

        return true;
    }

    partial void OnSelectedSnapshotChanged(SnapshotItemViewModel? value)
    {
        _ = LoadPathOptionsAsync();
        StartRestoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnTargetModeChanged(RestoreTargetMode value)
    {
        OnPropertyChanged(nameof(IsOriginalPathsMode));
        OnPropertyChanged(nameof(IsCustomRootMode));
        OnPropertyChanged(nameof(IsAlternateUserProfileMode));
        OnPropertyChanged(nameof(ShowCustomRootPicker));
        OnPropertyChanged(nameof(ShowAlternateProfilePicker));
        OnPropertyChanged(nameof(ShowOverwriteConfirmation));
        OnPropertyChanged(nameof(ShowTargetPreview));
        UpdateModePresentation();
        RefreshPathPreview();
        StartRestoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnCustomRootPathChanged(string value)
    {
        RefreshPathPreview();
        StartRestoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnAlternateUserProfilePathChanged(string value)
    {
        RefreshPathPreview();
        StartRestoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnOverwriteConfirmedChanged(bool value)
        => StartRestoreCommand.NotifyCanExecuteChanged();

    private async Task InitializeAsync()
    {
        var profiles = await _profileService.LoadProfilesAsync().ConfigureAwait(true);
        Programs.Clear();

        foreach (var profile in profiles)
        {
            var snapshots = await _snapshotService.LoadSnapshotsAsync(profile.Id).ConfigureAwait(true);
            Programs.Add(new RestoreProgramOptionViewModel(profile, snapshots.Count));
        }
    }

    private async Task LoadSnapshotsForProgramAsync(string programId)
    {
        Snapshots.Clear();
        PathOptions.Clear();
        SelectedSnapshot = null;
        _currentManifest = null;
        HasSnapshots = false;
        HasPathOptions = false;
        SnapshotSummary = "Kein Snapshot ausgewählt";

        var snapshots = await _snapshotService.LoadSnapshotsAsync(programId).ConfigureAwait(true);
        var index = 1;
        foreach (var snapshot in snapshots)
        {
            Snapshots.Add(new SnapshotItemViewModel(snapshot, index++));
        }

        HasSnapshots = Snapshots.Count > 0;
        SelectedSnapshot = Snapshots.FirstOrDefault();

        if (SelectedSnapshot is not null)
        {
            await LoadPathOptionsAsync().ConfigureAwait(true);
        }
    }

    private async Task LoadPathOptionsAsync()
    {
        PathOptions.Clear();
        HasPathOptions = false;
        _currentManifest = null;

        if (_selectedProfile is null || SelectedSnapshot is null)
        {
            return;
        }

        var manifest = await _snapshotService
            .LoadSnapshotManifestAsync(_selectedProfile.Id, SelectedSnapshot.Id)
            .ConfigureAwait(true);

        if (manifest is null)
        {
            SnapshotSummary = "Manifest nicht lesbar";
            return;
        }

        _currentManifest = manifest;

        foreach (var item in manifest.CapturedItems)
        {
            var option = new RestorePathOptionViewModel(item)
            {
                IsSelected = item.Exists
            };
            option.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(RestorePathOptionViewModel.IsSelected))
                {
                    RefreshPathPreview();
                }
            };
            PathOptions.Add(option);
        }

        HasPathOptions = PathOptions.Count > 0;
        SnapshotSummary = $"{SelectedSnapshot.Name} · {manifest.CapturedItems.Count(path => path.Exists)} Pfade verfügbar";
        RefreshPathPreview();
        StartRestoreCommand.NotifyCanExecuteChanged();
    }

    private void RefreshPathPreview()
    {
        if (_currentManifest is null)
        {
            PreviewSummary = string.Empty;
            HasTargetConflicts = false;
            return;
        }

        var options = BuildRestoreOptions();
        var selectedPaths = PathOptions
            .Where(path => path.IsSelected && path.IsAvailable)
            .Select(path => path.RelativePath)
            .ToList();

        var previews = RestorePathRemapper.BuildPreview(_currentManifest.CapturedItems, selectedPaths, options);
        var previewByRelative = previews.ToDictionary(
            preview => preview.RelativePath,
            preview => preview,
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in PathOptions)
        {
            if (previewByRelative.TryGetValue(path.RelativePath, out var preview))
            {
                path.TargetPath = preview.TargetPath;
                path.TargetExists = preview.TargetExists;
                path.IsRemapped = preview.IsRemapped;
            }
            else
            {
                path.TargetPath = path.SourcePath;
                path.TargetExists = false;
                path.IsRemapped = false;
            }
        }

        HasTargetConflicts = RestorePathRemapper.HasTargetConflicts(previews);
        var remappedCount = previews.Count(preview => preview.IsRemapped);
        PreviewSummary = TargetMode switch
        {
            RestoreTargetMode.CustomRoot => $"{remappedCount} Pfad(e) → Staging unter „{CustomRootPath}“",
            RestoreTargetMode.AlternateUserProfile => $"{remappedCount} Pfad(e) → Profil „{AlternateUserProfilePath}“",
            _ => "Originalpfade — keine Umleitung"
        };

        if (HasTargetConflicts && TargetMode != RestoreTargetMode.OriginalPaths)
        {
            PreviewSummary += $" · {previews.Count(p => p.TargetExists)} Konflikt(e)";
        }

        OnPropertyChanged(nameof(ShowTargetPreview));
        StartRestoreCommand.NotifyCanExecuteChanged();
    }

    private RestoreOptions BuildRestoreOptions()
        => new()
        {
            Mode = TargetMode,
            CustomRootPath = CustomRootPath,
            AlternateUserProfilePath = AlternateUserProfilePath,
            OverwriteConfirmed = OverwriteConfirmed,
            ReinstallProgram = ReinstallProgram && ShowReinstallOption
        };

    private void UpdateModePresentation()
    {
        InfoText = TargetMode switch
        {
            RestoreTargetMode.CustomRoot =>
                "Dateien werden unter den Staging-Ordner gespiegelt (AppData, UserProfile, Laufwerke). Ideal für Übertrag auf ein neues System.",
            RestoreTargetMode.AlternateUserProfile =>
                "Pfade unter dem erkannten Quell-Profil werden auf ein anderes Benutzerprofil umgebogen (z. B. %APPDATA% eines anderen Users).",
            _ => ShowReinstallOption && ReinstallProgram
                ? "Programm wird über winget neu installiert (--force), danach Einstellungen/Dateien auf die Originalpfade zurückgespielt."
                : "Installationen und Dateien 1:1 auf die Originalpfade aus dem Snapshot-Manifest zurückspielen."
        };
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        if (HostWindow is null)
        {
            return null;
        }

        var folders = await HostWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        }).ConfigureAwait(true);

        return folders.FirstOrDefault()?.Path.LocalPath;
    }

    private void UpdateStepVisibility()
    {
        StepTitle = CurrentStep switch
        {
            RestoreWizardStep.Auswahl => "Wiederherstellen — Auswahl",
            RestoreWizardStep.Fortschritt => "Wiederherstellen — Fortschritt",
            RestoreWizardStep.Ergebnis => "Wiederherstellen — Ergebnis",
            _ => "Wiederherstellen"
        };

        OnPropertyChanged(nameof(IsAuswahlStep));
        OnPropertyChanged(nameof(IsFortschrittStep));
        OnPropertyChanged(nameof(IsErgebnisStep));
        CancelWizardCommand.NotifyCanExecuteChanged();
    }
}
