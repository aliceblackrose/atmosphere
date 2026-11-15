# Supported 7 Days to Die modlets

Atmosphere recognizes the current **ModInfo.xml v2** format, directly inside each modlet folder under `Mods`:

```xml
<xml>
  <Name value="UniqueInternalID"/>
  <DisplayName value="Display Name"/>
  <Version value="1.0.0"/>
  <Description value="What this mod does"/>
  <Author value="Creator"/>
</xml>
```

Old `<xml><ModInfo>...</ModInfo></xml>` metadata is detected but flagged as incompatible with modern versions. Duplicate internal IDs, missing `ModInfo.xml`, and malformed XML are also flagged. A DLL is shown as an EAC compatibility warning; merely detecting a DLL never executes it.

Game-level Mods can load in addition to user-data Mods. Atmosphere must not claim perfect isolation when the base Steam install already has modlets installed.

References:
- https://7daystodie.wiki.gg/wiki/Mod_Structure
- https://7d2dmodding.wiki.gg/wiki/Getting_Started
