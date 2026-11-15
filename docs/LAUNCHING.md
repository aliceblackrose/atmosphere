# Profiles and launching

- **Vanilla** starts through Steam's regular `steam://rungameid/251570` URI. It uses the player's own Steam launch settings.
- **Profile launch** starts the chosen game's native executable with `-UserDataFolder=<Atmosphere profile path>`. Each profile contains its own `Mods`, `Saves`, and `GeneratedWorlds` folders.
- **Code mods** with DLLs require explicitly selecting **Launch without EAC**. That adds `-noeac` and is not an EAC bypass. Code mods can execute arbitrary game code: trust your sources.
- The **Create full isolated game copy** action copies the selected game installation into the profile's `GameCopy` folder and assigns the copy to the profile. This may take tens of gigabytes. File-copying does not alter the source Steam installation. Linked game files are refused to prevent unsafe traversal. Cancellation attempts to clean staging data.
- When using a shared Steam game installation, game-root Mods still load alongside profile Mods. Choose a clean game folder or a full isolated copy for overhaul profiles.
- Profiles can point at different game versions, but Atmosphere does not automatically download legacy Steam branches or validate mod-to-game compatibility. Match every overhaul to its supported game version.
- A running Steam client may be necessary for game entitlements. Linux Steam/Proton and macOS layouts need hardware verification.

References:
- https://support.7daystodie.com/hc/en-us/articles/48520694260628-Locating-or-moving-your-save-file
- https://7daystodie.wiki.gg/wiki/Launch_Parameters
