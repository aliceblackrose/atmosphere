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
    private const long MaxDownload = 512L * 1024 * 1024;
    private const long MaxMetadata = 2L * 1024 * 1024;
    private static readonly HttpClient SharedClient = NewClient();
    private readonly HttpClient _client;

    public GitHubReleaseService(HttpClient? client = null) => _client = client ?? SharedClient;

    private static HttpClient NewClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
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

    public async Task<GitHubRelease> GetLatestAsync(string githubUrl,
        CancellationToken cancellationToken = default)
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
                    size is >= 0 and <= MaxDownload && IsAllowedAssetUrl(repository, url))
                    assets.Add(new GitHubReleaseAsset(repository, name, url, size));
            }
        }

        var description = Read(root, "body");
        return new GitHubRelease(repository, Read(root, "tag_name"),
            Read(root, "name"), description.Length > 1800 ? description[..1800] : description,
            assets);
    }

    public async Task<DownloadResult> DownloadZipAsync(GitHubReleaseAsset asset,
        string destinationDirectory, IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAllowedAssetUrl(asset.Repository, asset.DownloadUrl) ||
            !asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            asset.Size is < 0 or > MaxDownload)
            throw new ArgumentException("Only ZIP assets from the selected GitHub Releases repository are allowed.");

        Directory.CreateDirectory(destinationDirectory);
        var path = Path.Combine(destinationDirectory, Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
            using var response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxDownload)
                throw new InvalidDataException("Download exceeds 512 MiB limit.");

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, useAsync: true);
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            var bytes = await CopyLimitedAsync(input, output, MaxDownload,
                (buffer, n, total) =>
                {
                    digest.AppendData(buffer, 0, n);
                    progress?.Report(total);
                }, cancellationToken);
            return new DownloadResult(path, Convert.ToHexString(digest.GetHashAndReset()), bytes);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
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

    private static async Task<long> CopyLimitedAsync(Stream input, Stream output, long limit,
        Action<byte[], int, long>? onChunk, CancellationToken token)
    {
        var buffer = new byte[81920];
        long total = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            total += count;
            if (total > limit) throw new InvalidDataException("Input exceeds configured size limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
            onChunk?.Invoke(buffer, count, total);
        }
        return total;
    }
}
