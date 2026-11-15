using System.IO.Compression;
using Atmosphere.Services;
using Xunit;

namespace Atmosphere.Tests;

public sealed class ProfileModInstallerTests
{
    private const string ValidMetadata =
        "<xml><Name value=\"ExampleMod\"/><DisplayName value=\"Example Mod\"/><Version value=\"1.0\"/></xml>";

    [Fact]
    public void InstallEnableDisableArchive_StaysInsideProfile()
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("Test");
        var installer = new ProfileModInstaller(profiles);
        var zipPath = Path.Combine(sandbox.Path, "mod.zip");
        WriteZip(zipPath, ("Downloads/Example/ModInfo.xml", ValidMetadata),
            ("Downloads/Example/Config/items.xml", "<configs/>"));

        var installed = Assert.Single(installer.InstallZip(profile.Id, zipPath));
        Assert.Equal("ExampleMod", installed.Name);
        Assert.True(installed.IsValid);
        Assert.Single(installer.Installed(profile.Id));
        Assert.Throws<IOException>(() => installer.InstallZip(profile.Id, zipPath));

        installer.SetEnabled(profile.Id, "ExampleMod", false);
        Assert.Empty(installer.Installed(profile.Id));
        Assert.Single(installer.Disabled(profile.Id));
        installer.SetEnabled(profile.Id, "ExampleMod", true);
        var archive = installer.ArchiveMod(profile.Id, "ExampleMod");
        Assert.True(File.Exists(Path.Combine(archive, "ModInfo.xml")));
        Assert.Empty(installer.Installed(profile.Id));
    }

    [Fact]
    public void InstallRejectsTraversal()
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("Test");
        var zipPath = Path.Combine(sandbox.Path, "bad.zip");
        WriteZip(zipPath, ("../escape.txt", "secret"),
            ("Good/ModInfo.xml", ValidMetadata));

        Assert.Throws<InvalidDataException>(() =>
            new ProfileModInstaller(profiles).InstallZip(profile.Id, zipPath));
        Assert.False(File.Exists(Path.Combine(profiles.GetDirectory(profile.Id), "escape.txt")));
    }

    [Fact]
    public void InstallRejectsLegacyAndMissingMetadata()
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var p = profiles.Create("Test");
        var zip = Path.Combine(sandbox.Path, "old.zip");
        WriteZip(zip, ("Old/ModInfo.xml", "<xml><ModInfo><Name value=\"Old\"/></ModInfo></xml>"));
        Assert.Throws<InvalidDataException>(() =>
            new ProfileModInstaller(profiles).InstallZip(p.Id, zip));
    }

    [Fact]
    public void Update_BackupsPriorInstalledVersion()
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("Test");
        var installer = new ProfileModInstaller(profiles);
        var zipPath = Path.Combine(sandbox.Path, "update.zip");
        WriteZip(zipPath, ("Example/ModInfo.xml", ValidMetadata),
            ("Example/Config/version.txt", "first"));
        installer.InstallZip(profile.Id, zipPath);

        File.Delete(zipPath);
        WriteZip(zipPath, ("Example/ModInfo.xml", ValidMetadata),
            ("Example/Config/version.txt", "second"));
        installer.InstallZip(profile.Id, zipPath, replaceExisting: true);

        var current = Path.Combine(profiles.GetDirectory(profile.Id), "Mods",
            "ExampleMod", "Config", "version.txt");
        Assert.Equal("second", File.ReadAllText(current));
        var backupFiles = Directory.GetFiles(
            Path.Combine(profiles.GetDirectory(profile.Id), "Backups", "Replaced"),
            "version.txt", SearchOption.AllDirectories);
        Assert.Single(backupFiles);
        Assert.Equal("first", File.ReadAllText(backupFiles[0]));
    }

    [Fact]
    public void RejectsSymlinksInZip()
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("Test");
        var archivePath = Path.Combine(sandbox.Path, "symlink.zip");
        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var link = zip.CreateEntry("Mod/linked");
            link.ExternalAttributes = (0xA000 | 0x1FF) << 16;
            using var writer = new StreamWriter(link.Open());
            writer.Write("../../../elsewhere");
        }

        Assert.Throws<InvalidDataException>(() =>
            new ProfileModInstaller(profiles).InstallZip(profile.Id, archivePath));
    }

    private static void WriteZip(string path, params (string Path, string Content)[] files)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var file in files)
        {
            var entry = archive.CreateEntry(file.Path);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(file.Content);
        }
    }
}
