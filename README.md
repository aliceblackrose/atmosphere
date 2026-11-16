<p align="center"><img src="icon.png" width="128"/><h1>Atmosphere</h1></p>

**Atmosphere** is a source-available desktop mod launcher for **7 Days to Die**, built with C#/.NET 10 and Avalonia 11.

It manages modlets in independent profile workspaces, not in your Steam installation. The application is a **v1 preview** pending real-game smoke tests; build/test and package workflows run on GitHub Actions.

## Features

- Modern dark Fluent desktop interface with a live Overview dashboard, sidebar navigation, responsive cards, accessible control states, and the original Atmosphere artwork displayed as the window icon
- Windows desktop executable icon generated from the repository's `icon.png` for packaged builds
- Steam game detection, alternate library paths, custom game folders, vanilla launching
- Read-only inventory of existing game/user mods with modern `ModInfo.xml` validation
- Named profiles with separately stored mods, saves, and generated worlds
- Optional full isolated game copies for overhaul profiles (may require tens of gigabytes)
- Profile storage management: usage breakdown, open profile folder, delete owned game copies, clear backups, and permanently delete active or archived profiles with typed confirmation
- Multi-mod ZIP pack installation and multiple ZIP selection, with 16 GiB per-ZIP / 64 GiB unpacked limits, disk preflight, and backup-based replacement
- Launch a selected game copy using `-UserDataFolder=<profile>`, with explicit `-noeac` option
- User-initiated GitHub repository search, Releases lookup, ZIP downloads, SHA-256, and source tracking
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

## Download Atmosphere

Download ready-to-run release assets from **[GitHub Releases](https://github.com/aliceblackrose/atmosphere/releases)**. No .NET installation is needed for these self-contained packages.

- **Windows (x64):** `Atmosphere.exe` is available as a direct download, or get `Atmosphere-win-x64.zip` for the complete published folder. Windows is the primary supported platform.
- **Linux (x64):** download `Atmosphere-linux-x64.tar.gz`, extract it, and launch `./Atmosphere` (some Linux desktop dependencies may still be required).
- **macOS (Apple Silicon):** download `Atmosphere-osx-arm64.tar.gz`, extract it, and launch `./Atmosphere`. This is an unsigned preview binary, not a `.app` bundle; macOS may block it until you explicitly permit it.
- **Integrity:** `SHA256SUMS.txt` contains SHA-256 hashes of all downloadable binary assets.

Every successful push to `main` automatically creates a uniquely tagged **preview prerelease** (`preview-<run-number>`). For a versioned release, push a version tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

After the Windows, Linux and macOS build/test jobs pass, the workflow publishes the downloadable files and generated release notes for that tag. A manually dispatched package workflow or a feature-branch build still uploads temporary Actions artifacts but does not create a Release.

**Preview warning:** Actual game integration and graphical smoke tests are not automated yet. Windows remains the primary supported target.
## Desktop icon and branding

The original `icon.png` in the repository root is embedded in the app and displayed in the header, Windows title bar, and task switcher via Avalonia's window icon. Release packaging additionally generates `src/Atmosphere/Assets/Atmosphere.ico` for Windows executable metadata (Explorer/taskbar icon), using the same source image.

For a local Windows release build, install [Pillow](https://pillow.readthedocs.io/) and generate the icon before publishing:

```bash
python -m pip install Pillow
python tools/make_icon.py
dotnet publish src/Atmosphere/Atmosphere.csproj -c Release -r win-x64 --self-contained true
```

Running `dotnet run` still shows the in-app/window PNG icon even if Pillow isn't installed.

## Documentation

- [User guide](docs/USER-GUIDE.md)
- [Mod format](docs/MOD-FORMAT.md)
- [Profiles and launching](docs/LAUNCHING.md)
- [GitHub downloads](docs/DOWNLOADS.md)
- [Roadmap](docs/ROADMAP.md)

## Safety notes

The Steam game directory is never modified by Atmosphere's mod installation workflow. Other game-root mods can **still load**, so use a clean game directory for profile isolation. If you enable DLL code mods, launch without EAC when appropriate and use only mods from trusted publishers. The ZIP SHA-256 is provided for reference, not as a signature guarantee.

Atmosphere is an independent community project, not affiliated with The Fun Pimps, Valve, or third-party mod authors. **No license has been selected yet; the repository should not be described as open-source until a license is added.**
