using Atmosphere.Services;
using Xunit;

namespace Atmosphere.Tests;

public sealed class ModSourceStoreTests
{
    [Fact]
    public void RecordAndForget_PersistsRepositoryProvenance()
    {
        using var root = new TempDirectory();
        var profiles = new ProfileStore(root.Path);
        var p = profiles.Create("Test");
        var service = new ModSourceStore(profiles);
        service.Record(p.Id, new[] { "TestMod" }, "owner/repo", "v1.2",
            "mod.zip", new string('A', 64));
        var source = Assert.Single(service.List(p.Id));
        Assert.Equal("owner/repo", source.Repository);
        Assert.Equal("v1.2", source.ReleaseTag);
        service.Forget(p.Id, new[] { "TestMod" });
        Assert.Empty(service.List(p.Id));
    }

    [Fact]
    public void Record_ReplacesCaseInsensitiveIdsAndRetainsOtherSourcesInOrder()
    {
        using var root = new TempDirectory();
        var profiles = new ProfileStore(root.Path);
        var profile = profiles.Create("Test");
        var service = new ModSourceStore(profiles);
        var hash = new string('A', 64);
        service.Record(profile.Id, ["Keep", "Replace"], "owner/repo", "v1", "mod.zip", hash);
        service.Record(profile.Id, ["replace", "REPLACE", "", " ", "New"], "owner/repo", "v2",
            "mod.zip", hash);

        var sources = service.List(profile.Id);
        Assert.Equal(new[] { "Keep", "replace", "New" }, sources.Select(source => source.InternalName));
        Assert.Equal("v1", sources[0].ReleaseTag);
        Assert.All(sources.Skip(1), source => Assert.Equal("v2", source.ReleaseTag));
    }
}
