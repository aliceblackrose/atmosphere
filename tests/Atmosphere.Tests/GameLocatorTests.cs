using Atmosphere.Services;
using Xunit;

namespace Atmosphere.Tests;

public sealed class GameLocatorTests
{
    [Fact]
    public void FindInSteamRoots_DetectsDefaultSteamLibrary()
    {
        var directory = Path.Combine(Path.GetTempPath(), "atmosphere-test-" + Guid.NewGuid());
        var gameDirectory = Path.Combine(directory, "steamapps", "common", "7 Days To Die");
        Directory.CreateDirectory(gameDirectory);

        try
        {
            File.WriteAllText(Path.Combine(gameDirectory, "7DaysToDie.exe"), "");

            Assert.Equal(gameDirectory, GameLocator.FindInSteamRoots(new[] { directory }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IsValidGameDirectory_RejectsRandomFolders()
    {
        Assert.False(GameLocator.IsValidGameDirectory(Path.GetTempPath()));
        Assert.False(GameLocator.IsValidGameDirectory(null));
    }
}
