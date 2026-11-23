namespace Cirrova.Models;

/// <summary>Informational download provenance, not a publisher identity guarantee.</summary>
public sealed record ModSource(
    string InternalName, string Repository, string ReleaseTag,
    string AssetName, string DownloadSha256, DateTime InstalledUtc);
