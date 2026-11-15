# Atmosphere

**Atmosphere** is a source-available desktop mod launcher for **7 Days to Die**, built with C#/.NET 10 and Avalonia 11.

It manages modlets in independent profile workspaces, not in your Steam installation. The application is a **v1 preview** pending real-game smoke tests; build/test and package workflows run on GitHub Actions.

## Features

- Steam game detection, alternate library paths, custom game folders, vanilla launching
- Read-only inventory of existing game/user mods with modern `ModInfo.xml` validation
- Named profiles with separately stored mods, saves, and generated worlds
- Optional full isolated game copies for overhaul profiles (may require tens of gigabytes)
- Safeguarded ZIP modlet installation and backup-based replacement, enabling, disabling, and archiving
- Launch a selected game copy using `-UserDataFolder=<profile>`, with explicit `-noeac` option
- User-initiated GitHub Releases lookup, ZIP asset selection, download limits, and SHA-256 reporting
- Windows and Linux automated build/tests; Windows, Linux, and macOS preview package workflows

## Run from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then:

```bash
git clone https://github.com/aliceblackrose/atmosphere.git
cd atmosphere
dotnet run --project src/Atmosphere/Atmosphere.csproj
```

To run tests:

```bash
dotnet test tests/Atmosphere.Tests/Atmosphere.Tests.csproj
```

GitHub Actions artifacts provide self-contained platform-specific preview executables on working feature branches. Download from the **Package desktop preview** workflow's artifact list. Windows is the primary supported target until manual platform testing.

## Documentation

- [User guide](docs/USER-GUIDE.md)
- [Mod format](docs/MOD-FORMAT.md)
- [Profiles and launching](docs/LAUNCHING.md)
- [GitHub downloads](docs/DOWNLOADS.md)
- [Roadmap](docs/ROADMAP.md)

## Safety notes

The Steam game directory is never modified by Atmosphere's mod installation workflow. Other game-root mods can **still load**, so use a clean game directory for profile isolation. If you enable DLL code mods, launch without EAC when appropriate and use only mods from trusted publishers. The ZIP SHA-256 is provided for reference, not as a signature guarantee.

Atmosphere is an independent community project, not affiliated with The Fun Pimps, Valve, or third-party mod authors. **No license has been selected yet; the repository should not be described as open-source until a license is added.**
