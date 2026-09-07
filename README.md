# CS Arena Client

Windows launcher and updater for CS Arena / Counter-Strike 1.6.

## Current client

- Detects the Steam installation and CS 1.6 Steam libraries automatically.
- Reads the active Steam identity locally from Steam's `loginusers.vdf`.
- Shows Steam PersonaName and SteamID64.
- Never asks for or stores a Steam password.
- Installs and updates the CS Arena package from an HTTPS manifest.
- Verifies the package with SHA-256 when a checksum is supplied.
- Rejects unsafe ZIP paths and limits package size.
- Backs up replaced files under `.csarena_backup/`.
- Rolls back files if installation fails.
- Records the installed Arena version in `.csarena-version`.
- Launches CS 1.6 directly with the Steam identity name.
- Connects directly to the CS Arena server at `185.211.103.215:7703`.
- Builds as a self-contained single-file `win-x64` executable through GitHub Actions.

## Build

```text
dotnet restore src/ArenaClient.csproj
dotnet build src/ArenaClient.csproj -c Release
dotnet publish src/ArenaClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The CI workflow also uploads `ArenaClient.exe` as the `ArenaClient-win-x64` artifact.

## Package format

The public manifest is expected at:

`https://csarena.pages.dev/client/manifest.json`

The package URL should point to an HTTPS ZIP containing paths relative to the CS 1.6 `cstrike` directory.
