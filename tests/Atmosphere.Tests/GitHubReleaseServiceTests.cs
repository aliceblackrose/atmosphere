using Atmosphere.Services;
using Xunit;

namespace Atmosphere.Tests;

public sealed class GitHubReleaseServiceTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo", "owner/repo")]
    [InlineData("https://github.com/owner/repo.git", "owner/repo")]
    [InlineData("https://github.com/owner/repo/", "owner/repo")]
    public void ParseRepository_ValidGithubUrls(string url, string expected) =>
        Assert.Equal(expected, GitHubReleaseService.ParseRepository(url));

    [Theory]
    [InlineData("http://github.com/a/b")]
    [InlineData("https://evil.github.com/a/b")]
    [InlineData("https://github.com/a/b/releases")]
    [InlineData("https://github.com/a/b?download=true")]
    [InlineData("https://github.com@attacker.example/a/b")]
    public void ParseRepository_RejectsUntrustedUrls(string url) =>
        Assert.Throws<ArgumentException>(() => GitHubReleaseService.ParseRepository(url));
}
