using HorosSaver.Models;

namespace HorosSaver.ViewModels;

public sealed class CursorSnapshotLevelOptionViewModel
{
    public CursorSnapshotLevelOptionViewModel(CursorSnapshotLevel level)
    {
        Level = level;
        Label = Services.CursorSnapshotPaths.GetLevelLabel(level);
        Description = Services.CursorSnapshotPaths.GetLevelDescription(level);
    }

    public CursorSnapshotLevel Level { get; }
    public string Label { get; }
    public string Description { get; }

    public static IReadOnlyList<CursorSnapshotLevelOptionViewModel> CreateAll()
        =>
        [
            new(CursorSnapshotLevel.Minimal),
            new(CursorSnapshotLevel.Standard),
            new(CursorSnapshotLevel.Full)
        ];
}
