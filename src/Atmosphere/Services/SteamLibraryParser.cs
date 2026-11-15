using System.Text.RegularExpressions;

namespace Atmosphere.Services;

/// <summary>
/// Reads the explicit "path" properties from Steam's current libraryfolders.vdf format.
/// This is deliberately a narrow parser, not a general VDF interpreter.
/// </summary>
public static class SteamLibraryParser
{
    private static readonly Regex LibraryPathPattern = new(
        @"""path""\s*""(?<path>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> ExtractPaths(string vdfContents)
    {
        if (string.IsNullOrWhiteSpace(vdfContents))
        {
            return Array.Empty<string>();
        }

        return LibraryPathPattern.Matches(vdfContents)
            .Select(match => match.Groups["path"].Value.Replace(@"\\", @"\"))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
