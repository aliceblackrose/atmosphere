using Atmosphere.Models;
using Atmosphere.Services;
using Xunit;

namespace Atmosphere.Tests;

public sealed class LauncherSettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsGameDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "atmosphere-settings-" + Guid.NewGuid());
        try
        {
            var store = new LauncherSettingsStore(directory);
            store.Save(new LauncherSettings { GameDirectory = "test-game-path" });

            Assert.Equal("test-game-path", store.Load().GameDirectory);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_InvalidJsonUsesDefaultSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "atmosphere-settings-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{not json}");
            Assert.Null(new LauncherSettingsStore(directory).Load().GameDirectory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
