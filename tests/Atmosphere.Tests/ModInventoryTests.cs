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
    public void Scan_FlagsDuplicateInternalNames()
    {
        using var sandbox = new TempDirectory();
        foreach (var name in new[] { "One", "Two" })
        {
            var directory = Path.Combine(sandbox.Path, name);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "ModInfo.xml"),
                "<xml><Name value=\"Same\"/><DisplayName value=\"Same\"/><Version value=\"1.0\"/></xml>");
        }

        Assert.All(ModInventory.Scan(sandbox.Path), mod => Assert.False(mod.IsValid));
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
