using System.Diagnostics;
using Atmosphere.Models;

namespace Atmosphere.Services;

/// <summary>Runs a selected game copy with per-profile save and mod data.</summary>
public sealed class ProfileGameLauncher
{
    private readonly ProfileStore _profiles;
    private readonly ProfileModInstaller _installer;

    public ProfileGameLauncher(ProfileStore profiles)
    {
        _profiles = profiles;
        _installer = new ProfileModInstaller(profiles);
    }

    public ProcessStartInfo BuildStartInfo(string profileId, string? defaultGameDirectory, bool noEac)
    {
        var profile = _profiles.Get(profileId);
        var gameDirectory = profile.GameDirectory ?? defaultGameDirectory;
        if (!GameLocator.IsValidGameDirectory(gameDirectory))
            throw new InvalidOperationException("Select a valid game installation before launching a profile.");

        var codeMods = _installer.Installed(profileId).Where(mod => mod.ContainsDll).ToArray();
        if (codeMods.Length > 0 && !noEac)
            throw new InvalidOperationException("This profile contains DLL mods. Select 'Launch without EAC' to proceed.");

        var exe = FindGameExecutable(gameDirectory!);
        var info = new ProcessStartInfo(exe)
        {
            WorkingDirectory = gameDirectory!,
            UseShellExecute = false
        };
        info.ArgumentList.Add("-UserDataFolder=" + _profiles.GetDirectory(profileId));
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
