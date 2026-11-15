using System.Diagnostics;

namespace Atmosphere.Services;

public static class SteamLauncher
{
    // Steam app ID for 7 Days to Die. Uses standard Steam launch settings.
    private const string LaunchUri = "steam://rungameid/251570";

    public static void LaunchVanilla()
    {
        // Shell handlers may dispatch a URL successfully without returning a Process.
        // An exception, not a null Process, indicates an immediate dispatch failure.
        Process.Start(new ProcessStartInfo
        {
            FileName = LaunchUri,
            UseShellExecute = true
        });
    }
}
