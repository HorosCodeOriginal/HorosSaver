using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HorosSaver.Models;
using HorosSaver.Services;

namespace HorosSaver.ViewModels;

public partial class BindProgramWizardViewModel : ViewModelBase
{
    private readonly IInstalledProgramDiscoveryService _discoveryService;
    private readonly IReadOnlyList<ProgramProfile> _existingProfiles;
    private readonly CursorSnapshotLevel _cursorSnapshotLevel;
    private List<DiscoveredProgram> _allPrograms = [];
    private readonly HashSet<string> _selectedProgramKeys = [];

    public BindProgramWizardViewModel(
        IInstalledProgramDiscoveryService discoveryService,
        IReadOnlyList<ProgramProfile> existingProfiles,
        CursorSnapshotLevel cursorSnapshotLevel = CursorSnapshotLevel.Standard)
    {
        _discoveryService = discoveryService;
        _existingProfiles = existingProfiles;
        _cursorSnapshotLevel = CursorSnapshotPaths.NormalizeLevel(cursorSnapshotLevel);
    }

    public event Action<ProgramProfile>? ProfileBound;
    public event Action? CloseRequested;

    public ObservableCollection<DiscoveredProgramItemViewModel> Programs { get; } = [];
    public ObservableCollection<BindPathEntryViewModel> PathEntries { get; } = [];

    [ObservableProperty]
    private BindProgramWizardStep _currentStep = BindProgramWizardStep.Discover;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DiscoveredProgramItemViewModel? _selectedProgram;

    [ObservableProperty]
    private string _statusMessage = "Installierte Programme werden geladen…";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _pathHint = string.Empty;

    [ObservableProperty]
    private string _selectedProgramSummary = string.Empty;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private string _selectionSummary = "Keine Auswahl";

    public bool IsDiscoverStep => CurrentStep == BindProgramWizardStep.Discover;
    public bool IsConfigureStep => CurrentStep == BindProgramWizardStep.Configure;
    public string StepTitle => CurrentStep switch
    {
        BindProgramWizardStep.Discover => "Programm auswählen",
        BindProgramWizardStep.Configure => "Speicherpfade konfigurieren",
        _ => "Programm einbinden"
    };

    partial void OnCurrentStepChanged(BindProgramWizardStep value)
    {
        OnPropertyChanged(nameof(IsDiscoverStep));
        OnPropertyChanged(nameof(IsConfigureStep));
        OnPropertyChanged(nameof(StepTitle));
        NextStepCommand.NotifyCanExecuteChanged();
        BackStepCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedProgramChanged(DiscoveredProgramItemViewModel? value)
    {
        NextStepCommand.NotifyCanExecuteChanged();
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            _allPrograms = (await _discoveryService.DiscoverInstalledProgramsAsync().ConfigureAwait(true)).ToList();
            ApplyFilter();
            var registryCount = _allPrograms.Count(program => program.Sources.HasFlag(ProgramDiscoverySource.Registry));
            var startMenuCount = _allPrograms.Count(program => program.Sources.HasFlag(ProgramDiscoverySource.StartMenu));
            StatusMessage = _allPrograms.Count == 0
                ? "Keine installierten Programme gefunden (nur Windows)."
                : $"{_allPrograms.Count} Programme gefunden (Registry: {registryCount}, Startmenü: {startMenuCount}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erkennung fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        foreach (var program in Programs)
        {
            if (program.IsSelected)
            {
                _selectedProgramKeys.Add(GetProgramKey(program.Program));
            }

            program.SelectionChanged -= OnProgramSelectionChanged;
        }

        Programs.Clear();
        var query = SearchText.Trim();

        var matches = string.IsNullOrEmpty(query)
            ? _allPrograms
            : _allPrograms.Where(program =>
                program.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (program.Publisher?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (program.InstallLocation?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (program.TargetPath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (program.DisplayVersion?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || program.SourceLabel.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var program in matches)
        {
            var alreadyBound = ProfileBindingFactory.IsAlreadyBound(program, _existingProfiles);
            var item = new DiscoveredProgramItemViewModel(program, alreadyBound);
            var key = GetProgramKey(program);
            item.IsSelected = _selectedProgramKeys.Contains(key);
            item.SelectionChanged += OnProgramSelectionChanged;
            Programs.Add(item);
        }

        UpdateSelectionState();
    }

    [RelayCommand(CanExecute = nameof(CanSelectAllFiltered))]
    private void SelectAllFiltered()
    {
        foreach (var program in Programs.Where(program => program.IsAvailable))
        {
            program.IsSelected = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearSelection))]
    private void ClearSelection()
    {
        foreach (var program in Programs)
        {
            program.IsSelected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextStep()
    {
        var selected = GetSelectedAvailablePrograms();
        if (selected.Count == 0)
        {
            return;
        }

        if (selected.Count == 1)
        {
            SelectedProgram = selected[0];
            LoadPathEntries(selected[0].Program);
            CurrentStep = BindProgramWizardStep.Configure;
            StatusMessage = "Pfade prüfen und bei Bedarf ergänzen.";
            return;
        }

        BatchBindPrograms(selected);
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void BackStep()
    {
        CurrentStep = BindProgramWizardStep.Discover;
        UpdateSelectionState();
        StatusMessage = $"{Programs.Count} Treffer angezeigt — {SelectionSummary}.";
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private void Finish()
    {
        if (SelectedProgram is null)
        {
            return;
        }

        var validPaths = PathEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.SourcePath))
            .Select(entry => entry.ToProfilePathEntry())
            .ToList();

        if (validPaths.Count == 0)
        {
            StatusMessage = "Mindestens ein Speicherpfad ist erforderlich.";
            return;
        }

        var profile = ProfileBindingFactory.CreateBoundProfile(
            SelectedProgram.Program,
            validPaths,
            _existingProfiles.Count,
            _existingProfiles.Select(item => item.Id).ToHashSet());

        if (CursorSnapshotPaths.IsCursorProgramName(SelectedProgram.Program.DisplayName))
        {
            profile.CursorSnapshotLevel = _cursorSnapshotLevel;
            CursorSnapshotPaths.ApplyLevelToProfile(profile, _cursorSnapshotLevel);
        }

        ProfileBound?.Invoke(profile);
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

    private void LoadPathEntries(DiscoveredProgram program)
    {
        PathEntries.Clear();
        SelectedProgramSummary = $"{program.DisplayName} · {program.Publisher ?? "Unbekannt"} · {program.DisplayVersion ?? "—"}";
        PathHint = KnownAppPathDefaults.GetPathHint(program.DisplayName);

        foreach (var path in ResolveDefaultPaths(program))
        {
            PathEntries.Add(BindPathEntryViewModel.FromProfilePath(path));
        }
    }

    private void BatchBindPrograms(IReadOnlyList<DiscoveredProgramItemViewModel> selected)
    {
        var existingIds = _existingProfiles.Select(profile => profile.Id).ToHashSet(StringComparer.Ordinal);
        var sortOrder = _existingProfiles.Count;
        var boundCount = 0;
        var skippedCount = 0;

        foreach (var item in selected)
        {
            var paths = ResolveDefaultPaths(item.Program);
            if (paths.Count == 0)
            {
                skippedCount++;
                continue;
            }

            var profile = ProfileBindingFactory.CreateBoundProfile(
                item.Program,
                paths,
                sortOrder,
                existingIds);

            if (CursorSnapshotPaths.IsCursorProgramName(item.Program.DisplayName))
            {
                profile.CursorSnapshotLevel = _cursorSnapshotLevel;
                CursorSnapshotPaths.ApplyLevelToProfile(profile, _cursorSnapshotLevel);
            }

            existingIds.Add(profile.Id);
            sortOrder++;
            boundCount++;
            ProfileBound?.Invoke(profile);
        }

        if (boundCount == 0)
        {
            StatusMessage = skippedCount > 0
                ? "Keine Standard-Pfade gefunden — bitte Programme einzeln mit Pfad-Konfiguration einbinden."
                : "Keine Programme konnten eingebunden werden.";
            return;
        }

        StatusMessage = skippedCount > 0
            ? $"{boundCount} Programme eingebunden ({skippedCount} ohne Standard-Pfade übersprungen)."
            : $"{boundCount} Programme eingebunden — Pfade später bei Bedarf bearbeiten.";
        CloseRequested?.Invoke();
    }

    private IReadOnlyList<ProfilePathEntry> ResolveDefaultPaths(DiscoveredProgram program)
        => CursorSnapshotPaths.IsCursorProgramName(program.DisplayName)
            ? CursorSnapshotPaths.Resolve(_cursorSnapshotLevel)
            : KnownAppPathDefaults.Resolve(
                program.DisplayName,
                program.InstallLocation ?? Path.GetDirectoryName(program.TargetPath ?? string.Empty));

    private List<DiscoveredProgramItemViewModel> GetSelectedAvailablePrograms()
        => Programs.Where(program => program.IsSelected && program.IsAvailable).ToList();

    private void OnProgramSelectionChanged(DiscoveredProgramItemViewModel item)
    {
        var key = GetProgramKey(item.Program);
        if (item.IsSelected)
        {
            _selectedProgramKeys.Add(key);
        }
        else
        {
            _selectedProgramKeys.Remove(key);
        }

        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        SelectedCount = Programs.Count(program => program.IsSelected && program.IsAvailable);
        SelectionSummary = SelectedCount switch
        {
            0 => "Keine Auswahl",
            1 => "1 ausgewählt",
            _ => $"{SelectedCount} ausgewählt"
        };

        SelectedProgram = Programs.FirstOrDefault(program => program.IsSelected && program.IsAvailable);
        NextStepCommand.NotifyCanExecuteChanged();
        SelectAllFilteredCommand.NotifyCanExecuteChanged();
        ClearSelectionCommand.NotifyCanExecuteChanged();
    }

    private static string GetProgramKey(DiscoveredProgram program)
        => $"{program.DisplayName}|{program.InstallLocation ?? program.TargetPath ?? string.Empty}";

    private bool CanGoNext() => CurrentStep == BindProgramWizardStep.Discover && SelectedCount > 0;
    private bool CanSelectAllFiltered() => Programs.Any(program => program.IsAvailable && !program.IsSelected);
    private bool CanClearSelection() => Programs.Any(program => program.IsSelected);
    private bool CanGoBack() => CurrentStep == BindProgramWizardStep.Configure;
    private bool CanFinish() => CurrentStep == BindProgramWizardStep.Configure && PathEntries.Count > 0;

    public Window? HostWindow { get; set; }
}

public partial class DiscoveredProgramItemViewModel : ObservableObject
{
    public DiscoveredProgramItemViewModel(DiscoveredProgram program, bool isAlreadyBound)
    {
        Program = program;
        DisplayName = program.DisplayName;
        Publisher = program.Publisher ?? "—";
        Version = program.DisplayVersion ?? "—";
        InstallLocation = program.InstallLocation ?? program.TargetPath ?? "—";
        SourceLabel = program.SourceLabel;
        IsAlreadyBound = isAlreadyBound;
        StatusLabel = isAlreadyBound ? "Bereits eingebunden" : SourceLabel;
    }

    public event Action<DiscoveredProgramItemViewModel>? SelectionChanged;

    public DiscoveredProgram Program { get; }

    public string DisplayName { get; }
    public string Publisher { get; }
    public string Version { get; }
    public string InstallLocation { get; }
    public string SourceLabel { get; }
    public bool IsAlreadyBound { get; }
    public string StatusLabel { get; }
    public bool IsAvailable => !IsAlreadyBound;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke(this);
}

public partial class BindPathEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _relativeTarget = string.Empty;

    [ObservableProperty]
    private bool _isDirectory = true;

    public string ExistsLabel => PathExists ? "Vorhanden" : "Nicht gefunden";

    public bool PathExists => IsDirectory
        ? Directory.Exists(SourcePath)
        : File.Exists(SourcePath);

    partial void OnSourcePathChanged(string value)
    {
        OnPropertyChanged(nameof(PathExists));
        OnPropertyChanged(nameof(ExistsLabel));
    }

    partial void OnIsDirectoryChanged(bool value)
    {
        OnPropertyChanged(nameof(PathExists));
        OnPropertyChanged(nameof(ExistsLabel));
    }

    public static BindPathEntryViewModel FromProfilePath(ProfilePathEntry entry)
        => new()
        {
            Label = entry.Label,
            SourcePath = entry.SourcePath,
            RelativeTarget = entry.RelativeTarget,
            IsDirectory = entry.IsDirectory
        };

    public ProfilePathEntry ToProfilePathEntry()
        => new()
        {
            Label = Label.Trim(),
            SourcePath = SourcePath.Trim(),
            RelativeTarget = string.IsNullOrWhiteSpace(RelativeTarget)
                ? Slugify(Label)
                : RelativeTarget.Trim(),
            IsDirectory = IsDirectory
        };

    private static string Slugify(string value)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "path" : slug;
    }
}
