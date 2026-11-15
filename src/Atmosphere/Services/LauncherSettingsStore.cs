using System.Text.Json;
using Atmosphere.Models;

namespace Atmosphere.Services;

public sealed class LauncherSettingsStore
{
    private readonly string _directory;
    private readonly string _path;

    public LauncherSettingsStore(string? directory = null)
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        _directory = directory ?? Path.Combine(baseDirectory, "Atmosphere");
        _path = Path.Combine(_directory, "settings.json");
    }

    public LauncherSettings Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(_path))
                    ?? new LauncherSettings()
                : new LauncherSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Keep the launcher usable if settings are missing, damaged, or locked.
            return new LauncherSettings();
        }
    }

    public void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(_directory);

        var temporary = _path + ".tmp";
        var content = JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(temporary, content);
        File.Move(temporary, _path, overwrite: true);
    }
}
