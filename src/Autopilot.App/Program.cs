using Avalonia;

namespace Autopilot.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        Configure()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// The application's setup without a platform, shared with the headless tests so they run
    /// the app as it ships.
    /// </summary>
    public static AppBuilder Configure() => AppBuilder.Configure<AutopilotApplication>();
}
