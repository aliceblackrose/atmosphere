namespace Atmosphere.Models;

/// <summary>A profile's working data and mods live outside the Steam game directory.</summary>
public sealed record ModProfile
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "New profile";
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public string? GameDirectory { get; init; }
    public string? GameVersionNote { get; init; }
}
