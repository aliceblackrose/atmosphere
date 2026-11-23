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
        CreateGamePlaceholder(game);
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "application"));
        var profile = profiles.Create("V2.0");
        var options = new ProfileGameLauncher(profiles).BuildStartInfo(profile.Id, game, true);
        Assert.Equal(game, options.WorkingDirectory);
        Assert.Contains("-noeac", options.ArgumentList);
        Assert.Contains("-UserDataFolder=" + profiles.GetDirectory(profile.Id), options.ArgumentList);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildStartInfo_RequiresExplicitNoEacForDllsInEitherActiveModRoot(bool gameRoot)
    {
        using var sandbox = new TempDirectory();
        var game = Path.Combine(sandbox.Path, "game");
        CreateGamePlaceholder(game);
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "application"));
        var profile = profiles.Create("Profile");
        var profileRoot = profiles.GetDirectory(profile.Id);
        var launcher = new ProfileGameLauncher(profiles);
        var harmlessDisabledFolder = Path.Combine(profileRoot, "DisabledMods", "Disabled");
        Directory.CreateDirectory(harmlessDisabledFolder);
        File.WriteAllBytes(Path.Combine(harmlessDisabledFolder, "Disabled.dll"), []);
        Assert.DoesNotContain("-noeac", launcher.BuildStartInfo(profile.Id, game, false).ArgumentList);

        var modFolder = Path.Combine(gameRoot ? game : profileRoot, "Mods", "Example", "bin");
        Directory.CreateDirectory(modFolder);
        File.WriteAllBytes(Path.Combine(modFolder, "Placeholder.DLL"), []);
        var error = Assert.Throws<InvalidOperationException>(() => launcher.BuildStartInfo(profile.Id, game, false));
        Assert.Contains("Launch without EAC", error.Message);
        Assert.Contains("-noeac", launcher.BuildStartInfo(profile.Id, game, true).ArgumentList);
    }

    private static void CreateGamePlaceholder(string game)
    {
        var executable = Path.Combine(game, OperatingSystem.IsWindows() ? "7DaysToDie.exe" :
            OperatingSystem.IsMacOS() ? Path.Combine("7DaysToDie.app", "Contents", "MacOS", "7DaysToDie") :
            "7DaysToDie.x86_64");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllBytes(executable, []);
    }
}
