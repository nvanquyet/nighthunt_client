# SafeZone System — Scene & Prefab Wiring Guide

> How to wire up the SafeZone refactor in Unity scenes and prefabs.
> No MatchPhaseManager / LockdownZone references remain — all replaced by SafeZoneManager.

---

## 1. SafeZoneManager (map scene — `02_Map_01.unity` etc.)

**Component:** `SafeZoneManager` (extends `NetworkBehaviour`)  
**Where:** A dedicated child GO under `[Managers]`, e.g. `[Managers] / SafeZoneManager`

Required components on the same GO:
- `NetworkObject` (FishNet)
- `SafeZoneManager`

**No serialized Inspector fields** — all state resolved via `static Instance` at runtime.

---

## 2. SafeZoneHUD (client canvas in `01_Home.unity` or a gameplay HUD prefab)

**Prefab structure:**
```
Canvas (Screen Space — Overlay)
  └─ ZoneHUDPanel
       ├─ ZoneRingTransform  (RectTransform — scaled to represent zone radius on minimap)
       ├─ ZoneLabel          (TextMeshProUGUI — "Zone 1", "Final Zone", etc.)
       ├─ CountdownText      (TextMeshProUGUI — "01:23")
       ├─ ShrinkWarning      (GameObject — "ZONE SHRINKING!" indicator, toggled)
       └─ DamageVignette     (Image — full-screen red vignette, toggled by IsOutsideZone)
```

**Component:** `SafeZoneHUD` on `ZoneHUDPanel`

| Inspector Field         | Assign To             | Notes                                      |
|-------------------------|-----------------------|--------------------------------------------|
| `_zoneRingTransform`    | `ZoneRingTransform`   | Scaled each frame to show zone radius      |
| `_zoneLabel`            | `ZoneLabel` TMP       | Shows "Zone N" or "Final Zone"             |
| `_countdownText`        | `CountdownText` TMP   | Shows seconds until next shrink            |
| `_shrinkWarning`        | `ShrinkWarning` GO    | SetActive(true) while zone is shrinking    |
| `_damageVignette`       | `DamageVignette` Image| SetActive(true) when local player outside  |
| `_worldMapDiameter`     | `800` (float)         | World-space diameter of the playable area  |
| `_minimapPixelSize`     | `256` (float)         | Pixel width/height of the minimap widget   |

---

## 3. SurvivalScoreSystem (map scene)

**Where:** Same GO as `ScoringSystem`, e.g. `[Managers] / ScoringSystem`

| Inspector Field | Assign To       |
|-----------------|-----------------|
| `_scoring`      | `ScoringSystem` component on same GO |

**Static Instance** is set in `OnStartServer` — no external wiring needed.

---

## 4. ServerGameManager (map scene — DS only)

**Removed field:** `_matchPhaseManager` — **delete from Inspector if still visible** (stale serialized ref).

`SafeZoneManager` is resolved via `SafeZoneManager.Instance` at runtime — no direct reference needed.

---

## 5. BossSpawnManager (map scene)

Each `BossSpawnEntry` in the Inspector list:

| Old Field    | New Field           | Typical Value                        |
|--------------|---------------------|--------------------------------------|
| `SpawnPhase` (Hunt) | `SpawnAtZoneIndex` | `1` (spawn during zone phase 1→2) |

`SpawnAtZoneIndex=0` → spawns when zone phase 0 starts (first shrink).  
`SpawnAtZoneIndex=1` → spawns when zone phase 1 starts (second shrink). **Most common.**

---

## 6. CaptureZoneObjective (map scene — per capture zone GO)

| Inspector Field        | Typical Value | Notes                              |
|------------------------|---------------|------------------------------------|
| `activeFromZoneIndex`  | `0`           | Zone phase the objective activates |
| `activeUntilZoneIndex` | `2`           | Zone phase the objective deactivates (exclusive) |

Use `activeFromZoneIndex = -1` to have the zone active from match start (before first shrink).  
Use `activeUntilZoneIndex = 99` to keep it active until the end of the match.

---

## 7. MinimapUI (persistent HUD canvas)

| Inspector Field    | Assign To            | Notes              |
|--------------------|----------------------|--------------------|
| `_closeMapButton`  | Close/back Button    | Optional; hides fullscreen map |

---

## 8. MatchUI (persistent HUD canvas)

| Old Field    | New Behavior                                     |
|--------------|--------------------------------------------------|
| `phaseText`  | Shows zone label (e.g. "Zone 1 of 4")            |
| `timerText`  | Shows countdown to next shrink (e.g. "01:30")    |

Both are driven by `SafeZoneHUDProxy` events — no direct `MatchPhaseManager` reference.

---

## 9. Backend Zone Config (per map)

Zone config is stored in `game_maps.zone_config` (JSON column).

- **DS** fetches it on boot via `ServerBootstrap.FetchZoneConfig()` → `GET /api/maps/{mapId}/zone-config`
- **Admin** sets it via `PATCH /api/admin/config/maps/{mapId}/zone` with a full `SafeZoneMatchConfig` JSON body
- If `zone_config` is NULL in the DB, the DS uses `SafeZoneMatchConfig.Default()` (4-phase config, 400→25m radius)

### Example zone config JSON (for 4 phases):
```json
{
  "initialRadius": 400.0,
  "finalZoneMinRadius": 25.0,
  "centerMode": "PureRandom",
  "maxCenterShiftPercent": 0.6,
  "minCenterShiftPercent": 0.1,
  "beaconAllowedInFinalZone": false,
  "baseSurvivalPtsPerSecond": 1.0,
  "captureZoneScorePerSecond": 20.0,
  "killScore": 100.0,
  "bossKillScore": 300.0,
  "killScoreStealPercent": 0.15,
  "phases": [
    { "zoneIndex": 0, "startRadius": 400, "endRadius": 200, "waitBeforeShrink": 60, "shrinkDuration": 90,  "damagePerSecond": 2,  "damageTick": 0.5 },
    { "zoneIndex": 1, "startRadius": 200, "endRadius": 100, "waitBeforeShrink": 45, "shrinkDuration": 75,  "damagePerSecond": 5,  "damageTick": 0.5 },
    { "zoneIndex": 2, "startRadius": 100, "endRadius": 50,  "waitBeforeShrink": 30, "shrinkDuration": 60,  "damagePerSecond": 10, "damageTick": 0.5, "isScoreBonusZone": true, "zoneBonusMultiplier": 1.5 },
    { "zoneIndex": 3, "startRadius": 50,  "endRadius": 25,  "waitBeforeShrink": 20, "shrinkDuration": 45,  "damagePerSecond": 20, "damageTick": 0.5, "minRadiusOverride": 25 }
  ]
}
```

---

## 10. Quick Checklist

- [ ] `SafeZoneManager` GO in every map scene with `NetworkObject` component
- [ ] `SafeZoneHUD` prefab wired with all 5 Inspector fields
- [ ] `SurvivalScoreSystem._scoring` assigned
- [ ] Old `_matchPhaseManager` Inspector refs cleared from `ServerGameManager`
- [ ] `BossSpawnEntry.SpawnAtZoneIndex` set (typically `1`)
- [ ] `CaptureZoneObjective.activeFromZoneIndex` + `activeUntilZoneIndex` set per zone
- [ ] DB `zone_config` populated via V29 migration (or admin PATCH endpoint)
