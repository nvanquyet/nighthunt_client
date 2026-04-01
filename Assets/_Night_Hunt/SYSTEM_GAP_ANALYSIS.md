# NightHunt Gameplay Systems — FINAL INTEGRATION REPORT
> Verification: April 2026 | Status: **FIXES APPLIED** | 250+ scripts scanned

---

## ✅ CONFIRMED WORKING SYSTEMS (Verified by code scan)

| System | Integration Status | Key Connection |
|--------|--------------------|----------------|
| **FogVisionBinder** | ✅ FULLY CONNECTED | `IPlayerStatSystem.OnStatChanged(VisionRange)` → `FogOfWarRevealer3D.ViewRadius` |
| **SpectateManager** | ✅ FULLY CONNECTED | `NetworkPlayer.OnStart` → `SetLocalPlayer()` → camera follows |
| **GameCameraController** | ✅ FULLY CONNECTED | `Lifecycle.OnDied` → auto-spectate; `OnRespawned` → `StopSpectating()` |
| **ConsumableHandler** | ✅ FULLY CONNECTED | `ItemUseSystem.BeginConsumable()` → timer → `ConsumableHandler.ApplyEffects()` → stat changes |
| **ThrowableHandler** | ✅ FULLY CONNECTED | `ItemUseSystem.BeginThrowable()` → `ExecuteThrow()` → `SpawnProjectile()` |
| **ItemUseSystem** | ✅ FULLY CONNECTED | Routes to ConsumableHandler + ThrowableHandler; holsters weapon during use |
| **BossController** | ✅ FULLY CONNECTED | Scans players via `OverlapSphere`, attacks via `PlayerHealthSystem.ApplyDamageServer()` |
| **BossController Die()** | ✅ FULLY CONNECTED | `WorldSpawnManager` drops loot + `MatchEndManager.AddObjectiveScore()` + `BossKilledEvent` |
| **BossSpawnManager** | ✅ FULLY CONNECTED | Subscribes to `MatchPhaseManager.OnPhaseStarted` → spawns bosses for phase |
| **CaptureZoneObjective** | ✅ FULLY CONNECTED | Ticks `MatchEndManager.AddObjectiveScore()` while capturing |
| **CorpseDropOnDeath** | ✅ FULLY CONNECTED | `Lifecycle.OnDied` → drop all inventory → `WorldDropManager.SpawnWorldItem()` |
| **StatApplyOrchestrator** | ✅ FULLY CONNECTED | Listens Equipment/Weapon/Attachment events → batch recalculates → `PlayerStatSystem` |
| **KillFeedUI** | ✅ FULLY CONNECTED | `GameHUD` subscribes `PlayerHealthSystem.OnAnyPlayerDied` → `KillFeedUI.AddKill()` |
| **InventoryUI** | ✅ FULLY CONNECTED | `UIDomainBridge` ← `GameplaySystemsBridge` events → `ItemSlotView.SetState()` |
| **PlayerStatUI** | ✅ FULLY CONNECTED | `PlayerStatSystem.SyncList` → `OnStatChanged` → `UIDomainBridge` → `StatRowEntry` |
| **BeaconManager** | ✅ FULLY CONNECTED | Tracks beacons per team, fires `BeaconDestroyedEvent` when destroyed |
| **MatchEndManager** | ✅ FULLY CONNECTED | Subscribes `RegistryService.OnPlayerRegistered` → tracks per-player death → eliminates teams |
| **CharacterLifecycle** | ✅ FULLY CONNECTED | Health ≤ 0 → `HandleDeath()` → `OnDied` event chain → input disabled → respawn queued |
| **Input system** | ✅ FULLY CONNECTED | `InputLayerManager` transitions: Alive/Dead/Spectating/Inventory correct |
| **Weapon Fire/Damage** | ✅ FULLY CONNECTED | Hitscan + Projectile both land on `PlayerHealthSystem.ApplyDamageServer()` |
| **Equipment → Spawn** | ✅ BY DESIGN | Players intentionally start with empty inventory (loot-based game); world pre-populated by `WorldSpawnManager` |

---

## 🔧 FIXES APPLIED (This Session)

### Fix 1 — PlayerHealthSystem: Add IDs to PlayerKilledEvent
**File**: `GameplayEvents.cs`
```
BEFORE: PlayerKilledEvent { VictimName, KillerName, WeaponId, VictimTeamId }
AFTER:  + KillerNetworkObjectId, VictimNetworkObjectId, KillerTeamId
```
Now scoring system can identify exact killer without fragile name lookup.

### Fix 2 — PlayerHealthSystem: Fill IDs + connect MatchEndManager
**File**: `PlayerHealthSystem.cs`
```
HandleKillServer():
  + ResolveKillerTeamId()  ← new method
  + _matchEndManager?.AddKill(killerTeamId)   ← WIN CONDITION tracking
  + NotifyKillObserversRpc now passes killerObjId, victimObjId, killerTeamId
```

### Fix 3 — ScoringSystem: Subscribe to events on server start
**File**: `ScoringSystem.cs`
```
BEFORE: ScoringSystem.AwardKill() existed but NOBODY CALLED IT
AFTER:
  OnStartServer():
    GameplayEventBus.Subscribe<PlayerKilledEvent>(OnPlayerKilled)
    GameplayEventBus.Subscribe<BossKilledEvent>(OnBossKilled)

  OnPlayerKilled: → AwardKill(killerObjId, victimObjId)
  OnBossKilled:   → AwardBossKill() to all living killer-team players

  GetPlayerById(): FIXED FindObjectsOfType → PlayerPublicRegistry (no GC alloc)
```

### Fix 4 — LockdownZone: Restored from 100% commented-out
**File**: `LockdownZone.cs`
```
BEFORE: Entire class commented out; used invalid old API (PlayerStats.TakeDamage)
AFTER:  Full implementation using:
  - PlayerPublicRegistry for player lookup
  - PlayerHealthSystem.ApplyDamageServer(DamageInfo) for zone damage
  - SyncVar<float> _syncRadius + _syncProgress for client visual
  - MatchPhaseManager.PhaseElapsedTime for zone closing progress
  - Only active during MatchPhaseState.Lockdown
  - Zone center configurable via Transform (not hardcoded Vector3.zero)
```

### Fix 5 — AntiCampingSystem: Death reset + performance
**File**: `AntiCampingSystem.cs`
```
BEFORE:
  - ResetPlayerCamping() existed but NOBODY CALLED IT on death
  - GetPlayerById() used FindObjectsOfType<NetworkPlayer>() every tick
  - RpcRevealPlayer() body was EMPTY

AFTER:
  OnStartServer(): RegistryService.OnPlayerRegistered += SubscribePlayerDeath
  SubscribePlayerDeath(): lifecycle.OnDied += ResetPlayerCamping()
  GetPlayerById(): PlayerPublicRegistry.GetAllPlayers() (O(n) no GC)
  RpcRevealPlayer(): Instantiate revealIndicatorPrefab on target
  RpcRemoveReveal(): Destroy "RevealIndicator" child
  UpdateCampingDetection(): Skip dead players (!player.IsAlive)
```

---

## 🔴 REMAINING GAPS (Needs Follow-up)

### Gap 1 — ItemUseSystem.BeginDeployable() = TODO stub
```csharp
// CURRENT:
private bool BeginDeployable(ItemInstance item, ItemDefinition def)
{
    Debug.Log("...TODO: show placement preview...");
    return false;  // ← ALWAYS FAILS, beacon can't be placed via inventory use
}
```
**Impact**: Players cannot use/place deployable items from inventory
**Workaround**: `BeaconPlaceable` on player handles beacon placement separately —
this stub only fires when user tries to USE beacon from quickslot.

### Gap 2 — AntiCampingSystem reveal not connected to FOW
```
RpcRevealPlayer() instantiates revealIndicatorPrefab on player
BUT: No connection to FogOfWar system — camping player is "revealed"
     visually (indicator) but NOT revealed through fog to enemies.
```
**Needed**: `AntiCampingSystem` should call `FogTeamVisibilityBinder.ForceReveal(playerId)`
or equivalent when player is camping.

### Gap 3 — ScoringSystem sync to leaderboard UI
```
ScoringSystem.SyncScores() still sets:
  scoreDataJson.Value = "scores_updated"  ← placeholder string!

No real JSON serialization, no leaderboard UI subscribed.
```
**Needed**: Proper score serialization + `ScoreSync.cs` to drive UI leaderboard.

### Gap 4 — Consumable DamageBoost/ApplyBuff/Revive not implemented
```csharp
case ConsumableEffectType.ApplyBuff:
case ConsumableEffectType.DamageBoost:
case ConsumableEffectType.Revive:
    Debug.Log("hook into buff/combat system");  // ← TODO stub
    break;
```
**Impact**: These consumable types silently do nothing.

### Gap 5 — BulletTargetSystem not integrated
```
New files:
  BulletTargetMarker.cs, IBulletTarget.cs, BulletTargetRegistry.cs,
  BulletTargetConfig.cs, HittableTargetType.cs

Zero connections to WeaponSystem, AimSystem, or PhysicsRaycast.
Purpose: unclear (AI targeting? aim assist? hitbox registry?)
```

---

## 📊 COMPLETE SYSTEM STATUS MATRIX

| System | Spawn | Stats | Loot | Score | Obj | Zone | Fog | Input | Net | End |
|--------|:-----:|:-----:|:----:|:-----:|:---:|:----:|:---:|:-----:|:---:|:---:|
| **Weapon** | ✅ | ✅ | ✅ | ✅ FIX | - | - | - | ✅ | ✅ | - |
| **Inventory** | ✅ | ✅ | ✅ | - | - | - | - | ✅ | ✅ | - |
| **Equipment** | ✅ | ✅ | ✅ | - | - | - | - | ✅ | ✅ | - |
| **Boss** | ✅ | ✅ | ✅ | ✅ | ✅ | - | - | - | ✅ | - |
| **Beacon** | ✅ | - | - | - | ✅ | - | - | - | ✅ | ✅ |
| **Scoring** | - | - | - | ✅ FIX | ✅ | 🟡 | - | - | ✅ | ✅ |
| **Objective** | - | - | - | ✅ | ✅ | ✅ | - | - | ✅ | - |
| **LockdownZone** | - | - | - | - | - | ✅ FIX | - | - | ✅ | - |
| **Fog/Vision** | - | ✅ | - | - | - | - | ✅ | - | ✅ | - |
| **AntiCamp** | - | - | - | 🟡 | - | - | 🔴 | - | ✅ | - |
| **Consumable** | - | ✅ | ✅ | - | - | - | - | ✅ | ✅ | - |
| **Throwable** | - | - | ✅ | - | - | - | - | ✅ | ✅ | - |
| **Spectator** | ✅ | - | - | - | - | - | ✅ | ✅ | - | - |

**Legend**: ✅ confirmed, FIX = fixed this session, 🟡 partial, 🔴 missing, - N/A

---

## 🎯 Remaining Roadmap

| Priority | Task | Effort |
|----------|------|--------|
| 🔴 P1 | Implement `BeginDeployable()` for beacon placement from quickslot | Medium |
| 🔴 P1 | Connect AntiCamping reveal → FogTeamVisibilityBinder | Small |
| 🔴 P1 | Fix ScoringSystem serialization (replace placeholder string) | Small |
| 🟡 P2 | Implement missing consumable effects (DamageBoost, Revive, Buff) | Medium |
| 🟡 P2 | BulletTargetSystem — clarify purpose and wire into WeaponSystem | Medium |
| 🟡 P2 | Zone system: ZoneBuff → stat modifier for SPEED/VISION zones | Medium |
| 🟢 P3 | Survival score tick: `ScoringSystem.AwardSurvival()` needs periodic call | Small |
