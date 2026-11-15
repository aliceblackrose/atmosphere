using Atmosphere.Services;
using Xunit;

namespace Atmosphere.Tests;

public sealed class ProfileGameLauncherTests
{
    [Fact]
    public void BuildStartInfo_UsesSeparateProfileUserDataAndOptionalEacFlag()
    {
        using var sandbox = new TempDirectory();
        var game = Path.Combine(sandbox.Path, "game");
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, OperatingSystem.IsWindows()
            ? "7DaysToDie.exe" : OperatingSystem.IsMacOS()
                ? "7DaysToDie.exe" : "7DaysToDie.x86_64"), Array.Empty<byte>());

        if (OperatingSystem.IsMacOS()) return; // game app bundle layout differs
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "application"));
        var profile = profiles.Create("V2.0");
        var options = new ProfileGameLauncher(profiles).BuildStartInfo(profile.Id, game, true);
        Assert.Equal(game, options.WorkingDirectory);
        Assert.Contains("-noeac", options.ArgumentList);
        Assert.Contains("-UserDataFolder=" + profiles.GetDirectory(profile.Id), options.ArgumentList);
    }
}
