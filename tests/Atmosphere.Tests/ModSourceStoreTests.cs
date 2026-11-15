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
}
