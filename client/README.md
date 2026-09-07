# CS Arena Client

Desktop client for CS Arena competitive matchmaking.

## Current stage
- Steam must be running before launch.
- Player selects the existing CS 1.6 root folder once.
- The client detects the `cstrike` folder automatically.
- The client downloads the Arena package from the configured manifest.
- Package SHA-256 is verified before installation.
- Existing files are backed up to `.csarena_backup/` before replacement.
- Arena files are installed directly into the game's `cstrike/` folder.
- `.csarena-version` records the installed Arena package version.
- The same flow can be used later for automatic updates.
- CS 1.6 can be launched directly from the selected installation.
- Steam identity/account sync remains part of the next stage.
- Gun skins are intentionally reserved for the final stage.

## Package flow

`manifest.json` provides the package URL, version and SHA-256 checksum. The package itself is a ZIP whose contents mirror the CS 1.6 `cstrike/` directory.

Example:

```text
arena-pack.zip
  addons/
  models/
  sound/
  sprites/
  resource/
  ...
```

## Security
The client never requests or stores a Steam password. ZIP extraction is checked against path traversal, and package integrity can be verified with SHA-256 before files are copied.
