using System.Text.Json;
using Atmosphere.Models;

namespace Atmosphere.Services;

public sealed class ProfileStore
{
    public string DataRoot { get; }
    private readonly string _indexPath;

    public ProfileStore(string? root = null)
    {
        DataRoot = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Atmosphere");
        _indexPath = Path.Combine(DataRoot, "profiles.json");
    }

    public IReadOnlyList<ModProfile> List()
    {
        if (!File.Exists(_indexPath)) return Array.Empty<ModProfile>();
        try
        {
            return JsonSerializer.Deserialize<List<ModProfile>>(File.ReadAllText(_indexPath))
                ?? throw new InvalidDataException("Profile index is empty or corrupt.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Profile index is corrupt; it has not been overwritten.", ex);
        }
    }

    public ModProfile Create(string name)
    {
        ValidateName(name);
        var profile = new ModProfile { Name = name.Trim() };
        var folder = Path.Combine(DataRoot, "profiles", profile.Id);
        Directory.CreateDirectory(Path.Combine(folder, "Mods"));
        try
        {
            Write(List().Append(profile).ToArray());
        }
        catch
        {
            // Only remove the newly created empty workspace.
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            throw;
        }
        return profile;
    }

    public ModProfile Rename(string id, string name)
    {
        ValidateName(name);
        return Update(id, profile => profile with { Name = name.Trim() });
    }

    public ModProfile SetGameDirectory(string id, string? path, string? versionNote = null)
    {
        if (!string.IsNullOrWhiteSpace(path) && !GameLocator.IsValidGameDirectory(path))
            throw new ArgumentException("Select a game directory containing a supported executable.");
        return Update(id, profile => profile with
        {
            GameDirectory = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path),
            GameVersionNote = versionNote
        });
    }

    public ModProfile Get(string id)
    {
        ValidateId(id);
        return List().SingleOrDefault(profile => profile.Id == id)
            ?? throw new KeyNotFoundException("The selected profile no longer exists.");
    }

    public string GetDirectory(string id)
    {
        Get(id);
        return Path.Combine(DataRoot, "profiles", id);
    }

    public void Archive(string id)
    {
        Get(id);
        var source = GetDirectory(id);
        var archived = Path.Combine(DataRoot, "archived-profiles", id);
        if (Directory.Exists(archived)) throw new IOException("An archive with this profile ID already exists.");

        Directory.CreateDirectory(Path.GetDirectoryName(archived)!);
        var moved = false;
        try
        {
            if (Directory.Exists(source))
            {
                Directory.Move(source, archived);
                moved = true;
            }
            Write(List().Where(p => p.Id != id).ToArray());
        }
        catch
        {
            if (moved && Directory.Exists(archived) && !Directory.Exists(source))
                Directory.Move(archived, source);
            throw;
        }
    }

    private ModProfile Update(string id, Func<ModProfile, ModProfile> change)
    {
        var profiles = List().ToList();
        var index = profiles.FindIndex(p => p.Id == id);
        if (index < 0) throw new KeyNotFoundException("The selected profile no longer exists.");
        var updated = change(profiles[index]);
        profiles[index] = updated;
        Write(profiles);
        return updated;
    }

    private void Write(IReadOnlyList<ModProfile> profiles)
    {
        Directory.CreateDirectory(DataRoot);
        var temporary = _indexPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, _indexPath, overwrite: true);
    }

    public static void ValidateId(string id)
    {
        if (!Guid.TryParseExact(id, "N", out var guid) || guid.ToString("N") != id)
            throw new ArgumentException("Invalid profile identifier.", nameof(id));
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80)
            throw new ArgumentException("Profile name must contain 1–80 characters.", nameof(name));
    }
}
