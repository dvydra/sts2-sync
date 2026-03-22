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

- .NET 10 installed via homebrew; SteamKit2 3.4.0 has a net10.0 build
- MAUI app needs Android SDK installed separately (`brew install --cask android-commandlinetools`)
- MAUI 10 requires explicit `<PackageReference Include="Microsoft.Maui.Controls">` (not auto-included)
- SteamKit2 must use `ProtocolTypes.WebSocket` on Android (TCP/UDP don't work)
- Refresh token goes in `SteamUser.LogOnDetails.AccessToken` (confusing name)
- `SteamConnectionManager` implements both `IDisposable` and `IAsyncDisposable` for test convenience
- Target device: Android 13 (API 33)
- **CRITICAL**: Must call `_unifiedMessages.CreateService<Cloud>()` during init — without it, CCloud RPCs silently time out (responses never routed to AsyncJob)
- SteamKit2 `AsyncJob` default timeout is 10 seconds; set `job.Timeout` for longer operations
- `Console.WriteLine` from .NET goes to Android logcat under `DOTNET` tag

## Android Deployment

Reliable deploy command (always use this):
```bash
ANDROID_HOME=/opt/homebrew/share/android-commandlinetools \
  dotnet build src/Sts2Sync.App/ -t:Install -f net10.0-android \
  -p:EmbedAssembliesIntoApk=true
```

Key gotchas:
- **Always use `-p:EmbedAssembliesIntoApk=true`** — fast deployment mode leaves assemblies in a device override dir that breaks after uninstall
- **Never `adb uninstall` then `adb install`** — use `-t:Install` or `-r` flag instead
- **Incremental builds may not update Core DLL** — touch `MauiProgram.cs` or use `--no-incremental` to force
- **When in doubt**: `rm -rf src/*/bin src/*/obj` and rebuild
