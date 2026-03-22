# STS2 Save File Format

Steam App ID: **2868840**

## Directory Layout

Per profile (3 slots), relative to the Steam Cloud root:

```
profile<1|2|3>/
├── saves/
│   ├── progress.save          # Meta-progression
│   ├── current_run.save       # Active solo run
│   └── current_run_mp.save    # Active multiplayer run
├── history/
│   └── <timestamp>.run        # Completed run records (immutable)
└── prefs.save                 # User preferences
```

### Platform Paths

| Platform | Path |
|----------|------|
| Windows (Steam) | `Steam/userdata/{accountId}/2868840/remote/{profile}/saves/` |
| Windows (AppData) | `AppData/Roaming/SlayTheSpire2/steam/{steamId}/{profile}/saves/` |
| GameHub (Android) | `/data/user/0/com.xiaoji.egggame/files/containers/{id}/.wine/drive_c/users/user/AppData/Roaming/SlayTheSpire2/steam/{steamId}/{profile}/saves/` |

## File Format

All save files are **plain-text JSON**, unencrypted, UTF-8 encoded.

## progress.save

Top-level meta-progression file tracking lifetime stats, unlocks, and discoveries.

```json
{
  "floors_climbed": <int>,           // ⚡ CONFLICT SIGNAL #1
  "total_playtime": <number>,        // ⚡ CONFLICT SIGNAL #4 (seconds)
  "character_stats": [               // ⚡ CONFLICT SIGNAL #2 (sum of games)
    {
      "id": "<CHARACTER.xxx>",
      "total_wins": <int>,
      "total_losses": <int>,
      "best_win_streak": <int>,
      "current_streak": <int>,
      "fastest_win_time": <number>,
      "max_ascension": <int>,
      "playtime": <number>,
      "preferred_ascension": <int>,
      "floors_climbed": <int>
    }
  ],
  "discovered_cards": ["<CARD.xxx>", ...],     // ⚡ CONFLICT SIGNAL #3
  "discovered_relics": ["<RELIC.xxx>", ...],   //    (sum of all discovered_* counts)
  "discovered_potions": ["<POTION.xxx>", ...],
  "discovered_events": ["<EVENT.xxx>", ...],
  "discovered_acts": ["<ACT.xxx>", ...]
}
```

### Characters (5)

`CHARACTER.IRONCLAD`, `CHARACTER.SILENT`, `CHARACTER.DEFECT`, `CHARACTER.NECROBINDER`, `CHARACTER.REGENT`

## current_run.save / current_run_mp.save

Active run state snapshot. Multiplayer runs have `players.length > 1`.

```json
{
  "schema_version": <int>,
  "ascension": <int>,
  "current_act_index": <int>,
  "run_time": <number>,
  "acts": [
    { "id": "<ACT.xxx>" }
  ],
  "map_point_history": [             // ⚡ CONFLICT SIGNAL (floor count)
    [ { "row": <int>, ... }, ... ],  //   array-of-arrays, one per act
    [ ... ]
  ],
  "players": [
    {
      "character_id": "<CHARACTER.xxx>",
      "current_hp": <int>,
      "max_hp": <int>,
      "gold": <int>,
      "max_energy": <int>,
      "max_potion_slot_count": <int>,
      "base_orb_slot_count": <int>,
      "deck": [
        {
          "id": "<CARD.xxx>",
          "current_upgrade_level": <int>,
          "floor_added_to_deck": <int>,
          "props": { ... }
        }
      ],
      "relics": [
        {
          "id": "<RELIC.xxx>",
          "floor_added_to_deck": <int>,
          "props": { ... }
        }
      ],
      "potions": [
        {
          "id": "<POTION.xxx>",
          "slot_index": <int>
        }
      ]
    }
  ]
}
```

## Conflict Resolution Strategy

All signals are monotonically increasing during normal gameplay.

### progress.save — Cascading Heuristic

| Priority | Signal | Logic |
|----------|--------|-------|
| 1 | `floors_climbed` | Higher wins |
| 2 | Sum of `total_wins + total_losses` across all characters | Higher wins |
| 3 | Sum of all `discovered_*` array counts | Higher wins |
| 4 | `total_playtime` | Higher wins |
| 5 | Tie | Cloud wins (PC is primary platform) |

### current_run.save — Floor Count

Sum of inner array lengths in `map_point_history`. Falls back to `acts.length` if missing. Higher wins. Tie → cloud wins.

### General Rules

- **SHA hash match** → files identical → skip (no sync needed)
- **Corrupt file** (doesn't start with `{`) → other side wins
- **Backup before every overwrite** → `{file}.{unix_timestamp}.{source}.bak`
- **Max 50 backups** per profile, oldest pruned
- **Run history files** (`.run`) → immutable, merge only (union both sides)

## ID Prefix Conventions

| Type | Prefix | Example |
|------|--------|---------|
| Card | `CARD.` | `CARD.Strike` |
| Relic | `RELIC.` | `RELIC.BurningBlood` |
| Potion | `POTION.` | `POTION.FirePotion` |
| Character | `CHARACTER.` | `CHARACTER.IRONCLAD` |
| Event | `EVENT.` | `EVENT.BigFish` |
| Act | `ACT.` | `ACT.ExordiaCrypt` |

## Sources

- [StS2-Launcher SaveProgressComparer.cs](https://github.com/Ekyso/StS2-Launcher/blob/main/src/STS2Mobile/Steam/SaveProgressComparer.cs)
- [StS2-Launcher CloudSyncCoordinator.cs](https://github.com/Ekyso/StS2-Launcher/blob/main/src/STS2Mobile/Steam/CloudSyncCoordinator.cs)
- [STS2-Save-Editor](https://github.com/Houssemamor/STS2-Save-Editor)
