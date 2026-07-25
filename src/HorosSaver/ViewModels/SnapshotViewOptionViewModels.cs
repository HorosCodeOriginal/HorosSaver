using HorosSaver.Models;

namespace HorosSaver.ViewModels;

public sealed class SnapshotViewLayoutOptionViewModel
{
    public SnapshotViewLayoutOptionViewModel(SnapshotViewMode layout, string label)
    {
        Layout = layout;
        Label = label;
    }

    public SnapshotViewMode Layout { get; }
    public string Label { get; }

    public static IReadOnlyList<SnapshotViewLayoutOptionViewModel> CreateAll() =>
    [
        new(SnapshotViewMode.Cards, "Karten (2–3 Spalten)"),
        new(SnapshotViewMode.CompactList, "Kompakte Liste"),
        new(SnapshotViewMode.Table, "Tabelle"),
        new(SnapshotViewMode.Gallery, "Galerie"),
        new(SnapshotViewMode.CompactGrid, "Kompakt-Gitter"),
        new(SnapshotViewMode.Chronology, "Chronologie"),
        new(SnapshotViewMode.Tree, "Baum")
    ];
}

public sealed class SnapshotGroupModeOptionViewModel
{
    public SnapshotGroupModeOptionViewModel(SnapshotGroupMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }

    public SnapshotGroupMode Mode { get; }
    public string Label { get; }

    public static IReadOnlyList<SnapshotGroupModeOptionViewModel> CreateAll() =>
    [
        new(SnapshotGroupMode.ProgramGroup, "Nach Programm-Gruppe"),
        new(SnapshotGroupMode.Program, "Nach Programm"),
        new(SnapshotGroupMode.None, "Keine"),
        new(SnapshotGroupMode.ByDate, "Nach Datum (Tag)")
    ];
}

public sealed class SnapshotSortModeOptionViewModel
{
    public SnapshotSortModeOptionViewModel(SnapshotSortMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }

    public SnapshotSortMode Mode { get; }
    public string Label { get; }

    public static IReadOnlyList<SnapshotSortModeOptionViewModel> CreateAll() =>
    [
        new(SnapshotSortMode.NewestFirst, "Neueste zuerst"),
        new(SnapshotSortMode.OldestFirst, "Älteste zuerst"),
        new(SnapshotSortMode.NameAsc, "Name A–Z"),
        new(SnapshotSortMode.SizeDesc, "Größe")
    ];
}

public sealed class SnapshotDateRangeOptionViewModel
{
    public SnapshotDateRangeOptionViewModel(int days, string label)
    {
        Days = days;
        Label = label;
    }

    public int Days { get; }
    public string Label { get; }

    public static IReadOnlyList<SnapshotDateRangeOptionViewModel> CreateAll() =>
    [
        new(0, "Alle Zeiträume"),
        new(7, "Letzte 7 Tage"),
        new(30, "Letzte 30 Tage"),
        new(90, "Letzte 90 Tage")
    ];
}
