# CS Arena Client

Desktop client for CS Arena competitive matchmaking.

## Current stage
- Steam must be running before launch.
- Steam identity integration is planned for the client.
- CS Arena account sync will use Steam identity.
- Server connection flow will be integrated with the CS Arena backend.
- Gun skins are intentionally reserved for the final stage.

## Planned structure
- `client/` - desktop launcher/client
- `config/` - client configuration
- `steam/` - Steam detection and identity integration
- `game/` - CS 1.6 launch and server connection
- `assets/` - branding and later visual assets
- `skins/` - reserved for final gun-skin package

## Security
The client will never request or store a Steam password. Steam authentication will rely on Steam's supported identity flow.
