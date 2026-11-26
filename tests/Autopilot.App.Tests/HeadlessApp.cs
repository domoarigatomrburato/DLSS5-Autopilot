using Autopilot.App.Tests;
using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(HeadlessApp))]

namespace Autopilot.App.Tests;

/// <summary>
/// The app on Avalonia's headless platform: nothing opens on the desktop. Every UI test shares
/// one session per test assembly, through <see cref="Session"/>.
/// </summary>
internal static class HeadlessApp
{
    public static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessApp).Assembly);

    public static AppBuilder BuildAvaloniaApp() => Program.Configure().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
