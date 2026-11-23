# Cirrova desktop experience

The desktop app uses a consistent dark Fluent design to simplify the original tabbed interface.

## Navigation

- **Overview** — live installation status, active profile, mod count, and quick actions.
- **Game** — Steam discovery, manual installation folder, vanilla launch, and read-only existing mods.
- **Profiles** — create/rename/archive/restore profiles, separate game-copy management, and storage cleanup.
- **My Mods** — multi-ZIP modpack installation, enable/disable, backup/archive, and source metadata.
- **Discover** — search public GitHub repositories, browse latest release ZIPs, download and install.

The left sidebar is an Avalonia TabControl, so it keeps keyboard navigation and selection semantics. Every existing mod management and launching action remains wired to the same underlying services. The overview updates whenever the chosen game directory, selected profile, or mod inventory changes.

## Visual identity

The actual existing repository artwork in `icon.png` is reused rather than replaced. Avalonia embeds it as `/Assets/icon.png` for the title bar and header. The package workflow converts the same PNG to a Windows `.ico` containing 16, 24, 32, 48, 64, 128 and 256 pixel frames for the executable.

## Known limitations

- This remains a preview; graphical interaction and real-game integration require human smoke testing.
- Linux desktop launchers and macOS `.app` bundles are not yet first-class installers; the embedded window icon is present in all builds.
- Window width under 980 logical pixels is not supported by the current layout.
- Icons are static and do not signal game/update status. The dashboard text and status footer convey state instead.
