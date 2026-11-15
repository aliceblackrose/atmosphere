namespace Atmosphere.Models;

public sealed record GitHubReleaseAsset(
    string Repository, string Name, string DownloadUrl, long Size);

public sealed record GitHubRelease(
    string Repository, string Tag, string Title, string Description,
    IReadOnlyList<GitHubReleaseAsset> ZipAssets);

public sealed record DownloadResult(string FilePath, string Sha256, long Bytes);
