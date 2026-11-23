using Microsoft.Win32;

namespace Cirrova.Services;

public sealed class GameLocator
{
    private const string GameFolder = "7 Days To Die";

    public string? TryFindGameDirectory() => FindInSteamRoots(GetSteamRoots());

    public static bool IsValidGameDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return File.Exists(Path.Combine(directory, "7DaysToDie.exe"))
            || File.Exists(Path.Combine(directory, "7DaysToDie.x86_64"))
            || Directory.Exists(Path.Combine(directory, "7DaysToDie.app"));
    }

    public static string? FindInSteamRoots(IEnumerable<string> steamRoots)
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in steamRoots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            libraries.Add(root);

            var file = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(file))
            {
                continue;
            }

            try
            {
                foreach (var library in SteamLibraryParser.ExtractPaths(File.ReadAllText(file)))
                {
                    libraries.Add(library);
                }
            }
            catch (IOException)
            {
                // Steam might be rewriting this file; another library may still work.
            }
            catch (UnauthorizedAccessException)
            {
                // A library with restricted access must not prevent discovery elsewhere.
            }
        }

        foreach (var library in libraries)
        {
            var candidate = Path.Combine(library, "steamapps", "common", GameFolder);
            if (IsValidGameDirectory(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            foreach (var programFiles in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                     })
            {
                if (!string.IsNullOrWhiteSpace(programFiles))
                {
                    yield return Path.Combine(programFiles, "Steam");
                }
            }

            string? registryPath = null;
            try
            {
                registryPath = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")
                    ?.GetValue("SteamPath")?.ToString();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The normal Program Files paths remain available.
            }

            if (!string.IsNullOrWhiteSpace(registryPath))
            {
                yield return registryPath;
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(home, "Library", "Application Support", "Steam");
        }
        else if (OperatingSystem.IsLinux())
        {
            yield return Path.Combine(home, ".local", "share", "Steam");
            yield return Path.Combine(home, ".steam", "steam");
            yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam",
                ".local", "share", "Steam");
        }
    }
}
