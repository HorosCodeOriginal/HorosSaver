using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using HorosSaver.Models;
using HorosSaver.Services;
using HorosSaver.ViewModels.Regions;
using HorosSaver.Views;

namespace HorosSaver.ViewModels;

public partial class MainViewModel : ViewModelBase, INavigationHost, IWorkflowHost
{
    private readonly IProgramProfileService _profileService;
    private readonly ISnapshotService _snapshotService;
    private readonly ISnapshotJobManager _snapshotJobManager;
    private readonly ISnapshotCompareService _compareService;
    private readonly IInstalledProgramDiscoveryService _discoveryService;
    private readonly IStoragePathResolver _paths;
    private readonly IAppSettingsService _settingsService;
    private readonly IAppReinstallEngineService _engineService;
    private readonly ISystemImageService _systemImageService;
    private List<ProgramProfile> _profiles = [];
    private List<ProgramGroup> _groups = [];

    public MainViewModel(
        IProgramProfileService profileService,
        ISnapshotService snapshotService,
        ISnapshotJobManager snapshotJobManager,
        ISnapshotCompareService compareService,
        IInstalledProgramDiscoveryService discoveryService,
        IStoragePathResolver paths,
        IAppSettingsService settingsService,
        IAppReinstallEnginePathResolver enginePathResolver,
        IAppReinstallEngineService engineService,
        ISystemImageService systemImageService)
    {
        _profileService = profileService;
        _snapshotService = snapshotService;
        _snapshotJobManager = snapshotJobManager;
        _compareService = compareService;
        _discoveryService = discoveryService;
        _paths = paths;
        _settingsService = settingsService;
        _engineService = engineService;
        _systemImageService = systemImageService;

        _snapshotJobManager.JobCompleted += OnSnapshotJobCompleted;

        RestoreWizard = new RestoreWizardViewModel(profileService, snapshotService);
        RestoreWizard.CloseWizardRequested += () => NavigateTo("programme");

        Sidebar = new SidebarRegionViewModel(this, this);
        Toolbar = new ToolbarRegionViewModel(this, settingsService);
        Programs = new ProgramsRegionViewModel(this);
        Timeline = new TimelineRegionViewModel(this);
        Snapshots = new SnapshotsRegionViewModel(this, settingsService);
        StatusBar = new StatusBarRegionViewModel();
        Settings = new SettingsRegionViewModel(
            paths,
            settingsService,
            enginePathResolver,
            engineService,
            ApplyCursorSnapshotLevelAsync,
            systemImageService);
        Settings.EngineStatusReported += (message, isError) => StatusBar.ApplyActionResult(message, isError);

        Snapshots.SnapshotSelected += OnOverviewSnapshotSelected;

        Programs.SelectedProgramChanged += OnSelectedProgramChanged;
        Programs.SortOrderChanged += OnSortOrderChanged;
        Timeline.SelectedSnapshotChanged += OnSelectedSnapshotChanged;

        Toolbar.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ToolbarRegionViewModel.SearchText))
            {
                return;
            }

            if (ActiveView == MainContentView.Snapshots)
            {
                Snapshots.ApplyFilter(Toolbar.SearchText);
            }
            else
            {
                Programs.ApplyFilter(Toolbar.SearchText);
            }
        };

        _ = InitializeAsync();
    }

    public RestoreWizardViewModel RestoreWizard { get; }
    public SidebarRegionViewModel Sidebar { get; }
    public ToolbarRegionViewModel Toolbar { get; }
    public ProgramsRegionViewModel Programs { get; }
    public TimelineRegionViewModel Timeline { get; }
    public SnapshotsRegionViewModel Snapshots { get; }
    public StatusBarRegionViewModel StatusBar { get; }
    public SettingsRegionViewModel Settings { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private MainContentView _activeView = MainContentView.Programme;

    public bool IsProgrammeView => ActiveView == MainContentView.Programme;
    public bool IsSnapshotsView => ActiveView == MainContentView.Snapshots;
    public bool IsTimelineView => ActiveView == MainContentView.Timeline;
    public bool IsRestoreWizardView => ActiveView == MainContentView.Wiederherstellen;
    public bool IsSettingsView => ActiveView == MainContentView.Einstellungen;

    public bool HasSelectedProgram => Programs.SelectedProgram is not null;

    public double DetailPanelWidth { get; private set; } = 360;

    public void SaveDetailPanelWidth(double width)
    {
        DetailPanelWidth = Math.Clamp(width, 240, 640);
    }

    partial void OnIsBusyChanged(bool value)
    {
        Toolbar.SaveSnapshotCommand.NotifyCanExecuteChanged();
        Toolbar.RestoreSnapshotCommand.NotifyCanExecuteChanged();
        Toolbar.CreateSystemAbbildCommand.NotifyCanExecuteChanged();
        Sidebar.CreateSystemAbbildCommand.NotifyCanExecuteChanged();
        Toolbar.BindProgramCommand.NotifyCanExecuteChanged();
        Toolbar.BindCustomPathsCommand.NotifyCanExecuteChanged();
        Toolbar.CaptureInventoryCommand.NotifyCanExecuteChanged();
        Toolbar.EditProfilePathsCommand.NotifyCanExecuteChanged();
        Toolbar.AutoCreateGroupsCommand.NotifyCanExecuteChanged();
        Timeline.EditProfilePathsCommand.NotifyCanExecuteChanged();
        Programs.NotifyWorkflowStateChanged();
        Snapshots.NotifyWorkflowStateChanged();
        Timeline.CompareSnapshotsCommand.NotifyCanExecuteChanged();
        Timeline.CompareSnapshotContextCommand.NotifyCanExecuteChanged();
        Timeline.OpenSnapshotInExplorerCommand.NotifyCanExecuteChanged();
        Timeline.CopySnapshotPathCommand.NotifyCanExecuteChanged();
        Snapshots.RestoreSnapshotCommand.NotifyCanExecuteChanged();
    }

    partial void OnActiveViewChanged(MainContentView value)
    {
        OnPropertyChanged(nameof(IsProgrammeView));
        OnPropertyChanged(nameof(IsSnapshotsView));
        OnPropertyChanged(nameof(IsTimelineView));
        OnPropertyChanged(nameof(IsRestoreWizardView));
        OnPropertyChanged(nameof(IsSettingsView));

        Programs.GridColumns = value == MainContentView.Timeline ? 2 : 3;
        Snapshots.GridColumns = 3;
        Programs.SectionTitle = value switch
        {
            MainContentView.Timeline => "Programm wählen",
            _ => "App-Profile"
        };

        if (value == MainContentView.Snapshots)
        {
            Programs.ApplyFilter(string.Empty);
            _ = RefreshSnapshotsOverviewAsync();
        }
        else
        {
            Programs.ApplyFilter(Toolbar.SearchText);
        }
    }

    public void NavigateTo(string viewId)
    {
        ActiveView = viewId switch
        {
            "programme" => MainContentView.Programme,
            "snapshots" => MainContentView.Snapshots,
            "timeline" => MainContentView.Timeline,
            "wiederherstellen" => MainContentView.Wiederherstellen,
            "einstellungen" => MainContentView.Einstellungen,
            _ => MainContentView.Programme
        };

        Sidebar.SetActiveNavigation(viewId);

        Toolbar.Breadcrumb = ActiveView switch
        {
            MainContentView.Programme => "Programme / App-Profile",
            MainContentView.Snapshots => "Snapshots / Übersicht",
            MainContentView.Timeline => "Timeline / Zeitzustände",
            MainContentView.Wiederherstellen => "Wiederherstellen / Wizard",
            MainContentView.Einstellungen => "Einstellungen",
            _ => "Programme / App-Profile"
        };
    }

    public async Task OpenRestoreWizardAsync(string? programId = null, SnapshotInfo? snapshot = null)
    {
        ProgramProfileItemViewModel? program = null;

        if (!string.IsNullOrWhiteSpace(programId))
        {
            program = Programs.GetAllPrograms().FirstOrDefault(item => item.Id == programId);
            if (program is not null)
            {
                Programs.SelectedProgram = program;
                await RefreshSnapshotsAsync(program).ConfigureAwait(true);
            }
        }
        else
        {
            program = Programs.SelectedProgram;
        }

        if (snapshot is not null)
        {
            var timelineSnapshot = Timeline.Snapshots.FirstOrDefault(item => item.Id == snapshot.Id);
            if (timelineSnapshot is not null)
            {
                Timeline.SelectedSnapshot = timelineSnapshot;
            }
        }
        else if (program is not null && Timeline.Snapshots.Count > 0)
        {
            Timeline.SelectedSnapshot = Timeline.Snapshots.FirstOrDefault(item => item.IsCurrent)
                ?? Timeline.Snapshots.FirstOrDefault();
            snapshot = Timeline.SelectedSnapshot?.Snapshot;
        }
        else
        {
            snapshot = Timeline.SelectedSnapshot?.Snapshot;
        }

        NavigateTo("wiederherstellen");

        RestoreWizard.HostWindow = GetMainWindow();

        await RestoreWizard.PrefillAsync(program?.Id, snapshot).ConfigureAwait(true);

        if (program is not null && snapshot is not null)
        {
            RestoreWizard.StatusMessage = $"Wizard geöffnet für „{program.Name}“ mit {snapshot.Name}.";
        }
        else if (program is not null)
        {
            RestoreWizard.StatusMessage = $"Wizard geöffnet für „{program.Name}“ — Snapshot wählen.";
        }
        else
        {
            RestoreWizard.StatusMessage = "Wählen Sie Programme, Snapshot und Pfade für die Wiederherstellung.";
        }

        StatusBar.ApplyActionResult("Wiederherstellungs-Assistent geöffnet.");
    }

    public async Task OpenRestoreWizardForGroupAsync(
        string groupTitle,
        IReadOnlyList<SnapshotOverviewItemViewModel> snapshots)
    {
        if (snapshots.Count == 0)
        {
            StatusBar.ApplyActionResult("Keine Snapshots in der Gruppe.");
            return;
        }

        NavigateTo("wiederherstellen");
        RestoreWizard.HostWindow = GetMainWindow();

        var selections = snapshots
            .Select(item => new RestoreBatchSelection(item.ProgramId, item.Snapshot.Snapshot))
            .ToList();

        await RestoreWizard.PrefillBatchAsync(groupTitle, selections).ConfigureAwait(true);
        RestoreWizard.StatusMessage =
            $"Gruppen-Wiederherstellung für „{groupTitle}“: {selections.Count} Programme vorausgewählt.";
        StatusBar.ApplyActionResult($"Gruppen-Wiederherstellung geöffnet ({selections.Count} Programme).");
    }

    public async Task OpenRestoreWizardForSelectionAsync(IReadOnlyList<SnapshotOverviewItemViewModel> snapshots)
    {
        if (snapshots.Count == 0)
        {
            StatusBar.ApplyActionResult("Keine Snapshots ausgewählt.");
            return;
        }

        NavigateTo("wiederherstellen");
        RestoreWizard.HostWindow = GetMainWindow();

        var selections = snapshots
            .Select(item => new RestoreBatchSelection(item.ProgramId, item.Snapshot.Snapshot))
            .ToList();

        var title = selections.Count == 1
            ? snapshots[0].Snapshot.Name
            : $"{selections.Count} ausgewählte Snapshots";

        await RestoreWizard.PrefillBatchAsync(title, selections).ConfigureAwait(true);
        RestoreWizard.StatusMessage =
            $"Auswahl-Wiederherstellung: {selections.Count} Snapshot(s) vorausgewählt.";
        StatusBar.ApplyActionResult($"Auswahl-Wiederherstellung geöffnet ({selections.Count}).");
    }

    public async Task DeleteSnapshotsBatchAsync(IReadOnlyList<SnapshotOverviewItemViewModel> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        var confirmed = await DesktopShellHelper.ConfirmAsync(
            GetMainWindow(),
            "Auswahl löschen",
            $"{snapshots.Count} Snapshot(s) wirklich löschen?\n\nDieser Vorgang kann nicht rückgängig gemacht werden.").ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var affectedPrograms = new HashSet<string>(StringComparer.Ordinal);
        var deletedCount = 0;

        foreach (var snapshot in snapshots)
        {
            try
            {
                var deleted = await _snapshotService.DeleteSnapshotAsync(snapshot.ProgramId, snapshot.Snapshot.Id)
                    .ConfigureAwait(true);
                if (!deleted)
                {
                    continue;
                }

                deletedCount++;
                affectedPrograms.Add(snapshot.ProgramId);
            }
            catch (Exception ex)
            {
                StatusBar.ApplyActionResult($"Löschen fehlgeschlagen: {ex.Message}", isError: true);
                break;
            }
        }

        foreach (var programId in affectedPrograms)
        {
            var program = Programs.GetAllPrograms().FirstOrDefault(item => item.Id == programId);
            if (program is null)
            {
                continue;
            }

            var programSnapshots = await _snapshotService.LoadSnapshotsAsync(programId).ConfigureAwait(true);
            program.Profile.LastSnapshotAt = programSnapshots.FirstOrDefault()?.CreatedAt;
            program.RefreshSnapshotStatus(program.Profile.LastSnapshotAt);
            await RefreshSnapshotsAsync(program).ConfigureAwait(true);
        }

        if (affectedPrograms.Count > 0)
        {
            await _profileService.SaveProfilesAsync(_profiles).ConfigureAwait(true);
        }

        if (ActiveView == MainContentView.Snapshots)
        {
            await RefreshSnapshotsOverviewAsync().ConfigureAwait(true);
        }

        UpdateToolbarStats();
        StatusBar.ApplyActionResult($"{deletedCount} Snapshot(s) gelöscht.");
    }

    public async Task OpenBindProgramWizardAsync()
    {
        var wizardViewModel = new BindProgramWizardViewModel(
            _discoveryService,
            _profiles,
            CursorSnapshotPaths.NormalizeLevel(_settingsService.Current.CursorSnapshotLevel));
        var dialog = new BindProgramWizardWindow
        {
            DataContext = wizardViewModel
        };

        var boundProfiles = new List<ProgramProfile>();
        wizardViewModel.ProfileBound += boundProfiles.Add;
        wizardViewModel.CloseRequested += () => dialog.Close();

        var owner = GetMainWindow();
        if (owner is not null)
        {
            await dialog.ShowDialog(owner).ConfigureAwait(true);
        }
        else
        {
            dialog.Show();
            await Task.Delay(100).ConfigureAwait(true);
        }

        if (boundProfiles.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var profile in boundProfiles)
            {
                _profiles.Add(profile);
            }

            await _profileService.SaveProfilesAsync(_profiles).ConfigureAwait(true);

            foreach (var profile in boundProfiles)
            {
                var item = new ProgramProfileItemViewModel(profile);
                item.AttachSnapshotJobManager(_snapshotJobManager);
                Programs.AddProgram(item);
            }

            UpdateToolbarStats();

            if (boundProfiles.Count == 1)
            {
                var single = boundProfiles[0];
                StatusBar.ApplySelection(Programs.GetAllPrograms().Count, single.Name);
                Toolbar.StatusMessage = $"„{single.Name}“ eingebunden — Snapshot speichern möglich.";
            }
            else
            {
                StatusBar.ApplySelection(Programs.GetAllPrograms().Count, $"{boundProfiles.Count} Programme");
                Toolbar.StatusMessage = $"{boundProfiles.Count} Programme eingebunden — Snapshot speichern möglich.";
            }

            StatusBar.ApplyActionResult(Toolbar.StatusMessage);
        }
        catch (Exception ex)
        {
            foreach (var profile in boundProfiles)
            {
                _profiles.Remove(profile);
            }

            Toolbar.StatusMessage = $"Einbinden fehlgeschlagen: {ex.Message}";
            StatusBar.ApplyActionResult(Toolbar.StatusMessage, isError: true);
        }
    }

    public async Task OpenCustomPathsWizardAsync()
    {
        await OpenProfilePathsWizardAsync(ProfilePathsWizardMode.CreateCustom, null).ConfigureAwait(true);
    }

    public async Task OpenEditProfilePathsAsync(ProgramProfile? profile = null)
    {
        var targetProfile = profile ?? Programs.SelectedProgram?.Profile;
        if (targetProfile is null)
        {
            Toolbar.StatusMessage = "Bitte zuerst ein Profil auswählen.";
            StatusBar.ApplyActionResult(Toolbar.StatusMessage);
            return;
        }

        if (profile is not null)
        {
            var item = Programs.GetAllPrograms().FirstOrDefault(program => program.Id == profile.Id);
            if (item is not null)
            {
                Programs.SelectedProgram = item;
            }
        }

        await OpenProfilePathsWizardAsync(ProfilePathsWizardMode.EditExisting, targetProfile).ConfigureAwait(true);
    }

    private async Task OpenProfilePathsWizardAsync(ProfilePathsWizardMode mode, ProgramProfile? existingProfile)
    {
        var wizardViewModel = new ProfilePathsWizardViewModel(
            mode,
            _profiles,
            existingProfile,
            CursorSnapshotPaths.NormalizeLevel(_settingsService.Current.CursorSnapshotLevel));
        var dialog = new ProfilePathsWizardWindow
        {
            DataContext = wizardViewModel
        };

        ProgramProfile? savedProfile = null;
        var isEdit = mode == ProfilePathsWizardMode.EditExisting;

        wizardViewModel.ProfileSaved += profile => savedProfile = profile;
        wizardViewModel.CloseRequested += () => dialog.Close();

        var owner = GetMainWindow();
        if (owner is not null)
        {
            await dialog.ShowDialog(owner).ConfigureAwait(true);
        }
        else
        {
            dialog.Show();
            await Task.Delay(100).ConfigureAwait(true);
        }

        if (savedProfile is null)
        {
            return;
        }

        try
        {
            if (isEdit)
            {
                await _profileService.SaveProfilesAsync(_profiles).ConfigureAwait(true);
                var item = Programs.GetAllPrograms().FirstOrDefault(program => program.Id == savedProfile.Id);
                item?.RefreshProfileDetails();
                Toolbar.StatusMessage = $"Pfade für „{savedProfile.Name}\" gespeichert ({savedProfile.Paths.Count}).";
            }
            else
            {
                _profiles.Add(savedProfile);
                await _profileService.SaveProfilesAsync(_profiles).ConfigureAwait(true);

                var item = new ProgramProfileItemViewModel(savedProfile);
                item.AttachSnapshotJobManager(_snapshotJobManager);
                Programs.AddProgram(item);
                UpdateToolbarStats();
                Toolbar.StatusMessage = $"„{savedProfile.Name}\" eingebunden — Snapshot speichern möglich.";
            }

            StatusBar.ApplySelection(Programs.GetAllPrograms().Count, savedProfile.Name);
            StatusBar.ApplyActionResult(Toolbar.StatusMessage);
        }
        catch (Exception ex)
        {
            if (!isEdit)
            {
                _profiles.Remove(savedProfile);
            }

            Toolbar.StatusMessage = $"Speichern fehlgeschlagen: {ex.Message}";
            StatusBar.ApplyActionResult(Toolbar.StatusMessage, isError: true);
        }
    }

    public async Task SaveSnapshotAsync()
    {
        var selected = Programs.SelectedProgram;
        if (selected is null)
        {
            Toolbar.StatusMessage = "Bitte zuerst ein Programm auswählen.";
            StatusBar.ApplyActionResult("Kein Programm ausgewählt.");
            return;
        }

        var captureTarget = await PromptSnapshotCaptureTargetAsync(selected.Profile).ConfigureAwait(true);
        if (captureTarget is null)
        {
            StatusBar.ApplyActionResult("Snapshot-Erstellung abgebrochen.");
            return;
        }

        if (!_snapshotJobManager.Enqueue(selected.Profile, captureTarget, selected, out var rejectionReason))
        {
            Toolbar.StatusMessage = rejectionReason ?? "Snapshot konnte nicht gestartet werden.";
            StatusBar.ApplyActionResult(Toolbar.StatusMessage, isError: true);
            return;
        }

        Toolbar.StatusMessage = $"Snapshot für „{selected.Name}“ in Warteschlange.";
        StatusBar.ApplyActionResult(Toolbar.StatusMessage);
        Programs.NotifyWorkflowStateChanged();
    }

    public async Task CreateSystemAbbildAsync()
    {
        await _settingsService.LoadAsync().ConfigureAwait(true);
        Settings.HostWindow = GetMainWindow();

        var mode = SystemAbbildPaths.NormalizeMode(_settingsService.Current.SystemAbbildMode);
        string targetPath;
        try
        {
            targetPath = SystemAbbildPaths.ResolveTargetPath(
                _paths,
                _settingsService.Current.SystemAbbildTarget,
                mode);
        }
        catch (InvalidOperationException ex)
        {
            Toolbar.StatusMessage = ex.Message;
            StatusBar.ApplyActionResult(ex.Message, isError: true);
            return;
        }

        var modeLabel = SystemAbbildPaths.GetLevelLabel(mode);
        var confirmMessage = mode switch
        {
            SystemAbbildMode.AllProgramsBundle =>
                $"Programm-Bundle für alle Profile erstellen?\n\nModus: {modeLabel}\nZiel-Basis: {targetPath}",
            _ =>
                $"Windows-Backup starten?\n\nModus: {modeLabel}\nZiel: {targetPath}\n\n" +
                "Ein UAC-Dialog (Administrator) wird angezeigt. HorosSaver führt wbadmin mit erhöhten Rechten aus."
        };

        var owner = GetMainWindow();
        var confirmed = await DesktopShellHelper.ConfirmAsync(
            owner,
            "System-Abbild erstellen",
            confirmMessage).ConfigureAwait(true);

        if (!confirmed)
        {
            StatusBar.ApplyActionResult("System-Abbild abgebrochen.");
            return;
        }

        IsBusy = true;
        Toolbar.SaveSnapshotCommand.NotifyCanExecuteChanged();
        Toolbar.RestoreSnapshotCommand.NotifyCanExecuteChanged();
        Toolbar.CreateSystemAbbildCommand.NotifyCanExecuteChanged();
        Sidebar.CreateSystemAbbildCommand.NotifyCanExecuteChanged();
        try
        {
            Toolbar.StatusMessage = $"System-Abbild wird erstellt ({modeLabel})…";
            StatusBar.ApplyActionResult(Toolbar.StatusMessage);

            var result = await _systemImageService.CreateAsync().ConfigureAwait(true);

            if (mode == SystemAbbildMode.AllProgramsBundle)
            {
                await RefreshSnapshotsOverviewAsync().ConfigureAwait(true);
                foreach (var program in Programs.GetAllPrograms())
                {
                    program.RefreshSnapshotStatus(program.Profile.LastSnapshotAt);
                }

                foreach (var group in Programs.GetAllGroups())
                {
                    group.RefreshLastSnapshotSummary();
                }

                UpdateToolbarStats();
                await Settings.LoadSettingsAsync().ConfigureAwait(true);
            }

            Toolbar.StatusMessage = result.Message;
            StatusBar.ApplyActionResult(result.Message, !result.Success);
        }
        catch (Exception ex)
        {
            Toolbar.StatusMessage = $"System-Abbild fehlgeschlagen: {ex.Message}";
            StatusBar.ApplyActionResult(Toolbar.StatusMessage, isError: true);
        }
        finally
        {
            IsBusy = false;
            Toolbar.SaveSnapshotCommand.NotifyCanExecuteChanged();
            Toolbar.RestoreSnapshotCommand.NotifyCanExecuteChanged();
            Toolbar.CreateSystemAbbildCommand.NotifyCanExecuteChanged();
            Sidebar.CreateSystemAbbildCommand.NotifyCanExecuteChanged();
        }
    }

    public async Task CompareSnapshotsAsync(
        SnapshotItemViewModel? selectedSnapshot,
        SnapshotItemViewModel? compareWithSnapshot)
    {
        var selectedProgram = Programs.SelectedProgram;
        if (selectedProgram is null)
        {
            Timeline.ActionMessage = "Bitte zuerst ein Programm auswählen.";
            return;
        }

        if (Timeline.Snapshots.Count < 2)
        {
            Timeline.ActionMessage = "Mindestens zwei Snapshots erforderlich — bitte zuerst weitere Zeitzustände speichern.";
            return;
        }

        if (selectedSnapshot is null || compareWithSnapshot is null)
        {
            Timeline.ActionMessage = "Bitte zwei Snapshots für den Vergleich auswählen.";
            return;
        }

        if (string.Equals(selectedSnapshot.Id, compareWithSnapshot.Id, StringComparison.Ordinal))
        {
            Timeline.ActionMessage = "Beide Snapshots sind identisch — bitte zwei verschiedene Zeitzustände wählen.";
            return;
        }

        var older = selectedSnapshot.CreatedAt <= compareWithSnapshot.CreatedAt
            ? selectedSnapshot
            : compareWithSnapshot;
        var newer = selectedSnapshot.CreatedAt > compareWithSnapshot.CreatedAt
            ? selectedSnapshot
            : compareWithSnapshot;

        IsBusy = true;
        Toolbar.SaveSnapshotCommand.NotifyCanExecuteChanged();
        Toolbar.RestoreSnapshotCommand.NotifyCanExecuteChanged();
        Timeline.CompareSnapshotsCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await _compareService.CompareAsync(
                selectedProgram.Id,
                older.Snapshot.Id,
                newer.Snapshot.Id,
                selectedProgram.Name).ConfigureAwait(true);

            if (!result.Success)
            {
                Timeline.ActionMessage = result.Message;
                StatusBar.ApplyActionResult(result.Message, isError: true);
                return;
            }

            var compareViewModel = new CompareViewModel(result);
            var dialog = new CompareWindow
            {
                DataContext = compareViewModel
            };

            compareViewModel.CloseRequested += () => dialog.Close();

            var owner = GetMainWindow();
            if (owner is not null)
            {
                await dialog.ShowDialog(owner).ConfigureAwait(true);
            }
            else
            {
                dialog.Show();
            }

            Timeline.ActionMessage = result.Message;
            StatusBar.ApplyActionResult($"Vergleich abgeschlossen: {result.Message}");
        }
        catch (Exception ex)
        {
            Timeline.ActionMessage = $"Vergleich fehlgeschlagen: {ex.Message}";
            StatusBar.ApplyActionResult(Timeline.ActionMessage, isError: true);
        }
        finally
        {
            IsBusy = false;
            Toolbar.SaveSnapshotCommand.NotifyCanExecuteChanged();
            Toolbar.RestoreSnapshotCommand.NotifyCanExecuteChanged();
            Timeline.CompareSnapshotsCommand.NotifyCanExecuteChanged();
        }
    }

    public async Task SaveSnapshotForProgramAsync(string? programId)
    {
        if (string.IsNullOrWhiteSpace(programId))
        {
            return;
        }

        var program = Programs.GetAllPrograms().FirstOrDefault(item => item.Id == programId);
        if (program is null)
        {
            return;
        }

        Programs.SelectedProgram = program;
        await SaveSnapshotAsync().ConfigureAwait(true);
    }

    public async Task SaveGroupSnapshotAsync(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        var group = Programs.GetAllGroups().FirstOrDefault(item => item.Id == groupId);
        if (group is null || group.Members.Count == 0)
        {
            Toolbar.StatusMessage = "Gruppe nicht gefunden.";
            StatusBar.ApplyActionResult(Toolbar.StatusMessage);
            return;
        }

        SnapshotCaptureTargetChoice? sharedCaptureTarget = null;
        var enqueuedCount = 0;
        var skippedCount = 0;
        var messages = new List<string>();

        foreach (var member in group.Members)
        {
            sharedCaptureTarget ??= await PromptSnapshotCaptureTargetAsync(member.Profile).ConfigureAwait(true);
            if (sharedCaptureTarget is null)
            {
                Toolbar.StatusMessage = "Snapshot-Erstellung abgebrochen.";
                StatusBar.ApplyActionResult(Toolbar.StatusMessage);
                return;
            }

            if (_snapshotJobManager.Enqueue(member.Profile, sharedCaptureTarget, member, out var rejectionReason))
            {
                enqueuedCount++;
            }
            else
            {
                skippedCount++;
                if (!string.IsNullOrWhiteSpace(rejectionReason))
                {
                    messages.Add($"„{member.Name}“: {rejectionReason}");
                }
            }
        }

        if (enqueuedCount == 0)
        {
            Toolbar.StatusMessage = messages.FirstOrDefault() ?? "Keine Snapshots gestartet.";
            StatusBar.ApplyActionResult(Toolbar.StatusMessage, isError: true);
            return;
        }

        var summary = skippedCount == 0
            ? $"{enqueuedCount} Snapshot(s) für Gruppe „{group.Name}“ in Warteschlange."
            : $"{enqueuedCount} Snapshot(s) in Warteschlange, {skippedCount} übersprungen.";
        Toolbar.StatusMessage = summary;
        StatusBar.ApplyActionResult(summary, skippedCount > 0);
        Programs.NotifyWorkflowStateChanged();
    }

    public async Task AutoCreateProgramGroupsAsync()
    {
        if (_profiles.Count == 0)
        {
            Toolbar.StatusMessage = "Keine Profile zum Gruppieren vorhanden.";
            StatusBar.ApplyActionResult(Toolbar.StatusMessage);
            return;
        }

        try
        {
            var detectedGroups = _profileService.AutoDetectGroups(_profiles).ToList();
            if (detectedGroups.Count == 0)
            {
                Toolbar.StatusMessage = "Keine verwandten Programme für Gruppen gefunden.";
                StatusBar.ApplyActionResult(Toolbar.StatusMessage);
                return;
            }

            _groups = detectedGroups;
            _profileService.ApplyAutoGroups(_profiles, _groups);
            await PersistProfileStoreAsync().ConfigureAwait(true);
            RefreshProgramsDisplay();

            var groupedCount = _profiles.Count(profile => !string.IsNullOrWhiteSpace(profile.GroupId));
            Toolbar.StatusMessage = $"{_groups.Count} Gruppen erstellt ({groupedCount} Profile zugeordnet).";
            StatusBar.ApplyActionResult(Toolbar.StatusMessage);
        }
        catch (Exception ex)
        {
            Toolbar.StatusMessage = $"Gruppen konnten nicht erstellt werden: {ex.Message}";
            StatusBar.ApplyActionResult(Toolbar.StatusMessage, isError: true);
        }
    }

    public async Task DeleteProgramProfileAsync(string? programId)
    {
        if (string.IsNullOrWhiteSpace(programId))
        {
            return;
        }

        var program = Programs.GetAllPrograms().FirstOrDefault(item => item.Id == programId);
        if (program is null)
        {
            return;
        }

        var confirm = await DesktopShellHelper.ConfirmWithOptionsAsync(
            GetMainWindow(),
            "Profil löschen",
            $"Profil „{program.Name}“ wirklich löschen?\n\nDas Profil wird aus profiles.json entfernt.",
            new ConfirmDialogOptions(
                ShowDeleteSnapshotsOption: true,
                DeleteSnapshotsDefault: true,
                DeleteSnapshotsLabel: "Zugehörige Snapshots auch löschen",
                ConfirmButtonText: "Profil löschen")).ConfigureAwait(true);

        if (!confirm.IsConfirmed)
        {
            return;
        }

        await DeleteProgramProfileCoreAsync(programId, confirm.DeleteSnapshots, program.Name).ConfigureAwait(true);
    }

    public async Task DissolveProgramGroupAsync(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        var group = Programs.GetAllGroups().FirstOrDefault(item => item.Id == groupId);
        if (group is null)
        {
            return;
        }

        var confirmed = await DesktopShellHelper.ConfirmAsync(
            GetMainWindow(),
            "Gruppe auflösen",
            $"Gruppe „{group.Name}“ wirklich auflösen?\n\nDie {group.MemberCount} Profile bleiben erhalten, die Gruppierung wird entfernt.").ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        try
        {
            foreach (var member in group.Members)
            {
                var profile = _profiles.FirstOrDefault(item => item.Id == member.Id);
                if (profile is not null)
                {
                    ProgramGroupDetector.ClearGroupMembership(profile);
                }
            }

            _groups.RemoveAll(item => string.Equals(item.Id, groupId, StringComparison.Ordinal));
            await PersistProfileStoreAsync().ConfigureAwait(true);
            RefreshProgramsDisplay();
            UpdateToolbarStats();
            StatusBar.ApplySelection(Programs.GetAllPrograms().Count, Programs.SelectedProgram?.Name);
            StatusBar.ApplyActionResult($"Gruppe „{group.Name}“ aufgelöst.");
        }
        catch (Exception ex)
        {
            StatusBar.ApplyActionResult($"Gruppe konnte nicht aufgelöst werden: {ex.Message}", isError: true);
        }
    }

    public async Task DeleteProgramGroupWithProfilesAsync(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        var group = Programs.GetAllGroups().FirstOrDefault(item => item.Id == groupId);
        if (group is null)
        {
            return;
        }

        var confirm = await DesktopShellHelper.ConfirmWithOptionsAsync(
            GetMainWindow(),
            "Gruppe inkl. Profile löschen",
            $"Gruppe „{group.Name}“ mit allen {group.MemberCount} Profilen wirklich löschen?\n\n" +
            "Alle betroffenen Profile werden aus profiles.json entfernt.",
            new ConfirmDialogOptions(
                ShowDeleteSnapshotsOption: true,
                DeleteSnapshotsDefault: true,
                DeleteSnapshotsLabel: "Zugehörige Snapshots aller Profile auch löschen",
                ConfirmButtonText: "Gruppe löschen")).ConfigureAwait(true);

        if (!confirm.IsConfirmed)
        {
            return;
        }

        try
        {
            var memberIds = group.Members.Select(member => member.Id).ToList();
            foreach (var memberId in memberIds)
            {
                await DeleteProgramProfileCoreAsync(memberId, confirm.DeleteSnapshots, suppressStatusBar: true)
                    .ConfigureAwait(true);
            }

            _groups.RemoveAll(item => string.Equals(item.Id, groupId, StringComparison.Ordinal));
            await PersistProfileStoreAsync().ConfigureAwait(true);
            RefreshProgramsDisplay();
            Programs.SelectedProgram = Programs.GetAllPrograms().FirstOrDefault();
            Timeline.SetSnapshots([]);

            if (ActiveView == MainContentView.Snapshots)
            {
                await RefreshSnapshotsOverviewAsync().ConfigureAwait(true);
            }

            UpdateToolbarStats();
            StatusBar.ApplySelection(Programs.GetAllPrograms().Count, Programs.SelectedProgram?.Name);
            StatusBar.ApplyActionResult(
                $"Gruppe „{group.Name}“ mit {memberIds.Count} Profil(en) gelöscht.");
        }
        catch (Exception ex)
        {
            StatusBar.ApplyActionResult($"Gruppe konnte nicht gelöscht werden: {ex.Message}", isError: true);
        }
    }

    private async Task DeleteProgramProfileCoreAsync(
        string programId,
        bool deleteSnapshots,
        string? programName = null,
        bool suppressStatusBar = false)
    {
        var program = Programs.GetAllPrograms().FirstOrDefault(item => item.Id == programId);
        var resolvedName = programName ?? program?.Name ?? programId;

        try
        {
            _profiles.RemoveAll(profile => profile.Id == programId);
            CleanupGroupsAfterProfileRemoval();
            await PersistProfileStoreAsync().ConfigureAwait(true);

            if (deleteSnapshots)
            {
                var snapshotDir = _paths.GetProgramSnapshotsDirectory(programId);
                if (Directory.Exists(snapshotDir))
                {
                    Directory.Delete(snapshotDir, recursive: true);
                }
            }

            RefreshProgramsDisplay();
            Programs.SelectedProgram = Programs.GetAllPrograms().FirstOrDefault();
            Timeline.SetSnapshots([]);

            if (ActiveView == MainContentView.Snapshots)
            {
                await RefreshSnapshotsOverviewAsync().ConfigureAwait(true);
            }

            UpdateToolbarStats();
            StatusBar.ApplySelection(Programs.GetAllPrograms().Count, Programs.SelectedProgram?.Name);

            if (!suppressStatusBar)
            {
                var suffix = deleteSnapshots ? string.Empty : " (Snapshots behalten)";
                StatusBar.ApplyActionResult($"Profil „{resolvedName}“ gelöscht{suffix}.");
            }
        }
        catch (Exception ex)
        {
            if (!suppressStatusBar)
            {
                StatusBar.ApplyActionResult($"Profil konnte nicht gelöscht werden: {ex.Message}", isError: true);
            }
            else
            {
                throw;
            }
        }
    }

    public async Task DeleteSnapshotAsync(string programId, string snapshotId)
    {
        var program = Programs.GetAllPrograms().FirstOrDefault(item => item.Id == programId);
        var snapshotLabel = Snapshots.AllSnapshots
            .FirstOrDefault(item => item.ProgramId == programId && item.Snapshot.Id == snapshotId)
            ?.Snapshot.Name
            ?? snapshotId;

        var confirmed = await DesktopShellHelper.ConfirmAsync(
            GetMainWindow(),
            "Snapshot löschen",
            $"Snapshot „{snapshotLabel}“ wirklich löschen?\n\nDieser Vorgang kann nicht rückgängig gemacht werden.").ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        try
        {
            var deleted = await _snapshotService.DeleteSnapshotAsync(programId, snapshotId).ConfigureAwait(true);
            if (!deleted)
            {
                StatusBar.ApplyActionResult("Snapshot wurde nicht gefunden.", isError: true);
                return;
            }

            if (program is not null)
            {
                var snapshots = await _snapshotService.LoadSnapshotsAsync(programId).ConfigureAwait(true);
                program.Profile.LastSnapshotAt = snapshots.FirstOrDefault()?.CreatedAt;
                program.RefreshSnapshotStatus(program.Profile.LastSnapshotAt);
                await _profileService.SaveProfilesAsync(_profiles).ConfigureAwait(true);
                await RefreshSnapshotsAsync(program).ConfigureAwait(true);
            }

            if (ActiveView == MainContentView.Snapshots)
            {
                await RefreshSnapshotsOverviewAsync().ConfigureAwait(true);
            }

            UpdateToolbarStats();
            StatusBar.ApplyActionResult($"Snapshot „{snapshotLabel}“ gelöscht.");
        }
        catch (Exception ex)
        {
            StatusBar.ApplyActionResult($"Snapshot konnte nicht gelöscht werden: {ex.Message}", isError: true);
        }
    }

    public Task OpenInstallFolderAsync(string? programId)
    {
        var program = ResolveProgram(programId);
        if (program is null || string.IsNullOrWhiteSpace(program.Profile.InstallLocation))
        {
            StatusBar.ApplyActionResult("Kein Installationsordner verfügbar.", isError: true);
            return Task.CompletedTask;
        }

        if (!DesktopShellHelper.TryOpenInExplorer(program.Profile.InstallLocation))
        {
            StatusBar.ApplyActionResult("Installationsordner konnte nicht geöffnet werden.", isError: true);
            return Task.CompletedTask;
        }

        StatusBar.ApplyActionResult($"Installationsordner geöffnet: {program.Profile.InstallLocation}");
        return Task.CompletedTask;
    }

    public Task OpenSnapshotInExplorerAsync(string programId, string snapshotId)
    {
        var snapshotPath = _snapshotService.GetSnapshotDirectoryPath(programId, snapshotId);
        if (!DesktopShellHelper.TryOpenInExplorer(snapshotPath))
        {
            StatusBar.ApplyActionResult("Snapshot-Ordner konnte nicht geöffnet werden.", isError: true);
            return Task.CompletedTask;
        }

        StatusBar.ApplyActionResult("Snapshot-Ordner im Explorer geöffnet.");
        return Task.CompletedTask;
    }

    public async Task CopySnapshotPathAsync(string programId, string snapshotId)
    {
        var snapshotPath = _snapshotService.GetSnapshotDirectoryPath(programId, snapshotId);
        if (!await DesktopShellHelper.TryCopyTextAsync(snapshotPath).ConfigureAwait(true))
        {
            StatusBar.ApplyActionResult("Pfad konnte nicht in die Zwischenablage kopiert werden.", isError: true);
            return;
        }

        StatusBar.ApplyActionResult("Snapshot-Pfad kopiert.");
    }

    public async Task EditSnapshotAsync(string programId, string snapshotId)
    {
        var program = Programs.GetAllPrograms().FirstOrDefault(item => item.Id == programId);
        if (program is null)
        {
            StatusBar.ApplyActionResult("Programm für den Snapshot nicht gefunden.", isError: true);
            return;
        }

        var snapshots = await _snapshotService.LoadSnapshotsAsync(programId).ConfigureAwait(true);
        var snapshot = snapshots.FirstOrDefault(item => item.Id == snapshotId);
        if (snapshot is null)
        {
            StatusBar.ApplyActionResult("Snapshot nicht gefunden.", isError: true);
            return;
        }

        var currentDir = _snapshotService.GetSnapshotDirectoryPath(programId, snapshotId);
        var viewModel = new EditSnapshotViewModel(program.Profile, snapshot, currentDir);
        var dialog = new EditSnapshotWindow { DataContext = viewModel };
        viewModel.CloseRequested += () => dialog.Close();

        var owner = GetMainWindow();
        if (owner is not null)
        {
            await dialog.ShowDialog(owner).ConfigureAwait(true);
        }
        else
        {
            dialog.Show();
            await Task.Delay(100).ConfigureAwait(true);
        }

        if (viewModel.Result is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _snapshotService.UpdateSnapshotAsync(
                program.Profile,
                snapshotId,
                viewModel.Result.DisplayName,
                viewModel.Result.NewStorageRoot).ConfigureAwait(true);

            await RefreshSnapshotsAsync(program).ConfigureAwait(true);
            if (ActiveView == MainContentView.Snapshots)
            {
                await RefreshSnapshotsOverviewAsync().ConfigureAwait(true);
            }

            UpdateToolbarStats();
            Toolbar.StatusMessage = result.Message;
            StatusBar.ApplyActionResult(result.Message, result.Status == SnapshotResultStatus.Failed);
        }
        catch (Exception ex)
        {
            StatusBar.ApplyActionResult($"Snapshot konnte nicht aktualisiert werden: {ex.Message}", isError: true);
        }
        finally
        {
            IsBusy = false;
            Snapshots.NotifyWorkflowStateChanged();
        }
    }

    private async Task<SnapshotCaptureTargetChoice?> PromptSnapshotCaptureTargetAsync(ProgramProfile profile)
    {
        await _settingsService.LoadAsync().ConfigureAwait(true);
        var viewModel = new SnapshotCaptureTargetViewModel(_paths, _settingsService.Current, profile);
        var dialog = new SnapshotCaptureTargetWindow { DataContext = viewModel };
        viewModel.CloseRequested += () => dialog.Close();

        var owner = GetMainWindow();
        if (owner is not null)
        {
            await dialog.ShowDialog(owner).ConfigureAwait(true);
        }
        else
        {
            dialog.Show();
            await Task.Delay(100).ConfigureAwait(true);
        }

        return viewModel.Result;
    }

    public async Task CompareSnapshotOverviewAsync(SnapshotOverviewItemViewModel snapshot)
    {
        var program = Programs.GetAllPrograms().FirstOrDefault(item => item.Id == snapshot.ProgramId);
        if (program is null)
        {
            StatusBar.ApplyActionResult("Programm für den Snapshot nicht gefunden.", isError: true);
            return;
        }

        Programs.SelectedProgram = program;
        await RefreshSnapshotsAsync(program).ConfigureAwait(true);

        var selected = Timeline.Snapshots.FirstOrDefault(item => item.Id == snapshot.Snapshot.Id);
        if (selected is null)
        {
            StatusBar.ApplyActionResult("Snapshot für den Vergleich nicht gefunden.", isError: true);
            return;
        }

        var compareTarget = Timeline.Snapshots
            .Where(item => item.Id != selected.Id)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault(item => item.CreatedAt < selected.CreatedAt)
            ?? Timeline.Snapshots.FirstOrDefault(item => item.Id != selected.Id);

        Timeline.SelectedSnapshot = selected;
        Timeline.CompareWithSnapshot = compareTarget;
        await CompareSnapshotsAsync(selected, compareTarget).ConfigureAwait(true);
    }

    private ProgramProfileItemViewModel? ResolveProgram(string? programId)
    {
        if (!string.IsNullOrWhiteSpace(programId))
        {
            return Programs.GetAllPrograms().FirstOrDefault(item => item.Id == programId);
        }

        return Programs.SelectedProgram;
    }

    public void NavigateToSettings()
    {
        NavigateTo("einstellungen");
    }

    public async Task<EngineExecutionResult> RunEngineActionAsync(
        AppReinstallEngineAction action,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return EngineExecutionResult.Missing("HorosSaver ist gerade beschäftigt — bitte warten.");
        }

        IsBusy = true;
        Toolbar.SaveSnapshotCommand.NotifyCanExecuteChanged();
        Toolbar.RestoreSnapshotCommand.NotifyCanExecuteChanged();
        Toolbar.CreateSystemAbbildCommand.NotifyCanExecuteChanged();
        Toolbar.BindProgramCommand.NotifyCanExecuteChanged();
        Toolbar.BindCustomPathsCommand.NotifyCanExecuteChanged();
        Toolbar.CaptureInventoryCommand.NotifyCanExecuteChanged();
        Toolbar.EditProfilePathsCommand.NotifyCanExecuteChanged();
        Toolbar.AutoCreateGroupsCommand.NotifyCanExecuteChanged();

        try
        {
            var result = await _engineService.RunActionAsync(action, logProgress, cancellationToken)
                .ConfigureAwait(true);

            if (result.EngineMissing)
            {
                NavigateToSettings();
            }

            Toolbar.StatusMessage = result.Message;
            StatusBar.ApplyActionResult(result.Message, isError: !result.Success);
            return result;
        }
        catch (Exception ex)
        {
            var message = $"Engine-Aktion fehlgeschlagen: {ex.Message}";
            Toolbar.StatusMessage = message;
            StatusBar.ApplyActionResult(message, isError: true);
            return new EngineExecutionResult
            {
                Success = false,
                ExitCode = -1,
                Message = message,
                StandardError = ex.Message
            };
        }
        finally
        {
            IsBusy = false;
            Toolbar.SaveSnapshotCommand.NotifyCanExecuteChanged();
            Toolbar.RestoreSnapshotCommand.NotifyCanExecuteChanged();
            Toolbar.BindProgramCommand.NotifyCanExecuteChanged();
            Toolbar.BindCustomPathsCommand.NotifyCanExecuteChanged();
            Toolbar.CaptureInventoryCommand.NotifyCanExecuteChanged();
            Toolbar.EditProfilePathsCommand.NotifyCanExecuteChanged();
        Toolbar.AutoCreateGroupsCommand.NotifyCanExecuteChanged();
            Timeline.CompareSnapshotsCommand.NotifyCanExecuteChanged();
        }
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }

    private async Task ApplyCursorSnapshotLevelAsync(CursorSnapshotLevel level)
    {
        var cursorProfile = _profiles.FirstOrDefault(CursorSnapshotPaths.IsCursorProfile);
        if (cursorProfile is null)
        {
            return;
        }

        CursorSnapshotPaths.ApplyLevelToProfile(cursorProfile, level);
        await _profileService.SaveProfilesAsync(_profiles).ConfigureAwait(true);

        var item = Programs.GetAllPrograms().FirstOrDefault(program => program.Id == cursorProfile.Id);
        item?.RefreshProfileDetails();
    }

    private void NormalizeLoadedCursorProfiles()
    {
        var defaultLevel = CursorSnapshotPaths.NormalizeLevel(_settingsService.Current.CursorSnapshotLevel);
        foreach (var profile in _profiles.Where(CursorSnapshotPaths.IsCursorProfile))
        {
            profile.CursorSnapshotLevel = profile.CursorSnapshotLevel == 0
                ? defaultLevel
                : CursorSnapshotPaths.NormalizeLevel(profile.CursorSnapshotLevel);
        }
    }

    public async Task PersistDetailPanelWidthAsync()
    {
        var settings = _settingsService.Current;
        if (Math.Abs(settings.DetailPanelWidth - DetailPanelWidth) < 0.5)
        {
            return;
        }

        settings.DetailPanelWidth = DetailPanelWidth;
        await _settingsService.SaveAsync(settings).ConfigureAwait(false);
    }

    private async Task InitializeAsync()
    {
        try
        {
            var migrationService = new DataStoreMigrationService();
            var migrationResult = await migrationService.TryMigrateIfNeededAsync(_paths).ConfigureAwait(true);

            var store = await _profileService.LoadProfileStoreAsync().ConfigureAwait(true);
            if (store.Profiles.Count == 0)
            {
                var recoveryService = new ProfileRecoveryService();
                var recoveryResult = await recoveryService.TryRecoverFromSnapshotsAsync(
                    _paths,
                    _profileService).ConfigureAwait(true);

                if (recoveryResult.Changed)
                {
                    store = await _profileService.LoadProfileStoreAsync().ConfigureAwait(true);
                }
            }

            _profiles = store.Profiles.ToList();
            _groups = store.Groups.ToList();
            await _settingsService.LoadAsync().ConfigureAwait(true);
            DetailPanelWidth = Math.Clamp(_settingsService.Current.DetailPanelWidth, 240, 640);
            Toolbar.ApplySettingsFrom(_settingsService.Current);
            Snapshots.ApplySettingsFrom(_settingsService.Current);
            NormalizeLoadedCursorProfiles();
            await Settings.LoadSettingsAsync().ConfigureAwait(true);
            Settings.HostWindow = GetMainWindow();

            ZuSavenSeedResult? seedResult = null;
            if (_settingsService.Current.ZuSavenSeedEnabled)
            {
                var zuSavenSeeder = new ZuSavenSeeder(_discoveryService, _paths);
                seedResult = await zuSavenSeeder.ApplyAsync(_profiles).ConfigureAwait(true);
                if (seedResult.Changed)
                {
                    await PersistProfileStoreAsync().ConfigureAwait(true);
                }
            }

            if (_groups.Count == 0)
            {
                var detectedGroups = _profileService.AutoDetectGroups(_profiles).ToList();
                if (detectedGroups.Count > 0)
                {
                    _groups = detectedGroups;
                    _profileService.ApplyAutoGroups(_profiles, _groups);
                    await PersistProfileStoreAsync().ConfigureAwait(true);
                }
            }

            RefreshProgramsDisplay();

            await RefreshAllProgramSnapshotStatusesAsync().ConfigureAwait(true);

            var defaultSelection = Programs.GetAllPrograms().FirstOrDefault();
            Programs.SelectedProgram = defaultSelection;

            if (defaultSelection is not null)
            {
                await RefreshSnapshotsAsync(defaultSelection).ConfigureAwait(true);
            }

            UpdateToolbarStats();
            StatusBar.ApplySelection(Programs.GetAllPrograms().Count, defaultSelection?.Name);
            StatusBar.ApplyEnvironment(RuntimeEnvironmentLabels.ShellLabel, RuntimeEnvironmentLabels.OsLabel);

            if (migrationResult.Changed)
            {
                var migrationSummary =
                    $"Daten wiederhergestellt: {migrationResult.ProfilesMerged} Profile, " +
                    $"{migrationResult.SnapshotFoldersCopied} Snapshots aus {migrationResult.SourceDataRoot}.";
                StatusBar.ApplyActionResult(migrationSummary);
            }

            if (seedResult?.Changed == true)
            {
                var seedSummary = $"zu saven: +{seedResult.ProfilesAdded} Profile, {seedResult.ProfilesUpdated} aktualisiert, {seedResult.PathsMerged} Pfade.";
                StatusBar.ApplyActionResult(seedSummary);
            }

            if (_groups.Count > 0)
            {
                var groupedCount = _profiles.Count(profile => !string.IsNullOrWhiteSpace(profile.GroupId));
                StatusBar.ApplyActionResult($"{_groups.Count} Programm-Gruppen aktiv ({groupedCount} Profile).");
            }

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ApplyDetailPanelWidthFromViewModel();
            }
        }
        catch (Exception ex)
        {
            StatusBar.ApplyActionResult($"Initialisierung fehlgeschlagen: {ex.Message}", isError: true);
        }
    }

    private async void OnSelectedProgramChanged(ProgramProfileItemViewModel? program)
    {
        Timeline.SetProgram(program);
        StatusBar.ApplySelection(Programs.GetAllPrograms().Count, program?.Name);
        Toolbar.EditProfilePathsCommand.NotifyCanExecuteChanged();
        Toolbar.AutoCreateGroupsCommand.NotifyCanExecuteChanged();
        Timeline.EditProfilePathsCommand.NotifyCanExecuteChanged();
        Programs.NotifyWorkflowStateChanged();

        if (program is null)
        {
            Timeline.SetSnapshots([]);
            return;
        }

        await RefreshSnapshotsAsync(program).ConfigureAwait(true);
    }

    private void OnSelectedSnapshotChanged(SnapshotItemViewModel? snapshot)
    {
        if (snapshot is not null)
        {
            Timeline.ActionMessage = $"Ausgewählt: {snapshot.Name}";
        }

    }

    private async void OnSortOrderChanged()
    {
        try
        {
            await PersistSortOrderAsync().ConfigureAwait(true);
            StatusBar.ApplyActionResult("Programm-Reihenfolge gespeichert.");
        }
        catch (Exception ex)
        {
            StatusBar.ApplyActionResult($"Reihenfolge konnte nicht gespeichert werden: {ex.Message}", isError: true);
        }
    }

    private async Task RefreshSnapshotsAsync(ProgramProfileItemViewModel program)
    {
        var snapshots = await _snapshotService.LoadSnapshotsAsync(program.Id).ConfigureAwait(true);
        var items = snapshots
            .Select((snapshot, index) => new SnapshotItemViewModel(snapshot, index + 1))
            .ToList();

        Timeline.SetSnapshots(items);
        program.RefreshSnapshotStatus(snapshots.FirstOrDefault()?.CreatedAt);
        UpdateToolbarStats();
    }

    private async Task RefreshAllProgramSnapshotStatusesAsync()
    {
        foreach (var program in Programs.GetAllPrograms())
        {
            var snapshots = await _snapshotService.LoadSnapshotsAsync(program.Id).ConfigureAwait(true);
            program.RefreshSnapshotStatus(snapshots.FirstOrDefault()?.CreatedAt);
        }

        foreach (var group in Programs.GetAllGroups())
        {
            group.RefreshLastSnapshotSummary();
        }
    }

    private async Task RefreshSnapshotsOverviewAsync()
    {
        var overviewItems = new List<SnapshotOverviewItemViewModel>();

        foreach (var program in Programs.GetAllPrograms())
        {
            var snapshots = await _snapshotService.LoadSnapshotsAsync(program.Id).ConfigureAwait(true);
            var index = 1;
            foreach (var snapshot in snapshots.OrderByDescending(item => item.CreatedAt))
            {
                overviewItems.Add(new SnapshotOverviewItemViewModel(
                    program.Id,
                    program.Name,
                    program.CategoryLabel,
                    new SnapshotItemViewModel(snapshot, index++),
                    program.Profile.GroupId,
                    program.Profile.GroupName,
                    program.SortOrder));
            }
        }

        overviewItems = overviewItems
            .OrderByDescending(item => item.Snapshot.CreatedAt)
            .ToList();

        Snapshots.SetGroupingContext(_groups, Programs.GetAllPrograms());
        Snapshots.SetSnapshots(overviewItems);
        Snapshots.ApplyFilter(Toolbar.SearchText);
        UpdateToolbarStats(overviewItems.Count);
    }

    private void OnOverviewSnapshotSelected(SnapshotOverviewItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var program = Programs.GetAllPrograms().FirstOrDefault(p => p.Id == item.ProgramId);
        if (program is not null)
        {
            Programs.SelectedProgram = program;
        }

        var timelineSnapshot = Timeline.Snapshots.FirstOrDefault(snapshot => snapshot.Id == item.Snapshot.Id);
        if (timelineSnapshot is not null)
        {
            Timeline.SelectedSnapshot = timelineSnapshot;
        }

        StatusBar.ApplyActionResult($"{item.ProgramName}: {item.Snapshot.Name} ausgewählt.");
    }

    private void UpdateToolbarStats(int? totalSnapshotCount = null)
    {
        var lastSnapshot = _profiles
            .Where(profile => profile.LastSnapshotAt.HasValue)
            .Select(profile => profile.LastSnapshotAt!.Value)
            .DefaultIfEmpty()
            .Max();

        var snapshotCount = totalSnapshotCount ?? Timeline.Snapshots.Count;

        DateTimeOffset? last = lastSnapshot == default ? null : lastSnapshot;
        Toolbar.UpdateStats(Programs.GetAllPrograms().Count, snapshotCount, last);
    }

    private void CleanupGroupsAfterProfileRemoval()
    {
        var validGroupIds = _profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.GroupId))
            .GroupBy(profile => profile.GroupId!, StringComparer.Ordinal)
            .Where(group => group.Count() >= 2)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var profile in _profiles.Where(profile => !string.IsNullOrWhiteSpace(profile.GroupId)))
        {
            if (!validGroupIds.Contains(profile.GroupId!))
            {
                ProgramGroupDetector.ClearGroupMembership(profile);
            }
        }

        _groups = _groups
            .Where(group => validGroupIds.Contains(group.Id))
            .ToList();
    }

    private void RefreshProgramsDisplay()
    {
        var items = _profiles.Select(profile =>
        {
            var item = new ProgramProfileItemViewModel(profile);
            item.AttachSnapshotJobManager(_snapshotJobManager);
            return item;
        }).ToList();
        if (_groups.Count > 0)
        {
            Programs.SetGroups(_groups, items);
        }
        else
        {
            Programs.SetPrograms(items);
        }
    }

    private void OnSnapshotJobCompleted(object? sender, SnapshotJobCompletedEventArgs args)
        => _ = HandleSnapshotJobCompletedAsync(args);

    private async Task HandleSnapshotJobCompletedAsync(SnapshotJobCompletedEventArgs args)
    {
        if (args.Result.Status is SnapshotResultStatus.Success or SnapshotResultStatus.Partial)
        {
            await PersistSortOrderAsync().ConfigureAwait(true);
        }

        var program = Programs.GetAllPrograms().FirstOrDefault(item => item.Id == args.ProgramId);
        if (program is not null)
        {
            program.RefreshSnapshotStatus(program.Profile.LastSnapshotAt);

            if (Programs.SelectedProgram?.Id == args.ProgramId)
            {
                await RefreshSnapshotsAsync(program).ConfigureAwait(true);
            }

            var group = Programs.GetAllGroups()
                .FirstOrDefault(entry => entry.Members.Any(member => member.Id == args.ProgramId));
            group?.RefreshLastSnapshotSummary();
        }

        if (ActiveView == MainContentView.Snapshots)
        {
            await RefreshSnapshotsOverviewAsync().ConfigureAwait(true);
        }

        UpdateToolbarStats();
        Toolbar.StatusMessage = args.Result.Message;
        var isError = args.Result.Status is SnapshotResultStatus.Failed or SnapshotResultStatus.Cancelled;
        StatusBar.ApplyActionResult(args.Result.Message, isError);
        Programs.NotifyWorkflowStateChanged();
    }

    private async Task PersistProfileStoreAsync()
        => await _profileService.SaveProfileStoreAsync(_profiles, _groups).ConfigureAwait(true);

    private void NotifyWorkflowCommandsChanged()
    {
        Toolbar.SaveSnapshotCommand.NotifyCanExecuteChanged();
        Toolbar.RestoreSnapshotCommand.NotifyCanExecuteChanged();
        Toolbar.CreateSystemAbbildCommand.NotifyCanExecuteChanged();
        Toolbar.BindProgramCommand.NotifyCanExecuteChanged();
        Toolbar.BindCustomPathsCommand.NotifyCanExecuteChanged();
        Toolbar.CaptureInventoryCommand.NotifyCanExecuteChanged();
        Toolbar.EditProfilePathsCommand.NotifyCanExecuteChanged();
        Toolbar.AutoCreateGroupsCommand.NotifyCanExecuteChanged();
        Sidebar.CreateSystemAbbildCommand.NotifyCanExecuteChanged();
        Programs.NotifyWorkflowStateChanged();
        Timeline.CompareSnapshotsCommand.NotifyCanExecuteChanged();
    }

    private async Task PersistSortOrderAsync()
    {
        var ordered = Programs.GetAllPrograms().Select(item => item.Profile).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].SortOrder = index;
        }

        _profiles = ordered;
        await PersistProfileStoreAsync().ConfigureAwait(true);
    }
}
