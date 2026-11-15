# GitHub Releases downloads

The Online workflow is **opt-in**. Enter a public GitHub repository URL and click Check Latest. Atmosphere uses GitHub's public REST release endpoint, shows ZIP assets and version tags, and downloads only when Install is selected. Downloads have a 512 MiB size limit and compute SHA-256.

A GitHub asset's checksum is displayed for provenance; it is *not* equivalent to verifying a trusted publisher signature. Downloaded code mods can execute inside the game. Only install mods from sources you trust.

The current catalog is URL-driven (one GitHub repository at a time), not a global indexed marketplace. Auto-update polling, dependency resolution, and third-party hosting are not implemented.
