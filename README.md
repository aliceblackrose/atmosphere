# Atmosphere

Atmosphere is a desktop launcher and mod manager for 7 Days to Die, built in independently reviewable phases.

## Current milestone: Phase 1

The first milestone provides a cross-platform desktop shell, Steam installation discovery (including alternate Steam libraries), a manually selectable game folder, persisted local settings, and a safe vanilla launch through Steam. Mod installation is **not yet implemented**.

## Run locally

1. Install the .NET 10 SDK.
2. Clone this repository.
3. From the repository root, run:

    dotnet run --project src/Atmosphere/Atmosphere.csproj

To run tests:

    dotnet test tests/Atmosphere.Tests/Atmosphere.Tests.csproj

## Technology

- C# / .NET 10 LTS
- Avalonia 11 desktop UI
- Local JSON settings (no login, telemetry, or network calls in Phase 1)

## Roadmap

See docs/ROADMAP.md for implementation phases, acceptance criteria, and safety requirements.

## Safety and compatibility

The Phase 1 launcher never writes to the game install or Mods folder. It opens the vanilla game via Steam. Future mod installation will use validated archives, staging, backups, and isolated profiles by default. Mods must match the 7 Days to Die version you play, and some code mods require EAC to be disabled.

Atmosphere is an independent community project and is not affiliated with The Fun Pimps or Valve. No license has been selected yet.
