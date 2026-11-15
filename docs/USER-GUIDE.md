# Atmosphere quick start

1. Install Steam and 7 Days to Die, then open Atmosphere.
2. On **Game**, detect or browse to the game installation, and choose **Save game path**.
3. On **Profiles**, create a named profile. To use a different 7 Days to Die game branch/copy, select that directory on the Game tab and click **Use current game folder for this profile**.
4. On **My Mods**, choose **Install mod ZIP(s)** and select one or several ZIPs. A ZIP can contain many modlets, including folders nested under a pack's `Mods/` directory, as long as each modlet has a modern `ModInfo.xml`. Each ZIP is handled independently and reported if it fails. ZIP archives up to 16 GiB are supported; expanded data is limited to 64 GiB per ZIP, with 16 GiB per individual file and 50,000 entries. Check disk space before installing large packs.
5. Disable or enable mods per profile; Archive moves them to backup, not Trash.
6. Launch from **Profiles**. If a DLL mod is detected, select **Launch without EAC**. Keep Steam running.
7. On **Online**, enter a GitHub repository URL, check its latest release, choose a ZIP, and install it into the selected profile. Check the author and game-version compatibility before installing.

## Caveats
- Game-root Mods always still load; Atmosphere does not modify the Steam install. Keep it clean for isolated profile play.
- The user-data folder flag separates saves, generated worlds and modlets, but does not provide a full isolated game binary copy. Point profiles at separate game copies when necessary.
- The GitHub flow is not an automatic mod marketplace or dependency resolver.
- DLL mods execute game code and are inherently high trust. The SHA-256 shown after download is informational, not publisher verification.
- Incompatible overhauls, EAC-enabled servers, or game version mismatches can prevent launching.
- Windows is the primary target; macOS and Linux need hardware validation, especially Steam runtimes and launch behavior.

## Profile storage and cleanup

Open **Profiles → Storage and Cleanup** for the selected profile. **Refresh storage usage** reports overall size, the owned GameCopy size, backup size, and other profile data. **Open profile folder** lets you inspect files before removing anything.

- **Delete only this profile's GameCopy** permanently removes the copy Atmosphere created under the profile. It does **not** delete an external Steam installation or any game folder selected manually. The profile's saves, Mods, and Backups remain.
- **Permanently clear backups** removes all replaced/archived mod backups for the selected profile while leaving the current mods and saves alone.
- **Permanently delete this profile and its data** removes its Mods, Saves, GeneratedWorlds, Backups, and any owned GameCopy. Close 7 Days to Die first. The Steam game installation is never deleted.
- In **Archived Profiles**, **Permanently delete selected archive** removes an archived profile and its stored files. **Restore selected archive** remains available for archives you want to keep.

All permanent deletion commands require typing the selected profile name in a confirmation dialog. Symlinks/junctions inside the deletion target are refused. If a profile deletion encounters a file error, inspect **Archived Profiles** for data that may remain. There is no automatic undo of successful permanent deletions.

## Recovery

Atmosphere keeps mod folders under `%APPDATA%/Atmosphere/profiles/{profile-id}` on Windows. Renamed or removed mods are under `Backups/Replaced` or `Backups/Removed`. Archived profiles remain under `archived-profiles` and can be restored from the Profiles tab. No operation automatically deletes your game installation.
