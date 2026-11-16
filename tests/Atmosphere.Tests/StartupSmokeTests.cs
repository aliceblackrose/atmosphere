using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Atmosphere;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Atmosphere.Tests.TestAppBuilder))]

namespace Atmosphere.Tests;

public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class StartupSmokeTests
{
    [AvaloniaFact]
    public void MainWindow_CanBeCreatedAndShown()
    {
        var window = new MainWindow();
        window.Show();
        var root = Assert.IsType<Grid>(window.Content);
        var tabs = Assert.Single(root.Children.OfType<TabControl>());
        Assert.Equal(5, tabs.Items.Count);
        window.Close();
    }
}
