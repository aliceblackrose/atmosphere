<div align="center">
  <img src="icon.png" alt="Cirrova icon" width="116" />

  # Cirrova

  **Your wasteland. Your rules.**

  A modern, profile-based mod launcher for **7 Days to Die**.

  [![Build & test](https://github.com/aliceblackrose/cirrova/actions/workflows/ci.yml/badge.svg)](https://github.com/aliceblackrose/cirrova/actions/workflows/ci.yml)
  [![Desktop packages](https://github.com/aliceblackrose/cirrova/actions/workflows/package.yml/badge.svg)](https://github.com/aliceblackrose/cirrova/actions/workflows/package.yml)
  ![C#](https://img.shields.io/badge/Source-C%23-6d60d9)
  ![.NET 10](https://img.shields.io/badge/.NET-10-512bd4)
  ![Preview](https://img.shields.io/badge/Status-Preview-orange)

  [**Download**](https://github.com/aliceblackrose/cirrova/releases) ·
  [User guide](docs/USER-GUIDE.md) ·
  [How profiles work](docs/LAUNCHING.md) ·
  [Roadmap](docs/ROADMAP.md)
</div>

---

## Download

**[Browse downloadable releases →](https://github.com/aliceblackrose/cirrova/releases)**

Cirrova publishes self-contained desktop builds. You **do not need to install the .NET SDK** to use a release package.

| Platform | Release asset | How to run |
| --- | --- | --- |
| **Windows x64** | `Cirrova.exe` | Download and launch directly |
| Windows x64 | `Cirrova-win-x64.zip` | Extract the complete package, then open `Cirrova.exe` |
| Linux x64 | `Cirrova-linux-x64.tar.gz` | Extract, then run `./Cirrova` |
| macOS Apple Silicon | `Cirrova-osx-arm64.tar.gz` | Extract, then run `./Cirrova` |

Each release includes `SHA256SUMS.txt` for file-integrity checks. Windows is the primary supported platform. macOS builds are unsigned command-line-launchable preview binaries, **not** signed `.app` bundles or installers, and may be blocked by Gatekeeper. Linux may require desktop-native libraries provided by your distribution.

> [!IMPORTANT]
> **Preview software.** Automated builds and unit tests do not substitute for graphical and real-game smoke tests. Back up important saves and profiles before experimenting with game copies or modpacks.

## What Cirrova does

| Area | Capabilities |
| --- | --- |
| **Overview** | Installation status, active profile, mod counts, and quick actions |
| **Game** | Find Steam and alternate libraries, choose a custom game folder, launch vanilla, inspect existing mods without modifying them |
| **Profiles** | Separate mods, saves, and generated worlds by profile; archive, restore, rename, and manage disk usage |
| **Game copies** | Optionally create an isolated game copy for large overhauls; view and clean up owned copies and backups |
| **My Mods** | Install mod ZIPs and modpacks, enable or disable mods, archive mods, and replace existing installations with a backup |
| **Discover** | Opt-in GitHub repository search, release-asset inspection, ZIP downloads, SHA-256 hashing, and mod source tracking |

**Mod isolation by design:** profile mod installation does not write to the original Steam game directory. Existing mods already in the game root may still load; start from a clean game directory for maximum isolation.

### Under the hood

- **Language:** C# throughout the application, UI composition, services, tests, and Windows icon tooling.
- **Framework:** .NET 10 and Avalonia 11 with a dark Fluent-style desktop UI.
- **UI:** controls, layouts, event wiring, and application styles are defined in C# instead of AXAML.
- **Packaging:** GitHub Actions builds self-contained Windows x64, Linux x64, and macOS arm64 binaries. YAML workflows and XML project files remain build configuration—not application source.
- **Windows icon:** a dedicated C# .NET tool turns the existing `icon.png` into a multi-resolution executable icon and verifies the packaged PE resources. No Python dependency.

## Build from source

Install the [**.NET 10 SDK**](https://dotnet.microsoft.com/download/dotnet/10.0), then run:

```bash
git clone https://github.com/aliceblackrose/cirrova.git
cd cirrova
dotnet restore tests/Cirrova.Tests/Cirrova.Tests.csproj
dotnet run --project src/Cirrova/Cirrova.csproj
```

Run the test suite:

```bash
dotnet test tests/Cirrova.Tests/Cirrova.Tests.csproj -c Release
```

Build a self-contained Windows package from **Windows**:

```powershell
dotnet run --project tools/Cirrova.IconTool/Cirrova.IconTool.csproj -c Release -- create
dotnet publish src/Cirrova/Cirrova.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
dotnet run --project tools/Cirrova.IconTool/Cirrova.IconTool.csproj -c Release -- verify publish/Cirrova.exe
```

For Linux and macOS use the appropriate runtime identifier (`linux-x64` or `osx-arm64`) with `dotnet publish`. The Windows `.ico` tool is Windows-only; the original app artwork is embedded for every platform.

## Releases and automation

- **Every successful push to `main`** builds and tests all three desktop targets and publishes a tagged preview prerelease (`preview-<run-number>`).
- **Version tags** (for example, `v0.1.0`) publish a regular GitHub Release with platform packages, a direct Windows EXE, and checksums.
- **Pull requests and working branches** run checks and/or upload downloadable GitHub Actions artifacts; they do not create public GitHub Releases.

To publish a versioned release after merging your changes:

```bash
git tag v0.1.0
git push origin v0.1.0
```

View [build/test runs](https://github.com/aliceblackrose/cirrova/actions/workflows/ci.yml), [desktop packaging](https://github.com/aliceblackrose/cirrova/actions/workflows/package.yml), and [published releases](https://github.com/aliceblackrose/cirrova/releases).

## Safety and limitations

- ZIP downloads are limited to **16 GiB per archive** and **64 GiB unpacked**. Cirrova performs disk-space checks and keeps backups when replacing mods.
- A recorded SHA-256 hash helps detect download changes; it **does not authenticate the publisher**. GitHub search results are not curated or verified.
- DLL-based game mods can execute arbitrary code. Install only from sources you trust, and check game-version compatibility.
- Use `-noeac` only when appropriate for the mods and game mode in question.
- Isolated game copies can use **tens of gigabytes** of disk space.
- The interface and game integration still require manual, in-game validation. Automated updates and dependency solving are not implemented.

## Documentation

[User guide](docs/USER-GUIDE.md) · [Profiles and launching](docs/LAUNCHING.md) · [Mod format](docs/MOD-FORMAT.md) · [Finding mods on GitHub](docs/DOWNLOADS.md) · [Desktop UI](docs/DESKTOP.md) · [Roadmap](docs/ROADMAP.md)

---

<sub>Cirrova is an independent community project and is not affiliated with The Fun Pimps, Valve, or third-party mod creators. **GNU GPL v3 license**; the repository is source-available and should not be described as open-source until a license is added.</sub>
