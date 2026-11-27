using CommunityToolkit.Mvvm.ComponentModel;

namespace Autopilot.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    public string Title { get; } = "Invidia Claritatis";
}
