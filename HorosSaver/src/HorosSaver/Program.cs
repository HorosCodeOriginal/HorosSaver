using Avalonia;
using HorosSaver.Services;
using System;

namespace HorosSaver;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var layout = AppStoragePaths.Resolve();
        AppFileLogger.Initialize(layout.LogsRoot);
        RegisterGlobalExceptionHandlers();

        PreviewLaunchOptions.Parse(args);
        CliLaunchOptions.Parse(args);

        if (CliLaunchOptions.IsHeadlessMode)
        {
            var exitCode = HeadlessCliRunner.RunAsync().GetAwaiter().GetResult();
            Environment.Exit(exitCode);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                AppFileLogger.LogUnhandledException(exception, "AppDomain");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            AppFileLogger.LogUnhandledException(eventArgs.Exception, "TaskScheduler");
            eventArgs.SetObserved();
        };
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
