namespace HorosSaver;

public static class CliLaunchOptions
{
    public static string? RestoreProgramId { get; private set; }
    public static string? RestoreSnapshotId { get; private set; }
    public static bool RegenerateRestoreBats { get; private set; }
    public static bool RestoreReinstallProgram { get; private set; }

    public static bool IsHeadlessMode =>
        IsRestoreMode || RegenerateRestoreBats;

    public static bool IsRestoreMode =>
        !string.IsNullOrWhiteSpace(RestoreProgramId)
        && !string.IsNullOrWhiteSpace(RestoreSnapshotId);

    public static void Parse(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--restore" when i + 2 < args.Length:
                    RestoreProgramId = args[++i];
                    RestoreSnapshotId = args[++i];
                    break;
                case "--reinstall":
                    RestoreReinstallProgram = true;
                    break;
                case "--regenerate-restore-bats":
                    RegenerateRestoreBats = true;
                    break;
            }
        }
    }
}
