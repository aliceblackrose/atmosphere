# Profiles and launching

- **Vanilla** starts through Steam's regular `steam://rungameid/251570` URI. It uses whatever launch options the player has configured in Steam.
- **Profile launch** starts the chosen game's native executable with `-UserDataFolder=<Atmosphere profile path>`. The profile contains `Mods`, `Saves`, and `GeneratedWorlds`, separating them from other Atmosphere profiles and normal user data.
- **Code mods** containing DLLs require explicitly selecting **Launch without EAC**. The switch adds `-noeac`; it does not bypass anti-cheat. Do not join protected multiplayer servers with incompatible mods.
- A clean **game installation Mods folder** is still recommended. Game-root Mods are also loaded by the game; Atmosphere warns when it detects these. Atmosphere cannot promise perfect isolation from game-root modlets.
- Profiles may point to different installed copies/game branches. Match each overhaul to its supported game version.
- Launching with the native executable requires Steam to be running for Steam entitlements. Linux Steam/Proton and macOS layouts need real-hardware verification.

References:
- https://support.7daystodie.com/hc/en-us/articles/48520694260628-Locating-or-moving-your-save-file
- https://7daystodie.wiki.gg/wiki/Launch_Parameters
