# STS2 Steam Cloud Sync for Android — Milestones

## Project Overview

A .NET MAUI Android app that syncs Slay The Spire 2 save files between Steam Cloud and the local device (GameHub). Authenticates directly with Steam via SteamKit2, uses content-based conflict resolution, and never overwrites a more up-to-date save.

### Architecture

```
┌──────────────┐    SteamKit2 CCloud RPCs     ┌──────────────┐
│  .NET MAUI   │ ◄───────────────────────────► │ Steam Servers │
│  Android App │   (protobuf over WebSocket)   └──────────────┘
│              │
│              │◄── Local filesystem ──► GameHub save directory
│              │                         (configurable path)
└──────────────┘
```

### Key Technical Decisions

- **Runtime:** .NET MAUI (Android target), C#
- **Steam protocol:** SteamKit2 — `CCloud` protobuf RPCs via `SteamUnifiedMessages`
- **Save format:** Plain JSON, unencrypted (App ID: `2868840`)
- **Conflict resolution:** Content-based (not timestamps — unreliable on mobile)
  - Cascading heuristics: floors_climbed → total_games → discovered_items → playtime
  - Cloud wins on tie (PC is primary platform)
- **Credential storage:** AES-256-GCM via Android Keystore
- **Local save access:** Configurable path (GameHub drive-mapped or root)
- **Reference impl:** [StS2-Launcher](https://github.com/Ekyso/StS2-Launcher) — informs protocol usage and conflict strategy

### Save Files Synced (per profile × 3 slots)

| File | Purpose | Conflict Strategy |
|------|---------|-------------------|
| `progress.save` | Meta-progression (unlocks, stats) | Content heuristics |
| `current_run.save` | Active run state | Floor count comparison |
| `current_run_mp.save` | Multiplayer run state | Floor count comparison |
| `*.run` | Run history (immutable) | Merge (no conflict possible) |

---

## Milestone 1: Project Scaffolding + Save File Analysis

**Goal:** Set up the .NET MAUI project and build thorough understanding of STS2 save files. Confidence in parsing before we touch sync.

### Scope
- Initialize .NET MAUI project targeting Android
- Define C# data models for `progress.save` and `current_run.save`
- Build JSON parser extracting all conflict-relevant fields:
  - `character_stats[]`: playtime, total_wins, total_losses, floors_climbed, max_ascension, current_streak, best_win_streak
  - Discovered items: cards, relics, potions, events, acts
  - Run state: `map_point_history` (floor count), deck, HP, gold
- SHA-256 hashing for change detection
- Comprehensive unit tests with real save file samples
- Document discovered schema in `SAVE_FORMAT.md`

### Acceptance Criteria
- [ ] .NET MAUI project builds and deploys to Android
- [ ] Parses real `progress.save` and `current_run.save` files
- [ ] Extracts all fields needed for conflict resolution
- [ ] SHA-256 change detection works
- [ ] Tests pass with sample data
- [ ] Save file schema documented

---

## Milestone 2: Steam Authentication

**Goal:** Log into Steam from the Android app using SteamKit2.

### Scope
- Add SteamKit2 NuGet dependency
- Implement login flow over WebSocket (`ProtocolTypes.WebSocket`):
  - `BeginAuthSessionViaCredentialsAsync` with username + password
  - Steam Guard support: TOTP, email code, mobile confirmation
  - Store `GuardData` for device remembering
- Secure credential storage (refresh token, guard data) via Android Keystore + AES-256-GCM
- Session reuse: log on with refresh token on subsequent launches
- Reconnection handling (WebSocket drops during 2FA)
- MAUI login UI: credential fields, 2FA input, status/errors

### Acceptance Criteria
- [ ] Authenticates with Steam successfully
- [ ] All three 2FA methods work (TOTP, email, mobile confirm)
- [ ] Credentials stored securely, reused across restarts
- [ ] Reconnection works if connection drops during 2FA
- [ ] Clear error messages for auth failures
- [ ] Tests for auth flow

---

## Milestone 3: Steam Cloud Read — Enumerate & Download

**Goal:** List and download STS2 cloud saves with full metadata.

### Scope
- `SteamUnifiedMessages` service for `CCloud`:
  - `EnumerateUserFiles` (app ID `2868840`, paginated 500/page)
  - `ClientFileDownload` → HTTP GET from signed CDN URL
- Handle compressed files (ZIP magic header detection, decompress if raw_size ≠ file_size)
- In-memory file cache (`CloudFileCache`): path → (size, timestamp, sha)
- Store downloaded files locally with metadata
- MAUI UI: cloud file list with metadata (name, size, timestamp, SHA)
- Parse downloaded saves using M1's analyzer — display stats

### Acceptance Criteria
- [ ] Enumerates all cloud saves for app 2868840
- [ ] Downloads and decompresses files correctly
- [ ] File cache populated and queryable
- [ ] Metadata displayed in UI
- [ ] Parsed save stats shown
- [ ] Handles: no saves, pagination, network errors, expired auth

---

## Milestone 4: Local Save Access (GameHub Integration)

**Goal:** Read and write STS2 saves from the GameHub directory on the device.

### Scope
- Configurable save path with presets:
  - GameHub drive-mapped: `/sdcard/STS2Saves/` (user configures in GameHub)
  - GameHub root: `/data/user/0/com.xiaoji.egggame/files/containers/{id}/.wine/drive_c/users/user/AppData/Roaming/SlayTheSpire2/steam/{steamId}/`
  - Custom path
- Auto-detect available profiles (profile1/2/3) and their save files
- Read/write local saves, track local file metadata (size, SHA, mtime)
- Settings page: path configuration, access method selection
- Side-by-side view: local vs cloud metadata per profile

### Acceptance Criteria
- [ ] Reads saves from configured path
- [ ] Writes saves to configured path
- [ ] Detects available profiles
- [ ] Settings UI for path config
- [ ] Side-by-side local vs cloud comparison view
- [ ] Works with both root and non-root paths

---

## Milestone 5: Conflict Resolution Engine

**Goal:** Content-based comparison that never overwrites a more up-to-date save.

### Scope
- `SaveProgressComparer` with cascading heuristics:
  1. SHA match → identical, skip
  2. Corrupt check (doesn't start with `{` or `[`) → other side wins
  3. For `progress.save`: floors_climbed → total_games → discovered_items → playtime
  4. For `current_run.save` / `current_run_mp.save`: floor count from `map_point_history`
  5. Tie → cloud wins (PC is primary)
- `BackupManager`:
  - Backup before every overwrite: `{filename}.{unix_timestamp}.{source}.bak`
  - Max 50 backups per profile, prune oldest
- Conflict result types: `Identical`, `CloudWins`, `LocalWins`, `Conflict` (manual resolution needed)
- Conflict UI: side-by-side stats comparison, user picks winner
- Exhaustive test suite covering every scenario

### Acceptance Criteria
- [ ] Identifies identical files (no-op)
- [ ] Identifies clear winner via heuristics
- [ ] Detects true conflicts, presents comparison
- [ ] Never overwrites a more up-to-date save
- [ ] Backups created before every write
- [ ] 50-backup cap with pruning
- [ ] Comprehensive tests for all conflict scenarios

---

## Milestone 6: Steam Cloud Write — Upload

**Goal:** Upload local saves back to Steam Cloud when local is newer.

### Scope
- Upload flow via CCloud RPCs:
  1. `BeginAppUploadBatch`
  2. SHA-1 hash raw bytes
  3. Attempt ZIP compression (use only if smaller)
  4. `ClientBeginFileUpload` → get block requests (URL, offset, length, method)
  5. HTTP PUT/POST each block
  6. `ClientCommitFileUpload`
  7. `CompleteAppUploadBatchBlocking`
- Sequential write queue (single background thread, `BlockingCollection`)
- Verify upload: re-enumerate, compare SHA
- Retry with exponential backoff (3 attempts)
- Upload progress in UI

### Acceptance Criteria
- [ ] Uploads saves to Steam Cloud successfully
- [ ] Only uploads when conflict resolver says local wins
- [ ] Compression applied when beneficial
- [ ] Post-upload verification via re-enumeration
- [ ] Retry on failure with backoff
- [ ] Write queue processes sequentially
- [ ] Progress shown in UI

---

## Milestone 7: Sync Orchestrator + Background Sync

**Goal:** End-to-end sync flow, on-demand and automatic.

### Scope
- `SyncOrchestrator` full cycle:
  1. Authenticate (reuse session / refresh token)
  2. Enumerate cloud saves (refresh cache)
  3. Read local saves
  4. Run conflict resolution per file per profile
  5. Download/upload as needed
  6. Report results
- On-demand: sync button / pull-to-refresh
- Background sync via Android WorkManager:
  - Periodic (configurable, default 15 min)
  - Battery-aware (skip on low battery)
  - Network-aware (WiFi only option)
- Connection management:
  - Lazy connect on first sync
  - Idle timeout (auto-disconnect after 30s inactivity)
  - Exponential backoff on connection failures
- Notifications: sync complete, conflicts needing attention
- Sync history log (last N syncs with results)
- Flush pending writes on app background/shutdown

### Acceptance Criteria
- [ ] Full sync cycle works end-to-end
- [ ] On-demand sync via UI
- [ ] Background sync on schedule
- [ ] Lazy connect + idle disconnect
- [ ] Notifications for conflicts
- [ ] Sync history viewable
- [ ] Graceful handling: no network, auth expired, Steam down
- [ ] Pending writes flushed on app pause

---

## Milestone 8: Polish & Hardening

**Goal:** Reliable for daily personal use.

### Scope
- First-time setup flow: configure GameHub path → Steam login → initial sync
- Edge cases:
  - No local saves yet (first sync from cloud)
  - No cloud saves yet (first push to cloud)
  - All 3 profiles synced independently
  - `.run` history files: merge-only (immutable, just union both sides)
- Diagnostics screen: last sync details, connection state, file cache state, recent errors
- App lifecycle: survive process death, handle Android doze/standby
- Logging throughout for debugging sync issues
- Final UI polish on all screens

### Acceptance Criteria
- [ ] Setup flow works for fresh install
- [ ] All 3 profiles sync correctly
- [ ] Run history files merged correctly
- [ ] Diagnostics screen useful for debugging
- [ ] Survives process death / config changes
- [ ] Full end-to-end: PC → Steam Cloud → app → GameHub → play → app → Steam Cloud → PC
