using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HorosSaver.Models;
using HorosSaver.Services;
using HorosSaver.ViewModels;

namespace HorosSaver.ViewModels.Regions;

public partial class SidebarRegionViewModel : ViewModelBase
{
    private readonly INavigationHost _navigationHost;
    private readonly IWorkflowHost? _workflowHost;

    public SidebarRegionViewModel(INavigationHost navigationHost, IWorkflowHost? workflowHost = null)
    {
        _navigationHost = navigationHost;
        _workflowHost = workflowHost;
        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new() { Id = "programme", Label = "Programme", IconKey = "programme", IsActive = true },
            new() { Id = "snapshots", Label = "Snapshots", IconKey = "snapshots" },
            new() { Id = "timeline", Label = "Timeline", IconKey = "timeline" },
            new() { Id = "einstellungen", Label = "Einstellungen", IconKey = "settings" }
        };
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public string VersionLabel => "HorosSaver 1.0.0";
    public string CopyrightLabel => "© 2026 HorosCode GmbH";

    [RelayCommand]
    private void SelectNavigation(string? viewId)
    {
        if (string.IsNullOrWhiteSpace(viewId))
        {
            return;
        }

        _navigationHost.NavigateTo(viewId);
    }

    public void SetActiveNavigation(string viewId)
    {
        foreach (var item in NavigationItems)
        {
            item.IsActive = item.Id == viewId;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateSystemAbbild))]
    private async Task CreateSystemAbbildAsync()
    {
        if (_workflowHost is null)
        {
            return;
        }

        await _workflowHost.CreateSystemAbbildAsync().ConfigureAwait(false);
    }

    private bool CanCreateSystemAbbild() => _workflowHost is not null && !_workflowHost.IsBusy;
}

public partial class ToolbarRegionViewModel : ViewModelBase
{
    private readonly IWorkflowHost _workflowHost;
    private readonly IAppSettingsService? _settingsService;
    private bool _isApplyingSettings;

    public ToolbarRegionViewModel(IWorkflowHost workflowHost, IAppSettingsService? settingsService = null)
    {
        _workflowHost = workflowHost;
        _settingsService = settingsService;
        ApplyBreadcrumbParts(Breadcrumb);
    }

    public void ApplySettingsFrom(AppSettings settings)
    {
        _isApplyingSettings = true;
        IsToolbarExpanded = settings.IsToolbarExpanded;
        _isApplyingSettings = false;
    }

    [ObservableProperty]
    private string _breadcrumb = "Programme / App-Profile";

    [ObservableProperty]
    private string _breadcrumbPrimary = "Programme";

    [ObservableProperty]
    private string _breadcrumbSecondary = " / App-Profile";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _programCountLabel = "0 Programme";

    [ObservableProperty]
    private string _snapshotCountLabel = "0 Snapshots";

    [ObservableProperty]
    private string _lastSavePrefix = "Letzter Save: ";

    [ObservableProperty]
    private string _lastSaveAccent = "—";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isToolbarExpanded = true;

    public string ToolbarExpandTooltip => IsToolbarExpanded ? "Toolbar einklappen" : "Toolbar ausklappen";

    public string ToolbarChevron => IsToolbarExpanded ? "▲" : "▼";

    partial void OnIsToolbarExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ToolbarExpandTooltip));
        OnPropertyChanged(nameof(ToolbarChevron));

        if (!_isApplyingSettings)
        {
            _ = PersistToolbarExpandedAsync();
        }
    }

    partial void OnBreadcrumbChanged(string value) => ApplyBreadcrumbParts(value);

    [RelayCommand]
    private void ToggleToolbar()
    {
        IsToolbarExpanded = !IsToolbarExpanded;
    }

    private async Task PersistToolbarExpandedAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        var settings = _settingsService.Current;
        if (settings.IsToolbarExpanded == IsToolbarExpanded)
        {
            return;
        }

        settings.IsToolbarExpanded = IsToolbarExpanded;
        await _settingsService.SaveAsync(settings).ConfigureAwait(false);
    }

    public void UpdateStats(int programCount, int snapshotCount, DateTimeOffset? lastSnapshotAt)
    {
        ProgramCountLabel = $"{programCount} Programme";
        SnapshotCountLabel = $"{snapshotCount} Snapshots";
        LastSavePrefix = "Letzter Save: ";
        LastSaveAccent = lastSnapshotAt.HasValue
            ? FormatRelative(lastSnapshotAt.Value)
            : "—";
    }

    private void ApplyBreadcrumbParts(string value)
    {
        var separatorIndex = value.IndexOf(" / ", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            BreadcrumbPrimary = value;
            BreadcrumbSecondary = string.Empty;
            return;
        }

        BreadcrumbPrimary = value[..separatorIndex];
        BreadcrumbSecondary = value[separatorIndex..];
    }

    [RelayCommand(CanExecute = nameof(CanRunWorkflow))]
    private async Task SaveSnapshotAsync()
    {
        await _workflowHost.SaveSnapshotAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanRunWorkflow))]
    private async Task RestoreSnapshotAsync()
    {
        await _workflowHost.OpenRestoreWizardAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanRunWorkflow))]
    private async Task BindProgramAsync()
    {
        await _workflowHost.OpenBindProgramWizardAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanRunWorkflow))]
    private async Task BindCustomPathsAsync()
    {
        await _workflowHost.OpenCustomPathsWizardAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanRunWorkflow))]
    private async Task CaptureInventoryAsync()
    {
        await _workflowHost.RunEngineActionAsync(AppReinstallEngineAction.Capture).ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanRunWorkflow))]
    private async Task EditProfilePathsAsync()
    {
        await _workflowHost.OpenEditProfilePathsAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanRunWorkflow))]
    private async Task AutoCreateGroupsAsync()
    {
        await _workflowHost.AutoCreateProgramGroupsAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanRunWorkflow))]
    private async Task CreateSystemAbbildAsync()
    {
        await _workflowHost.CreateSystemAbbildAsync().ConfigureAwait(false);
    }

    private bool CanEditProfilePaths() => !_workflowHost.IsBusy && _workflowHost.HasSelectedProgram;

    private bool CanRunWorkflow() => !_workflowHost.IsBusy;

    private static string FormatRelative(DateTimeOffset timestamp)
    {
        var delta = DateTimeOffset.Now - timestamp;
        if (delta.TotalMinutes < 1)
        {
            return "gerade eben";
        }

        if (delta.TotalHours < 1)
        {
            return $"vor {(int)Math.Max(1, delta.TotalMinutes)} Min.";
        }

        if (delta.TotalHours < 24)
        {
            return $"vor {(int)Math.Max(1, delta.TotalHours)} Std.";
        }

        if (delta.TotalDays < 2)
        {
            return $"gestern {timestamp:HH:mm}";
        }

        return timestamp.ToString("dd.MM.yyyy HH:mm");
    }
}

public partial class ProgramsRegionViewModel : ViewModelBase
{
    private readonly IWorkflowHost? _workflowHost;
    private readonly List<ProgramProfileItemViewModel> _allPrograms = [];
    private readonly List<ProgramGroupItemViewModel> _allGroups = [];
    private readonly HashSet<string> _renderedGroupIds = new(StringComparer.Ordinal);

    public ProgramsRegionViewModel(IWorkflowHost? workflowHost = null)
    {
        _workflowHost = workflowHost;
    }

    public ObservableCollection<ProgramListItemViewModel> DisplayItems { get; } = [];

    [ObservableProperty]
    private ProgramProfileItemViewModel? _selectedProgram;

    [ObservableProperty]
    private string _sectionTitle = "App-Profile";

    [ObservableProperty]
    private int _gridColumns = 2;

    [ObservableProperty]
    private string _sortModeLabel = "Sortierung: manuell";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _hasVisiblePrograms = true;

    [ObservableProperty]
    private string _emptyFilterMessage = string.Empty;

    [ObservableProperty]
    private bool _isFilterActive;

    [ObservableProperty]
    private bool _isProgramsEmpty;

    [ObservableProperty]
    private string _emptyProgramsMessage =
        "Noch keine Programme — „Programm einbinden“ oder „Dateien & Ordner“ in der Toolbar.";

    [ObservableProperty]
    private string _emptyStateTitle = "Keine Treffer";

    [ObservableProperty]
    private string _emptyStateMessage = string.Empty;

    public event Action<ProgramProfileItemViewModel?>? SelectedProgramChanged;
    public event Action? SortOrderChanged;

    partial void OnSelectedProgramChanged(ProgramProfileItemViewModel? value)
    {
        foreach (var program in _allPrograms)
        {
            program.IsSelected = program == value;
        }

        foreach (var group in _allGroups)
        {
            group.IsSelected = value is not null
                && !string.IsNullOrWhiteSpace(value.Profile.GroupId)
                && string.Equals(group.Id, value.Profile.GroupId, StringComparison.Ordinal);
        }

        SelectedProgramChanged?.Invoke(value);
    }

    public IReadOnlyList<ProgramProfileItemViewModel> GetAllPrograms() => _allPrograms;

    public IReadOnlyList<ProgramGroupItemViewModel> GetAllGroups() => _allGroups;

    public void SetPrograms(IEnumerable<ProgramProfileItemViewModel> programs)
    {
        _allPrograms.Clear();
        _allPrograms.AddRange(programs);
        RebuildGroupsFromPrograms();
        ApplyFilter(SearchText);
    }

    public void SetGroups(IEnumerable<ProgramGroup> groups, IEnumerable<ProgramProfileItemViewModel> programs)
    {
        _allPrograms.Clear();
        _allPrograms.AddRange(programs);
        _allGroups.Clear();

        var programsByGroup = _allPrograms
            .Where(program => !string.IsNullOrWhiteSpace(program.Profile.GroupId))
            .GroupBy(program => program.Profile.GroupId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        foreach (var group in groups.OrderBy(entry => entry.SortOrder))
        {
            if (!programsByGroup.TryGetValue(group.Id, out var members) || members.Count < 2)
            {
                continue;
            }

            _allGroups.Add(new ProgramGroupItemViewModel(group, members));
        }

        ApplyFilter(SearchText);
    }

    private void RebuildGroupsFromPrograms()
    {
        _allGroups.Clear();

        var grouped = _allPrograms
            .Where(program => !string.IsNullOrWhiteSpace(program.Profile.GroupId))
            .GroupBy(program => program.Profile.GroupId!, StringComparer.Ordinal);

        foreach (var groupMembers in grouped)
        {
            if (groupMembers.Count() < 2)
            {
                continue;
            }

            var first = groupMembers.First();
            var group = new ProgramGroup
            {
                Id = groupMembers.Key,
                Name = first.Profile.GroupName ?? ProgramGroupDetector.ExtractGroupStem(first.Name) ?? first.Name,
                SortOrder = groupMembers.Min(member => member.SortOrder)
            };

            _allGroups.Add(new ProgramGroupItemViewModel(group, groupMembers));
        }
    }

    public void AddProgram(ProgramProfileItemViewModel program)
    {
        _allPrograms.Add(program);
        ApplyFilter(SearchText);
        SelectedProgram = program;
    }

    public void ApplyFilter(string? searchText = null)
    {
        if (searchText is not null)
        {
            SearchText = searchText;
        }

        var query = SearchText.Trim();
        IsFilterActive = query.Length > 0;

        DisplayItems.Clear();
        _renderedGroupIds.Clear();

        foreach (var program in _allPrograms.OrderBy(item => item.SortOrder))
        {
            if (!string.IsNullOrWhiteSpace(program.Profile.GroupId))
            {
                var group = _allGroups.FirstOrDefault(entry => entry.Id == program.Profile.GroupId);
                if (group is null)
                {
                    if (!program.MatchesSearch(query))
                    {
                        continue;
                    }

                    DisplayItems.Add(program);
                    continue;
                }

                if (_renderedGroupIds.Contains(group.Id))
                {
                    continue;
                }

                if (!group.MatchesSearch(query))
                {
                    continue;
                }

                if (IsFilterActive)
                {
                    group.IsExpanded = true;
                }

                _renderedGroupIds.Add(group.Id);
                DisplayItems.Add(group);
                continue;
            }

            if (!program.MatchesSearch(query))
            {
                continue;
            }

            DisplayItems.Add(program);
        }

        HasVisiblePrograms = DisplayItems.Count > 0;
        IsProgramsEmpty = _allPrograms.Count == 0;
        EmptyFilterMessage = IsFilterActive && !HasVisiblePrograms
            ? $"Keine Programme für „{query}“ gefunden."
            : string.Empty;
        EmptyStateTitle = IsProgramsEmpty && !IsFilterActive ? "Noch keine Programme" : "Keine Treffer";
        EmptyStateMessage = IsProgramsEmpty && !IsFilterActive
            ? EmptyProgramsMessage
            : EmptyFilterMessage;

        MoveProgramUpCommand.NotifyCanExecuteChanged();
        MoveProgramDownCommand.NotifyCanExecuteChanged();
        DeleteProgramCommand.NotifyCanExecuteChanged();
        DissolveGroupCommand.NotifyCanExecuteChanged();
        DeleteGroupWithProfilesCommand.NotifyCanExecuteChanged();
        OpenInstallFolderCommand.NotifyCanExecuteChanged();
        SaveProgramSnapshotCommand.NotifyCanExecuteChanged();
        SaveGroupSnapshotCommand.NotifyCanExecuteChanged();
        ToggleGroupExpandedCommand.NotifyCanExecuteChanged();

        var visibleProgramCount = DisplayItems.Sum(item => item.IsGroup
            ? ((ProgramGroupItemViewModel)item).Members.Count
            : 1);

        SortModeLabel = IsFilterActive
            ? $"Sortierung: manuell · {visibleProgramCount} von {_allPrograms.Count} angezeigt (↑/↓ auf Gesamtliste)"
            : "Sortierung: manuell (↑/↓ auf Gesamtliste)";
    }

    [RelayCommand]
    private void SelectProgram(ProgramProfileItemViewModel? program)
    {
        SelectedProgram = program;
    }

    [RelayCommand(CanExecute = nameof(CanEditProfilePaths))]
    private async Task EditProfilePathsAsync(ProgramProfileItemViewModel? program)
    {
        if (program is null || _workflowHost is null)
        {
            return;
        }

        SelectedProgram = program;
        await _workflowHost.OpenEditProfilePathsAsync(program.Profile).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanRunProgramWorkflow))]
    private async Task SaveProgramSnapshotAsync(ProgramProfileItemViewModel? program)
    {
        if (program is null || _workflowHost is null)
        {
            return;
        }

        SelectedProgram = program;
        await _workflowHost.SaveSnapshotForProgramAsync(program.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOpenInstallFolder))]
    private async Task OpenInstallFolderAsync(ProgramProfileItemViewModel? program)
    {
        if (program is null || _workflowHost is null)
        {
            return;
        }

        SelectedProgram = program;
        await _workflowHost.OpenInstallFolderAsync(program.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanDeleteProgram))]
    private async Task DeleteProgramAsync(ProgramProfileItemViewModel? program)
    {
        if (program is null || _workflowHost is null)
        {
            return;
        }

        await _workflowHost.DeleteProgramProfileAsync(program.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanRunGroupWorkflow))]
    private async Task DissolveGroupAsync(ProgramGroupItemViewModel? group)
    {
        if (group is null || _workflowHost is null)
        {
            return;
        }

        await _workflowHost.DissolveProgramGroupAsync(group.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanRunGroupWorkflow))]
    private async Task DeleteGroupWithProfilesAsync(ProgramGroupItemViewModel? group)
    {
        if (group is null || _workflowHost is null)
        {
            return;
        }

        await _workflowHost.DeleteProgramGroupWithProfilesAsync(group.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanRunGroupWorkflow))]
    private async Task SaveGroupSnapshotAsync(ProgramGroupItemViewModel? group)
    {
        if (group is null || _workflowHost is null)
        {
            return;
        }

        await _workflowHost.SaveGroupSnapshotAsync(group.Id).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ToggleGroupExpanded(ProgramGroupItemViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        group.IsExpanded = !group.IsExpanded;
    }

    [RelayCommand(CanExecute = nameof(CanRunWorkflowHost))]
    private async Task AutoCreateGroupsAsync()
    {
        if (_workflowHost is null)
        {
            return;
        }

        await _workflowHost.AutoCreateProgramGroupsAsync().ConfigureAwait(true);
    }

    public void NotifyWorkflowStateChanged()
    {
        EditProfilePathsCommand.NotifyCanExecuteChanged();
        SaveProgramSnapshotCommand.NotifyCanExecuteChanged();
        SaveGroupSnapshotCommand.NotifyCanExecuteChanged();
        OpenInstallFolderCommand.NotifyCanExecuteChanged();
        DeleteProgramCommand.NotifyCanExecuteChanged();
        DissolveGroupCommand.NotifyCanExecuteChanged();
        DeleteGroupWithProfilesCommand.NotifyCanExecuteChanged();
        MoveProgramUpCommand.NotifyCanExecuteChanged();
        MoveProgramDownCommand.NotifyCanExecuteChanged();
        AutoCreateGroupsCommand.NotifyCanExecuteChanged();
    }

    private bool CanRunProgramWorkflow(ProgramProfileItemViewModel? program)
        => program is not null
           && _workflowHost is not null
           && !_workflowHost.IsBusy
           && !program.HasActiveSnapshotJob;

    private bool CanRunGroupWorkflow(ProgramGroupItemViewModel? group)
        => group is not null
           && _workflowHost is not null
           && !_workflowHost.IsBusy;

    private bool CanRunWorkflowHost() => _workflowHost is not null && !_workflowHost.IsBusy;

    private bool CanOpenInstallFolder(ProgramProfileItemViewModel? program)
        => CanRunProgramWorkflow(program) && program!.HasInstallLocation;

    private bool CanEditProfilePaths(ProgramProfileItemViewModel? program)
        => CanRunProgramWorkflow(program);

    private bool CanDeleteProgram(ProgramProfileItemViewModel? program)
        => program is not null
           && _workflowHost is not null
           && !_workflowHost.IsBusy;

    [RelayCommand(CanExecute = nameof(CanMoveProgram))]
    private void MoveProgramUp(ProgramProfileItemViewModel? program)
    {
        if (program is null)
        {
            return;
        }

        var index = _allPrograms.IndexOf(program);
        if (index <= 0)
        {
            return;
        }

        _allPrograms.RemoveAt(index);
        _allPrograms.Insert(index - 1, program);
        ApplyFilter();
        SortOrderChanged?.Invoke();
        MoveProgramUpCommand.NotifyCanExecuteChanged();
        MoveProgramDownCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanMoveProgramDown))]
    private void MoveProgramDown(ProgramProfileItemViewModel? program)
    {
        if (program is null)
        {
            return;
        }

        var index = _allPrograms.IndexOf(program);
        if (index < 0 || index >= _allPrograms.Count - 1)
        {
            return;
        }

        _allPrograms.RemoveAt(index);
        _allPrograms.Insert(index + 1, program);
        ApplyFilter();
        SortOrderChanged?.Invoke();
        MoveProgramUpCommand.NotifyCanExecuteChanged();
        MoveProgramDownCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveProgram(ProgramProfileItemViewModel? program)
    {
        if (program is null)
        {
            return false;
        }

        return _allPrograms.IndexOf(program) > 0;
    }

    private bool CanMoveProgramDown(ProgramProfileItemViewModel? program)
    {
        if (program is null)
        {
            return false;
        }

        var index = _allPrograms.IndexOf(program);
        return index >= 0 && index < _allPrograms.Count - 1;
    }
}

public partial class TimelineRegionViewModel : ViewModelBase
{
    private readonly IWorkflowHost _workflowHost;

    public TimelineRegionViewModel(IWorkflowHost workflowHost)
    {
        _workflowHost = workflowHost;
    }

    public ObservableCollection<SnapshotItemViewModel> Snapshots { get; } = [];
    public ObservableCollection<SnapshotItemViewModel> CompareOptions { get; } = [];

    [ObservableProperty]
    private SnapshotItemViewModel? _selectedSnapshot;

    [ObservableProperty]
    private SnapshotItemViewModel? _compareWithSnapshot;

    [ObservableProperty]
    private string _title = "Zeitzustände";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProgramName))]
    private string _programName = string.Empty;

    public bool HasProgramName => !string.IsNullOrWhiteSpace(ProgramName);

    [ObservableProperty]
    private string _emptyMessage = "Wählen Sie ein Programm, um Snapshots anzuzeigen.";

    [ObservableProperty]
    private string _actionMessage = string.Empty;

    [ObservableProperty]
    private bool _hasSnapshots;

    [ObservableProperty]
    private bool _canCompare;

    public event Action<SnapshotItemViewModel?>? SelectedSnapshotChanged;

    partial void OnSelectedSnapshotChanged(SnapshotItemViewModel? value)
    {
        foreach (var snapshot in Snapshots)
        {
            snapshot.IsSelected = snapshot == value;
        }

        UpdateCompareOptions();
        SelectedSnapshotChanged?.Invoke(value);
        CompareSnapshotsCommand.NotifyCanExecuteChanged();
        CompareSnapshotContextCommand.NotifyCanExecuteChanged();
        OpenSnapshotInExplorerCommand.NotifyCanExecuteChanged();
        CopySnapshotPathCommand.NotifyCanExecuteChanged();
    }

    partial void OnCompareWithSnapshotChanged(SnapshotItemViewModel? value)
    {
        CompareSnapshotsCommand.NotifyCanExecuteChanged();
    }

    public void SetProgram(ProgramProfileItemViewModel? program)
    {
        ProgramName = program?.Name ?? string.Empty;
        Title = program is null ? "Zeitzustände" : $"{program.Name} — Zeitzustände";
        EmptyMessage = program is null
            ? "Wählen Sie ein Programm, um Snapshots anzuzeigen."
            : "Noch keine Snapshots — „Snapshot speichern“ starten.";
    }

    public void SetSnapshots(IEnumerable<SnapshotItemViewModel> snapshots)
    {
        Snapshots.Clear();
        var list = snapshots.ToList();
        for (var index = 0; index < list.Count; index++)
        {
            list[index].IsLast = index == list.Count - 1;
            Snapshots.Add(list[index]);
        }

        SelectedSnapshot = Snapshots.FirstOrDefault();
        HasSnapshots = Snapshots.Count > 0;
        CanCompare = Snapshots.Count >= 2;
        UpdateCompareOptions();
        CompareSnapshotsCommand.NotifyCanExecuteChanged();
    }

    private void UpdateCompareOptions()
    {
        CompareOptions.Clear();

        if (SelectedSnapshot is null)
        {
            CompareWithSnapshot = null;
            return;
        }

        foreach (var snapshot in Snapshots.Where(item => item.Id != SelectedSnapshot.Id))
        {
            CompareOptions.Add(snapshot);
        }

        var selectedIndex = Snapshots.IndexOf(SelectedSnapshot);
        CompareWithSnapshot = selectedIndex >= 0 && selectedIndex + 1 < Snapshots.Count
            ? Snapshots[selectedIndex + 1]
            : CompareOptions.FirstOrDefault();
    }

    [RelayCommand]
    private void SelectSnapshot(SnapshotItemViewModel? snapshot)
    {
        SelectedSnapshot = snapshot;
    }

    [RelayCommand(CanExecute = nameof(CanCompareSnapshotContext))]
    private async Task CompareSnapshotContextAsync(SnapshotItemViewModel? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        SelectedSnapshot = snapshot;
        var compareTarget = Snapshots
            .Where(item => item.Id != snapshot.Id)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault(item => item.CreatedAt < snapshot.CreatedAt)
            ?? Snapshots.FirstOrDefault(item => item.Id != snapshot.Id);

        await _workflowHost.CompareSnapshotsAsync(snapshot, compareTarget).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOpenSnapshotInExplorer))]
    private async Task OpenSnapshotInExplorerAsync(SnapshotItemViewModel? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        SelectedSnapshot = snapshot;
        await _workflowHost.OpenSnapshotInExplorerAsync(snapshot.Snapshot.ProgramId, snapshot.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOpenSnapshotInExplorer))]
    private async Task CopySnapshotPathAsync(SnapshotItemViewModel? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        SelectedSnapshot = snapshot;
        await _workflowHost.CopySnapshotPathAsync(snapshot.Snapshot.ProgramId, snapshot.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanCompareSnapshots))]
    private async Task CompareSnapshotsAsync()
    {
        await _workflowHost.CompareSnapshotsAsync(SelectedSnapshot, CompareWithSnapshot).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanEditProfilePaths))]
    private async Task EditProfilePathsAsync()
    {
        await _workflowHost.OpenEditProfilePathsAsync().ConfigureAwait(true);
    }

    private bool CanEditProfilePaths()
        => !_workflowHost.IsBusy && _workflowHost.HasSelectedProgram;

    private bool CanCompareSnapshotContext(SnapshotItemViewModel? snapshot)
        => !_workflowHost.IsBusy
           && snapshot is not null
           && Snapshots.Count >= 2;

    private bool CanOpenSnapshotInExplorer(SnapshotItemViewModel? snapshot)
        => !_workflowHost.IsBusy && snapshot is not null;

    private bool CanCompareSnapshots()
        => !_workflowHost.IsBusy
           && Snapshots.Count >= 2
           && SelectedSnapshot is not null
           && CompareWithSnapshot is not null
           && !string.Equals(SelectedSnapshot.Id, CompareWithSnapshot.Id, StringComparison.Ordinal);
}

public partial class SnapshotsRegionViewModel : ViewModelBase
{
    private readonly IWorkflowHost _workflowHost;
    private readonly IAppSettingsService _settingsService;
    private readonly List<SnapshotOverviewItemViewModel> _allSnapshots = [];
    private readonly List<ProgramGroup> _programGroups = [];
    private readonly List<ProgramProfileItemViewModel> _allPrograms = [];
    private readonly HashSet<string> _validProgramGroupIds = new(StringComparer.Ordinal);
    private string _activeFilter = string.Empty;
    private bool _isApplyingSettings;

    public SnapshotsRegionViewModel(IWorkflowHost workflowHost, IAppSettingsService settingsService)
    {
        _workflowHost = workflowHost;
        _settingsService = settingsService;

        ViewLayoutOptions = SnapshotViewLayoutOptionViewModel.CreateAll();
        GroupModeOptions = SnapshotGroupModeOptionViewModel.CreateAll();
        SortModeOptions = SnapshotSortModeOptionViewModel.CreateAll();
        DateRangeOptions = SnapshotDateRangeOptionViewModel.CreateAll();

        ApplySettingsFrom(_settingsService.Current);
    }

    public ObservableCollection<SnapshotDisplayGroupViewModel> DisplayGroups { get; } = [];

    public IReadOnlyList<SnapshotOverviewItemViewModel> AllSnapshots => _allSnapshots;

    public IReadOnlyList<SnapshotViewLayoutOptionViewModel> ViewLayoutOptions { get; }
    public IReadOnlyList<SnapshotGroupModeOptionViewModel> GroupModeOptions { get; }
    public IReadOnlyList<SnapshotSortModeOptionViewModel> SortModeOptions { get; }
    public IReadOnlyList<SnapshotDateRangeOptionViewModel> DateRangeOptions { get; }

    public event Action<SnapshotOverviewItemViewModel?>? SnapshotSelected;

    [ObservableProperty]
    private string _sectionTitle = "Alle Snapshots";

    [ObservableProperty]
    private string _sectionSubtitle = "Gesicherte Zeitzustände über alle Programme hinweg.";

    [ObservableProperty]
    private string _statsLine = string.Empty;

    [ObservableProperty]
    private bool _hasSnapshots;

    [ObservableProperty]
    private bool _hasVisibleSnapshots = true;

    [ObservableProperty]
    private bool _isFilterActive;

    [ObservableProperty]
    private SnapshotViewMode _currentViewMode = SnapshotViewMode.Cards;

    [ObservableProperty]
    private bool _showGrouped = true;

    [ObservableProperty]
    private bool _onlyWithSnapshots = true;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private SnapshotViewLayoutOptionViewModel? _selectedViewLayoutOption;

    [ObservableProperty]
    private SnapshotGroupModeOptionViewModel? _selectedGroupModeOption;

    [ObservableProperty]
    private SnapshotSortModeOptionViewModel? _selectedSortModeOption;

    [ObservableProperty]
    private SnapshotDateRangeOptionViewModel? _selectedDateRangeOption;

    [ObservableProperty]
    private string _emptyMessage = "Noch keine Snapshots vorhanden — „Snapshot speichern“ in der Toolbar starten.";

    [ObservableProperty]
    private string _emptyFilterMessage = string.Empty;

    [ObservableProperty]
    private int _gridColumns = 3;

    public bool ShowFilterEmpty => HasSnapshots && !HasVisibleSnapshots && IsFilterActive;

    public bool IsCardsView => CurrentViewMode == SnapshotViewMode.Cards;
    public bool IsCompactListView => CurrentViewMode == SnapshotViewMode.CompactList;
    public bool IsTableView => CurrentViewMode == SnapshotViewMode.Table;
    public bool IsGalleryView => CurrentViewMode == SnapshotViewMode.Gallery;
    public bool IsCompactGridView => CurrentViewMode == SnapshotViewMode.CompactGrid;
    public bool IsChronologyView => CurrentViewMode == SnapshotViewMode.Chronology;
    public bool IsTreeView => CurrentViewMode == SnapshotViewMode.Tree;

    partial void OnHasSnapshotsChanged(bool value) => OnPropertyChanged(nameof(ShowFilterEmpty));
    partial void OnHasVisibleSnapshotsChanged(bool value) => OnPropertyChanged(nameof(ShowFilterEmpty));
    partial void OnIsFilterActiveChanged(bool value) => OnPropertyChanged(nameof(ShowFilterEmpty));

    partial void OnSelectedViewLayoutOptionChanged(SnapshotViewLayoutOptionViewModel? value)
    {
        CurrentViewMode = value?.Layout ?? SnapshotViewMode.Cards;
        NotifyViewModePropertiesChanged();
        if (_isApplyingSettings || value is null)
        {
            return;
        }

        _ = PersistViewOptionsAsync();
        RebuildFromActiveFilter();
    }

    partial void OnCurrentViewModeChanged(SnapshotViewMode value)
    {
        NotifyViewModePropertiesChanged();
    }

    private void NotifyViewModePropertiesChanged()
    {
        OnPropertyChanged(nameof(IsCardsView));
        OnPropertyChanged(nameof(IsCompactListView));
        OnPropertyChanged(nameof(IsTableView));
        OnPropertyChanged(nameof(IsGalleryView));
        OnPropertyChanged(nameof(IsCompactGridView));
        OnPropertyChanged(nameof(IsChronologyView));
        OnPropertyChanged(nameof(IsTreeView));
    }

    partial void OnSelectedGroupModeOptionChanged(SnapshotGroupModeOptionViewModel? value)
    {
        ShowGrouped = value?.Mode != SnapshotGroupMode.None;
        if (_isApplyingSettings || value is null)
        {
            return;
        }

        _ = PersistViewOptionsAsync();
        RebuildFromActiveFilter();
    }

    partial void OnSelectedSortModeOptionChanged(SnapshotSortModeOptionViewModel? value)
    {
        if (_isApplyingSettings || value is null)
        {
            return;
        }

        _ = PersistViewOptionsAsync();
        RebuildFromActiveFilter();
    }

    partial void OnSelectedDateRangeOptionChanged(SnapshotDateRangeOptionViewModel? value)
    {
        if (_isApplyingSettings || value is null)
        {
            return;
        }

        _ = PersistViewOptionsAsync();
        RebuildFromActiveFilter();
    }

    partial void OnOnlyWithSnapshotsChanged(bool value)
    {
        if (_isApplyingSettings)
        {
            return;
        }

        _ = PersistViewOptionsAsync();
        RebuildFromActiveFilter();
    }

    public void ApplySettingsFrom(AppSettings settings)
    {
        _isApplyingSettings = true;
        try
        {
            SelectedViewLayoutOption = ResolveViewLayoutOption(settings.SnapshotViewLayout);
            SelectedGroupModeOption = ResolveGroupModeOption(settings.SnapshotGroupMode);
            SelectedSortModeOption = ResolveSortModeOption(settings.SnapshotSortMode);
            SelectedDateRangeOption = ResolveDateRangeOption(settings.SnapshotDateRangeDays);
            OnlyWithSnapshots = settings.SnapshotOnlyWithSnapshots;
            CurrentViewMode = settings.SnapshotViewLayout;
            ShowGrouped = settings.SnapshotGroupMode != SnapshotGroupMode.None;
            NotifyViewModePropertiesChanged();
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    public void SetSnapshots(IEnumerable<SnapshotOverviewItemViewModel> snapshots)
    {
        _allSnapshots.Clear();
        _allSnapshots.AddRange(snapshots);
        ApplyFilter(_activeFilter);
    }

    public void SetGroupingContext(
        IEnumerable<ProgramGroup> groups,
        IEnumerable<ProgramProfileItemViewModel> programs)
    {
        _programGroups.Clear();
        _programGroups.AddRange(groups.OrderBy(group => group.SortOrder));
        _allPrograms.Clear();
        _allPrograms.AddRange(programs);
        _validProgramGroupIds.Clear();

        foreach (var programGroup in programs
                     .Where(program => !string.IsNullOrWhiteSpace(program.Profile.GroupId))
                     .GroupBy(program => program.Profile.GroupId!, StringComparer.Ordinal)
                     .Where(group => group.Count() >= 2))
        {
            _validProgramGroupIds.Add(programGroup.Key);
        }

        ApplyFilter(_activeFilter);
    }

    public void ApplyFilter(string? searchText = null)
    {
        var query = searchText?.Trim() ?? string.Empty;
        _activeFilter = query;
        IsFilterActive = query.Length > 0
                        || !OnlyWithSnapshots
                        || SelectedDateRangeOption?.Days > 0;

        var matches = FilterSnapshots(_allSnapshots, query);
        RebuildDisplayGroups(matches);

        HasSnapshots = _allSnapshots.Count > 0;
        HasVisibleSnapshots = OnlyWithSnapshots
            ? matches.Count > 0
            : DisplayGroups.Count > 0;
        UpdateStats(matches);
        EmptyFilterMessage = IsFilterActive && !HasVisibleSnapshots
            ? BuildEmptyFilterMessage(query)
            : string.Empty;

        OnPropertyChanged(nameof(ShowFilterEmpty));
    }

    private void RebuildFromActiveFilter() => ApplyFilter(_activeFilter);

    private IReadOnlyList<SnapshotOverviewItemViewModel> FilterSnapshots(
        IEnumerable<SnapshotOverviewItemViewModel> source,
        string query)
    {
        var rangeDays = SelectedDateRangeOption?.Days ?? 0;
        var cutoff = rangeDays > 0
            ? DateTimeOffset.Now.AddDays(-rangeDays)
            : (DateTimeOffset?)null;

        return source
            .Where(item =>
            {
                if (cutoff.HasValue && item.Snapshot.CreatedAt < cutoff.Value)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(query))
                {
                    return true;
                }

                return item.ProgramName.Contains(query, StringComparison.OrdinalIgnoreCase)
                       || item.ProgramCategory.Contains(query, StringComparison.OrdinalIgnoreCase)
                       || item.Snapshot.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                       || item.Snapshot.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                       || item.Snapshot.MetadataLabel.Contains(query, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }

    private void RebuildDisplayGroups(IReadOnlyList<SnapshotOverviewItemViewModel> matches)
    {
        var groupMode = SelectedGroupModeOption?.Mode ?? SnapshotGroupMode.ProgramGroup;
        var sortedMatches = SortSnapshots(matches);
        var buckets = new Dictionary<string, SnapshotGroupBucket>(StringComparer.Ordinal);

        foreach (var snapshot in sortedMatches)
        {
            var bucketKey = ResolveBucketKey(snapshot, groupMode);
            if (!buckets.TryGetValue(bucketKey, out var bucket))
            {
                bucket = CreateBucket(snapshot, bucketKey, groupMode);
                buckets[bucketKey] = bucket;
            }

            bucket.Snapshots.Add(snapshot);
        }

        if (!OnlyWithSnapshots && groupMode is SnapshotGroupMode.Program or SnapshotGroupMode.ProgramGroup)
        {
            AddEmptyProgramBuckets(buckets, groupMode);
        }

        DisplayGroups.Clear();
        foreach (var bucket in buckets.Values
                     .OrderBy(entry => entry.SortOrder)
                     .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase))
        {
            var orderedSnapshots = SortSnapshots(bucket.Snapshots);
            var group = new SnapshotDisplayGroupViewModel(
                bucket.Key,
                bucket.Title,
                bucket.Subtitle,
                bucket.IsProgramGroup,
                bucket.SortOrder,
                orderedSnapshots);

            if (IsFilterActive)
            {
                group.IsExpanded = true;
            }

            DisplayGroups.Add(group);
        }

        if (groupMode == SnapshotGroupMode.None && DisplayGroups.Count == 1)
        {
            DisplayGroups[0].IsExpanded = true;
        }
    }

    private void AddEmptyProgramBuckets(
        Dictionary<string, SnapshotGroupBucket> buckets,
        SnapshotGroupMode groupMode)
    {
        foreach (var program in _allPrograms.OrderBy(program => program.SortOrder))
        {
            var bucketKey = groupMode == SnapshotGroupMode.ProgramGroup
                && !string.IsNullOrWhiteSpace(program.Profile.GroupId)
                && _validProgramGroupIds.Contains(program.Profile.GroupId)
                ? $"group:{program.Profile.GroupId}"
                : $"program:{program.Id}";

            if (buckets.ContainsKey(bucketKey))
            {
                continue;
            }

            if (bucketKey.StartsWith("group:", StringComparison.Ordinal))
            {
                var groupId = bucketKey["group:".Length..];
                var programGroup = _programGroups.FirstOrDefault(group => group.Id == groupId);
                buckets[bucketKey] = new SnapshotGroupBucket(
                    bucketKey,
                    programGroup?.Name ?? program.Profile.GroupName ?? program.Name,
                    "Programm-Gruppe",
                    isProgramGroup: true,
                    sortOrder: programGroup?.SortOrder ?? program.SortOrder);
                continue;
            }

            buckets[bucketKey] = new SnapshotGroupBucket(
                bucketKey,
                program.Name,
                program.CategoryLabel,
                isProgramGroup: false,
                sortOrder: program.SortOrder);
        }
    }

    private List<SnapshotOverviewItemViewModel> SortSnapshots(IEnumerable<SnapshotOverviewItemViewModel> snapshots)
    {
        var mode = SelectedSortModeOption?.Mode ?? SnapshotSortMode.NewestFirst;
        return mode switch
        {
            SnapshotSortMode.OldestFirst => snapshots.OrderBy(item => item.Snapshot.CreatedAt).ToList(),
            SnapshotSortMode.NameAsc => snapshots.OrderBy(item => item.Snapshot.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            SnapshotSortMode.SizeDesc => snapshots.OrderByDescending(item => item.Snapshot.SizeBytes).ToList(),
            _ => snapshots.OrderByDescending(item => item.Snapshot.CreatedAt).ToList()
        };
    }

    private string ResolveBucketKey(SnapshotOverviewItemViewModel snapshot, SnapshotGroupMode groupMode)
    {
        return groupMode switch
        {
            SnapshotGroupMode.None => "all",
            SnapshotGroupMode.ByDate => $"date:{snapshot.Snapshot.CreatedAt:yyyy-MM-dd}",
            SnapshotGroupMode.Program => $"program:{snapshot.ProgramId}",
            _ => ResolveProgramGroupBucketKey(snapshot)
        };
    }

    private string ResolveProgramGroupBucketKey(SnapshotOverviewItemViewModel snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.GroupId)
            && _validProgramGroupIds.Contains(snapshot.GroupId))
        {
            return $"group:{snapshot.GroupId}";
        }

        return $"program:{snapshot.ProgramId}";
    }

    private SnapshotGroupBucket CreateBucket(
        SnapshotOverviewItemViewModel snapshot,
        string bucketKey,
        SnapshotGroupMode groupMode)
    {
        if (groupMode == SnapshotGroupMode.None)
        {
            return new SnapshotGroupBucket(
                bucketKey,
                "Alle Snapshots",
                "Flache Ansicht",
                isProgramGroup: false,
                sortOrder: 0);
        }

        if (groupMode == SnapshotGroupMode.ByDate)
        {
            var date = snapshot.Snapshot.CreatedAt;
            return new SnapshotGroupBucket(
                bucketKey,
                date.ToString("dddd, dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")),
                date.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")),
                isProgramGroup: false,
                sortOrder: int.MaxValue - (int)date.ToUnixTimeSeconds());
        }

        if (bucketKey.StartsWith("group:", StringComparison.Ordinal))
        {
            var groupId = bucketKey["group:".Length..];
            var programGroup = _programGroups.FirstOrDefault(group => group.Id == groupId);
            var title = programGroup?.Name
                ?? snapshot.GroupName
                ?? ProgramGroupDetector.ExtractGroupStem(snapshot.ProgramName)
                ?? snapshot.ProgramName;

            return new SnapshotGroupBucket(
                bucketKey,
                title,
                "Programm-Gruppe",
                isProgramGroup: true,
                sortOrder: programGroup?.SortOrder ?? snapshot.ProgramSortOrder);
        }

        return new SnapshotGroupBucket(
            bucketKey,
            snapshot.ProgramName,
            snapshot.ProgramCategory,
            isProgramGroup: false,
            sortOrder: snapshot.ProgramSortOrder);
    }

    private void UpdateStats(IReadOnlyList<SnapshotOverviewItemViewModel> visibleSnapshots)
    {
        var snapshotCount = visibleSnapshots.Count;
        var groupCount = DisplayGroups.Count(group => group.Snapshots.Count > 0);
        var totalBytes = visibleSnapshots.Sum(item => item.Snapshot.SizeBytes);
        var sizeLabel = SnapshotMetadataFormatter.FormatSize(totalBytes);
        StatsLine = snapshotCount == 0
            ? "0 Snapshots"
            : $"{snapshotCount} Snapshots · {groupCount} Gruppen · {sizeLabel}";
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        SelectedCount = _allSnapshots.Count(item => item.IsSelected);
        HasSelection = SelectedCount > 0;
        RestoreSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    private static string BuildEmptyFilterMessage(string query)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            return $"Keine Snapshots für „{query}“ gefunden.";
        }

        return "Keine Snapshots für die gewählten Filter gefunden.";
    }

    private async Task PersistViewOptionsAsync()
    {
        var settings = _settingsService.Current;
        settings.SnapshotViewLayout = SelectedViewLayoutOption?.Layout ?? SnapshotViewMode.Cards;
        settings.SnapshotGroupMode = SelectedGroupModeOption?.Mode ?? SnapshotGroupMode.ProgramGroup;
        settings.SnapshotSortMode = SelectedSortModeOption?.Mode ?? SnapshotSortMode.NewestFirst;
        settings.SnapshotDateRangeDays = SelectedDateRangeOption?.Days ?? 0;
        settings.SnapshotOnlyWithSnapshots = OnlyWithSnapshots;
        await _settingsService.SaveAsync(settings).ConfigureAwait(true);
    }

    private SnapshotViewLayoutOptionViewModel ResolveViewLayoutOption(SnapshotViewMode layout)
        => ViewLayoutOptions.FirstOrDefault(option => option.Layout == layout)
           ?? ViewLayoutOptions.First(option => option.Layout == SnapshotViewMode.Cards);

    private SnapshotGroupModeOptionViewModel ResolveGroupModeOption(SnapshotGroupMode mode)
        => GroupModeOptions.First(option => option.Mode == mode);

    private SnapshotSortModeOptionViewModel ResolveSortModeOption(SnapshotSortMode mode)
        => SortModeOptions.First(option => option.Mode == mode);

    private SnapshotDateRangeOptionViewModel ResolveDateRangeOption(int days)
        => DateRangeOptions.FirstOrDefault(option => option.Days == days)
           ?? DateRangeOptions[0];

    [RelayCommand]
    private void ToggleGroupExpanded(SnapshotDisplayGroupViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        group.IsExpanded = !group.IsExpanded;
    }

    [RelayCommand]
    private void ToggleSnapshotSelection(SnapshotOverviewItemViewModel? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        snapshot.IsSelected = !snapshot.IsSelected;
        UpdateSelectionState();
    }

    [RelayCommand]
    private void SelectAllVisible()
    {
        foreach (var snapshot in DisplayGroups.SelectMany(group => group.Snapshots))
        {
            snapshot.IsSelected = true;
        }

        UpdateSelectionState();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var snapshot in _allSnapshots)
        {
            snapshot.IsSelected = false;
        }

        UpdateSelectionState();
    }

    private sealed class SnapshotGroupBucket
    {
        public SnapshotGroupBucket(
            string key,
            string title,
            string subtitle,
            bool isProgramGroup,
            int sortOrder)
        {
            Key = key;
            Title = title;
            Subtitle = subtitle;
            IsProgramGroup = isProgramGroup;
            SortOrder = sortOrder;
            Snapshots = [];
        }

        public string Key { get; }

        public string Title { get; }

        public string Subtitle { get; }

        public bool IsProgramGroup { get; }

        public int SortOrder { get; }

        public List<SnapshotOverviewItemViewModel> Snapshots { get; }
    }

    [RelayCommand]
    private void SelectSnapshot(SnapshotOverviewItemViewModel? snapshot)
    {
        SnapshotSelected?.Invoke(snapshot);
    }

    [RelayCommand(CanExecute = nameof(CanRestoreGroupSnapshots))]
    private async Task RestoreGroupSnapshotsAsync(SnapshotDisplayGroupViewModel? group)
    {
        if (group is null || !group.IsProgramGroup || group.Snapshots.Count == 0)
        {
            return;
        }

        await _workflowHost.OpenRestoreWizardForGroupAsync(group.Name, group.Snapshots)
            .ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanRestoreSelected))]
    private async Task RestoreSelectedAsync()
    {
        var selected = _allSnapshots.Where(item => item.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        await _workflowHost.OpenRestoreWizardForSelectionAsync(selected).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        var selected = _allSnapshots.Where(item => item.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        await _workflowHost.DeleteSnapshotsBatchAsync(selected).ConfigureAwait(true);
        ClearSelection();
    }

    [RelayCommand(CanExecute = nameof(CanRestoreSnapshot))]
    private async Task RestoreSnapshotAsync(SnapshotOverviewItemViewModel? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        SnapshotSelected?.Invoke(snapshot);
        await _workflowHost.OpenRestoreWizardAsync(snapshot.ProgramId, snapshot.Snapshot.Snapshot)
            .ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanCompareOverviewSnapshot))]
    private async Task CompareSnapshotAsync(SnapshotOverviewItemViewModel? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        SnapshotSelected?.Invoke(snapshot);
        await _workflowHost.CompareSnapshotOverviewAsync(snapshot).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOpenOverviewSnapshot))]
    private async Task OpenSnapshotInExplorerAsync(SnapshotOverviewItemViewModel? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        SnapshotSelected?.Invoke(snapshot);
        await _workflowHost.OpenSnapshotInExplorerAsync(snapshot.ProgramId, snapshot.Snapshot.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOpenOverviewSnapshot))]
    private async Task CopySnapshotPathAsync(SnapshotOverviewItemViewModel? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        SnapshotSelected?.Invoke(snapshot);
        await _workflowHost.CopySnapshotPathAsync(snapshot.ProgramId, snapshot.Snapshot.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOpenOverviewSnapshot))]
    private async Task EditSnapshotAsync(SnapshotOverviewItemViewModel? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        SnapshotSelected?.Invoke(snapshot);
        await _workflowHost.EditSnapshotAsync(snapshot.ProgramId, snapshot.Snapshot.Id).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanDeleteOverviewSnapshot))]
    private async Task DeleteSnapshotAsync(SnapshotOverviewItemViewModel? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        await _workflowHost.DeleteSnapshotAsync(snapshot.ProgramId, snapshot.Snapshot.Id).ConfigureAwait(true);
    }

    public void NotifyWorkflowStateChanged()
    {
        RestoreSnapshotCommand.NotifyCanExecuteChanged();
        RestoreGroupSnapshotsCommand.NotifyCanExecuteChanged();
        RestoreSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        CompareSnapshotCommand.NotifyCanExecuteChanged();
        OpenSnapshotInExplorerCommand.NotifyCanExecuteChanged();
        CopySnapshotPathCommand.NotifyCanExecuteChanged();
        EditSnapshotCommand.NotifyCanExecuteChanged();
        DeleteSnapshotCommand.NotifyCanExecuteChanged();
    }

    private bool CanRestoreSnapshot() => !_workflowHost.IsBusy;

    private bool CanRestoreGroupSnapshots(SnapshotDisplayGroupViewModel? group)
        => !_workflowHost.IsBusy
           && group is not null
           && group.IsProgramGroup
           && group.Snapshots.Count > 0;

    private bool CanRestoreSelected()
        => !_workflowHost.IsBusy && _allSnapshots.Any(item => item.IsSelected);

    private bool CanDeleteSelected()
        => !_workflowHost.IsBusy && _allSnapshots.Any(item => item.IsSelected);

    private bool CanCompareOverviewSnapshot(SnapshotOverviewItemViewModel? snapshot)
        => !_workflowHost.IsBusy
           && snapshot is not null
           && _allSnapshots.Count(item => item.ProgramId == snapshot.ProgramId) >= 2;

    private bool CanOpenOverviewSnapshot(SnapshotOverviewItemViewModel? snapshot)
        => !_workflowHost.IsBusy && snapshot is not null;

    private bool CanDeleteOverviewSnapshot(SnapshotOverviewItemViewModel? snapshot)
        => CanOpenOverviewSnapshot(snapshot);
}

public partial class StatusBarRegionViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _leftLabel = "Bereit";

    [ObservableProperty]
    private string _shellLabel = "PowerShell";

    [ObservableProperty]
    private string _osLabel = "Windows";

    public void ApplySelection(int programCount, string? selectedProgramName)
    {
        LeftLabel = selectedProgramName is null
            ? $"{programCount} Profile geladen"
            : $"{programCount} Profile geladen · {selectedProgramName} ausgewählt";
    }

    public void ApplyEnvironment(string shellLabel, string osLabel)
    {
        ShellLabel = shellLabel;
        OsLabel = osLabel;
    }

    public void ApplyActionResult(string message, bool isError = false)
    {
        LeftLabel = message;
    }
}

public partial class SettingsRegionViewModel : ViewModelBase
{
    private readonly IStoragePathResolver _paths;
    private readonly IAppSettingsService _settings;
    private readonly IAppReinstallEnginePathResolver _enginePathResolver;
    private readonly IAppReinstallEngineService _engineService;
    private readonly Func<CursorSnapshotLevel, Task>? _applyCursorSnapshotLevelAsync;
    private readonly ISystemImageService? _systemImageService;
    private bool _isLoading;
    private bool _isSyncingSystemAbbildTarget;

    public Window? HostWindow { get; set; }

    public SettingsRegionViewModel(
        IStoragePathResolver paths,
        IAppSettingsService settings,
        IAppReinstallEnginePathResolver enginePathResolver,
        IAppReinstallEngineService engineService,
        Func<CursorSnapshotLevel, Task>? applyCursorSnapshotLevelAsync = null,
        ISystemImageService? systemImageService = null)
    {
        _paths = paths;
        _settings = settings;
        _enginePathResolver = enginePathResolver;
        _engineService = engineService;
        _applyCursorSnapshotLevelAsync = applyCursorSnapshotLevelAsync;
        _systemImageService = systemImageService;
        DataRoot = _paths.DataRoot;
        SnapshotsRoot = _paths.SnapshotsRoot;
        ProfilesFilePath = _paths.ProfilesFilePath;
        SettingsFilePath = _paths.SettingsFilePath;
        CursorSnapshotLevelOptions = CursorSnapshotLevelOptionViewModel.CreateAll();
        SystemAbbildModeOptions = SystemAbbildModeOptionViewModel.CreateAll();
        SystemAbbildTargetDriveOptions = new ObservableCollection<SystemAbbildTargetDriveOptionViewModel>();
        RefreshEngineStatus();
    }

    public ObservableCollection<SystemAbbildTargetDriveOptionViewModel> SystemAbbildTargetDriveOptions { get; }

    public event Action<string, bool>? EngineStatusReported;

    public string DataRoot { get; }
    public string SnapshotsRoot { get; }
    public string ProfilesFilePath { get; }
    public string SettingsFilePath { get; }

    [ObservableProperty]
    private bool _incrementalSnapshotsEnabled = true;

    [ObservableProperty]
    private bool _compressSnapshotsEnabled = true;

    [ObservableProperty]
    private bool _copyAclsEnabled = true;

    [ObservableProperty]
    private string _engineRootPath = string.Empty;

    [ObservableProperty]
    private string _engineStatusMessage = "Engine wird geprüft…";

    [ObservableProperty]
    private string _engineLog = string.Empty;

    [ObservableProperty]
    private bool _isEngineRunning;

    public IReadOnlyList<CursorSnapshotLevelOptionViewModel> CursorSnapshotLevelOptions { get; }

    [ObservableProperty]
    private CursorSnapshotLevelOptionViewModel? _selectedCursorSnapshotLevelOption;

    [ObservableProperty]
    private string _cursorSnapshotLevelDescription = string.Empty;

    [ObservableProperty]
    private string _cursorSnapshotSecretsHint = string.Empty;

    [ObservableProperty]
    private bool _showCursorSecretsHint;

    public IReadOnlyList<SystemAbbildModeOptionViewModel> SystemAbbildModeOptions { get; }

    [ObservableProperty]
    private SystemAbbildModeOptionViewModel? _selectedSystemAbbildModeOption;

    [ObservableProperty]
    private string _systemAbbildModeDescription = string.Empty;

    [ObservableProperty]
    private string _systemAbbildAdminHint = string.Empty;

    [ObservableProperty]
    private bool _showSystemAbbildAdminHint;

    [ObservableProperty]
    private bool _isSystemAbbildTargetRequired;

    [ObservableProperty]
    private bool _showSystemAbbildDrivePicker;

    [ObservableProperty]
    private bool _showSystemAbbildWindowsBackupHint;

    [ObservableProperty]
    private string _systemAbbildWindowsBackupHint = string.Empty;

    [ObservableProperty]
    private bool _showCustomSystemAbbildTargetPath;

    [ObservableProperty]
    private SystemAbbildTargetDriveOptionViewModel? _selectedSystemAbbildTargetDriveOption;

    [ObservableProperty]
    private string _systemAbbildTarget = string.Empty;

    [ObservableProperty]
    private string _systemAbbildRestoreHint = string.Empty;

    [ObservableProperty]
    private string _systemAbbildBundleStatus = string.Empty;

    [ObservableProperty]
    private bool _isSystemAbbildRunning;

    public bool CanRunEngineActions => !IsEngineRunning && !IsSystemAbbildRunning;

    public string InfoText =>
        _paths.IsPortable
            ? $"Portable-Modus: Snapshots unter {Path.Combine(_paths.DataRoot, "snapshots")}. " +
              "Profile in profiles.json, Einstellungen in settings.json — alles im data\\-Ordner neben der EXE."
            : $"Snapshots werden lokal unter {_paths.SnapshotsRoot} gespeichert. " +
              "Profile in profiles.json, Einstellungen in settings.json.";

    public string SnapshotSettingsHint =>
        "Inkrementell: unveränderte Dateien werden per Hash referenziert (Vorgänger-Snapshot). " +
        "Ohne Vorgänger oder bei deaktivierter Option wird ein vollständiger Snapshot erstellt. " +
        "ACLs: NTFS-Berechtigungen als SDDL-Sidecar (Standard: an).";

    public string CursorSnapshotSettingsHint =>
        "Gilt für das Cursor-Profil. Der nächste Snapshot nutzt die gewählten Pfade — bestehende Snapshots bleiben unverändert. " +
        "IDE-Binaries (Program Files, Local\\Programs\\cursor) werden nie gesichert.";

    public string SystemAbbildSettingsHint =>
        "System-Abbild-Modi: (1) Windows-Systemabbild via wbadmin auf einem gewählten Laufwerk, " +
        "(2) alle Programme als Snapshot-Bundle (Standard), (3) alle NTFS-Festplattenvolumes via wbadmin. " +
        "Modi 1 und 3 benötigen ein Ziel-Laufwerk und Administratorrechte.";

    public string EngineSettingsHint =>
        "Die App-Reinstall-Engine (PowerShell) erfasst installierte Programme und erzeugt installed-programs.csv. " +
        "Standard-Pfad: repos/app-reinstall-workflow neben dem HorosSaver-Repo.";

    public async Task LoadSettingsAsync()
    {
        _isLoading = true;
        try
        {
            var settings = await _settings.LoadAsync().ConfigureAwait(true);
            IncrementalSnapshotsEnabled = settings.IncrementalSnapshotsEnabled;
            CompressSnapshotsEnabled = settings.CompressSnapshotsEnabled;
            CopyAclsEnabled = settings.CopyAclsEnabled;
            EngineRootPath = settings.EngineRootPath ?? _enginePathResolver.EngineRoot ?? string.Empty;
            SelectedCursorSnapshotLevelOption = ResolveCursorSnapshotLevelOption(
                CursorSnapshotPaths.NormalizeLevel(settings.CursorSnapshotLevel));
            UpdateCursorSnapshotLevelUi(SelectedCursorSnapshotLevelOption.Level);
            SelectedSystemAbbildModeOption = ResolveSystemAbbildModeOption(
                SystemAbbildPaths.NormalizeMode(settings.SystemAbbildMode));
            SystemAbbildTarget = settings.SystemAbbildTarget ?? string.Empty;
            RefreshSystemAbbildTargetDrives();
            UpdateSystemAbbildUi(SelectedSystemAbbildModeOption.Mode);
            SyncSystemAbbildTargetDriveSelection();
            await RefreshSystemAbbildBundleStatusAsync().ConfigureAwait(true);
            RefreshEngineStatus();
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task ApplyEnginePathAsync()
    {
        var normalized = string.IsNullOrWhiteSpace(EngineRootPath) ? null : EngineRootPath.Trim();
        if (normalized is not null)
        {
            var scriptPath = Path.Combine(normalized, "scripts", "AppReinstall.ps1");
            if (!File.Exists(scriptPath))
            {
                EngineStatusMessage = $"Ungültiger Pfad — AppReinstall.ps1 fehlt unter {scriptPath}";
                EngineStatusReported?.Invoke(EngineStatusMessage, true);
                return;
            }
        }

        await PersistSettingsAsync().ConfigureAwait(true);
        _enginePathResolver.Reload(normalized, useSavedSettings: false);
        EngineRootPath = _enginePathResolver.EngineRoot ?? string.Empty;
        RefreshEngineStatus();
        EngineStatusMessage = "Engine-Pfad gespeichert.";
        EngineStatusReported?.Invoke(EngineStatusMessage, false);
    }

    [RelayCommand(CanExecute = nameof(CanRunEngineActions))]
    private Task RunEngineDoctorAsync() => RunEngineActionAsync(AppReinstallEngineAction.Doctor);

    [RelayCommand(CanExecute = nameof(CanRunEngineActions))]
    private Task RunEngineCaptureAsync() => RunEngineActionAsync(AppReinstallEngineAction.Capture);

    [RelayCommand(CanExecute = nameof(CanRunEngineActions))]
    private Task RunEngineValidateAsync() => RunEngineActionAsync(AppReinstallEngineAction.Validate);

    [RelayCommand(CanExecute = nameof(CanRunEngineActions))]
    private Task RunEngineInitializeAsync() => RunEngineActionAsync(AppReinstallEngineAction.Initialize);

    [RelayCommand(CanExecute = nameof(CanRunEngineActions))]
    private Task RunEngineStatusAsync() => RunEngineActionAsync(AppReinstallEngineAction.Status);

    partial void OnIsEngineRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunEngineActions));
        RunEngineDoctorCommand.NotifyCanExecuteChanged();
        RunEngineCaptureCommand.NotifyCanExecuteChanged();
        RunEngineValidateCommand.NotifyCanExecuteChanged();
        RunEngineInitializeCommand.NotifyCanExecuteChanged();
        RunEngineStatusCommand.NotifyCanExecuteChanged();
        RestoreSystemAbbildBundleCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSystemAbbildRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunEngineActions));
        RunEngineDoctorCommand.NotifyCanExecuteChanged();
        RunEngineCaptureCommand.NotifyCanExecuteChanged();
        RunEngineValidateCommand.NotifyCanExecuteChanged();
        RunEngineInitializeCommand.NotifyCanExecuteChanged();
        RunEngineStatusCommand.NotifyCanExecuteChanged();
        RestoreSystemAbbildBundleCommand.NotifyCanExecuteChanged();
    }

    private async Task RunEngineActionAsync(AppReinstallEngineAction action)
    {
        IsEngineRunning = true;
        EngineLog = string.Empty;

        try
        {
            var progress = new Progress<string>(line =>
            {
                EngineLog = string.IsNullOrWhiteSpace(EngineLog) ? line : $"{EngineLog}{Environment.NewLine}{line}";
            });

            var result = await _engineService.RunActionAsync(action, progress).ConfigureAwait(true);
            EngineStatusMessage = result.Message;

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                EngineLog = string.IsNullOrWhiteSpace(EngineLog)
                    ? result.StandardOutput
                    : $"{EngineLog}{Environment.NewLine}{result.StandardOutput}";
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                EngineLog = string.IsNullOrWhiteSpace(EngineLog)
                    ? result.StandardError
                    : $"{EngineLog}{Environment.NewLine}{result.StandardError}";
            }

            EngineStatusReported?.Invoke(result.Message, !result.Success);
        }
        catch (Exception ex)
        {
            EngineStatusMessage = $"Engine-Fehler: {ex.Message}";
            EngineStatusReported?.Invoke(EngineStatusMessage, true);
        }
        finally
        {
            IsEngineRunning = false;
            RefreshEngineStatus();
        }
    }

    private void RefreshEngineStatus()
    {
        var availability = _engineService.DescribeAvailability();
        if (string.IsNullOrWhiteSpace(EngineStatusMessage) || EngineStatusMessage == "Engine wird geprüft…")
        {
            EngineStatusMessage = availability.Message;
        }
    }

    partial void OnIncrementalSnapshotsEnabledChanged(bool value)
    {
        if (!_isLoading)
        {
            _ = PersistSettingsAsync();
        }
    }

    partial void OnCompressSnapshotsEnabledChanged(bool value)
    {
        if (!_isLoading)
        {
            _ = PersistSettingsAsync();
        }
    }

    partial void OnCopyAclsEnabledChanged(bool value)
    {
        if (!_isLoading)
        {
            _ = PersistSettingsAsync();
        }
    }

    partial void OnSelectedCursorSnapshotLevelOptionChanged(CursorSnapshotLevelOptionViewModel? value)
    {
        if (_isLoading || value is null)
        {
            return;
        }

        UpdateCursorSnapshotLevelUi(value.Level);
        _ = ApplyCursorSnapshotLevelAsync(value.Level);
    }

    partial void OnSelectedSystemAbbildModeOptionChanged(SystemAbbildModeOptionViewModel? value)
    {
        if (_isLoading || value is null)
        {
            return;
        }

        UpdateSystemAbbildUi(value.Mode);
        _ = PersistSettingsAsync();
    }

    partial void OnSystemAbbildTargetChanged(string value)
    {
        if (!_isLoading && !_isSyncingSystemAbbildTarget)
        {
            SyncSystemAbbildTargetDriveSelection();
            UpdateSystemAbbildWindowsBackupHint();
            _ = PersistSettingsAsync();
        }
    }

    partial void OnSelectedSystemAbbildTargetDriveOptionChanged(SystemAbbildTargetDriveOptionViewModel? value)
    {
        if (_isLoading || _isSyncingSystemAbbildTarget || value is null)
        {
            return;
        }

        _isSyncingSystemAbbildTarget = true;
        try
        {
            SystemAbbildTarget = value.TargetPath;
            ShowCustomSystemAbbildTargetPath = false;
            UpdateSystemAbbildWindowsBackupHint();
        }
        finally
        {
            _isSyncingSystemAbbildTarget = false;
        }

        _ = PersistSettingsAsync();
    }

    private void UpdateCursorSnapshotLevelUi(CursorSnapshotLevel level)
    {
        CursorSnapshotLevelDescription = CursorSnapshotPaths.GetLevelDescription(level);
        CursorSnapshotSecretsHint = CursorSnapshotPaths.GetSecretsHint(level);
        ShowCursorSecretsHint = !string.IsNullOrWhiteSpace(CursorSnapshotSecretsHint);
    }

    private CursorSnapshotLevelOptionViewModel ResolveCursorSnapshotLevelOption(CursorSnapshotLevel level)
        => CursorSnapshotLevelOptions.First(option => option.Level == level);

    private SystemAbbildModeOptionViewModel ResolveSystemAbbildModeOption(SystemAbbildMode mode)
        => SystemAbbildModeOptions.First(option => option.Mode == mode);

    private void UpdateSystemAbbildUi(SystemAbbildMode mode)
    {
        var normalized = SystemAbbildPaths.NormalizeMode(mode);
        SystemAbbildModeDescription = SystemAbbildPaths.GetLevelDescription(normalized);
        SystemAbbildAdminHint = SystemAbbildPaths.GetAdminHint(normalized);
        ShowSystemAbbildAdminHint = !string.IsNullOrWhiteSpace(SystemAbbildAdminHint);
        IsSystemAbbildTargetRequired = SystemAbbildPaths.RequiresTargetPath(normalized);
        ShowSystemAbbildDrivePicker = IsSystemAbbildTargetRequired;
        ShowSystemAbbildWindowsBackupHint = normalized == SystemAbbildMode.WindowsSystemImage;
        SystemAbbildRestoreHint = SystemAbbildPaths.GetRestoreHint(normalized);
        UpdateSystemAbbildWindowsBackupHint();

        if (ShowSystemAbbildDrivePicker)
        {
            RefreshSystemAbbildTargetDrives();
            SyncSystemAbbildTargetDriveSelection();
        }
    }

    private void RefreshSystemAbbildTargetDrives()
    {
        SystemAbbildTargetDriveOptions.Clear();
        foreach (var drive in SystemAbbildPaths.EnumerateTargetDrives())
        {
            SystemAbbildTargetDriveOptions.Add(
                new SystemAbbildTargetDriveOptionViewModel(drive.TargetPath, drive.Label, drive.IsNtfs));
        }
    }

    private void SyncSystemAbbildTargetDriveSelection()
    {
        if (!ShowSystemAbbildDrivePicker)
        {
            return;
        }

        _isSyncingSystemAbbildTarget = true;
        try
        {
            var match = SystemAbbildTargetDriveOptions.FirstOrDefault(drive =>
                SystemAbbildPaths.IsDriveRootTarget(SystemAbbildTarget, drive.TargetPath));
            SelectedSystemAbbildTargetDriveOption = match;
            ShowCustomSystemAbbildTargetPath = match is null && !string.IsNullOrWhiteSpace(SystemAbbildTarget);
        }
        finally
        {
            _isSyncingSystemAbbildTarget = false;
        }
    }

    private void UpdateSystemAbbildWindowsBackupHint()
        => SystemAbbildWindowsBackupHint = SystemAbbildPaths.GetWindowsBackupTargetHint(SystemAbbildTarget);

    [RelayCommand]
    private async Task BrowseSystemAbbildTargetAsync()
    {
        if (HostWindow is null)
        {
            return;
        }

        var folders = await HostWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Ziel für System-Abbild wählen",
            AllowMultiple = false
        }).ConfigureAwait(true);

        var folder = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            SystemAbbildTarget = folder;
            ShowCustomSystemAbbildTargetPath = true;
            SyncSystemAbbildTargetDriveSelection();
            UpdateSystemAbbildWindowsBackupHint();
        }
    }

    [RelayCommand]
    private void UseSystemAbbildDriveTarget()
    {
        ShowCustomSystemAbbildTargetPath = false;
        if (SelectedSystemAbbildTargetDriveOption is not null)
        {
            SystemAbbildTarget = SelectedSystemAbbildTargetDriveOption.TargetPath;
        }
        else if (SystemAbbildTargetDriveOptions.Count > 0)
        {
            SelectedSystemAbbildTargetDriveOption = SystemAbbildTargetDriveOptions[0];
            SystemAbbildTarget = SystemAbbildTargetDriveOptions[0].TargetPath;
        }

        UpdateSystemAbbildWindowsBackupHint();
    }

    [RelayCommand(CanExecute = nameof(CanRestoreSystemAbbildBundle))]
    private async Task RestoreSystemAbbildBundleAsync()
    {
        if (_systemImageService is null)
        {
            SystemAbbildBundleStatus = "System-Abbild-Service nicht verfügbar.";
            return;
        }

        var bundles = await _systemImageService.ListBundleIdsAsync().ConfigureAwait(true);
        if (bundles.Count == 0)
        {
            SystemAbbildBundleStatus = "Kein Programm-Bundle zum Wiederherstellen gefunden.";
            EngineStatusReported?.Invoke(SystemAbbildBundleStatus, true);
            return;
        }

        IsSystemAbbildRunning = true;
        try
        {
            var latestBundleId = bundles[0];
            var result = await _systemImageService.RestoreBundleAsync(latestBundleId).ConfigureAwait(true);
            SystemAbbildBundleStatus = result.Message;
            EngineStatusReported?.Invoke(result.Message, !result.Success);
        }
        catch (Exception ex)
        {
            SystemAbbildBundleStatus = $"Bundle-Wiederherstellung fehlgeschlagen: {ex.Message}";
            EngineStatusReported?.Invoke(SystemAbbildBundleStatus, true);
        }
        finally
        {
            IsSystemAbbildRunning = false;
        }
    }

    private bool CanRestoreSystemAbbildBundle()
        => !IsSystemAbbildRunning && !IsEngineRunning && _systemImageService is not null;

    private async Task RefreshSystemAbbildBundleStatusAsync()
    {
        if (_systemImageService is null)
        {
            SystemAbbildBundleStatus = string.Empty;
            return;
        }

        var bundles = await _systemImageService.ListBundleIdsAsync().ConfigureAwait(true);
        SystemAbbildBundleStatus = bundles.Count == 0
            ? "Keine Programm-Bundles vorhanden."
            : $"Letztes Bundle: {bundles[0]} ({bundles.Count} gesamt)";
    }

    private async Task ApplyCursorSnapshotLevelAsync(CursorSnapshotLevel level)
    {
        await PersistSettingsAsync(level).ConfigureAwait(true);

        if (_applyCursorSnapshotLevelAsync is not null)
        {
            await _applyCursorSnapshotLevelAsync(level).ConfigureAwait(true);
        }
    }

    private async Task PersistSettingsAsync(CursorSnapshotLevel? cursorSnapshotLevel = null)
    {
        var normalizedLevel = CursorSnapshotPaths.NormalizeLevel(
            cursorSnapshotLevel ?? SelectedCursorSnapshotLevelOption?.Level ?? CursorSnapshotLevel.Standard);
        var normalizedSystemAbbildMode = SystemAbbildPaths.NormalizeMode(
            SelectedSystemAbbildModeOption?.Mode ?? SystemAbbildMode.AllProgramsBundle);
        var normalizedEngineRoot = string.IsNullOrWhiteSpace(EngineRootPath) ? null : EngineRootPath.Trim();
        var normalizedSystemAbbildTarget = string.IsNullOrWhiteSpace(SystemAbbildTarget)
            ? null
            : SystemAbbildTarget.Trim();
        await _settings.SaveAsync(new AppSettings
        {
            IncrementalSnapshotsEnabled = IncrementalSnapshotsEnabled,
            CompressSnapshotsEnabled = CompressSnapshotsEnabled,
            CopyAclsEnabled = CopyAclsEnabled,
            EngineRootPath = normalizedEngineRoot,
            CursorSnapshotLevel = normalizedLevel,
            SystemAbbildMode = normalizedSystemAbbildMode,
            SystemAbbildTarget = normalizedSystemAbbildTarget
        }).ConfigureAwait(true);

        _enginePathResolver.Reload(normalizedEngineRoot, useSavedSettings: false);
    }

    private async Task PersistSettingsAsync()
        => await PersistSettingsAsync(cursorSnapshotLevel: null).ConfigureAwait(true);
}

public interface INavigationHost
{
    void NavigateTo(string viewId);
}

public interface IWorkflowHost
{
    bool IsBusy { get; }
    bool HasSelectedProgram { get; }
    Task SaveSnapshotAsync();
    Task SaveSnapshotForProgramAsync(string? programId);
    Task OpenRestoreWizardAsync(string? programId = null, SnapshotInfo? snapshot = null);
    Task OpenRestoreWizardForGroupAsync(string groupTitle, IReadOnlyList<SnapshotOverviewItemViewModel> snapshots);
    Task OpenRestoreWizardForSelectionAsync(IReadOnlyList<SnapshotOverviewItemViewModel> snapshots);
    Task DeleteSnapshotsBatchAsync(IReadOnlyList<SnapshotOverviewItemViewModel> snapshots);
    Task OpenBindProgramWizardAsync();
    Task OpenCustomPathsWizardAsync();
    Task SaveGroupSnapshotAsync(string? groupId);
    Task AutoCreateProgramGroupsAsync();
    Task OpenEditProfilePathsAsync(ProgramProfile? profile = null);
    Task CompareSnapshotsAsync(SnapshotItemViewModel? selectedSnapshot, SnapshotItemViewModel? compareWithSnapshot);
    Task CompareSnapshotOverviewAsync(SnapshotOverviewItemViewModel snapshot);
    Task DeleteProgramProfileAsync(string? programId);
    Task DissolveProgramGroupAsync(string? groupId);
    Task DeleteProgramGroupWithProfilesAsync(string? groupId);
    Task DeleteSnapshotAsync(string programId, string snapshotId);
    Task OpenInstallFolderAsync(string? programId);
    Task OpenSnapshotInExplorerAsync(string programId, string snapshotId);
    Task CopySnapshotPathAsync(string programId, string snapshotId);
    Task EditSnapshotAsync(string programId, string snapshotId);
    Task CreateSystemAbbildAsync();
    Task<EngineExecutionResult> RunEngineActionAsync(
        AppReinstallEngineAction action,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default);
    void NavigateToSettings();
}
