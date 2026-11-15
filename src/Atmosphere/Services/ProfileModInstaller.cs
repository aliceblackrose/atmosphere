using System.IO.Compression;
using System.Text.RegularExpressions;
using Atmosphere.Models;

namespace Atmosphere.Services;

/// <summary>
/// Installs current-format modlets ONLY under the application's profile workspace.
/// No operation in this class writes to the original game installation.
/// </summary>
public sealed class ProfileModInstaller
{
    private readonly ModArchiveLimits _limits;
    private static readonly Regex SafeFolder = new(
        @"^[A-Za-z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly ProfileStore _profiles;

    public ProfileModInstaller(ProfileStore profiles, ModArchiveLimits? limits = null)
    {
        _profiles = profiles;
        _limits = limits ?? ModArchiveLimits.Default;
        _limits.Validate();
    }

    private string GetSafeDirectory(string profileId)
    {
        var profile = _profiles.GetDirectory(profileId);
        // Do not follow an externally replaced Mods, backup, or staging symlink.
        if (!Directory.Exists(profile) ||
            (File.GetAttributes(profile) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Profile workspace is missing or linked to another location.");

        foreach (var child in new[] { "Mods", "DisabledMods", "Backups", ".staging" })
        {
            var path = Path.Combine(profile, child);
            if (Directory.Exists(path) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked profile directories are not supported: " + child);
        }
        return profile;
    }

    public IReadOnlyList<ModRecord> Installed(string profileId) =>
        ModInventory.Scan(Path.Combine(GetSafeDirectory(profileId), "Mods"));

    public IReadOnlyList<ModRecord> Disabled(string profileId) =>
        ModInventory.Scan(Path.Combine(GetSafeDirectory(profileId), "DisabledMods"));

    public IReadOnlyList<ModRecord> InstallZip(string profileId, string zipPath, bool replaceExisting = false)
    {
        var profileRoot = GetSafeDirectory(profileId);
        if (!File.Exists(zipPath)) throw new FileNotFoundException("ZIP archive not found.", zipPath);
        var zipBytes = new FileInfo(zipPath).Length;
        if (zipBytes > _limits.MaxArchiveBytes)
            throw new InvalidDataException(
                $"ZIP size ({zipBytes / ModArchiveLimits.GiB:F2} GiB) exceeds the " +
                $"{_limits.MaxArchiveBytes / ModArchiveLimits.GiB:F2} GiB safety limit. " +
                "Split the modpack into smaller ZIPs.");

        var staging = Path.Combine(profileRoot, ".staging", Guid.NewGuid().ToString("N"));
        var expanded = Path.Combine(staging, "expanded");
        var modsRoot = Path.Combine(profileRoot, "Mods");
        var installed = new List<ModRecord>();
        var changes = new List<(string Target, string? Backup)>();
        Directory.CreateDirectory(expanded);

        try
        {
            ExtractValidated(zipPath, expanded);

            var modRoots = Directory.EnumerateFiles(expanded, "ModInfo.xml", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                MaxRecursionDepth = 12
            }).Select(Path.GetDirectoryName).OfType<string>().ToArray();

            if (modRoots.Length == 0)
                throw new InvalidDataException("ZIP contains no modlet with ModInfo.xml.");

            var mods = modRoots.Select(ModInventory.ReadMod).ToArray();
            var conflicts = mods.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1).ToArray();
            if (conflicts.Length > 0 || mods.Any(x => !x.IsValid))
                throw new InvalidDataException("ZIP has duplicate IDs, legacy metadata, or invalid ModInfo.xml.");

            foreach (var mod in mods)
            {
                if (!SafeFolder.IsMatch(mod.Name))
                    throw new InvalidDataException("Invalid internal mod Name.");
                var target = Path.Combine(modsRoot, mod.Name);
                var disabled = Path.Combine(profileRoot, "DisabledMods", mod.Name);
                if (Directory.Exists(disabled))
                    throw new IOException("Mod is already installed but disabled: " + mod.Name);
                if (Directory.Exists(target) && !replaceExisting)
                    throw new IOException("Mod is already installed: " + mod.Name);

                // Never follow or replace a directory link in an existing profile.
                if (Directory.Exists(target) &&
                    (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing to overwrite a linked mod directory.");
            }

            Directory.CreateDirectory(modsRoot);
            foreach (var mod in mods)
            {
                var target = Path.Combine(modsRoot, mod.Name);
                string? backup = null;
                if (Directory.Exists(target))
                {
                    backup = Path.Combine(profileRoot, "Backups", "Replaced",
                        DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N"),
                        mod.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    Directory.Move(target, backup);
                }

                try
                {
                    Directory.Move(mod.DirectoryPath, target);
                    changes.Add((target, backup));
                    installed.Add(ModInventory.ReadMod(target));
                }
                catch
                {
                    if (backup is not null && !Directory.Exists(target))
                        Directory.Move(backup, target);
                    throw;
                }
            }

            return installed;
        }
        catch
        {
            foreach (var change in changes.AsEnumerable().Reverse())
            {
                if (Directory.Exists(change.Target))
                    Directory.Move(change.Target,
                        Path.Combine(staging, "rollback-" + Guid.NewGuid().ToString("N")));
                if (change.Backup is not null && Directory.Exists(change.Backup))
                    Directory.Move(change.Backup, change.Target);
            }
            throw;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    public void SetEnabled(string profileId, string internalId, bool enabled)
    {
        ValidateInternalName(internalId);
        var root = GetSafeDirectory(profileId);
        var from = Path.Combine(root, enabled ? "DisabledMods" : "Mods", internalId);
        var to = Path.Combine(root, enabled ? "Mods" : "DisabledMods", internalId);
        EnsureManagedDirectory(from);
        if (Directory.Exists(to)) throw new IOException("Target mod folder already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        Directory.Move(from, to);
    }

    public string ArchiveMod(string profileId, string internalId, bool disabled = false)
    {
        ValidateInternalName(internalId);
        var root = GetSafeDirectory(profileId);
        var from = Path.Combine(root, disabled ? "DisabledMods" : "Mods", internalId);
        EnsureManagedDirectory(from);
        var backup = Path.Combine(root, "Backups", "Removed",
            DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N"),
            internalId);
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        Directory.Move(from, backup);
        return backup;
    }

    private static void ValidateInternalName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !SafeFolder.IsMatch(name))
            throw new ArgumentException("Invalid mod folder name.", nameof(name));
    }

    private static void EnsureManagedDirectory(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Mod is not installed: " + path);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Refusing to operate on a linked mod directory.");
    }

    private void ExtractValidated(string zipPath, string destination)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > _limits.MaxEntries)
            throw new InvalidDataException(
                $"Archive contains too many entries (maximum {_limits.MaxEntries:N0}).");

        // Check archive headers and destination space before writing any extracted files.
        // A malicious ZIP can lie about its sizes, so streaming checks below are retained.
        long declaredTotal = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length < 0 || entry.Length > _limits.MaxSingleFileBytes ||
                declaredTotal > _limits.MaxExpandedBytes - entry.Length)
                throw new InvalidDataException(
                    $"Expanded ZIP exceeds a safety limit (" +
                    $"{_limits.MaxExpandedBytes / ModArchiveLimits.GiB:F0} GiB total, " +
                    $"{_limits.MaxSingleFileBytes / ModArchiveLimits.GiB:F0} GiB per file).");
            declaredTotal += entry.Length;
        }

        var driveRoot = Path.GetPathRoot(Path.GetFullPath(destination));
        if (!string.IsNullOrEmpty(driveRoot))
        {
            var available = new DriveInfo(driveRoot).AvailableFreeSpace;
            var reserve = Math.Min(declaredTotal / 20, ModArchiveLimits.GiB);
            if (available < declaredTotal + reserve)
                throw new IOException(
                    $"Not enough free disk space to unpack this modpack. " +
                    $"Need about {(declaredTotal + reserve) / ModArchiveLimits.GiB:F2} GiB; " +
                    $"available {available / ModArchiveLimits.GiB:F2} GiB.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrEmpty(relative)) continue;
            if (relative.StartsWith('/') || relative.Contains(':') ||
                relative.Split('/').Any(s => s is "." or ".." || s.Length == 0 && !relative.EndsWith('/')))
                throw new InvalidDataException("Unsafe archive entry path: " + entry.FullName);

            var unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixMode == 0xA000) // Symbolic link
                throw new InvalidDataException("Archives containing symbolic links are not supported.");

            var target = Path.GetFullPath(Path.Combine(destination,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("Archive path escapes extraction directory.");
            if (!seen.Add(target)) throw new InvalidDataException("Duplicate archive entry: " + entry.FullName);

            if (relative.EndsWith('/'))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            if (entry.Length > _limits.MaxSingleFileBytes ||
                total > _limits.MaxExpandedBytes - entry.Length)
                throw new InvalidDataException("Archive exceeds permitted extraction size.");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var source = entry.Open();
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long fileBytes = 0;
            int count;
            while ((count = source.Read(buffer)) != 0)
            {
                fileBytes += count;
                total += count;
                if (fileBytes > _limits.MaxSingleFileBytes || total > _limits.MaxExpandedBytes)
                    throw new InvalidDataException("Archive exceeded extraction size limits.");
                output.Write(buffer, 0, count);
            }
        }
    }
}
