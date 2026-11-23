using Atmosphere.Services;
using Xunit;

namespace Atmosphere.Tests;

public sealed class ModInventoryTests
{
    [Fact]
    public void Scan_RecognizesCurrentModInfoFormatAndDlls()
    {
        using var sandbox = new TempDirectory();
        var folder = Path.Combine(sandbox.Path, "Mods", "Example");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "ModInfo.xml"),
            "<xml><Name value=\"A_Test\"/><DisplayName value=\"A Test\"/><Version value=\"1.2.3\"/><Author value=\"Author\"/></xml>");
        File.WriteAllBytes(Path.Combine(folder, "example.dll"), new byte[] { 0 });

        var mod = Assert.Single(ModInventory.Scan(Path.Combine(sandbox.Path, "Mods")));
        Assert.True(mod.IsValid);
        Assert.Equal("A Test", mod.DisplayName);
        Assert.True(mod.ContainsDll);
        Assert.Equal("1.2.3", mod.Version);
    }

    [Fact]
    public void Scan_FlagsLegacyModInfoAndInvalidFolders()
    {
        using var sandbox = new TempDirectory();
        var a = Path.Combine(sandbox.Path, "Legacy");
        var b = Path.Combine(sandbox.Path, "Empty");
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);
        File.WriteAllText(Path.Combine(a, "ModInfo.xml"),
            "<xml><ModInfo><Name value=\"Old\"/></ModInfo></xml>");

        var mods = ModInventory.Scan(sandbox.Path);
        Assert.Equal(2, mods.Count);
        Assert.All(mods, x => Assert.False(x.IsValid));
        Assert.Contains(mods, x => x.IsLegacy);
    }

    [Fact]
    public void Scan_RejectsMalformedVersion()
    {
        using var sandbox = new TempDirectory();
        var folder = Path.Combine(sandbox.Path, "Example");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "ModInfo.xml"),
            "<xml><Name value=\"Example\"/><DisplayName value=\"Example\"/><Version value=\"bad\"/></xml>");
        Assert.False(Assert.Single(ModInventory.Scan(sandbox.Path)).IsValid);
    }

    [Fact]
    public void Scan_DisablesExternalEntities()
    {
        using var sandbox = new TempDirectory();
        var folder = Path.Combine(sandbox.Path, "Bad");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "ModInfo.xml"),
            "<!DOCTYPE xml [<!ENTITY data SYSTEM \"file:///etc/passwd\">]><xml><Name value=\"&data;\"/></xml>");

        var mod = Assert.Single(ModInventory.Scan(sandbox.Path));
        Assert.False(mod.IsValid);
    }

    [Fact]
    public void Scan_FlagsAllValidCaseInsensitiveDuplicatesButPreservesOtherWarnings()
    {
        using var sandbox = new TempDirectory();
        WriteMod(sandbox.Path, "First", "Same", "1.0");
        WriteMod(sandbox.Path, "Another", "sAME", "1.0");
        WriteMod(sandbox.Path, "Third", "same", "1.0");
        WriteMod(sandbox.Path, "Malformed", "Same", "invalid");
        WriteMod(sandbox.Path, "OnlyValid", "Solo", "1.0");
        WriteMod(sandbox.Path, "OnlyInvalid", "Solo", "invalid");

        var mods = ModInventory.Scan(sandbox.Path);
        Assert.Equal(mods.Select(mod => mod.FolderName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
            mods.Select(mod => mod.FolderName));
        Assert.Equal("OnlyValid", Assert.Single(mods, mod => mod.IsValid).FolderName);
        var duplicates = mods.Where(mod => !mod.IsValid &&
            mod.Name.Equals("same", StringComparison.OrdinalIgnoreCase) && mod.Version == "1.0").ToArray();
        Assert.Equal(3, duplicates.Length);
        Assert.All(duplicates, mod => Assert.Contains("Duplicate internal mod Name", mod.Warning));
        Assert.Contains("Version must be numeric", mods.Single(mod => mod.FolderName == "Malformed").Warning);
        Assert.Contains("Version must be numeric", mods.Single(mod => mod.FolderName == "OnlyInvalid").Warning);
    }

    [Fact]
    public void Scan_DoesNotConfuseCaseDistinctFoldersWithDifferentModNames()
    {
        using var sandbox = new TempDirectory();
        WriteMod(sandbox.Path, "Echo", "Unique", "1.0");
        // A case-insensitive volume cannot host both directories.
        if (Directory.Exists(Path.Combine(sandbox.Path, "echo"))) return;

        WriteMod(sandbox.Path, "echo", "Same", "1.0");
        WriteMod(sandbox.Path, "Other", "same", "1.0");

        var mods = ModInventory.Scan(sandbox.Path);
        Assert.Equal("Unique", Assert.Single(mods, mod => mod.IsValid).Name);
    }

    private static void WriteMod(string root, string folder, string name, string version)
    {
        var directory = Directory.CreateDirectory(Path.Combine(root, folder));
        File.WriteAllText(Path.Combine(directory.FullName, "ModInfo.xml"),
            $"""<xml><Name value="{name}"/><DisplayName value="{name}"/><Version value="{version}"/></xml>""");
    }
}

public sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atmosphere-" + Guid.NewGuid().ToString("N"));
    public TempDirectory() => Directory.CreateDirectory(Path);
    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, true);
    }
}
