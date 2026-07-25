using HorosSaver.Models;
using HorosSaver.Services;

namespace HorosSaver.ViewModels;

public sealed class SystemAbbildModeOptionViewModel
{
    public SystemAbbildModeOptionViewModel(SystemAbbildMode mode)
    {
        Mode = mode;
        Label = SystemAbbildPaths.GetLevelLabel(mode);
        Description = SystemAbbildPaths.GetLevelDescription(mode);
    }

    public SystemAbbildMode Mode { get; }
    public string Label { get; }
    public string Description { get; }

    public static IReadOnlyList<SystemAbbildModeOptionViewModel> CreateAll()
        =>
        [
            new(SystemAbbildMode.WindowsSystemImage),
            new(SystemAbbildMode.AllProgramsBundle),
            new(SystemAbbildMode.AllVolumes)
        ];
}
