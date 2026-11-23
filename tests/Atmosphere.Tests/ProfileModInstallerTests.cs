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

    [Fact]
    public void RefusesExternallyLinkedModsFolder()
    {
        if (OperatingSystem.IsWindows()) return;
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("Test");
        var mods = Path.Combine(profiles.GetDirectory(profile.Id), "Mods");
        var external = Path.Combine(sandbox.Path, "external");
        Directory.CreateDirectory(external);
        Directory.Delete(mods);
        Directory.CreateSymbolicLink(mods, external);

        Assert.Throws<IOException>(() =>
            new ProfileModInstaller(profiles).Installed(profile.Id));
    }

    [Fact]
    public void InstallZip_WithNestedMultiModPack_InstallsAllModlets()
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("Multi Mod Test");
        var zip = Path.Combine(sandbox.Path, "modpack.zip");
        WriteZip(zip,
            ("Pack/Mods/First/ModInfo.xml",
                "<xml><Name value=\"FirstMod\"/><DisplayName value=\"First Mod\"/><Version value=\"1.0\"/></xml>"),
            ("Pack/Mods/First/Config/items.xml", "<configs/>"),
            ("Pack/Mods/Second/ModInfo.xml",
                "<xml><Name value=\"SecondMod\"/><DisplayName value=\"Second Mod\"/><Version value=\"2.0\"/></xml>"),
            ("Pack/Mods/Second/Config/blocks.xml", "<configs/>"));

        var imported = new ProfileModInstaller(profiles).InstallZip(profile.Id, zip);
        Assert.Equal(2, imported.Count);
        Assert.Contains(imported, mod => mod.Name == "FirstMod");
        Assert.Contains(imported, mod => mod.Name == "SecondMod");
        Assert.True(File.Exists(Path.Combine(profiles.GetDirectory(profile.Id), "Mods",
            "FirstMod", "Config", "items.xml")));
        Assert.True(File.Exists(Path.Combine(profiles.GetDirectory(profile.Id), "Mods",
            "SecondMod", "Config", "blocks.xml")));
    }

    [Fact]
    public void InstallZip_RejectsArchiveOverConfiguredLimit_WithoutModChanges()
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("Limits Test");
        var zip = Path.Combine(sandbox.Path, "modpack.zip");
        WriteZip(zip, ("Example/ModInfo.xml", ValidMetadata));

        var limits = new ModArchiveLimits(1, 10000, 10000, 100);
        var error = Assert.Throws<InvalidDataException>(() =>
            new ProfileModInstaller(profiles, limits).InstallZip(profile.Id, zip));
        Assert.Contains("safety limit", error.Message);
        Assert.Empty(new ProfileModInstaller(profiles).Installed(profile.Id));
    }

    [Fact]
    public void InstallZip_RejectsExpandedSizeOverConfiguredLimit_WithoutModChanges()
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("Expansion Test");
        var zip = Path.Combine(sandbox.Path, "modpack.zip");
        WriteZip(zip, ("Example/ModInfo.xml", ValidMetadata),
            ("Example/Config/huge.xml", new string('X', 2500)));

        var limits = new ModArchiveLimits(100000, 1024, 1024, 100);
        Assert.Throws<InvalidDataException>(() =>
            new ProfileModInstaller(profiles, limits).InstallZip(profile.Id, zip));
        Assert.Empty(new ProfileModInstaller(profiles).Installed(profile.Id));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void InstallZip_EnforcesEntryBudgetBeforeStaging(int maxEntries, bool allowed)
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("Entry budget");
        var zipPath = Path.Combine(sandbox.Path, "mod.zip");
        WriteZip(zipPath, ("Example/ModInfo.xml", ValidMetadata), ("Example/readme.txt", "Example"));

        var installer = new ProfileModInstaller(profiles, new ModArchiveLimits(100000, 100000, 10000, maxEntries));
        if (allowed)
        {
            Assert.Single(installer.InstallZip(profile.Id, zipPath));
        }
        else
        {
            var error = Assert.Throws<InvalidDataException>(() => installer.InstallZip(profile.Id, zipPath));
            Assert.Contains("too many entries", error.Message);
            Assert.False(Directory.Exists(Path.Combine(profiles.GetDirectory(profile.Id), ".staging")));
            Assert.Empty(installer.Installed(profile.Id));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstallZip_AcceptsStandardZip64Metadata(bool requireZip64Offset)
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("ZIP64 compatibility");
        var zipPath = Path.Combine(sandbox.Path, "mod.zip");
        // Generated with Python's standard zipfile writer using ZIP64_LIMIT=0.
        var zip = Convert.FromBase64String(
            "UEsDBC0AAAAIAAAAIVg4W20V//////////8TABQARXhhbXBsZS9Nb2RJbmZvLnhtbAEAEABdAAAAAAAAAD8AAAAAAAAA" +
            "s6nIzbGz8UvMTVUoS8wpTbVVcq1IzC3ISfXNT1HSt7NxySwuyEmsxKJAAaoiLLWoODM/DyZrqGcAEtUHmQsAUEsB" +
            "Ai0DLQAAAAgAAAAhWDhbbRX//////////xMAFAAAAAAAAAAAAIABAAAAAEV4YW1wbGUvTW9kSW5mby54bWwBABAAXQAA" +
            "AAAAAAA/AAAAAAAAAFBLBgYsAAAAAAAAAC0ALQAAAAAAAAAAAAEAAAAAAAAAAQAAAAAAAABVAAAAAAAAAIQAAAAAAAAA" +
            "UEsGBwAAAADZAAAAAAAAAAEAAABQSwUGAAAAAAEAAQBVAAAAhAAAABMAWklQNjQgY29tcGF0aWJpbGl0eQ==");
        // ZIP64 specifies all ones in classic EOCD fields that use an extended value.
        if (requireZip64Offset)
            zip.AsSpan(zip.Length - "ZIP64 compatibility".Length - 6, 4).Fill(0xff);
        File.WriteAllBytes(zipPath, zip);

        var installed = Assert.Single(new ProfileModInstaller(profiles).InstallZip(profile.Id, zipPath));
        Assert.Equal("ExampleMod", installed.Name);
    }

    [Fact]
    public void InstallZip_RejectsOverlappingModRootsBeforeInstalling()
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("Overlapping roots");
        var zipPath = Path.Combine(sandbox.Path, "mod.zip");
        WriteZip(zipPath, ("Example/ModInfo.xml", ValidMetadata),
            ("Example/Child/ModInfo.xml", ValidMetadata.Replace("ExampleMod", "ChildMod")));

        var installer = new ProfileModInstaller(profiles);
        var error = Assert.Throws<InvalidDataException>(() => installer.InstallZip(profile.Id, zipPath));
        Assert.Contains("nested inside", error.Message);
        Assert.Empty(installer.Installed(profile.Id));
    }

    [Fact]
    public void InstallZip_ReplacesExistingModIgnoringCaseAndPreservesFolder()
    {
        using var sandbox = new TempDirectory();
        var profiles = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = profiles.Create("Case-insensitive mod IDs");
        var installer = new ProfileModInstaller(profiles);
        var first = Path.Combine(sandbox.Path, "first.zip");
        var update = Path.Combine(sandbox.Path, "update.zip");
        WriteZip(first, ("Example/ModInfo.xml", ValidMetadata), ("Example/readme.txt", "First"));
        WriteZip(update, ("Example/ModInfo.xml", ValidMetadata.Replace("ExampleMod", "examplemod")),
            ("Example/readme.txt", "Updated"));

        installer.InstallZip(profile.Id, first);
        Assert.Throws<IOException>(() => installer.InstallZip(profile.Id, update));
        installer.SetEnabled(profile.Id, "ExampleMod", false);
        Assert.Throws<IOException>(() => installer.InstallZip(profile.Id, update, replaceExisting: true));
        installer.SetEnabled(profile.Id, "ExampleMod", true);

        var mod = Assert.Single(installer.InstallZip(profile.Id, update, replaceExisting: true));
        Assert.Equal("ExampleMod", mod.FolderName);
        Assert.Equal("examplemod", mod.Name);
        Assert.Equal("Updated", File.ReadAllText(Path.Combine(mod.DirectoryPath, "readme.txt")));
        Assert.Single(installer.Installed(profile.Id));

        installer.SetEnabled(profile.Id, mod.Name, false);
        Assert.Equal("ExampleMod", Assert.Single(installer.Disabled(profile.Id)).FolderName);
        var archivePath = installer.ArchiveMod(profile.Id, mod.Name, disabled: true);
        Assert.Equal("ExampleMod", Path.GetFileName(archivePath));
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
