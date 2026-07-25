using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HorosSaver.Models;

namespace HorosSaver.ViewModels;

public partial class SnapshotDiffItemViewModel : ObservableObject
{
    public SnapshotDiffItemViewModel(SnapshotFileDiff diff)
    {
        Diff = diff;
        Kind = diff.Kind;
        RelativePath = diff.RelativePath;
        StatusLabel = diff.Kind switch
        {
            SnapshotDiffKind.Added => "Hinzugefügt",
            SnapshotDiffKind.Removed => "Entfernt",
            SnapshotDiffKind.Changed => "Geändert",
            _ => "—"
        };
        OlderSizeLabel = diff.OlderSizeBytes.HasValue ? FormatSize(diff.OlderSizeBytes.Value) : "—";
        NewerSizeLabel = diff.NewerSizeBytes.HasValue ? FormatSize(diff.NewerSizeBytes.Value) : "—";
        Detail = diff.Detail;
    }

    public SnapshotFileDiff Diff { get; }
    public SnapshotDiffKind Kind { get; }
    public string RelativePath { get; }
    public string StatusLabel { get; }
    public string OlderSizeLabel { get; }
    public string NewerSizeLabel { get; }
    public string Detail { get; }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var value = bytes / 1024d;
        if (value < 1024)
        {
            return $"{value:0.#} KB";
        }

        value /= 1024d;
        return $"{value:0.#} MB";
    }
}

public partial class CompareViewModel : ViewModelBase
{
    public CompareViewModel(SnapshotCompareResult result)
    {
        Result = result;
        Title = $"Snapshot-Vergleich — {result.ProgramName}";
        SummaryLabel = result.Success
            ? $"{result.AddedCount} hinzugefügt · {result.RemovedCount} entfernt · {result.ChangedCount} geändert"
            : result.Message;
        OlderLabel = result.OlderSnapshotLabel;
        NewerLabel = result.NewerSnapshotLabel;
        RangeLabel = $"{result.OlderCreatedAt.ToLocalTime():dd.MM.yyyy HH:mm} → {result.NewerCreatedAt.ToLocalTime():dd.MM.yyyy HH:mm}";
        HasDifferences = result.Differences.Count > 0;
        EmptyMessage = result.Success
            ? "Keine Dateiunterschiede zwischen den beiden Snapshots."
            : result.Message;

        foreach (var diff in result.Differences)
        {
            Differences.Add(new SnapshotDiffItemViewModel(diff));
        }
    }

    public SnapshotCompareResult Result { get; }
    public ObservableCollection<SnapshotDiffItemViewModel> Differences { get; } = [];

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _summaryLabel = string.Empty;

    [ObservableProperty]
    private string _olderLabel = string.Empty;

    [ObservableProperty]
    private string _newerLabel = string.Empty;

    [ObservableProperty]
    private string _rangeLabel = string.Empty;

    [ObservableProperty]
    private string _emptyMessage = string.Empty;

    [ObservableProperty]
    private bool _hasDifferences;

    public event Action? CloseRequested;

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke();
    }
}
