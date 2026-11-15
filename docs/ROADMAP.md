# Atmosphere development roadmap

## Vision
A safe, native launcher that supports vanilla play, modlets, isolated overhaul installations, game-version compatibility, and reproducible profiles for 7 Days to Die.

Development proceeds via small pull requests. No phase is considered complete until it builds in CI and passes its acceptance checks.

## Phase 1 — Desktop foundation (current)
- .NET 10 + Avalonia desktop application
- Detect Steam 7 Days to Die in default and alternate libraries
- Manually choose installation directory
- Persist configuration locally in JSON
- Launch *vanilla* via Steam and open installation folder
- Tests for library parser, game detection, and preferences
- Windows + Linux build/test workflow

Acceptance: app starts, finds a valid installation, remembers a manually chosen path, launches the Steam URL, and does not modify game files.

## Phase 2 — Local mod inventory
- Scan Mods directories and parse current ModInfo.xml formats
- Show detected mods, metadata, authors and versions
- Flag missing/invalid metadata and duplicated mod IDs
- Read-only inventory first; no deletion

Acceptance: catalog lists actual installed modlets without modifying them.

## Phase 3 — Safe installation and removal
- Install ZIP modlets from locally selected files
- Path traversal and zip bomb defenses; archive size limits
- Find nested ModInfo.xml without guessing unrelated folders
- Stage, validate, and atomically replace files; backup and rollback
- Uninstall only launcher-managed content; never delete pre-existing user files

Acceptance: install/uninstall works without corrupting or deleting unrelated content, even on failure.

## Phase 4 — Profiles and isolation
- Named mod sets, enabled/disabled state, profile switching
- Per-profile working directories and load-order awareness
- Separately isolated overhaul game copies, leaving Steam game untouched
- Detect storage limits, copies, and incompatible profile/game versions

Acceptance: switching profiles does not mix files or change the vanilla install.

## Phase 5 — Catalog, downloads and updates
- Trusted sources such as explicit GitHub releases or user-provided URLs
- Explicit consent before network/download actions
- Checksums, progress, cancellation, retry, and update history
- Mod/game version compatibility and sources provenance

Acceptance: reproducible downloads and verified updates with rollback.

## Phase 6 — Modded launching
- Launch specific profiles and game versions
- Handle EAC requirements explicitly (no bypass)
- Surface executable/Steam options, errors, and logs
- Save/backup separation via supported game flags where applicable

Acceptance: vanilla and modded launches are clearly distinguished and tested.

## Phase 7 — Polish and release
- Windows installer, Linux package, release channel
- CI release builds, signing where available, release notes
- Error reporting is opt-in and never sends game data without consent
- Accessibility, docs, first-run onboarding, troubleshooting

Acceptance: publicly usable beta with reproducible builds and documented limitations.

## Non-negotiable safety rules
1. Never silently modify or delete an original Steam game installation.
2. Never extract archive entries outside the intended staging directory.
3. Never trust downloaded files, executable content, or mod metadata.
4. Use separate mod/profile namespaces and preserve user-created files.
5. Keep vanilla launching available even when mod services fail.
6. No forced EAC bypass or misleading compatibility promises.

## Research references
- Official game support: https://support.7daystodie.com/hc/en-us/articles/48520694260628-Locating-or-moving-your-save-file
- Community mod information: https://7d2dmodding.wiki.gg/wiki/Glossary
- Avalonia getting started: https://docs.avaloniaui.net/docs/get-started
