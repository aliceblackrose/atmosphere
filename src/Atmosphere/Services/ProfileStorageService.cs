using Atmosphere.Models;

namespace Atmosphere.Services;

/// <summary>
/// Deletes only directories owned by Atmosphere under a selected profile.
/// Paths are generated from validated profile IDs; supplied GameDirectory is never
/// treated as an owned path. Junctions and symbolic links are rejected before deletion.
/// </summary>
public sealed class ProfileStorageService
{
    private readonly ProfileStore _profiles;
    public ProfileStorageService(ProfileStore profiles) => _profiles = profiles;

    public sealed record Usage(long TotalBytes, long GameCopyBytes, long BackupBytes);

    public Usage GetUsage(string profileId)
    {
        var root = ActiveProfilePath(profileId);
        var all = DirectoryBytes(root);
        var copy = DirectoryBytes(Path.Combine(root, "GameCopy"));
        var backups = DirectoryBytes(Path.Combine(root, "Backups"));
        return new Usage(all, copy, backups);
    }

    /// <summary>Deletes a GameCopy owned by this profile, not an external game folder.</summary>
    public void DeleteGameCopy(string profileId)
    {
        var profile = _profiles.Get(profileId);
        var root = ActiveProfilePath(profileId);
        var copy = Path.Combine(root, "GameCopy");
        if (!Directory.Exists(copy))
            throw new DirectoryNotFoundException("This profile has no managed GameCopy.");
        CheckSafeTree(copy);

        // If the profile currently launches the copy, detach it before deleting.
        // Other game directories are intentionally never changed or deleted.
        var selected = profile.GameDirectory is not null &&
            SamePath(profile.GameDirectory, copy);
        if (selected)
            _profiles.SetGameDirectory(profileId, null, profile.GameVersionNote);

        try
        {
            Directory.Delete(copy, recursive: true);
        }
        catch
        {
            // A running game may lock files on Windows; restore the selection
            // only when the copy still contains a valid executable.
            if (selected && GameLocator.IsValidGameDirectory(copy))
            {
                try { _profiles.SetGameDirectory(profileId, copy, profile.GameVersionNote); }
                catch { /* Keep original delete failure; report it to the UI. */ }
            }
            throw;
        }
    }

    public void ClearBackups(string profileId)
    {
        var root = ActiveProfilePath(profileId);
        var backups = Path.Combine(root, "Backups");
        if (!Directory.Exists(backups))
            return;
        CheckSafeTree(backups);
        Directory.Delete(backups, recursive: true);
    }

    /// <summary>
    /// Permanently removes a profile's owned workspace, including Mods, saves,
    /// backups and its internal GameCopy. It NEVER deletes GameDirectory elsewhere.
    /// Profiles are archived before deletion; if deletion itself fails, remaining
    /// files are retained under archived-profiles for recovery.
    /// </summary>
    public void DeleteProfile(string profileId)
    {
        var root = ActiveProfilePath(profileId);
        CheckSafeTree(root);
        _profiles.Archive(profileId);
        DeleteArchivedProfile(profileId);
    }

    public void DeleteArchivedProfile(string profileId)
    {
        ProfileStore.ValidateId(profileId);
        if (!_profiles.ListArchived().Any(x => x.Id == profileId))
            throw new KeyNotFoundException("Archived profile not found.");
        var root = ManagedPath("archived-profiles", profileId);
        CheckSafeTree(root);
        Directory.Delete(root, recursive: true);
    }

    private string ActiveProfilePath(string profileId)
    {
        _profiles.Get(profileId); // requires a known active profile
        var root = ManagedPath("profiles", profileId);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Profile data folder does not exist.");
        CheckRoot(root, "profiles");
        return root;
    }

    private string ManagedPath(string category, string profileId)
    {
        ProfileStore.ValidateId(profileId);
        if (category is not ("profiles" or "archived-profiles"))
            throw new ArgumentException("Not a profile storage folder.", nameof(category));
        var root = Path.GetFullPath(_profiles.DataRoot);
        var directory = Path.GetFullPath(Path.Combine(root, category, profileId));
        var expectedParent = Path.Combine(root, category);
        if (!SamePath(Path.GetDirectoryName(directory)!, expectedParent))
            throw new IOException("Profile directory escapes Atmosphere storage.");
        CheckRoot(directory, category);
        return directory;
    }

    private void CheckRoot(string directory, string category)
    {
        var root = Path.GetFullPath(_profiles.DataRoot);
        foreach (var path in new[] { root, Path.Combine(root, category), directory })
        {
            if (!Directory.Exists(path))
                continue;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing storage operation through a symbolic link or junction: " + path);
        }
    }

    private static void CheckSafeTree(string directory)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException("Profile storage directory is missing.");
        var pending = new Stack<string>();
        pending.Push(directory);
        while (pending.Count != 0)
        {
            var current = pending.Pop();
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing to delete a linked directory: " + current);
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing to delete linked file or directory: " + entry);
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push(entry);
            }
        }
    }

    private static long DirectoryBytes(string directory)
    {
        if (!Directory.Exists(directory)) return 0;
        CheckSafeTree(directory);
        long sum = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            sum = checked(sum + new FileInfo(path).Length);
        return sum;
    }

    private static bool SamePath(string left, string right)
    {
        static string Normalize(string value) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        return string.Equals(Normalize(left), Normalize(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
