using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Atmosphere.Models;

namespace Atmosphere.Services;

/// <summary>
/// Explicit, opt-in GitHub Releases lookup. No network operation occurs at startup.
/// Only HTTPS github.com repositories/assets are accepted as download sources.
/// </summary>
public sealed class GitHubReleaseService
{
    private static readonly long MaxDownload = ModArchiveLimits.Default.MaxArchiveBytes;
    private const long MaxMetadata = 2L * 1024 * 1024;
    private static readonly HttpClient SharedClient = NewClient();
    private readonly HttpClient _client;
    private readonly TimeSpan _metadataTimeout;
    private readonly TimeSpan _downloadTimeout;

    public GitHubReleaseService(HttpClient? client = null, TimeSpan? metadataTimeout = null,
        TimeSpan? downloadTimeout = null)
    {
        _client = client ?? SharedClient;
        _metadataTimeout = metadataTimeout ?? TimeSpan.FromSeconds(30);
        _downloadTimeout = downloadTimeout ?? TimeSpan.FromMinutes(45);
        static bool ValidTimeout(TimeSpan value) =>
            value > TimeSpan.Zero && value.TotalMilliseconds <= uint.MaxValue - 1;
        if (!ValidTimeout(_metadataTimeout)) throw new ArgumentOutOfRangeException(nameof(metadataTimeout));
        if (!ValidTimeout(_downloadTimeout)) throw new ArgumentOutOfRangeException(nameof(downloadTimeout));
    }

    private static HttpClient NewClient()
    {
        // Explicit operation deadlines below cover headers and the streamed response body.
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Atmosphere-ModLauncher/1.0");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static string ParseRepository(string input)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort)
            throw new ArgumentException("Enter a GitHub repository URL such as https://github.com/owner/repo.");

        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 2) throw new ArgumentException("URL must point to a GitHub repository.");
        var owner = parts[0];
        var repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? parts[1][..^4] : parts[1];
        static bool ValidPart(string value) =>
            value.Length is > 0 and <= 100 &&
            value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' or '.');
        if (!ValidPart(owner) || !ValidPart(repo))
            throw new ArgumentException("GitHub owner/repository name contains invalid characters.");
        return owner + "/" + repo;
    }

    public Task<IReadOnlyList<GitHubCatalogEntry>> SearchCatalogAsync(
        string keywords, CancellationToken cancellationToken = default) =>
        WithDeadlineAsync(token => SearchCatalogCoreAsync(keywords, token), _metadataTimeout, cancellationToken);

    private async Task<IReadOnlyList<GitHubCatalogEntry>> SearchCatalogCoreAsync(
        string keywords, CancellationToken cancellationToken)
    {
        var search = keywords.Trim();
        if (search.Length is < 2 or > 90)
            throw new ArgumentException("Enter 2–90 search characters.");

        var query = Uri.EscapeDataString("7 days to die " + search + " mod");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.github.com/search/repositories?q=" + query +
            "&sort=stars&order=desc&per_page=20");
        request.Headers.UserAgent.ParseAdd("Atmosphere-ModLauncher/1.0");
        using var response = await _client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxMetadata)
            throw new InvalidDataException("GitHub search response is too large.");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        await CopyLimitedAsync(input, buffer, MaxMetadata, null, cancellationToken);
        buffer.Position = 0;
        using var json = await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken);
        var results = new List<GitHubCatalogEntry>();
        if (!json.RootElement.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array) return results;

        foreach (var item in items.EnumerateArray())
        {
            var repo = item.TryGetProperty("full_name", out var fullName)
                ? fullName.GetString() ?? "" : "";
            try { ParseRepository("https://github.com/" + repo); }
            catch (ArgumentException) { continue; }
            var description = item.TryGetProperty("description", out var desc) &&
                desc.ValueKind == JsonValueKind.String ? desc.GetString() ?? "" : "";
            var stars = item.TryGetProperty("stargazers_count", out var rating) &&
                rating.TryGetInt32(out var count) ? count : 0;
            results.Add(new GitHubCatalogEntry(repo,
                description.Length > 300 ? description[..300] : description, stars));
        }
        return results;
    }

    public Task<GitHubRelease> GetLatestAsync(string githubUrl, CancellationToken cancellationToken = default) =>
        WithDeadlineAsync(token => GetLatestCoreAsync(githubUrl, token), _metadataTimeout, cancellationToken);

    private async Task<GitHubRelease> GetLatestCoreAsync(string githubUrl, CancellationToken cancellationToken)
    {
        var repository = ParseRepository(githubUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.github.com/repos/" + repository + "/releases/latest");
        request.Headers.UserAgent.ParseAdd("Atmosphere-ModLauncher/1.0");
        using var response = await _client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxMetadata)
            throw new InvalidDataException("GitHub release response is too large.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var limited = new MemoryStream();
        await CopyLimitedAsync(stream, limited, MaxMetadata, null, cancellationToken);
        limited.Position = 0;
        using var json = await JsonDocument.ParseAsync(limited, cancellationToken: cancellationToken);
        var root = json.RootElement;

        static string Read(JsonElement element, string name) =>
            element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() ?? "" : "";
        var assets = new List<GitHubReleaseAsset>();
        if (root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in list.EnumerateArray())
            {
                var name = Read(asset, "name");
                var url = Read(asset, "browser_download_url");
                var size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var n)
                    ? n : -1L;
                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                    size >= 0 && size <= MaxDownload && IsAllowedAssetUrl(repository, url))
                    assets.Add(new GitHubReleaseAsset(repository, name, url, size));
            }
        }

        var description = Read(root, "body");
        return new GitHubRelease(repository, Read(root, "tag_name"),
            Read(root, "name"), description.Length > 1800 ? description[..1800] : description,
            assets);
    }

    public Task<DownloadResult> DownloadZipAsync(GitHubReleaseAsset asset,
        string destinationDirectory, IProgress<long>? progress = null,
        CancellationToken cancellationToken = default) =>
        WithDeadlineAsync(token => DownloadZipCoreAsync(asset, destinationDirectory, progress, token),
            _downloadTimeout, cancellationToken);

    private async Task<DownloadResult> DownloadZipCoreAsync(GitHubReleaseAsset asset,
        string destinationDirectory, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        if (!IsAllowedAssetUrl(asset.Repository, asset.DownloadUrl) ||
            !asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            (asset.Size < 0 || asset.Size > MaxDownload))
            throw new ArgumentException("Only ZIP assets from the selected GitHub Releases repository are allowed.");

        Directory.CreateDirectory(destinationDirectory);
        var path = Path.Combine(destinationDirectory, Guid.NewGuid().ToString("N") + ".zip");
        var created = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
            using var response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxDownload)
                throw new InvalidDataException("Download exceeds the 16 GiB safety limit.");

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, useAsync: true);
            created = true;
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            var bytes = await CopyLimitedAsync(input, output, MaxDownload,
                (buffer, n, total) =>
                {
                    digest.AppendData(buffer, 0, n);
                    progress?.Report(total);
                }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes != asset.Size)
                throw new InvalidDataException("Download size differs from the selected GitHub asset.");
            return new DownloadResult(path, Convert.ToHexString(digest.GetHashAndReset()), bytes);
        }
        catch
        {
            if (created)
            {
                try { File.Delete(path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { /* Do not hide the original download error. */ }
            }
            throw;
        }
    }

    private static bool IsAllowedAssetUrl(string repository, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
            return false;
        return uri.AbsolutePath.StartsWith("/" + repository + "/releases/download/",
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<T> WithDeadlineAsync<T>(Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            return await operation(deadline.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The GitHub request timed out. Try again.", ex);
        }
    }

    private static async Task<long> CopyLimitedAsync(Stream input, Stream output, long limit,
        Action<byte[], int, long>? onChunk, CancellationToken token)
    {
        var buffer = new byte[81920];
        long total = 0;
        using var readDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        while (true)
        {
            readDeadline.CancelAfter(TimeSpan.FromMinutes(1));
            var count = await input.ReadAsync(buffer, readDeadline.Token);
            readDeadline.CancelAfter(Timeout.InfiniteTimeSpan);
            readDeadline.Token.ThrowIfCancellationRequested();
            if (count == 0) return total;
            total += count;
            if (total > limit) throw new InvalidDataException("Input exceeds configured size limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
            onChunk?.Invoke(buffer, count, total);
        }
    }
}
