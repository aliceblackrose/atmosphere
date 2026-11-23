using System.Text.Json;
using Cirrova.Models;

namespace Cirrova.Services;

public sealed class ModSourceStore
{
    private readonly ProfileStore _profiles;
    public ModSourceStore(ProfileStore profiles) => _profiles = profiles;

    public IReadOnlyList<ModSource> List(string profileId)
    {
        var file = IndexPath(profileId);
        if (!File.Exists(file)) return Array.Empty<ModSource>();
        try
        {
            return JsonSerializer.Deserialize<List<ModSource>>(File.ReadAllText(file))
                ?? throw new InvalidDataException("Mod source index is corrupt.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Mod source index is corrupt and was not overwritten.", ex);
        }
    }

    public void Record(string profileId, IEnumerable<string> modIds, string repository,
        string releaseTag, string assetName, string sha256)
    {
        GitHubReleaseService.ParseRepository("https://github.com/" + repository);
        if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
            throw new ArgumentException("SHA-256 must contain 64 hexadecimal characters.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var updates = new List<ModSource>();
        var installedAt = DateTime.UtcNow;
        foreach (var id in modIds)
        {
            if (!string.IsNullOrWhiteSpace(id) && ids.Add(id))
                updates.Add(new ModSource(id, repository, releaseTag, assetName, sha256, installedAt));
        }

        var records = List(profileId).Where(x => !ids.Contains(x.InternalName)).ToList();
        records.AddRange(updates);
        Write(profileId, records);
    }

    public void Forget(string profileId, IEnumerable<string> modIds)
    {
        var ids = modIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Write(profileId, List(profileId).Where(x => !ids.Contains(x.InternalName)).ToArray());
    }

    private string IndexPath(string profileId) =>
        Path.Combine(_profiles.GetDirectory(profileId), "mod-sources.json");

    private void Write(string profileId, IReadOnlyList<ModSource> data)
    {
        var file = IndexPath(profileId);
        var temporary = file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(data,
            new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, file, overwrite: true);
    }
}
