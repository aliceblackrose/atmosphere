using System.Diagnostics;

namespace Cirrova.Services;

/// <summary>Runs a selected game copy with per-profile save and mod data.</summary>
public sealed class ProfileGameLauncher
{
    private readonly ProfileStore _profiles;

    public ProfileGameLauncher(ProfileStore profiles)
    {
        _profiles = profiles;
    }

    public ProcessStartInfo BuildStartInfo(string profileId, string? defaultGameDirectory, bool noEac)
    {
        var profile = _profiles.Get(profileId);
        var gameDirectory = profile.GameDirectory ?? defaultGameDirectory;
        if (!GameLocator.IsValidGameDirectory(gameDirectory))
            throw new InvalidOperationException("Select a valid game installation before launching a profile.");

        var profileDirectory = _profiles.GetDirectory(profileId);
        RequireUnlinkedDirectory(profileDirectory, allowMissing: false);
        var profileMods = Path.Combine(profileDirectory, "Mods");
        RequireUnlinkedDirectory(profileMods, allowMissing: true);

        if (!noEac && (ContainsDll(profileMods) || ContainsDll(Path.Combine(gameDirectory!, "Mods"))))
            throw new InvalidOperationException(
                "The profile or game folder contains DLL mods. Select 'Launch without EAC' to proceed.");

        var exe = FindGameExecutable(gameDirectory!);
        var info = new ProcessStartInfo(exe)
        {
            WorkingDirectory = gameDirectory!,
            UseShellExecute = false
        };
        info.ArgumentList.Add("-UserDataFolder=" + profileDirectory);
        if (noEac) info.ArgumentList.Add("-noeac");
        return info;
    }

    public Process Launch(string profileId, string? defaultGameDirectory, bool noEac)
    {
        var info = BuildStartInfo(profileId, defaultGameDirectory, noEac);
        Directory.CreateDirectory(Path.Combine(_profiles.GetDirectory(profileId), "Mods"));
        return Process.Start(info) ??
            throw new InvalidOperationException("The game process could not be started.");
    }

    private static bool RequireUnlinkedDirectory(string path, bool allowMissing)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (IOException ex) when (allowMissing && ex is (FileNotFoundException or DirectoryNotFoundException))
        {
            return false;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
            throw new IOException("Expected a regular directory at: " + path);
        return true;
    }

    private static bool ContainsDll(string modsDirectory)
    {
        try
        {
            if (!RequireUnlinkedDirectory(modsDirectory, allowMissing: true))
                return false;

            var pending = new Stack<string>();
            pending.Push(modsDirectory);
            while (pending.TryPop(out var directory))
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Linked mod files and directories cannot be checked: " + entry);
                    if ((attributes & FileAttributes.Directory) != 0)
                        pending.Push(entry);
                    else if (string.Equals(Path.GetExtension(entry), ".dll", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Could not verify mods for a normal launch. Check the mod folders before proceeding: " + ex.Message, ex);
        }
    }

    public static string FindGameExecutable(string directory)
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { "7DaysToDie.exe" }
            : OperatingSystem.IsMacOS()
                ? new[] { Path.Combine("7DaysToDie.app", "Contents", "MacOS", "7DaysToDie") }
                : new[] { "7DaysToDie.x86_64" };

        foreach (var relative in candidates)
        {
            var candidate = Path.Combine(directory, relative);
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException(
            "No supported game executable found for this operating system. " +
            "Mac launch layouts and Proton installs may need manual setup.");
    }

    public static int CountGameRootMods(string? gameDirectory) =>
        GameLocator.IsValidGameDirectory(gameDirectory)
            ? ModInventory.Scan(Path.Combine(gameDirectory!, "Mods")).Count
            : 0;
}
