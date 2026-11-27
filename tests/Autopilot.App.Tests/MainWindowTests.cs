namespace Autopilot.App.Tests;

[TestClass]
public sealed class MainWindowTests(TestContext testContext)
{
    [TestMethod]
    public async Task The_window_is_titled_with_the_product_name()
    {
        string? title = await HeadlessApp.Session.Dispatch(
            () =>
            {
                MainWindow window = CompositionRoot.CreateMainWindow();
                window.Show();
                try
                {
                    return window.Title;
                }
                finally
                {
                    window.Close();
                }
            },
            testContext.CancellationToken);

        Assert.AreEqual("Invidia Claritatis", title);
    }
}
