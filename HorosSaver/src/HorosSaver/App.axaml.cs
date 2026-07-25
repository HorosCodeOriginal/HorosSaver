using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HorosSaver.Services;
using HorosSaver.ViewModels;
using HorosSaver.ViewModels.Previews;
using HorosSaver.Views;
using HorosSaver.Views.Previews;

namespace HorosSaver;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (PreviewLaunchOptions.IsPreviewMode)
            {
                desktop.MainWindow = CreatePreviewWindow(PreviewLaunchOptions.PreviewRegion!);
                ConfigureCaptureOnOpen(desktop.MainWindow, PreviewLaunchOptions.CaptureOutputPath);
            }
            else
            {
                var pathResolver = new StoragePathResolver();
                pathResolver.EnsureDataDirectories();
                LogPortableStartup(pathResolver);

                var settingsService = new AppSettingsService(pathResolver);
                var profileService = new ProgramProfileService(pathResolver);
                var locationsIndex = new SnapshotLocationsIndexService(pathResolver);
                var discoveryService = new InstalledProgramDiscoveryService();
                var programInstallService = new ProgramInstallService();
                var snapshotService = new SnapshotService(
                    pathResolver,
                    settingsService,
                    programInstallService,
                    profileService,
                    locationsIndex);
                var snapshotJobManager = new SnapshotJobManager(snapshotService);
                var compareService = new SnapshotCompareService(pathResolver, locationsIndex);
                var wbadminRunner = new WbadminBackupRunner(pathResolver);
                var systemImageService = new SystemImageService(
                    pathResolver,
                    settingsService,
                    profileService,
                    snapshotService,
                    wbadminRunner);
                var enginePathResolver = new AppReinstallEnginePathResolver(settingsService);
                var engineProcessService = new EngineProcessService();
                var engineService = new AppReinstallEngineService(enginePathResolver, engineProcessService);

                var mainViewModel = new MainViewModel(
                    profileService,
                    snapshotService,
                    snapshotJobManager,
                    compareService,
                    discoveryService,
                    pathResolver,
                    settingsService,
                    enginePathResolver,
                    engineService,
                    systemImageService);

                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };

                if (PreviewLaunchOptions.IsCaptureOnlyMode)
                {
                    ConfigureCaptureOnOpen(desktop.MainWindow, PreviewLaunchOptions.CaptureOutputPath);
                }

                mainViewModel.RestoreWizard.HostWindow = desktop.MainWindow;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void LogPortableStartup(IStoragePathResolver paths)
    {
        AppFileLogger.Info(
            paths.IsPortable
                ? $"Portable-Modus aktiv. Daten: {paths.DataRoot}"
                : $"Standard-Modus aktiv. Daten: {paths.DataRoot}");

        if (!paths.IsPortable)
        {
            return;
        }

        var legacyRoot = AppStoragePaths.GetLegacyLocalAppDataRoot();
        if (!Directory.Exists(legacyRoot))
        {
            return;
        }

        var portableHasProfiles = File.Exists(paths.ProfilesFilePath);
        var legacyHasProfiles = File.Exists(Path.Combine(legacyRoot, "profiles.json"));
        if (!portableHasProfiles && legacyHasProfiles)
        {
            AppFileLogger.Warning(
                $"Portable data folder is empty but legacy data exists at {legacyRoot}. " +
                "Copy profiles.json, settings.json and snapshots/ manually if you want to migrate.");
        }
    }

    private static Window CreatePreviewWindow(string region) =>
        region.ToLowerInvariant() switch
        {
            "sidebar" => new SidebarPreview
            {
                DataContext = SidebarPreviewViewModel.DesignInstance
            },
            "toolbar" => new ToolbarPreview
            {
                DataContext = CreateToolbarPreviewViewModel()
            },
            "app-grid" => new ProgramsGridPreview
            {
                DataContext = ProgramsGridPreviewViewModel.DesignInstance
            },
            "timeline" => new TimelinePreview
            {
                DataContext = TimelinePreviewViewModel.DesignInstance
            },
            "statusbar" => new StatusBarPreview
            {
                DataContext = StatusBarPreviewViewModel.DesignInstance
            },
            "chrome" => new WindowChromePreview(),
            _ => throw new InvalidOperationException($"Unknown preview region: {region}")
        };

    private static ToolbarPreviewViewModel CreateToolbarPreviewViewModel()
        => ToolbarPreviewViewModel.CreateDesignInstance(!PreviewLaunchOptions.ToolbarCollapsed);

    private static void ConfigureCaptureOnOpen(Window window, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        window.Opened += async (_, _) =>
        {
            try
            {
                var delayMs = PreviewLaunchOptions.IsCaptureOnlyMode ? 2500 : 800;
                await PreviewCaptureService.CaptureWindowAsync(window, outputPath, delayMs);
            }
            finally
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
        };
    }
}
