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
        Assert.NotNull(window.Content);
        Assert.NotNull(window.FindControl<TabControl>("MainTabs"));
        Assert.NotNull(window.FindControl<TextBlock>("GlobalStatusText"));
        window.Close();
    }
}
