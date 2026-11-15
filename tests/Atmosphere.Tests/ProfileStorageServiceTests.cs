using Atmosphere.Services;
using Xunit;

namespace Atmosphere.Tests;

public sealed class ProfileStorageServiceTests
{
    [Fact]
    public void DeleteGameCopy_DetachesProfileButPreservesSteamGameAndSaveFiles()
    {
        using var scratch = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(scratch.Path, "app"));
        var profile = profiles.Create("Overhaul");
        var steamGame = FakeGame(Path.Combine(scratch.Path, "steam"));
        var copy = FakeGame(Path.Combine(profiles.GetDirectory(profile.Id), "GameCopy"));
        var saves = Path.Combine(profiles.GetDirectory(profile.Id), "Saves");
        Directory.CreateDirectory(saves);
        File.WriteAllText(Path.Combine(saves, "my-save.dat"), "keep saves");
        profiles.SetGameDirectory(profile.Id, copy);

        var service = new ProfileStorageService(profiles);
        var before = service.GetUsage(profile.Id);
        Assert.True(before.GameCopyBytes > 0);

        service.DeleteGameCopy(profile.Id);

        Assert.False(Directory.Exists(copy));
        Assert.Null(profiles.Get(profile.Id).GameDirectory);
        Assert.True(GameLocator.IsValidGameDirectory(steamGame));
        Assert.Equal("keep saves", File.ReadAllText(Path.Combine(saves, "my-save.dat")));
        Assert.Equal(0, service.GetUsage(profile.Id).GameCopyBytes);
    }

    [Fact]
    public void DeleteGameCopy_DoesNotModifyExternalDirectorySelectedForProfile()
    {
        using var scratch = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(scratch.Path, "app"));
        var profile = profiles.Create("Challenge");
        var externalGame = FakeGame(Path.Combine(scratch.Path, "external-game"));
        var copy = FakeGame(Path.Combine(profiles.GetDirectory(profile.Id), "GameCopy"));
        profiles.SetGameDirectory(profile.Id, externalGame);

        new ProfileStorageService(profiles).DeleteGameCopy(profile.Id);

        Assert.Equal(externalGame, profiles.Get(profile.Id).GameDirectory);
        Assert.True(GameLocator.IsValidGameDirectory(externalGame));
        Assert.False(Directory.Exists(copy));
    }

    [Fact]
    public void DeleteProfile_PermanentlyRemovesOwnedFilesButNeverExternalGame()
    {
        using var scratch = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(scratch.Path, "app"));
        var profile = profiles.Create("Discard");
        var externalGame = FakeGame(Path.Combine(scratch.Path, "steam-game"));
        profiles.SetGameDirectory(profile.Id, externalGame);

        var owned = profiles.GetDirectory(profile.Id);
        Directory.CreateDirectory(Path.Combine(owned, "Mods", "Sample"));
        File.WriteAllText(Path.Combine(owned, "Mods", "Sample", "ModInfo.xml"), "<xml/>");
        Directory.CreateDirectory(Path.Combine(owned, "Saves"));
        File.WriteAllText(Path.Combine(owned, "Saves", "save.xml"), "old-save");
        FakeGame(Path.Combine(owned, "GameCopy"));
        Directory.CreateDirectory(Path.Combine(owned, "Backups"));
        File.WriteAllText(Path.Combine(owned, "Backups", "old.zip"), "backup");

        new ProfileStorageService(profiles).DeleteProfile(profile.Id);

        Assert.Empty(profiles.List());
        Assert.Empty(profiles.ListArchived());
        Assert.False(Directory.Exists(owned));
        Assert.True(GameLocator.IsValidGameDirectory(externalGame));
    }

    [Fact]
    public void DeleteArchivedProfile_RemovesOnlySelectedArchive()
    {
        using var scratch = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(scratch.Path, "app"));
        var doomed = profiles.Create("Old");
        var retained = profiles.Create("Keep");
        profiles.Archive(doomed.Id);

        new ProfileStorageService(profiles).DeleteArchivedProfile(doomed.Id);

        Assert.Empty(profiles.ListArchived());
        Assert.Single(profiles.List());
        Assert.Equal(retained.Id, profiles.List()[0].Id);
        Assert.True(Directory.Exists(profiles.GetDirectory(retained.Id)));
    }

    [Fact]
    public void ClearBackups_LeavesCurrentModsAndGameCopy()
    {
        using var scratch = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(scratch.Path, "app"));
        var profile = profiles.Create("Keep Current");
        var root = profiles.GetDirectory(profile.Id);
        var mods = Path.Combine(root, "Mods", "Current");
        var backups = Path.Combine(root, "Backups", "Removed");
        Directory.CreateDirectory(mods);
        Directory.CreateDirectory(backups);
        File.WriteAllText(Path.Combine(mods, "ModInfo.xml"), "current");
        File.WriteAllText(Path.Combine(backups, "old.txt"), "old");
        var copy = FakeGame(Path.Combine(root, "GameCopy"));

        var storage = new ProfileStorageService(profiles);
        Assert.True(storage.GetUsage(profile.Id).BackupBytes > 0);
        storage.ClearBackups(profile.Id);

        Assert.False(Directory.Exists(Path.Combine(root, "Backups")));
        Assert.Equal("current", File.ReadAllText(Path.Combine(mods, "ModInfo.xml")));
        Assert.True(GameLocator.IsValidGameDirectory(copy));
    }

    [Fact]
    public void RefusesToDeleteProfileWithSymlinkToExternalFiles()
    {
        if (OperatingSystem.IsWindows()) return; // Creating test symlinks needs privileges on some hosts.

        using var scratch = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(scratch.Path, "app"));
        var profile = profiles.Create("Link test");
        var external = Path.Combine(scratch.Path, "external");
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, "keep.txt"), "important");
        Directory.CreateSymbolicLink(Path.Combine(profiles.GetDirectory(profile.Id), "linked"), external);

        Assert.Throws<IOException>(() => new ProfileStorageService(profiles).DeleteProfile(profile.Id));

        Assert.Single(profiles.List());
        Assert.Empty(profiles.ListArchived());
        Assert.Equal("important", File.ReadAllText(Path.Combine(external, "keep.txt")));
    }

    [Fact]
    public void RefusesToDeleteLinkedGameCopy()
    {
        if (OperatingSystem.IsWindows()) return;

        using var scratch = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(scratch.Path, "app"));
        var profile = profiles.Create("Link test");
        var externalGame = FakeGame(Path.Combine(scratch.Path, "external"));
        var link = Path.Combine(profiles.GetDirectory(profile.Id), "GameCopy");
        Directory.CreateSymbolicLink(link, externalGame);

        Assert.Throws<IOException>(() =>
            new ProfileStorageService(profiles).DeleteGameCopy(profile.Id));
        Assert.True(GameLocator.IsValidGameDirectory(externalGame));
    }

    [Fact]
    public void MissingGameCopyCannotDeleteOtherFolders()
    {
        using var scratch = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(scratch.Path, "app"));
        var profile = profiles.Create("Vanilla");
        Assert.Throws<DirectoryNotFoundException>(() =>
            new ProfileStorageService(profiles).DeleteGameCopy(profile.Id));
        Assert.Single(profiles.List());
    }

    private static string FakeGame(string path)
    {
        Directory.CreateDirectory(path);
        if (OperatingSystem.IsMacOS())
        {
            var executableFolder = Path.Combine(path, "7DaysToDie.app", "Contents", "MacOS");
            Directory.CreateDirectory(executableFolder);
            File.WriteAllText(Path.Combine(executableFolder, "7DaysToDie"), "fake executable");
        }
        else
        {
            File.WriteAllText(Path.Combine(path,
                OperatingSystem.IsWindows() ? "7DaysToDie.exe" : "7DaysToDie.x86_64"),
                "fake executable");
        }
        return path;
    }
}
