using Autopilot.ViewModels;

namespace Autopilot.App;

/// <summary>
/// Builds the object graph.
/// </summary>
internal static class CompositionRoot
{
    public static MainWindow CreateMainWindow() => new() { DataContext = new MainWindowViewModel() };
}
