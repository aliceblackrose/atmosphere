using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Atmosphere.Models;

namespace Atmosphere.Services;

/// <summary>Read-only inventory of immediate modlet directories. No game files are changed.</summary>
public static class ModInventory
{
    private const long MaxMetadataBytes = 1024 * 1024;
    private static readonly Regex ValidName = new(
        @"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ValidVersion = new(
        @"^[0-9]+\.[0-9]+(\.[0-9]+){0,2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<ModRecord> Scan(string? modsDirectory)
    {
        if (string.IsNullOrWhiteSpace(modsDirectory) || !Directory.Exists(modsDirectory))
            return Array.Empty<ModRecord>();

        var results = new List<ModRecord>();
        var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in Directory.EnumerateDirectories(modsDirectory).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                var mod = ReadMod(directory);
                results.Add(mod);
                if (mod.IsValid)
                    nameCounts[mod.Name] = nameCounts.GetValueOrDefault(mod.Name) + 1;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
            {
                results.Add(Invalid(directory, "Could not read mod directory: " + ex.Message));
            }
        }

        for (var i = 0; i < results.Count; i++)
        {
            var mod = results[i];
            if (mod.IsValid && nameCounts[mod.Name] > 1)
                results[i] = mod with { IsValid = false, Warning = "Duplicate internal mod Name in this Mods folder." };
        }
        return results.AsReadOnly();
    }

    public static ModRecord ReadMod(string directory)
    {
        if (!Directory.Exists(directory)) return Invalid(directory, "Mod folder not found.");
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            return Invalid(directory, "Linked directories are not supported.");

        var path = Path.Combine(directory, "ModInfo.xml");
        if (!File.Exists(path)) return Invalid(directory, "Missing ModInfo.xml at mod folder root.");

        try
        {
            if (new FileInfo(path).Length > MaxMetadataBytes)
                return Invalid(directory, "ModInfo.xml is too large.");

            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxMetadataBytes
            });
            var root = XDocument.Load(reader).Root;
            if (root is null || !root.Name.LocalName.Equals("xml", StringComparison.OrdinalIgnoreCase))
                return Invalid(directory, "Invalid ModInfo.xml root.");

            var legacyFields = root.Elements().FirstOrDefault(x =>
                x.Name.LocalName.Equals("ModInfo", StringComparison.OrdinalIgnoreCase));
            var legacy = legacyFields is not null;
            var fields = legacyFields ?? root;
            string Value(string name) => fields.Elements().FirstOrDefault(x =>
                x.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Attribute("value")?.Value.Trim() ?? "";

            var id = Value("Name");
            var display = Value("DisplayName");
            var version = Value("Version");
            var warning = legacy ? "Legacy ModInfo format; current game versions require v2." :
                !ValidName.IsMatch(id) ? "Missing or invalid internal Name (letters, digits, _ and - only)." :
                string.IsNullOrWhiteSpace(display) ? "Missing DisplayName required by current game versions." :
                string.IsNullOrWhiteSpace(version) ? "Missing Version; compatibility cannot be determined." :
                !ValidVersion.IsMatch(version) ? "Version must be numeric major.minor[.build[.revision]]." : "";
            var hasDll = Directory.EnumerateFiles(directory, "*.dll", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }).Any();

            return new ModRecord(Path.GetFileName(directory), directory, id, display, version,
                Value("Author"), Value("Description"), warning.Length == 0,
                legacy, hasDll, warning);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return Invalid(directory, "Invalid ModInfo.xml: " + ex.Message);
        }
    }

    private static ModRecord Invalid(string directory, string warning) =>
        new(Path.GetFileName(directory), directory, "", Path.GetFileName(directory),
            "", "", "", false, false, false, warning);

    public static string GetDefaultUserModsDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userData = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "7DaysToDie")
            : OperatingSystem.IsMacOS()
                ? Path.Combine(home, "Library", "Application Support", "7DaysToDie")
                : Path.Combine(home, ".local", "share", "7DaysToDie");
        return Path.Combine(userData, "Mods");
    }
}
