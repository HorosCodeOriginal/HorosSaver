namespace HorosSaver.Services;

public sealed record ConfirmDialogResult(bool IsConfirmed, bool DeleteSnapshots = true);

public sealed record ConfirmDialogOptions(
    bool ShowDeleteSnapshotsOption = false,
    bool DeleteSnapshotsDefault = true,
    string DeleteSnapshotsLabel = "Zugehörige Snapshots auch löschen",
    string ConfirmButtonText = "Bestätigen");
