namespace Cirrova.Services;

/// <summary>
/// User-selected ZIPs may be large modpacks. Keep bounded limits to defend against
/// damaged or malicious archives. Limits apply to each individual ZIP.
/// </summary>
public sealed record ModArchiveLimits(
    long MaxArchiveBytes, long MaxExpandedBytes, long MaxSingleFileBytes, int MaxEntries)
{
    public const long GiB = 1024L * 1024 * 1024;

    // Supports typical large overhaul packs without allowing unlimited extraction.
    public static ModArchiveLimits Default { get; } =
        new(16L * GiB, 64L * GiB, 16L * GiB, 50000);

    public void Validate()
    {
        if (MaxArchiveBytes <= 0 || MaxExpandedBytes <= 0 ||
            MaxSingleFileBytes <= 0 || MaxSingleFileBytes > MaxExpandedBytes ||
            MaxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(ModArchiveLimits),
                "Archive limits must be positive and internally consistent.");
    }
}
