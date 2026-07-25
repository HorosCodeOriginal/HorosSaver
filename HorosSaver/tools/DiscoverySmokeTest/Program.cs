using HorosSaver.Models;
using HorosSaver.Services;

var discovery = new InstalledProgramDiscoveryService();
var programs = await discovery.DiscoverInstalledProgramsAsync();

var registryOnly = programs.Count(program => program.Sources == ProgramDiscoverySource.Registry);
var startMenuOnly = programs.Count(program => program.Sources == ProgramDiscoverySource.StartMenu);
var both = programs.Count(program => program.Sources == (ProgramDiscoverySource.Registry | ProgramDiscoverySource.StartMenu));

Console.WriteLine($"TOTAL={programs.Count}");
Console.WriteLine($"REGISTRY_ONLY={registryOnly}");
Console.WriteLine($"STARTMENU_ONLY={startMenuOnly}");
Console.WriteLine($"BOTH={both}");

var sampleStartMenu = programs.FirstOrDefault(program => program.Sources.HasFlag(ProgramDiscoverySource.StartMenu));
if (sampleStartMenu is not null)
{
    Console.WriteLine($"SAMPLE={sampleStartMenu.DisplayName} | {sampleStartMenu.SourceLabel} | {sampleStartMenu.TargetPath}");
}

return programs.Count > 0 && startMenuOnly + both > 0 ? 0 : 1;
