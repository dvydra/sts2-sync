# STS2 Steam Cloud Sync

## Project

.NET MAUI Android app syncing Slay The Spire 2 saves between Steam Cloud and GameHub on Android.

## Tech Stack

- .NET MAUI (Android target), C#
- SteamKit2 for Steam protocol (CCloud protobuf RPCs over WebSocket)
- STS2 App ID: `2868840`

## Key Reference

- [StS2-Launcher](https://github.com/Ekyso/StS2-Launcher) — reference implementation for Steam Cloud sync on Android
  - `src/STS2Mobile/Steam/SteamKit2CloudSaveStore.cs` — CCloud RPC usage
  - `src/STS2Mobile/Steam/SaveProgressComparer.cs` — content-based conflict resolution
  - `src/STS2Mobile/Steam/SteamAuth.cs` — auth flow with reconnection handling
  - `src/STS2Mobile/Steam/SteamCredentialStore.cs` — Android Keystore encryption

## Architecture Notes

- Conflict resolution is content-based, NOT timestamp-based (timestamps unreliable on mobile)
- Cloud wins on tie (PC is primary platform)
- Backups before every overwrite, max 50 per profile
- Sequential write queue for uploads (single background thread)
- Lazy Steam connection with idle timeout

## Meta Learnings

(Accumulated during milestone implementation)
