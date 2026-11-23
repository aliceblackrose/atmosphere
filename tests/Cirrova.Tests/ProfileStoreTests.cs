using Cirrova.Services;
using Xunit;

namespace Cirrova.Tests;

public sealed class ProfileStoreTests
{
    [Fact]
    public void CreateRenameAndArchive_PreservesProfileData()
    {
        using var directory = new TempDirectory();
        var store = new ProfileStore(directory.Path);
        var p = store.Create("Survival");
        Assert.Single(store.List());

        store.Rename(p.Id, "Challenge");
        Assert.Equal("Challenge", store.Get(p.Id).Name);
        var workspace = store.GetDirectory(p.Id);
        File.WriteAllText(Path.Combine(workspace, "custom.txt"), "keep me");

        store.Archive(p.Id);
        Assert.Empty(store.List());
        Assert.Equal("keep me", File.ReadAllText(Path.Combine(
            directory.Path, "archived-profiles", p.Id, "custom.txt")));
        Assert.Single(store.ListArchived());

        var restored = store.Restore(p.Id);
        Assert.Equal("Challenge", restored.Name);
        Assert.Single(store.List());
        Assert.Empty(store.ListArchived());
        Assert.Equal("keep me", File.ReadAllText(Path.Combine(
            store.GetDirectory(p.Id), "custom.txt")));
    }

    [Fact]
    public void RejectsTraversalAndCorruptIndex()
    {
        using var directory = new TempDirectory();
        var store = new ProfileStore(directory.Path);
        Assert.Throws<ArgumentException>(() => store.GetDirectory("../outside"));
        File.WriteAllText(Path.Combine(directory.Path, "profiles.json"), "{bad json");
        Assert.Throws<InvalidDataException>(() => store.List());
    }
}
