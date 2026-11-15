# Atmosphere development roadmap

## Implemented in the v1 preview branch

- Phase 1: Avalonia desktop foundation, Steam discovery, local settings, vanilla Steam launch
- Phase 2: Read-only installed mod inventory; modern ModInfo.xml validation and warnings
- Phase 3: Named profile workspaces, ZIP archive validation, backup-based installation/disable/archive
- Phase 4: Explicit GitHub latest release/ZIP download, SHA-256 output; profile-specific launching
- Phase 5: Desktop UI wiring for game, profiles, mods and online releases
- Phase 6: CI/package workflows, malicious archive tests, end-user documentation
- Phase 7: Optional full independent game copies with disk-space preflight and cancellation

## Remaining release gates

- Windows manual GUI test and real 7 Days to Die launch with and without DLL mods
- Linux and macOS manual checks of Steam runtime/executable discovery
- Screenshots, icon assets, accessibility testing, and UX polish
- Binary signing/notarization, native installers, crash diagnostics
- Additional mod-host connectors and indexed catalog search
- Checksum/signature verification against authoritative publisher manifests
- Automated mod dependency and version resolution
- Automated migration or patching of copied game files across Steam versions
- A repository license selected by the owner

## Acceptance for a public v1

At minimum, no crashing first-run dialogs, successful Steam and profile launches on Windows, safe ZIP install/disable/replace/archive with rollback, basic mod metadata display, downloadable ZIP release and correct save separation. No issue should be marked done solely on CI success.

## Non-negotiable safety rules
1. Never silently modify/delete the original Steam installation.
2. Never extract archive entries outside an isolated staging directory.
3. No silent downloads or executing downloaded code.
4. Preserve existing mod content through backups and profile separation.
5. Keep vanilla launching available when mod services fail.
6. Explicitly handle EAC compatibility and third-party code risks.
