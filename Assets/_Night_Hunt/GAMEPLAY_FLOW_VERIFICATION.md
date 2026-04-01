# NightHunt Gameplay Flow — Complete Verification Checklist
> Last Updated: April 2026 | Scope: Full match lifecycle from spawn → end

---

## 🎮 1. Match Initialization & Spawn Flow

### ✅ **Spawn System**
- [x] `SpawnSystem` khởi tạo tại `OnNetworkStarted()`
- [x] `SpawnPoint` được đặt trên map
- [x] Player gets respawn token từ backend → `RespawnSystem` ready
- [ ] **TODO**: Verify `BossSpawnManager` được trigger tại startup → ví dụ: phase manager khởi động boss
- [ ] **TODO**: Boss spawn → boss stats được apply qua `PlayerStatSystem` (hay boss có riêng stat system?)

### 🔴 **DETECTED GAP: Boss Integration**
```
SpawnSystem.SpawnPlayer()
  → NetworkPlayer initialized
  → PlayerStatSystem setup

BossSpawnManager.SpawnBoss()   ← **Khi nào gọi???**
  → BossController spawned
  → Boss stats initialized? (health, damage, armor, vision)
  → Boss AI state machine started?
```
**Cần verify**: Boss được spawn tại startup hay khi phase 1 bắt đầu? Có event từ `MatchPhaseManager` không?

---

## ⚙️ 2. Character Setup → Stats → Equipment Flow

### ✅ **Character Lifecycle**
```
Player spawned
  ├─ CharacterLifecycleController.Initialize()
  ├─ CharacterStateMachine → state: Alive
  └─ InputLayerManager.TransitionToState(PlayerAlive)
```

### ✅ **Stat System Initialization**
```
PlayerStatSystem.InitializeStatsServer()
  │ [server]
  ├─ For each PlayerStatType:
  │  └─ base value from PlayerStatConfig
  ├─ _syncedStats pushed via FishNet
  └─ OnStatChanged fires → OnStatChanged event
      → UIDomainBridge → PlayerStatUIPanel.Refresh()
```

### ✅ **Character Models & Animation**
```
PlayerModelLoader.LoadModel(characterDefinition)
  ├─ Spawn HeldPrefab models (weapon/equipment)
  ├─ CharacterAnimationController subscribe to movement state
  └─ CharacterVisualController handles ragdoll on death
```

### ⚠️ **Equipment Auto-Equip at Spawn?**
- [ ] **TODO**: Verify khi player spawn, được auto-equip starting equipment nào?
  - Ví dụ: starter weapon, starter armor?
  - Data source: `ItemDatabase`, `GameplayConfig`, hay hardcode?
  - Nếu có: `EquipmentSystem.EquipItem()` → `StatApplyOrchestrator.ScheduleRecalc()` fires
  - Nếu không: Player bắt đầu naked?

---

## 🎯 3. Combat & Damage Flow (Hitscan + Projectile)

### ✅ **Input → Fire**
```
Player presses Fire button
  → InputLayerManager.InvalidatesPlayerAlive / !InventoryOpen
  → CombatInputHandler.OnFirePressed
  → WeaponSystem.StartFire()
  → [ServerRpc] WeaponSystem.Fire.TryFireOnce()
```

### ✅ **Hitscan Damage**
```
[Server] TryFireOnce()
  ├─ Consume magazine → ItemInstance.AdjustCurrentValue(Magazine, -1)
  ├─ Physics.Raycast(fireOrigin, aimDirection)
  ├─ hit.GetComponent<IHittable>()
  └─ IHittable.TakeHit(DamageInfo)
      → PlayerHealthSystem.TakeHit() [server]
          ├─ DamageCalculator.Calculate(raw, armor)
          ├─ PlayerStatSystem.AdjustCurrentStat(Health, -damage)
          ├─ [ClientRpc] NotifyHitObserversRpc() → VFX/damage numbers
          └─ if health ≤ 0 → CharacterLifecycleController.OnDied()
```

### ✅ **Projectile Damage**
```
WeaponSystem.Fire() → SpawnProjectile [NetworkObject]
  → ProjectileNetworked (server physics)
  → OnTriggerEnter → [Server] Check IHittable
  → TakeHit() *(direct server call, no RPC needed)*
```

### 🔴 **POTENTIAL GAP: Anti-Cheat Validation**
```
PlayerHealthSystem.TakeHit()
  ├─ Distance validation? (ray vs hit point)
  ├─ Armor calculation happens on SERVER only ✅
  └─ BUT: Did attacker.stat.DamageOutput exist? Verify.
```
**Check**: Có `PlayerStatType.DamageOutput` hay weapons tính damage riêng?

---

## 👁️ 4. Vision & Fog of War System

### ⚠️ **Status: Partially Implemented**
- [x] `FogOfWar` folder có 3 binder classes
- [x] `PlayerVisionSystem` exists
- [ ] **TODO**: Verify how `PlayerVisionSystem` feeds into fog:
  ```
  PlayerStatSystem stat: VisionRange
    → updates when equipment changes (via StatApplyOrchestrator)
    → sent to client via SyncVar?
    → FogTeamVisibilityBinder.Update() reads player.VisionRange?
    → FogTeamDebugController can toggle visualization
  ```

### 🔴 **CRITICAL GAP: Team Line-of-Sight**
```
FogTeamVisibilityBinder — does what exactly?
  ├─ Updates which teammates are visible to each other?
  ├─ Broadcasts world state (enemies in fog vs visible)?
  ├─ Works on client or server-authoritative?
  └─ Synced via ClientRpc or automatic?
```

**Missing connection**: How does fog state propagate to:
- Minimap display
- Enemy outlines/culling
- Team communication (cannot see = cannot target)

---

## 🎁 5. Loot & Item Drop System

### ✅ **Corpse Loot Drop (On Death)**
```
CharacterLifecycleController.OnDied()
  ├─ CorpseDropOnDeath.DropAll()
  │  └─ iterate InventorySystem.GetAllItems()
  ├─ WorldDropManager.SpawnWorldItem(ItemInstanceData, deathPosition)
  │  └─ WorldCorpse spawned with ContainerLootSource
  └─ WorldItem [NetworkObject] appears on ground
      └─ Player walks near → InteractionPromptUI
          → press Interact → InventorySystem.AddItem()
```

### ✅ **World Loot Spawning (Initial Map Items)**
```
WorldSpawnManager [server]
  ├─ Iterate WorldItemSpawnPoints
  ├─ Roll ItemDefinition from SpawnTable
  └─ WorldItem.InitializeBeforeSpawn(ItemInstanceData)
      → _syncItemData.Value = data
      → ServerManager.Spawn(worldItemNetObj)
```

### 🔴 **GAP: Beacon-Generated Loot**
```
BeaconManager.SpawnBeacon()
  └─ Does it trigger loot at its location?
     ├─ If YES: WorldDropManager called with beacon position?
     ├─ If NO: Loot only from corpses + initial spawn points?
```
**Need to verify**: Beacon mechanic — does placing beacon generate equipment, consumables, or just vision?

---

## 📊 6. Scoring System

### ⚠️ **Status: Exists but Integration Unclear**
```
ScoringSystem exists in NightHunt.Gameplay.Scoring/
├─ ScoreEvent (defines score reasons)
├─ ScoringSystem (tracks per-player score)
└─ ScoreSync (server → client network sync)
```

### 🔴 **CRITICAL INTEGRATION GAPS**

**What fires score events?**
- [ ] Kill enemy → `+100 points` (who fires: `PlayerHealthSystem.OnDied`?)
- [ ] Loot pickup → `+10 points per value` (who fires: `InventorySystem.OnItemAdded`?)
- [ ] Objective capture → `+500 points` (who fires: `ObjectiveSystem.OnCaptured`?)
- [ ] Elimination streak → bonus multiplier (who fires: `ScoringSystem` or separate tracker?)

**Missing connections**:
```
PlayerHealthSystem.OnDied()
  └─ should call → ScoringSystem.AddScore(killer, ScoreEvent.Kill)?

InventorySystem.OnItemAdded()
  └─ should call → ScoringSystem.AddScore(player, ScoreEvent.LootPickup, item.Value)?

ObjectiveSystem.OnCaptured()
  └─ should call → ScoringSystem.AddScore(team, ScoreEvent.ObjectiveCapture)?
```

**Verification needed**: Search `ScoringSystem` for who calls `AddScore()`. If empty → **GAP FOUND**.

---

## 🎯 7. Objective System Integration

### ⚠️ **Status: Exists but Flow Unclear**
```
ObjectiveSystem exists with 3 types:
├─ CaptureZoneObjective
├─ EMPNodeObjective
└─ RadarStationObjective
```

### 🔴 **DETECTION GAPS**

**Flow questions**:
1. **Objective → Stat Buff?**
   ```
   Player captures objective
     └─ ZoneBuff applied (e.g., +20% movement speed)?
         └─ Stat is applied via StatApplyOrchestrator OR separate?
   ```

2. **Objective → Score?**
   ```
   ObjectiveSystem.OnCaptured()
     └─ Fire ScoringSystem event? (verify above)
   ```

3. **Objective → Team Broadcast?**
   ```
   EnemyTeam sees objective captured
     └─ Via what channel: objective event or map update sync?
   ```

4. **Objective → KillFeed?**
   ```
   Does KillFeedUI show "Team Red captured Radar Station"?
     ├─ Who broadcasts: ObjectiveSystem or MatchUI?
   ```

---

## 🌪️ 8. Zone System (Lockdown?)

### ⚠️ **Status: Exists but Integration Unclear**
```
ZoneSystem, LockdownZone, ZoneBuff, ZoneSync exist
```

### 🔴 **INTEGRATION GAPS**

**Questions**:
1. **Zone → Player Stat Debuff?**
   ```
   Player enters LockdownZone (high radiation?)
     └─ Health drain? Movement penalty?
         └─ Implemented via ZoneBuff → StatApplyOrchestrator?
   ```

2. **Zone → FOW Blocker?**
   ```
   High-radiation zone blocks vision/radar?
     └─ Affects FogTeamVisibilityBinder behavior?
   ```

3. **Zone → Score Penalty?**
   ```
   Player stays in toxic zone too long
     └─ Score penalty applied? Via ScoringSystem?
   ```

---

## 💨 9. Consumable & Item Usage System

### ✅ **System Exists**
```
ItemUseSystem (NetworkBehaviour)
├─ ConsumableHandler
└─ ThrowableHandler
```

### 🔴 **INTEGRATION GAPS**

**Missing connections**:
1. **Consumable stat effects?**
   ```
   Player uses Health Potion
     └─ ItemUseSystem.UseItem() → ConsumableHandler.Consume()
         ├─ Health restored via PlayerStatSystem.SetCurrentStat(Health, +50)?
         ├─ Or separate logic? Animation before stat change?
   ```

2. **Buff consumables (e.g., Super Armor)?**
   ```
   Player uses Armor Buff
     └─ ScoringSystem marks it as active?
     └─ StatApplyOrchestrator registers IStatContributor?
     └─ Time limits? (expires after 30s?)
   ```

3. **Throwable items (grenades, smoke)?**
   ```
   Player throws smoke grenade
     ├─ ClientRpc spawn vfx?
     ├─ ProjectileNetworked handles physics?
     └─ On impact: create FOW blocker? (see #8 Zone System)
   ```

---

## 🔄 10. Equipment & Attachment Stat Cascade

### ✅ **Core System Works**
```
EquipmentSystem.EquipItem()
  → fire OnItemEquipped
  → StatApplyOrchestrator.ScheduleRecalc()
  → LateUpdate: StatApplyOrchestrator.Recalculate()
    ├─ ItemStatComputer.Compute(vestInstance)
    ├─ Collect attachment modifiers
    └─ PlayerStatSystem.AddModifier()
      → field.ProcessDirtyStats()
      → stat synced via SyncList
```

### ⚠️ **Potential Edge Cases**
- [ ] **Armor cascades**: Vest+Helmet+Legs all increase Armor → all recalc LateUpdate?
- [ ] **Weight on pickup**: Add item → weight exceeds capacity → movement slowed? Tested?
- [ ] **Vision range changes**: Equip helmet with optics → zoom changes? Aiming affected?

---

## 📹 11. Spectator & Death Cam Flow

### ⚠️ **Status: Partial**
```
SpectateManager exists
```

### 🔴 **CRITICAL GAPS**

**Flow undefined**:
1. **Player dies**
   ```
   CharacterLifecycleController.OnDied()
     ├─ IsAlive SyncVar → false
     ├─ camera.StopLocalPlayerMode()?
     ├─ SpectateManager.StartSpectating()? ← **Verify**
     └─ DeathScreen shows options
         ├─ Button 1: Spectate teammate
         ├─ Button 2: Spectate enemy (fog-filtered?)
         └─ Button 3: Wait for respawn
   ```

2. **Spectator camera**
   ```
   CameraStateManager.EnterSpectateMode()
     ├─ Switch to SpectateManager.activeSpectateTarget
     ├─ Can cycle through players?
     ├─ Can see through fog?
     └─ Can see UI/scoreboard?
   ```

3. **Return from spectate**
   ```
   RespawnSystem.BeginRespawn() fires
     └─ if spectating: SpectateManager.ExitSpectate()?
     └─ camera.RestoreLocalPlayerMode()?
   ```

---

## 🔔 12. Respawn System & Beacon Integration

### ✅ **Core Works**
```
CharacterLifecycleController.OnDied()
  → RespawnSystem.BeginRespawn()
    ├─ RespawnBeacon selection UI shown
    ├─ Player picks respawn location
    └─ countdown timer
        → spawn player at selected beacon
```

### ⚠️ **Missing Details**
- [ ] **No respawn tokens**: What happens if no beacons available?
- [ ] **Beacon destroyed mid-respawn**: Fallback spawn point?
- [ ] **Anti-spawn-camp**: Check spawn area clear before respawn?

---

## 🎮 13. Input System State Management

### ✅ **Well Structured**
```
InputLayerManager (singleton)
├─ InputState enum: PlayerAlive, InventoryOpen, Dead, Spectating...
├─ _contextStack: PushContext/PopContext
└─ TransitionToState() applies preset ActionMap mask

Triggers:
├─ Spawn: TransitionToState(PlayerAlive)
├─ Inventory button: PushContext(InventoryOpen) → blocks Combat layer
├─ Death: TransitionToState(PlayerDead) → block all except UI, Spectator
├─ Spectating: TransitionToState(Spectating)
```

### 🔴 **Verify State Transitions**

Are all transitions covered?
- [x] Alive → Dead (on health ≤ 0)
- [x] Dead → Spectating (manual or auto?)
- [x] Dead → Respawning → Alive (beacon respawn)
- [ ] **Spectating → Alive** (respawn after spectate): triggered where?
- [ ] Pause menu: where does pause state live? GameManager or InputLayerManager?
- [ ] Cinematic/dialogue sequence: who manages state?

---

## 🏪 14. Drag-Drop UI → Server Flow

### ✅ **Flow Complete**
```
UI drag-drop
  → DragDropController.NotifyDropTarget()
  → DragDropValidator.CanDrop()
  → Execute via GameplaySystemsBridge (ServerRpc)
    ├─ MoveItem(instID, slot)
    ├─ SwapItems(inst1, inst2)
    ├─ EquipItem(slot, instID)
    ├─ AttachItemTo(parentID, attachSlot, childID)
    └─ ... etc
```

### ✅ **Visual Feedback**
```
SyncList callback → InventorySystem.OnItemsChanged()
  → fire OnItemAdded/Removed/Moved/Swapped
  → GameplaySystemsBridge re-publishes
  → UIDomainBridge wires to InventoryScreen
  → ItemSlotView updates (debounced)
```

### ⚠️ **Performance Check**
- [ ] Verify debouncing on fast drag-drops (no redundant rebuilds)

---

## 🌐 15. Network Sync & Anti-Cheat

### ✅ **Server Authority**
```
All mutations (equip, pickup, damage) go through [ServerRpc]
FishNet SyncList/SyncDict propagate to all clients
No client-side authority
```

### ⚠️ **Gaps**
- [ ] **Damage validation**: Check bullet distance vs reported hit distance?
- [ ] **Pickup distance**: Verify player close enough to item?
- [ ] **Equipment swap spam**: Rate limit rapid equip/unequip?
- [ ] **Network latency**: Do hit registrations account for client's ping?

---

## 🎬 16. Match End Flow

### 🔴 **STATUS: UNKNOWN**

**Questions**:
1. **Who determines match end?**
   ```
   MatchPhaseManager.OnPhaseEnded() fires
     ├─ Server broadcasts phase end?
     ├─ MatchEndManager.EndMatch() called?
     └─ ResultsView shown to all clients?
   ```

2. **Score calculation at end?**
   ```
   FinalScore = BaseScore + KillBonus + ObjectiveBonus + LootBonus?
     └─ ScoringSystem.CalculateFinalScore() exists?
   ```

3. **Ranking/Leaderboard**?
   ```
   After match ends
     ├─ Sort players by final score
     ├─ ResultsView renders leaderboard
     └─ Backend records result via HTTP?
   ```

---

## 🔴 17. Systems with No Detected Connections

### Anti-Camping System
```
AntiCampingSystem, CampingDetector, CampingPenalty exist
├─ Where is CampingDetector checking positions?
├─ When player camps > 60s at same spot:
│  ├─ CampingPenalty.ApplyPenalty() called?
│  ├─ Health drain? (via PlayerStatSystem.SetCurrentStat)
│  ├─ Movement speed reduced? (via StatApplyOrchestrator)
│  └─ Score penalty? (via ScoringSystem)
└─ **NOT FOUND in any flow diagrams**
```

### Bullet Target System (NEW)
```
New files:
├─ BulletTargetMarker.cs
├─ HittableTargetType.cs
├─ IBulletTarget.cs
├─ BulletTargetConfig.cs
├─ BulletTargetRegistry.cs

Questions:
├─ What is this? AI target registry?
├─ Who registers targets: enemies only, or all hittables?
├─ Used for: weapon aim assist? AI targeting?
└─ **CONNECTION TO WEAPON SYSTEM UNCLEAR**
```

---

## 📋 18. Complete Integration Checklist

| System | Spawn | Stats | Loot | Score | Objective | Zone | Fog | Input | Network | End |
|--------|-------|-------|------|-------|-----------|------|-----|-------|---------|-----|
| **Weapon** | ✅ | ✅ | ✅ | 🔴 | - | - | - | ✅ | ✅ | - |
| **Inventory** | ✅ | ✅ | ✅ | 🔴 | - | - | - | ✅ | ✅ | - |
| **Equipment** | 🔴 | ✅ | ✅ | - | - | - | - | ✅ | ✅ | - |
| **Beacon** | - | - | 🔴 | 🔴 | ✅ | - | - | - | ✅ | - |
| **Scoring** | - | - | 🔴 | ✅ | 🔴 | 🔴 | - | - | ✅ | 🔴 |
| **Objective** | - | - | - | 🔴 | ✅ | 🔴 | - | - | ✅ | - |
| **Zone** | - | 🔴 | - | 🔴 | ✅ | ✅ | 🔴 | - | ✅ | - |
| **Fog/Vision** | - | 🔴 | - | - | - | 🔴 | ✅ | - | ✅ | - |
| **AntiCamp** | - | 🔴 | - | 🔴 | - | - | - | - | ✅ | - |
| **Consumable** | - | 🔴 | 🔴 | - | - | - | - | ✅ | ✅ | - |

**Legend**: ✅ = connected, 🔴 = disconnected/unknown, - = N/A

---

## 🚀 Priority Fixes

### 🔴 **P0 - Blocking (Match won't end properly)**
1. [ ] Verify `MatchPhaseManager` → `MatchEndManager` data flow
2. [ ] Verify scoring fires on: Kill, Loot, Objective capture, Zone penalties
3. [ ] Verify `ResultsView` calculates and displays final scores

### 🔴 **P1 - Critical (Gameplay feels broken)**
1. [ ] Equipment spawned at character init (not naked start)
2. [ ] Beacon loot drops when beacon destroyed OR when objective complete
3. [ ] Vision system works: players can't see through fog, UI reflects it
4. [ ] Anti-camp penalty applies: health drain + score penalty
5. [ ] Zone buffs/debuffs apply via stat system

### 🟡 **P2 - Important (Playability)**
1. [ ] Consumables apply stats properly
2. [ ] Respawn beacon selection works across network
3. [ ] Spectator mode displays correctly
4. [ ] New `BulletTargetSystem` integrated with weapon aim

---

## ✅ Verification Workflow

```bash
# 1. Search for missing event subscribers
rg "OnKill|OnScore|OnObjectiveCapture|OnLootPickup|OnZoneEnter"

# 2. Verify MatchPhaseManager → MatchEndManager
rg "MatchEndManager" --type cs

# 3. Ensure StatApplyOrchestrator listens to zone/consumable changes
rg "IStatContributor|AddModifier"

# 4. Trace FogOfWar sync to clients
rg "FogOfWar|FogTeam|Visibility" --type cs

# 5. Find all ScoringSystem calls
rg "ScoringSystem\." --type cs
```

