using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HorosSaver.Models;
using HorosSaver.Services;

namespace HorosSaver.ViewModels;

public abstract partial class ProgramListItemViewModel : ObservableObject
{
    public abstract bool IsGroup { get; }
}

public partial class ProgramGroupItemViewModel : ProgramListItemViewModel
{
    public ProgramGroupItemViewModel(ProgramGroup group, IEnumerable<ProgramProfileItemViewModel> members)
    {
        Group = group;
        Members = new ObservableCollection<ProgramProfileItemViewModel>(members.OrderBy(member => member.SortOrder));
        Id = group.Id;
        Name = group.Name;
        MemberCount = Members.Count;
        DisplayTitle = $"{Name} ({MemberCount})";
        IconGlyph = CreateGlyph(group.Name);
    }

    public override bool IsGroup => true;

    public ProgramGroup Group { get; }

    public string Id { get; }

    public string Name { get; }

    public int MemberCount { get; }

    public string DisplayTitle { get; }

    public string IconGlyph { get; }

    public ObservableCollection<ProgramProfileItemViewModel> Members { get; }

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isSelected;

    public string LastSnapshotSummary => BuildLastSnapshotSummary();

    public ProgramSnapshotDisplayStatus SnapshotSummaryStatus => EvaluateSnapshotSummaryStatus();

    public string SnapshotSummaryTooltip => SnapshotSummaryStatus switch
    {
        ProgramSnapshotDisplayStatus.None => "Kein Snapshot vorhanden",
        ProgramSnapshotDisplayStatus.Current => "Snapshot aktuell (alle Mitglieder)",
        ProgramSnapshotDisplayStatus.Outdated => "Mindestens ein veralteter Snapshot",
        ProgramSnapshotDisplayStatus.Partial => "Nicht alle Mitglieder haben einen Snapshot",
        _ => string.Empty
    };

    public void RefreshLastSnapshotSummary()
    {
        OnPropertyChanged(nameof(LastSnapshotSummary));
        OnPropertyChanged(nameof(SnapshotSummaryStatus));
        OnPropertyChanged(nameof(SnapshotSummaryTooltip));
    }

    public bool MatchesSearch(string? searchText)
    {
        var query = searchText?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return true;
        }

        return Contains(Name, query)
            || Contains(DisplayTitle, query)
            || Members.Any(member => member.MatchesSearch(query));
    }

    private string BuildLastSnapshotSummary()
    {
        var withSnapshot = Members.Count(member => member.Profile.LastSnapshotAt.HasValue);
        if (withSnapshot == 0)
        {
            return "Noch kein Snapshot";
        }

        if (withSnapshot == Members.Count)
        {
            var latest = Members
                .Select(member => member.Profile.LastSnapshotAt!.Value)
                .Max();
            return $"Zuletzt: {latest:dd.MM.yyyy HH:mm} (alle)";
        }

        return $"Zuletzt: {withSnapshot}/{Members.Count} mit Snapshot";
    }

    private ProgramSnapshotDisplayStatus EvaluateSnapshotSummaryStatus()
    {
        if (Members.Count == 0)
        {
            return ProgramSnapshotDisplayStatus.None;
        }

        var membersWithSnapshot = Members.Count(member => !member.HasNoSnapshot);
        if (membersWithSnapshot == 0)
        {
            return ProgramSnapshotDisplayStatus.None;
        }

        if (membersWithSnapshot < Members.Count)
        {
            return ProgramSnapshotDisplayStatus.Partial;
        }

        if (Members.Any(member => member.IsSnapshotOutdated))
        {
            return ProgramSnapshotDisplayStatus.Outdated;
        }

        return ProgramSnapshotDisplayStatus.Current;
    }

    private static string CreateGlyph(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "GR";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }

        return name.Length >= 2
            ? $"{char.ToUpperInvariant(name[0])}{char.ToUpperInvariant(name[1])}"
            : char.ToUpperInvariant(name[0]).ToString();
    }

    private static bool Contains(string? value, string query)
        => !string.IsNullOrEmpty(value)
           && value.Contains(query, StringComparison.OrdinalIgnoreCase);
}

public partial class ProgramProfileItemViewModel : ProgramListItemViewModel
{
    public ProgramProfileItemViewModel(ProgramProfile profile)
    {
        Profile = profile;
        Id = profile.Id;
        Name = profile.Name;
        Category = profile.Category;
        Subtitle = profile.Subtitle;
        IconGlyph = profile.IconGlyph;
        IconBackground = profile.IconBackground;
        IsActive = profile.IsActive;
        IsBound = profile.IsBound;
        SortOrder = profile.SortOrder;
        DetailLines = new ObservableCollection<string>(profile.DetailLines);
        DisplayDetails = ProfileDetailDisplayItem.ParseMany(profile.DetailLines);
        CompactDisplayDetails = ProfileDetailDisplayItem.SelectCompact(DisplayDetails);
        _lastSnapshotLabel = "Noch kein Snapshot";
        RefreshLastSnapshotLabel();
        NotifySnapshotDisplayProperties();
    }

    public ProgramProfile Profile { get; }

    public override bool IsGroup => false;

    public string Id { get; }
    public string Name { get; }
    public string Category { get; }
    public string Subtitle { get; }
    public string IconGlyph { get; }
    public string IconBackground { get; }
    public bool IsActive { get; }
    public bool IsBound { get; }
    public int SortOrder { get; }
    public ObservableCollection<string> DetailLines { get; }
    public string CategoryLabel => $"{Category} · {Subtitle}";

    [ObservableProperty]
    private IReadOnlyList<ProfileDetailDisplayItem> _displayDetails = [];

    [ObservableProperty]
    private IReadOnlyList<ProfileDetailDisplayItem> _compactDisplayDetails = [];

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _lastSnapshotLabel;

    private DateTimeOffset? _latestSnapshotAt;

    public bool HasNoSnapshot => !Profile.LastSnapshotAt.HasValue && !_latestSnapshotAt.HasValue;

    public bool IsSnapshotOutdated
    {
        get
        {
            if (Profile.LastSnapshotAt.HasValue && !_latestSnapshotAt.HasValue)
            {
                return true;
            }

            if (Profile.LastSnapshotAt.HasValue
                && _latestSnapshotAt.HasValue
                && Profile.LastSnapshotAt.Value < _latestSnapshotAt.Value.AddSeconds(-1))
            {
                return true;
            }

            return false;
        }
    }

    public bool IsSnapshotUpToDate => !HasNoSnapshot && !IsSnapshotOutdated;

    public ProgramSnapshotDisplayStatus SnapshotDisplayStatus
    {
        get
        {
            if (HasNoSnapshot)
            {
                return ProgramSnapshotDisplayStatus.None;
            }

            if (IsSnapshotOutdated)
            {
                return ProgramSnapshotDisplayStatus.Outdated;
            }

            return ProgramSnapshotDisplayStatus.Current;
        }
    }

    public string SnapshotStatusTooltip => SnapshotDisplayStatus switch
    {
        ProgramSnapshotDisplayStatus.None => "Kein Snapshot vorhanden",
        ProgramSnapshotDisplayStatus.Current => "Snapshot aktuell",
        ProgramSnapshotDisplayStatus.Outdated => "Snapshot veraltet — neuer Snapshot empfohlen",
        _ => string.Empty
    };

    [ObservableProperty]
    private bool _isSnapshotInProgress;

    [ObservableProperty]
    private double _snapshotProgress;

    [ObservableProperty]
    private string _snapshotProgressText = string.Empty;

    [ObservableProperty]
    private bool _hasSnapshotError;

    [ObservableProperty]
    private bool _hasSnapshotWarning;

    [ObservableProperty]
    private string _snapshotErrorMessage = string.Empty;

    [ObservableProperty]
    private string _snapshotErrorDetail = string.Empty;

    [ObservableProperty]
    private string _snapshotWarningMessage = string.Empty;

    [ObservableProperty]
    private string _snapshotWarningDetail = string.Empty;

    [ObservableProperty]
    private bool _isQueued;

    [ObservableProperty]
    private bool _isPaused;

    private ISnapshotJobManager? _snapshotJobManager;

    public bool ShowSnapshotJobOverlay => IsQueued || IsSnapshotInProgress;

    public bool HasActiveSnapshotJob => IsQueued || IsSnapshotInProgress;

    public bool ShowPauseSnapshotButton => IsSnapshotInProgress && !IsPaused;

    public bool ShowResumeSnapshotButton => IsSnapshotInProgress && IsPaused;

    public bool ShowIndeterminateSnapshotProgress => IsSnapshotInProgress && SnapshotProgress <= 0 && !IsPaused;

    public void AttachSnapshotJobManager(ISnapshotJobManager snapshotJobManager)
        => _snapshotJobManager = snapshotJobManager;

    public void BeginQueued()
    {
        IsQueued = true;
        IsSnapshotInProgress = false;
        IsPaused = false;
        SnapshotProgress = 0;
        SnapshotProgressText = "Wartend in Warteschlange…";
        HasSnapshotError = false;
        HasSnapshotWarning = false;
        SnapshotErrorMessage = string.Empty;
        SnapshotErrorDetail = string.Empty;
        SnapshotWarningMessage = string.Empty;
        SnapshotWarningDetail = string.Empty;
        NotifySnapshotJobOverlayChanged();
        NotifySnapshotControlCommands();
    }

    public void BeginSnapshot()
    {
        IsQueued = false;
        IsSnapshotInProgress = true;
        IsPaused = false;
        SnapshotProgress = 0;
        SnapshotProgressText = "Snapshot wird vorbereitet…";
        HasSnapshotError = false;
        HasSnapshotWarning = false;
        SnapshotErrorMessage = string.Empty;
        SnapshotErrorDetail = string.Empty;
        SnapshotWarningMessage = string.Empty;
        SnapshotWarningDetail = string.Empty;
        NotifySnapshotJobOverlayChanged();
        NotifySnapshotControlCommands();
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        if (paused)
        {
            SnapshotProgressText = "Pausiert — Fortsetzen zum Weitermachen";
        }

        OnPropertyChanged(nameof(ShowPauseSnapshotButton));
        OnPropertyChanged(nameof(ShowResumeSnapshotButton));
        OnPropertyChanged(nameof(ShowIndeterminateSnapshotProgress));
        NotifySnapshotControlCommands();
    }

    [RelayCommand(CanExecute = nameof(CanPauseSnapshot))]
    private void PauseSnapshot()
        => _snapshotJobManager?.Pause(Id);

    [RelayCommand(CanExecute = nameof(CanResumeSnapshot))]
    private void ResumeSnapshot()
        => _snapshotJobManager?.Resume(Id);

    [RelayCommand(CanExecute = nameof(CanCancelSnapshot))]
    private void CancelSnapshot()
        => _snapshotJobManager?.Cancel(Id);

    private bool CanPauseSnapshot() => IsSnapshotInProgress && !IsPaused;

    private bool CanResumeSnapshot() => IsSnapshotInProgress && IsPaused;

    private bool CanCancelSnapshot() => HasActiveSnapshotJob;

    private void NotifySnapshotControlCommands()
    {
        PauseSnapshotCommand.NotifyCanExecuteChanged();
        ResumeSnapshotCommand.NotifyCanExecuteChanged();
        CancelSnapshotCommand.NotifyCanExecuteChanged();
    }

    private void NotifySnapshotJobOverlayChanged()
    {
        OnPropertyChanged(nameof(ShowSnapshotJobOverlay));
        OnPropertyChanged(nameof(HasActiveSnapshotJob));
        OnPropertyChanged(nameof(ShowPauseSnapshotButton));
        OnPropertyChanged(nameof(ShowResumeSnapshotButton));
        OnPropertyChanged(nameof(ShowIndeterminateSnapshotProgress));
    }

    partial void OnIsQueuedChanged(bool value)
    {
        NotifySnapshotJobOverlayChanged();
        NotifySnapshotControlCommands();
    }

    partial void OnIsSnapshotInProgressChanged(bool value)
    {
        NotifySnapshotJobOverlayChanged();
        NotifySnapshotControlCommands();
    }

    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowIndeterminateSnapshotProgress));
        NotifySnapshotControlCommands();
    }

    public void ApplySnapshotProgress(SnapshotProgressReport report)
    {
        SnapshotProgress = report.Percent;

        if (!string.IsNullOrWhiteSpace(report.PhaseLabel))
        {
            SnapshotProgressText = report.Total > 0
                ? $"{report.PhaseLabel} ({report.Current}/{report.Total})"
                : report.PhaseLabel;
        }
        else if (report.Total > 0)
        {
            SnapshotProgressText = $"{report.Percent:0}% ({report.Current}/{report.Total})";
            if (!string.IsNullOrWhiteSpace(report.CurrentPath))
            {
                SnapshotProgressText += $" — {Path.GetFileName(report.CurrentPath)}";
            }
        }
        else
        {
            SnapshotProgressText = "Snapshot wird erstellt…";
        }

        OnPropertyChanged(nameof(ShowIndeterminateSnapshotProgress));
    }

    public void EndSnapshotSuccess()
        => EndSnapshotSuccess(null, null);

    public void EndSnapshotSuccess(int? skippedLockedCount, IReadOnlyList<string>? skippedLockedPaths)
    {
        IsQueued = false;
        IsSnapshotInProgress = false;
        IsPaused = false;
        SnapshotProgress = 0;
        SnapshotProgressText = string.Empty;
        HasSnapshotError = false;
        SnapshotErrorMessage = string.Empty;
        SnapshotErrorDetail = string.Empty;

        if (skippedLockedCount is > 0)
        {
            HasSnapshotWarning = true;
            SnapshotWarningMessage = $"{skippedLockedCount} Dateien übersprungen (gesperrt)";
            SnapshotWarningDetail = skippedLockedPaths is { Count: > 0 }
                ? string.Join(Environment.NewLine, skippedLockedPaths)
                : SnapshotWarningMessage;
            AppFileLogger.Warning(
                $"Snapshot mit gesperrten Dateien abgeschlossen für „{Name}“ ({Id}): {skippedLockedCount} übersprungen.");
        }
        else
        {
            HasSnapshotWarning = false;
            SnapshotWarningMessage = string.Empty;
            SnapshotWarningDetail = string.Empty;
        }

        RefreshLastSnapshotLabel();
        RefreshSnapshotStatus(Profile.LastSnapshotAt);
        NotifySnapshotJobOverlayChanged();
        NotifySnapshotControlCommands();
    }

    public void EndSnapshotError(string message)
    {
        IsQueued = false;
        IsSnapshotInProgress = false;
        IsPaused = false;
        SnapshotProgress = 0;
        SnapshotProgressText = string.Empty;
        HasSnapshotError = true;
        HasSnapshotWarning = false;
        SnapshotWarningMessage = string.Empty;
        SnapshotWarningDetail = string.Empty;
        var detail = string.IsNullOrWhiteSpace(message)
            ? "Snapshot fehlgeschlagen."
            : message;
        SnapshotErrorDetail = detail;
        SnapshotErrorMessage = SnapshotErrorFormatter.FormatForUi(detail);
        AppFileLogger.Error($"Snapshot fehlgeschlagen für „{Name}“ ({Id}): {detail}");
        NotifySnapshotJobOverlayChanged();
        NotifySnapshotControlCommands();
    }

    public void EndSnapshotCancelled()
    {
        IsQueued = false;
        IsSnapshotInProgress = false;
        IsPaused = false;
        SnapshotProgress = 0;
        SnapshotProgressText = string.Empty;
        HasSnapshotError = true;
        HasSnapshotWarning = false;
        SnapshotWarningMessage = string.Empty;
        SnapshotWarningDetail = string.Empty;
        SnapshotErrorDetail = "Snapshot abgebrochen.";
        SnapshotErrorMessage = "Snapshot abgebrochen.";
        AppFileLogger.Warning($"Snapshot abgebrochen für „{Name}“ ({Id}).");
        NotifySnapshotJobOverlayChanged();
        NotifySnapshotControlCommands();
    }

    public void RefreshSnapshotStatus(DateTimeOffset? latestSnapshotAt)
    {
        _latestSnapshotAt = latestSnapshotAt;
        RefreshLastSnapshotLabel();
        NotifySnapshotDisplayProperties();
    }

    public void RefreshLastSnapshotLabel()
    {
        if (HasNoSnapshot)
        {
            LastSnapshotLabel = "Noch kein Snapshot";
            return;
        }

        var displayAt = IsSnapshotOutdated && Profile.LastSnapshotAt.HasValue
            ? Profile.LastSnapshotAt.Value
            : _latestSnapshotAt ?? Profile.LastSnapshotAt!.Value;
        var suffix = IsSnapshotOutdated ? " (alt)" : string.Empty;
        LastSnapshotLabel = $"Zuletzt: {displayAt:dd.MM.yyyy HH:mm}{suffix}";
    }

    private void NotifySnapshotDisplayProperties()
    {
        OnPropertyChanged(nameof(HasNoSnapshot));
        OnPropertyChanged(nameof(IsSnapshotUpToDate));
        OnPropertyChanged(nameof(IsSnapshotOutdated));
        OnPropertyChanged(nameof(SnapshotDisplayStatus));
        OnPropertyChanged(nameof(SnapshotStatusTooltip));
    }

    public void RefreshProfileDetails()
    {
        DetailLines.Clear();
        foreach (var line in Profile.DetailLines)
        {
            DetailLines.Add(line);
        }

        DisplayDetails = ProfileDetailDisplayItem.ParseMany(Profile.DetailLines);
        CompactDisplayDetails = ProfileDetailDisplayItem.SelectCompact(DisplayDetails);
    }

    public bool MatchesSearch(string? searchText)
    {
        var query = searchText?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return true;
        }

        return Contains(Name, query)
            || Contains(Category, query)
            || Contains(Subtitle, query)
            || Contains(CategoryLabel, query)
            || Contains(Profile.GroupName, query)
            || (IsBound && Contains("eingebunden", query))
            || DetailLines.Any(line => Contains(line, query));
    }

    public bool HasInstallLocation =>
        !string.IsNullOrWhiteSpace(Profile.InstallLocation) && Directory.Exists(Profile.InstallLocation);

    private static bool Contains(string? value, string query)
        => !string.IsNullOrEmpty(value)
           && value.Contains(query, StringComparison.OrdinalIgnoreCase);
}

public sealed class ProfileDetailDisplayItem
{
    public ProfileDetailDisplayItem(string label, string value, string iconKey)
    {
        Label = label;
        Value = value;
        IconKey = iconKey;
    }

    public string Label { get; }
    public string Value { get; }
    public string IconKey { get; }

    public static IReadOnlyList<ProfileDetailDisplayItem> ParseMany(IEnumerable<string> lines)
    {
        return lines.Select(Parse).ToList();
    }

    public static IReadOnlyList<ProfileDetailDisplayItem> SelectCompact(IEnumerable<ProfileDetailDisplayItem> items)
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            return list;
        }

        string[] priorityLabels = ["Quelle", "Pfade", "Version"];
        var compact = new List<ProfileDetailDisplayItem>();

        foreach (var label in priorityLabels)
        {
            if (compact.Count >= 2)
            {
                break;
            }

            var item = list.FirstOrDefault(entry =>
                string.Equals(entry.Label, label, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                continue;
            }

            compact.Add(new ProfileDetailDisplayItem(
                item.Label,
                TruncateValue(item.Value, label),
                item.IconKey));
        }

        if (compact.Count > 0)
        {
            return compact;
        }

        return list
            .Take(2)
            .Select(item => new ProfileDetailDisplayItem(
                item.Label,
                TruncateValue(item.Value, item.Label),
                item.IconKey))
            .ToList();
    }

    private static string TruncateValue(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var maxLength = string.Equals(label, "Install", StringComparison.OrdinalIgnoreCase) ? 36 : 42;
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 1)] + "…";
    }

    private static ProfileDetailDisplayItem Parse(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex > 0)
        {
            var label = line[..colonIndex].Trim();
            var value = line[(colonIndex + 1)..].Trim();
            return new ProfileDetailDisplayItem(label, value, ResolveIconKey(label));
        }

        var openIndex = line.IndexOf('(');
        if (openIndex > 0 && line.EndsWith(')'))
        {
            var label = line[..openIndex].Trim();
            var value = line.Substring(openIndex + 1, line.Length - openIndex - 2).Trim();
            return new ProfileDetailDisplayItem(label, value, ResolveIconKey(label));
        }

        return new ProfileDetailDisplayItem(line, string.Empty, "default");
    }

    private static string ResolveIconKey(string label) => label.ToLowerInvariant() switch
    {
        var value when value.Contains("extension") => "extension",
        var value when value.Contains("setting") => "settings",
        var value when value.Contains("keybinding") => "keybinding",
        var value when value.Contains("workspace") => "workspace",
        var value when value.Contains("bookmark") => "bookmark",
        var value when value.Contains("profile") => "profile",
        var value when value.Contains("container") => "container",
        var value when value.Contains("image") => "image",
        var value when value.Contains("volume") => "volume",
        var value when value.Contains("library") => "library",
        var value when value.Contains("playtime") => "playtime",
        var value when value.Contains("tool") => "tools",
        _ => "default"
    };
}

public partial class SnapshotItemViewModel : ObservableObject
{
    public SnapshotItemViewModel(SnapshotInfo snapshot, int index)
    {
        Snapshot = snapshot;
        Index = index;
        Id = snapshot.Id;
        Name = snapshot.Name;
        Description = snapshot.Description;
        CreatedAt = snapshot.CreatedAt;
        SizeBytes = snapshot.SizeBytes;
        IsCurrent = snapshot.IsCurrent;
        KindLabel = snapshot.Kind == SnapshotKind.Incremental ? "Inkrementell" : "Vollständig";
        StatusLabel = snapshot.IsCurrent ? "Aktuell" : KindLabel;
        MetadataLabel = SnapshotMetadataFormatter.Build(snapshot);
        StoragePathLabel = FormatStoragePath(snapshot.StoragePath);
        SizeLabel = SnapshotMetadataFormatter.FormatSize(snapshot.SizeBytes);
        var fileCount = snapshot.StoredFileCount + snapshot.ReferencedFileCount;
        FileCount = fileCount;
        FileCountLabel = fileCount > 0 ? $"{fileCount:N0} Dateien" : "—";
    }

    public string StoragePathLabel { get; }

    public SnapshotInfo Snapshot { get; }
    public int Index { get; }
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public DateTimeOffset CreatedAt { get; }
    public long SizeBytes { get; }
    public int FileCount { get; }
    public string SizeLabel { get; }
    public string FileCountLabel { get; }
    public bool IsCurrent { get; }
    public string KindLabel { get; }
    public string StatusLabel { get; }
    public string MetadataLabel { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLast;

    private static string FormatStoragePath(string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return "Speicherort: —";
        }

        var path = storagePath.Trim();
        const int maxLength = 72;
        if (path.Length <= maxLength)
        {
            return $"Speicherort: {path}";
        }

        return $"Speicherort: …{path[^(maxLength - 1)..]}";
    }
}

public partial class SnapshotOverviewItemViewModel : ObservableObject
{
    public SnapshotOverviewItemViewModel(
        string programId,
        string programName,
        string programCategory,
        SnapshotItemViewModel snapshot,
        string? groupId = null,
        string? groupName = null,
        int programSortOrder = 0)
    {
        ProgramId = programId;
        ProgramName = programName;
        ProgramCategory = programCategory;
        GroupId = groupId;
        GroupName = groupName;
        ProgramSortOrder = programSortOrder;
        Snapshot = snapshot;
    }

    public string ProgramId { get; }
    public string ProgramName { get; }
    public string ProgramCategory { get; }
    public string? GroupId { get; }
    public string? GroupName { get; }
    public int ProgramSortOrder { get; }
    public SnapshotItemViewModel Snapshot { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public partial class SnapshotDisplayGroupViewModel : ObservableObject
{
    public SnapshotDisplayGroupViewModel(
        string key,
        string title,
        string subtitle,
        bool isProgramGroup,
        int sortOrder,
        IEnumerable<SnapshotOverviewItemViewModel> snapshots)
    {
        Key = key;
        Name = title;
        Subtitle = subtitle;
        IsProgramGroup = isProgramGroup;
        SortOrder = sortOrder;
        ShowHeader = !string.Equals(key, "all", StringComparison.Ordinal);
        Snapshots = new ObservableCollection<SnapshotOverviewItemViewModel>(snapshots);
        RefreshTotals();
        DisplayTitle = $"{title} ({Snapshots.Count})";
        IconGlyph = CreateGlyph(title);
    }

    public void RefreshTotals()
    {
        TotalSizeBytes = Snapshots.Sum(item => item.Snapshot.SizeBytes);
        TotalFileCount = Snapshots.Sum(item => item.Snapshot.FileCount);
        TotalSizeLabel = SnapshotMetadataFormatter.FormatSize(TotalSizeBytes);
        StatsSubtitle = Snapshots.Count == 0
            ? Subtitle
            : $"{Snapshots.Count} Snapshots · {TotalSizeLabel} · {TotalFileCount:N0} Dateien";
        DisplayTitle = $"{Name} ({Snapshots.Count})";
    }

    public string Key { get; }

    public string Name { get; }

    public string DisplayTitle { get; private set; }

    public string Subtitle { get; }

    public bool IsProgramGroup { get; }

    public bool ShowHeader { get; }

    public int SortOrder { get; }

    public string IconGlyph { get; }

    public long TotalSizeBytes { get; private set; }

    public int TotalFileCount { get; private set; }

    public string TotalSizeLabel { get; private set; } = "0 B";

    public string StatsSubtitle { get; private set; } = string.Empty;

    public ObservableCollection<SnapshotOverviewItemViewModel> Snapshots { get; }

    [ObservableProperty]
    private bool _isExpanded = true;

    public bool MatchesSearch(string? searchText)
    {
        var query = searchText?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return true;
        }

        return Contains(Name, query)
            || Contains(DisplayTitle, query)
            || Contains(Subtitle, query)
            || Snapshots.Any(snapshot =>
                snapshot.ProgramName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || snapshot.ProgramCategory.Contains(query, StringComparison.OrdinalIgnoreCase)
                || snapshot.Snapshot.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || snapshot.Snapshot.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || snapshot.Snapshot.MetadataLabel.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateGlyph(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "SN";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }

        return name.Length >= 2
            ? $"{char.ToUpperInvariant(name[0])}{char.ToUpperInvariant(name[1])}"
            : char.ToUpperInvariant(name[0]).ToString();
    }

    private static bool Contains(string? value, string query)
        => !string.IsNullOrEmpty(value)
           && value.Contains(query, StringComparison.OrdinalIgnoreCase);
}

internal static class SnapshotMetadataFormatter
{
    public static string Build(SnapshotInfo snapshot)
    {
        var parts = new List<string>
        {
            snapshot.Kind == SnapshotKind.Incremental ? "Inkrementell" : "Vollständig"
        };

        if (!string.IsNullOrWhiteSpace(snapshot.Description))
        {
            var description = snapshot.Description.Trim();
            if (!string.Equals(description, parts[0], StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(Truncate(description, 28));
            }
        }

        if (snapshot.StoredFileCount > 0 || snapshot.ReferencedFileCount > 0)
        {
            parts.Add($"{snapshot.StoredFileCount} neu, {snapshot.ReferencedFileCount} ref.");
        }

        if (snapshot.IsCurrent && snapshot.SizeBytes > 0)
        {
            parts.Add(FormatSize(snapshot.SizeBytes));
        }

        parts.Add(FormatRelativeTime(snapshot.CreatedAt));
        return string.Join(" · ", parts);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var size = bytes / 1024d;
        if (size < 1024)
        {
            return $"{size.ToString("0.#", CultureInfo.GetCultureInfo("de-DE"))} KB";
        }

        size /= 1024d;
        if (size < 1024)
        {
            return $"{size.ToString("0.#", CultureInfo.GetCultureInfo("de-DE"))} MB";
        }

        size /= 1024d;
        return $"{size.ToString("0.#", CultureInfo.GetCultureInfo("de-DE"))} GB";
    }

    private static string FormatRelativeTime(DateTimeOffset timestamp)
    {
        var now = DateTimeOffset.Now;
        var delta = now - timestamp;

        if (delta.TotalMinutes < 1)
        {
            return "Gerade eben";
        }

        if (now.Date == timestamp.Date)
        {
            return $"Heute {timestamp:HH:mm}";
        }

        if (now.Date.AddDays(-1) == timestamp.Date)
        {
            return "Gestern";
        }

        if (delta.TotalDays < 7)
        {
            var days = Math.Max(1, (int)delta.TotalDays);
            return $"{days} Tage";
        }

        if (delta.TotalDays < 30)
        {
            var weeks = Math.Max(1, (int)Math.Round(delta.TotalDays / 7));
            return weeks == 1 ? "1 Woche" : $"{weeks} Wochen";
        }

        return timestamp.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("de-DE"));
    }
}
