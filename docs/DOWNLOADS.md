# GitHub search and Releases downloads

The **Online** workflow is opt-in. You can search public GitHub repositories (these are **not curated or verified**), select a result, or paste a known GitHub repository URL. Click **Check latest release** to inspect ZIP assets. Download only after selecting an asset and profile.

Downloads are capped at 512 MiB and hashed with SHA-256. The app records the GitHub repository, release tag, ZIP asset, and hash for the installed modlets. Select an installed mod and click **Find selected mod's source** to load its repository URL into the Online tab. To update a mod, check the latest release and select **Replace existing** on the My Mods tab before installing.

A download's SHA-256 is informational and **not** publisher-signature verification. Code mods can run arbitrary game code, so check the publisher and compatibility. GitHub API requests are rate limited; repeated searches or release checks may get HTTP 403/429.

There is no automatic background fetching, dependency solver, curated listing, or hands-off update.
