using System.Net;
using Atmosphere.Models;
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

    [Fact]
    public async Task LatestRelease_DeadlineCoversResponseBody()
    {
        using var body = new WaitingResponseBody();
        using var client = UseBody(body);
        var service = new GitHubReleaseService(client, metadataTimeout: TimeSpan.FromMilliseconds(500));

        var check = service.GetLatestAsync("https://github.com/example/mod");
        await body.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<TimeoutException>(() => check.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(body.ReadCancelled);
    }

    [Fact]
    public async Task Download_UserCancellationRemovesTemporaryFile()
    {
        using var root = new TempDirectory();
        using var body = new WaitingResponseBody();
        using var client = UseBody(body);
        using var userCancellation = new CancellationTokenSource();
        var service = new GitHubReleaseService(client);
        var download = service.DownloadZipAsync(Asset, root.Path, cancellationToken: userCancellation.Token);

        await body.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(Directory.EnumerateFiles(root.Path));
        userCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(body.ReadCancelled);
        Assert.Empty(Directory.EnumerateFiles(root.Path));
    }

    [Fact]
    public async Task Download_WritesCompleteAssetAndReportsDigest()
    {
        using var root = new TempDirectory();
        byte[] content = [1, 2, 3];
        using var client = UseBody(new MemoryStream(content));
        var service = new GitHubReleaseService(client);

        var result = await service.DownloadZipAsync(Asset, root.Path);

        Assert.Equal(content.Length, result.Bytes);
        Assert.Equal(content, await File.ReadAllBytesAsync(result.FilePath));
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)), result.Sha256);
    }

    [Fact]
    public async Task Download_RejectsIncompleteAssetAndRemovesTemporaryFile()
    {
        using var root = new TempDirectory();
        using var client = UseBody(new MemoryStream(new byte[] { 1, 2 }));
        var service = new GitHubReleaseService(client);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadZipAsync(Asset, root.Path));
        Assert.Empty(Directory.EnumerateFiles(root.Path));
    }

    private static GitHubReleaseAsset Asset =>
        new("example/mod", "mod.zip", "https://github.com/example/mod/releases/download/v1/mod.zip", 3);

    private static HttpClient UseBody(Stream body) => new(new BodyHandler(body))
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private sealed class BodyHandler(Stream body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body)
        });
    }

    private sealed class WaitingResponseBody : MemoryStream
    {
        public TaskCompletionSource<bool> ReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ReadCancelled { get; private set; }
        public override bool CanSeek => false;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult(true);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException)
            {
                ReadCancelled = true;
                throw;
            }
            return 0;
        }
    }
}
