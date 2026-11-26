using CommunityToolkit.Mvvm.ComponentModel;

namespace Autopilot.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    public string Title { get; } = "DLSS5 Autopilot";
}
