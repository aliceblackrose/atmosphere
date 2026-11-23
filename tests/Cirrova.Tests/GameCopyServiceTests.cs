using Cirrova.Services;
using Xunit;

namespace Cirrova.Tests;

public sealed class GameCopyServiceTests
{
    [Fact]
    public async Task CopiesGameWithoutChangingOriginalAndAssignsProfile()
    {
        using var sandbox = new TempDirectory();
        var source = Path.Combine(sandbox.Path, "steam", "7 Days To Die");
        Directory.CreateDirectory(source);
        if (OperatingSystem.IsMacOS())
            Directory.CreateDirectory(Path.Combine(source, "7DaysToDie.app"));
        else
            File.WriteAllText(Path.Combine(source,
                OperatingSystem.IsWindows() ? "7DaysToDie.exe" : "7DaysToDie.x86_64"), "binary");
        Directory.CreateDirectory(Path.Combine(source, "Mods", "ExistingMod"));
        File.WriteAllText(Path.Combine(source, "Mods", "ExistingMod", "readme.txt"), "original");

        var store = new ProfileStore(Path.Combine(sandbox.Path, "app"));
        var profile = store.Create("Overhaul");
        var target = await new GameCopyService(store).CreateCopyAsync(profile.Id, source);
        Assert.True(Directory.Exists(target));
        Assert.Equal(target, store.Get(profile.Id).GameDirectory);
        Assert.Equal("original", File.ReadAllText(Path.Combine(source, "Mods", "ExistingMod", "readme.txt")));
        Assert.Equal("original", File.ReadAllText(Path.Combine(target, "Mods", "ExistingMod", "readme.txt")));
        File.WriteAllText(Path.Combine(target, "Mods", "ExistingMod", "readme.txt"), "new");
        Assert.Equal("original", File.ReadAllText(Path.Combine(source, "Mods", "ExistingMod", "readme.txt")));
    }
}
