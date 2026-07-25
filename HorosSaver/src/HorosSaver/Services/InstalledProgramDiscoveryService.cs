using System.Runtime.InteropServices;
using HorosSaver.Models;
using Microsoft.Win32;

namespace HorosSaver.Services;

public sealed class InstalledProgramDiscoveryService : IInstalledProgramDiscoveryService
{
    private static readonly string[] UninstallRegistryPaths =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    private static readonly string[] NoiseNameFragments =
    [
        "Update for",
        "Security Update",
        "Hotfix for",
        "KB",
        "Microsoft Visual C++",
        ".NET Framework",
        "Windows SDK"
    ];

    private readonly StartMenuDiscoveryService _startMenuDiscovery = new();

    public Task<IReadOnlyList<DiscoveredProgram>> DiscoverInstalledProgramsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult<IReadOnlyList<DiscoveredProgram>>([]);
        }

        var registryPrograms = DiscoverFromRegistry(cancellationToken);
        var startMenuPrograms = _startMenuDiscovery.Discover(cancellationToken);

        var merged = ProgramDiscoveryMerger.Merge(registryPrograms, startMenuPrograms)
            .Where(program => !IsNoise(program.DisplayName))
            .ToList();

        return Task.FromResult<IReadOnlyList<DiscoveredProgram>>(merged);
    }

    private List<DiscoveredProgram> DiscoverFromRegistry(CancellationToken cancellationToken)
    {
        var results = new List<DiscoveredProgram>();

        foreach (var path in UninstallRegistryPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.AddRange(ReadUninstallKey(Registry.LocalMachine, path, "machine"));
        }

        cancellationToken.ThrowIfCancellationRequested();
        results.AddRange(ReadUninstallKey(Registry.CurrentUser, UninstallRegistryPaths[0], "user"));

        return results
            .GroupBy(program => NormalizeKey(program.DisplayName, program.Publisher, program.DisplayVersion))
            .Select(group => group.First())
            .ToList();
    }

    private static IEnumerable<DiscoveredProgram> ReadUninstallKey(
        RegistryKey root,
        string subPath,
        string scope)
    {
        using var uninstallKey = root.OpenSubKey(subPath);
        if (uninstallKey is null)
        {
            yield break;
        }

        foreach (var subKeyName in uninstallKey.GetSubKeyNames())
        {
            using var subKey = uninstallKey.OpenSubKey(subKeyName);
            if (subKey is null)
            {
                continue;
            }

            var displayName = subKey.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            if (subKey.GetValue("SystemComponent") is int systemComponent && systemComponent == 1)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(subKey.GetValue("ParentKeyName") as string))
            {
                continue;
            }

            var releaseType = subKey.GetValue("ReleaseType") as string;
            if (!string.IsNullOrWhiteSpace(releaseType)
                && releaseType.Contains("Update", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var installLocation = NormalizePath(subKey.GetValue("InstallLocation") as string);
            var publisher = TrimToNull(subKey.GetValue("Publisher") as string);
            var version = TrimToNull(subKey.GetValue("DisplayVersion") as string);

            yield return new DiscoveredProgram
            {
                DisplayName = displayName.Trim(),
                DisplayVersion = version,
                Publisher = publisher,
                InstallLocation = installLocation,
                RegistryKeyName = subKeyName,
                Scope = scope,
                Sources = ProgramDiscoverySource.Registry
            };
        }
    }

    private static bool IsNoise(string displayName)
    {
        if (displayName.Length < 2)
        {
            return true;
        }

        return NoiseNameFragments.Any(fragment =>
            displayName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeKey(string displayName, string? publisher, string? version)
        => $"{displayName}|{publisher ?? string.Empty}|{version ?? string.Empty}".ToLowerInvariant();

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim().Trim('"');
        return trimmed.EndsWith('\\') ? trimmed.TrimEnd('\\') : trimmed;
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
