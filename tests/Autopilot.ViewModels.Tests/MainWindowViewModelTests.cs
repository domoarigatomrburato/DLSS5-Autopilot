namespace Autopilot.ViewModels.Tests;

[TestClass]
public sealed class MainWindowViewModelTests
{
    [TestMethod]
    public void The_window_is_titled_with_the_product_name()
    {
        var viewModel = new MainWindowViewModel();

        Assert.AreEqual("Invidia Claritatis", viewModel.Title);
    }
}
