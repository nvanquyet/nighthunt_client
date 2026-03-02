# 🎮 GAMEPLAY SETUP GUIDE - Night Hunt Client

**Ngôn ngữ: Vietnamese | Language: EN**

Hướng dẫn hoàn chỉnh để setup Gameplay trên Client (Máy người chơi).

---

## 📋 MỤC LỤC (TABLE OF CONTENTS)

1. [Tổng Quan Kiến Trúc](#1-tổng-quan-kiến-trúc)
2. [Setup UI Hierarchy](#2-setup-ui-hierarchy)
3. [Setup Weapon Prefab](#3-setup-weapon-prefab)
4. [Setup Projectile Prefab](#4-setup-projectile-prefab)
5. [Setup Player GameObject](#5-setup-player-gameobject)
6. [Scripts & Components Mapping](#6-scripts--components-mapping)
7. [Setup Inventory System](#7-setup-inventory-system)
8. [Setup Scoring System](#8-setup-scoring-system)
9. [Setup Boss System](#9-setup-boss-system)
10. [Setup Match Management](#10-setup-match-management)
11. [Event Bus Integration](#11-event-bus-integration)
12. [Final Integration Checklist](#12-final-integration-checklist)

---

## 1. Tổng Quan Kiến Trúc

### Gameplay Systems Flow

```
PlayerNetworkObject (NetworkBehaviour)
├── PlayerStatSystem (Stats: HP, Stamina, Armor)
├── InventorySystem (Quản lý item)
├── WeaponSystem (Quản lý weapon slots)
├── EquipmentSystem (Armor, items)
├── QuickSlotSystem (4 quick slots)
└── AttachmentSystem

UI Canvas
├── GameHUD (Orchestrator)
│   ├── CombatHUDPanel (Weapon & Quick Slots)
│   │   ├── WeaponSlotButton (Primary, Secondary, Melee)
│   │   └── QuickSlotHUDButton (4 slots)
│   ├── PlayerHUDPanel (HP, Stamina, Armor bars)
│   ├── CrosshairUI
│   ├── InteractionPromptUI
│   ├── MinimapUI
│   └── DeathScreen

Player Weapon Equipment
├── ProjectileWeapon (Script)
├── ProjectileSpawner (Script - auto-added)
├── FirePoint (Transform)
└── VisualProjectilePrefab (Reference)
```

---

## 2. Setup UI Hierarchy

### 2.1 Canvas Structure

**Path:** `Assets/_Night_Hunt/Prefabs/UI/HUD.prefab`

```
Canvas (HUD Master Canvas)
│
├── PlayerHUDPanel
│   ├── HealthBar (Slider)
│   ├── HealthText (TextMeshProUGUI)
│   ├── StaminaBar (Slider)
│   ├── ArmorBar (Slider)
│   └── WeightBar (Slider)
│
├── CombatHUDPanel
│   ├── WeaponSlots
│   │   ├── PrimarySlotButton (WeaponSlotButton) ⭐
│   │   ├── SecondarySlotButton (WeaponSlotButton) ⭐
│   │   └── MeleeSlotButton (WeaponSlotButton) ⭐
│   │
│   ├── QuickSlots
│   │   ├── QuickSlot0 (QuickSlotHUDButton)
│   │   ├── QuickSlot1 (QuickSlotHUDButton)
│   │   ├── QuickSlot2 (QuickSlotHUDButton)
│   │   └── QuickSlot3 (QuickSlotHUDButton)
│   │
│   ├── AmmoDisplay
│   │   ├── AmmoText (TextMeshProUGUI) - Hiện "30 / 90"
│   │   ├── ReloadingIndicator (GameObject)
│   │   └── DepletedWarning (GameObject)
│   │
│   └── CooldownRing (Image) - Reload animation
│
├── CrosshairUI
│   └── CrosshairImage (Image)
│
├── InteractionPromptUI
│   └── PromptText (TextMeshProUGUI)
│
├── MinimapUI
│   └── MiniMapPanel
│
├── DeathScreen
│   ├── KillerNameText
│   └── RespawnButton
│
└── KillFeedUI
    └── KillFeedContainer
```

### 2.2 WeaponSlotButton Setup (Chi tiết)

**Component: `WeaponSlotButton`**
- **Script:** [WeaponSlotButton.cs](Assets/_Night_Hunt/Scripts/Gameplay/GameplaySystems/UI/Combat/WeaponSlotButton.cs)

**Inspector Fields cần setup:**

```
Weapon Slot UI
├── Selected Border (Image) - Viền highlight khi active
├── Ammo Text (TextMeshProUGUI) - Hiện số ammo
├── Empty Slot Overlay (Image) - Hiện khi slot rỗng

Slot Config
└── Slot Type (Enum: Primary / Secondary / Melee) ⭐ QUAN TRỌNG

Action Button (Parent class)
├── Button (Button component)
├── Icon (Image)
├── Cooldown Ring (Image)
└── Label (TextMeshProUGUI) - Tên weapon
```

**Setup Steps:**

1. **Tạo 3 Button cho Primary/Secondary/Melee:**
   - Add `WeaponSlotButton` component
   - Assign `_slotType` trong Inspector:
     - Primary Button → `WeaponSlotType.Primary`
     - Secondary Button → `WeaponSlotType.Secondary`
     - Melee Button → `WeaponSlotType.Melee`

2. **Link các UI Elements:**
   ```
   _selectedBorder → Border Image (được highlight khi active)
   _ammoText → TextMeshPro component hiện "Clip / Reserve"
   _emptySlotOverlay → Overlay image (inactive khi có weapon)
   ```

3. **Script gắn tự động từ:**
   - `ActionButton` base class là parent
   - Mỗi button có Button component riêng

---

## 3. Setup Weapon Prefab

### 3.1 Weapon GameObject Structure

**Prefab Location:** `Assets/_Night_Hunt/Prefabs/Items/Weapons/`

```
WeaponName (GameObject)
│
├── MeshRenderer/SkinnedMeshRenderer (Visual)
├── Collider (Trigger, Layer: Weapon)
├── NetworkIdentity (FishNet)
│
├── ProjectileWeapon (Script) ⭐ MAIN
│   ├── Header: Weapon Settings
│   │   ├── Weapon Config (WeaponConfigData) - Data stats
│   │   └── Fire Point (Transform) - Vị trí bắn
│   │
│   └── Header: Projectile Settings
│       ├── Visual Projectile Prefab (Prefab Ref) ⭐
│       ├── Use Hitscan For Logic (bool) - true/false
│       └── Hit Layers (LayerMask) - Layer bắn trúng
│
├── BoxCollider (Trigger)
│
├── FirePoint (Empty GameObject)
│   └── Position: (0.5, 0, 0.5) từ weapon
│
└── ProjectileSpawner (Added automatically by ProjectileWeapon)
    └── Projectile Prefab (Prefab Ref) ⭐ SAME AS Visual
```

### 3.2 ProjectileWeapon Component Setup

**Script:** `ProjectileWeapon.cs`

**Code:**
```csharp
[Header("Projectile Settings")]
[SerializeField] private GameObject visualProjectilePrefab; // Bullet visual
[SerializeField] private bool useHitscanForLogic = false;   // true=raycast, false=collider
[SerializeField] private LayerMask hitLayers = -1;          // -1 = all layers
```

**Setup trong Inspector:**

| Field | Value | Mô Tả |
|-------|-------|-------|
| `Weapon Config` | WeaponConfigData asset | Dữ liệu stats (ammo, damage, speed) |
| `Fire Point` | Transform child object | Điểm bắn (tip of muzzle) |
| `Visual Projectile Prefab` | Projectile prefab | Prefab cho visual bullet |
| `Use Hitscan For Logic` | `false` | Use collider-based hoặc server sync |
| `Hit Layers` | `-1` (all) hoặc (Enemy\|Env) | Layers để raycast kiểm tra |

**Awake() tự động:**
```csharp
// Tự động add ProjectileSpawner nếu không có
projectileSpawner = GetComponent<ProjectileSpawner>();
if (projectileSpawner == null)
{
    projectileSpawner = gameObject.AddComponent<ProjectileSpawner>();
}
```

---

## 4. Setup Projectile Prefab

### 4.1 Projectile GameObject Structure

**Prefab Location:** `Assets/_Night_Hunt/Prefabs/Items/Projectiles/`

```
Bullet (GameObject)
│
├── MeshFilter + MeshRenderer (Sphere hoặc custom bullet mesh)
├── Collider (Capsule/Sphere + isTrigger=true)
│   ├── Collision Detection: Continuous
│   └── Layer: Projectile (riêng biệt)
│
└── ProjectileComponent (Script) ⭐ MAIN
    ├── Public Method: Initialize(config, direction, useHitscan)
    │
    └── Runtime Fields (set by Initialize):
        ├── weaponConfig (WeaponConfigData)
        ├── direction (Vector3)
        ├── speed (float) - từ config.ProjectileSpeed
        ├── lifetime (float) - MaxRange / speed
        └── useHitscanLogic (bool)
```

### 4.2 ProjectileComponent Setup

**Script:** `ProjectileComponent.cs`

**Tự động initialize (không cần inspector setup):**

```csharp
public void Initialize(WeaponConfigData config, Vector3 dir, bool useHitscan)
{
    weaponConfig = config;       // Weapon stats
    direction = dir.normalized;  // Shooting direction
    speed = config.ProjectileSpeed;
    lifetime = config.MaxRange / speed;
    useHitscanLogic = useHitscan;
    distanceTraveled = 0f;
}
```

**Behavior trong Update():**
- Di chuyển theo hướng với speed
- Apply gravity (nếu ballistic = "Projectile")
- Destroy khi lifetime = 0 hoặc distanceTraveled >= MaxRange
- OnTriggerEnter: Hit detection (nếu không dùng hitscan)

**Prefab Requirements:**

| Component | Setting | Mô Tả |
|-----------|---------|-------|
| Collider | Is Trigger: ✓ | Không vật lý, chỉ detect |
| Collision Detection | Continuous | Không miss nhanh |
| Mesh | Simple sphere | Nhẹ |
| Layer | "Projectile" | Track projectiles |

---

## 5. Setup Player GameObject

### 5.1 Automatic Setup Tool

**Menu:** `Tools → GameplaySystems → Setup Player Prefab`

```
Player Prefab Setup Tool
├── Select Player Prefab (Prefab field)
├── Enable Debug UI (toggle)
├── Component Status (checker)
└── Setup Player Prefab (button)
```

**Required Components (tự động thêm):**
- ✓ PlayerStatSystem
- ✓ InventorySystem
- ✓ WeaponSystem
- ✓ EquipmentSystem
- ✓ QuickSlotSystem
- ✓ AttachmentSystem

### 5.2 Manual Setup (Nếu tool không work)

**Đầu tiên, đảm bảo Player có:**

```
Player (NetworkObject)
├── NetworkObject (FishNet) ✓
├── Transform ✓
├── Camera ✓
└── Rigidbody ✓
```

**Add Components (Đúng thứ tự):**

1. **PlayerStatSystem**
   ```
   Script: PlayerStatSystem.cs
   Fields:
   - _statConfig → PlayerStatConfig asset
   - _gameplayConfig → GameplayConfig asset
   - _showDebugUI → true/false
   ```

2. **InventorySystem**
   ```
   Script: InventorySystem.cs
   Fields:
   - _gameplayConfig → GameplayConfig
   - _inventoryConfig → InventoryConfig
   - _statSystem → PlayerStatSystem (reference bên trên)
   - _showDebugUI → true/false
   ```

3. **WeaponSystem**
   ```
   Script: WeaponSystem.cs
   Fields:
   - _inventoryConfig → InventoryConfig
   - _statSystemComponent → PlayerStatSystem component
   - _inventorySystemComponent → InventorySystem component
   - _slotPriority → [Primary, Secondary, Melee]
   ```

4. **EquipmentSystem**
   ```
   Script: EquipmentSystem.cs
   Fields:
   - _inventoryConfig → InventoryConfig
   - (các equipment slots)
   ```

5. **QuickSlotSystem**
   ```
   Script: QuickSlotSystem.cs
   Fields:
   - _quickSlotConfig → QuickSlotConfig
   - (4 slots)
   ```

6. **AttachmentSystem**
   ```
   Script: AttachmentSystem.cs
   (Config tự động)
   ```

---

## 6. Scripts & Components Mapping

### 6.1 Combat-Related Scripts

| Script | Location | Attach To | Purpose |
|--------|----------|-----------|---------|
| **ProjectileWeapon** | `Character/Combat/Weapons/` | Weapon Object | Bắn projectile |
| **ProjectileSpawner** | `Character/Combat/Weapons/` | (Auto-add) | Spawn projectile network |
| **ProjectileComponent** | `Character/Combat/Weapons/` | Projectile Prefab | Xử lý projectile behavior |
| **WeaponBase** | `Character/Combat/Weapons/` | Parent class | Weapon abstraction |
| **HitscanWeapon** | `Character/Combat/Weapons/` | Weapon Object | Bắn hitscan (raycast) |

### 6.2 UI-Related Scripts

| Script | Location | Attach To | Purpose |
|--------|----------|-----------|---------|
| **GameHUD** | `UI/` | Canvas | Orchestrate all HUD panels |
| **CombatHUDPanel** | `GameplaySystems/UI/Combat/` | CombatPanel | Weapon + Quick slots display |
| **WeaponSlotButton** | `GameplaySystems/UI/Combat/` | Weapon Slot Button | 1 weapon slot (Primary/Sec/Melee) |
| **QuickSlotHUDButton** | `GameplaySystems/UI/Combat/` | Quick Slot Button | 1 quick slot |
| **CrosshairUI** | `GameplaySystems/UI/Combat/` | Canvas child | Crosshair display |
| **PlayerHUDPanel** | `GameplaySystems/UI/` | PlayerStatsPanel | HP/Stamina/Armor bars |

### 6.3 Gameplay Systems Scripts

| Script | Location | Attach To | Purpose |
|--------|----------|-----------|---------|
| **PlayerStatSystem** | `StatSystem/Systems/` | Player | Player stats (HP, Stamina) |
| **InventorySystem** | `Inventory/` | Player | Inventory management |
| **WeaponSystem** | `GameplaySystems/Systems/Weapon/` | Player | Weapon equip/unequip |
| **EquipmentSystem** | `GameplaySystems/Systems/equipment/` | Player | Armor management |
| **QuickSlotSystem** | `GameplaySystems/Systems/QuickSlot/` | Player | 4 quick slots |
| **AttachmentSystem** | `GameplaySystems/Systems/Attachment/` | Player | Weapon attachments |

---

## 7. Setup Inventory System

### 7.1 Inventory Architecture

```
InventorySystem (NetworkBehaviour)
├── Server-Authoritative Item Storage
│   ├── Dictionary<int, ItemInstanceData> (Key = InstanceID)
│   ├── SyncList<ItemInstanceData> (Network Replication)
│   └── ItemDatabase (Centralized item definitions)
│
├── Core Operations
│   ├── AddItem(itemId, quantity)          → Return instanceId
│   ├── RemoveItem(instanceId, quantity)   → Return true/false
│   ├── StackItems(fromInstance, toInstance)
│   ├── SplitStack(instanceId, amount)     → Return new instanceId
│   ├── SwapItems(slot1, slot2)
│   ├── DropItem(instanceId)                → Spawn prefab at player
│   └── ClearInventory()
│
└── Weight System
    ├── TotalWeight = Sum of all items
    ├── Max Weight = PlayerStats.MaxCarryWeight
    ├── Stamina Modifier = Weight / MaxWeight
    └── Movement Speed *= (1.0 - StaminaModifier * 0.5)
```

### 7.2 Item Data Structure

**ItemInstanceData (Network Synchronized):**

```csharp
public struct ItemInstanceData : INetSerializable
{
    public int InstanceId;           // Unique per item
    public int ItemDefinitionId;     // Reference to ItemDatabase
    public int StackQuantity;        // How many in stack
    public int Durability;           // 0-100
    public List<int> AttachmentIds;  // For weapons
    public string CustomData;        // JSON serialized
}
```

**ItemDefinition (Database):**

```csharp
[System.Serializable]
public class ItemDefinition
{
    public int ItemId;               // Unique ID
    public string DisplayName;       // "AK-47", "Med Kit"
    public string Description;
    public Sprite Icon;
    public float Weight;             // kg
    public int MaxStack;             // -1 = not stackable
    public ItemType ItemType;        // Weapon, Consumable, etc.
    public GameObject DropPrefab;    // Dropped item visual
    public float DropLifetime;       // Despawn time
}
```

### 7.3 Setup Steps

**Step 1: Create ItemDatabase Asset**

```
Right-click in Assets/_Night_Hunt/Resources/Configs/
→ Create → Night Hunt → Item Database
Name: ItemDatabase.asset
```

**Step 2: Add Items to Database**

Open ItemDatabase.asset inspector:

```
Item Database
├── Size: 10 (expand as needed)
│
├── [0] AK-47
│   ├── Item Id: 1001
│   ├── Display Name: "AK-47 Rifle"
│   ├── Description: "5.56mm Rifle"
│   ├── Icon: rifle_icon [sprite]
│   ├── Weight: 3.5
│   ├── Max Stack: 1
│   ├── Item Type: Weapon
│   ├── Drop Prefab: DroppedWeapon.prefab
│   └── Drop Lifetime: 300
│
├── [1] Medical Kit
│   ├── Item Id: 2001
│   ├── Display Name: "Medical Kit"
│   ├── Weight: 0.5
│   ├── Max Stack: 5
│   ├── Item Type: Consumable
│   ├── Drop Prefab: DroppedMedKit.prefab
│   └── Drop Lifetime: 60
│
└── [2] Ammo Box (5.56mm)
    ├── Item Id: 3001
    ├── Display Name: "5.56mm Ammunition"
    ├── Weight: 2.0
    ├── Max Stack: 999
    ├── Item Type: Ammo
    ├── Drop Prefab: DroppedAmmo.prefab
    └── Drop Lifetime: 120
```

**Step 3: Configure InventorySystem on Player**

Player prefab → Add Component → **InventorySystem.Core**

Inspector settings:

```
Inventory System (Core)
├── Inventory Config: InventoryConfig.asset
├── Item Database: ItemDatabase.asset
├── Max Slots: 20
├── Starting Items: 0 (expand to add starting gear)
│   └── [0] Item
│       ├── Item Definition: ItemDefinition (from DB)
│       ├── Quantity: 1
│       └── Durability: 100
└── Show Debug UI: true (in development)
```

### 7.4 Inventory Events (For UI)

**Events Published by InventorySystem:**

```csharp
public event Action<ItemInstanceData> OnItemAdded;
public event Action<int> OnItemRemoved;         // instanceId
public event Action OnItemsSwapped;
public event Action OnInventoryCleared;
public event Action<float> OnWeightChanged;     // current / max
public event Action<int> OnItemCountChanged;    // total items
```

**UI Bridge Setup:**

Player → Add Component → **UIDomainBridge**

```
UI Domain Bridge
├── Listen Inventory Events: ✓
├── Listen Equipment Events: ✓
├── Listen Weapon Events: ✓
├── OnItemAdded → HUDPanel.UpdateInventoryDisplay()
├── OnItemRemoved → HUDPanel.RemoveItemVisual()
└── OnWeightChanged → PlayerHUDPanel.UpdateWeightBar()
```

---

## 8. Setup Scoring System

### 8.1 Scoring Architecture

```
ScoringSystem (NetworkBehaviour - Server Only)
│
├── Score Types
│   ├── Kill (1 point) * Phase Multiplier
│   ├── Assist (0.5 points)
│   ├── Boss Kill (10 points)
│   ├── Objective Capture (varies)
│   └── Survival (1 point per minute)
│
├── Data Tracking
│   ├── PlayerScore Dictionary<uint, PlayerScore>
│   │   ├── PlayerId
│   │   ├── TotalScore (int)
│   │   ├── Kills (int)
│   │   ├── Assists (int)
│   │   ├── Deaths (int)
│   │   └── LastScoreTime (float)
│   │
│   └── TeamScore Dictionary<int, TeamScore>
│       ├── TeamId
│       ├── TotalScore (int)
│       ├── MembersCount (int)
│       └── JsonData (SyncVar - network)
│
├── Phase Multipliers (From MatchPhaseManager)
│   ├── Lobby Phase: 0.5x
│   ├── Hunt Phase: 2.0x
│   └── Endgame Phase: 1.0x
│
└── Network Sync
    ├── SyncVar<string> scoreDataJson
    ├── Serializes all scores to JSON
    └── Replicated every score award
```

### 8.2 ScoreEvent Publishing

**Event Structure:**

```csharp
public class ScoreEvent
{
    public int TeamId { get; set; }
    public int Points { get; set; }
    public string Action { get; set; }  // "Kill", "Assist", etc.
    public uint PlayerId { get; set; }
    public float Timestamp { get; set; }
}
```

### 8.3 Setup Steps

**Step 1: Create ScoringConfig**

```
Right-click Assets/_Night_Hunt/Resources/Configs/
→ Create → Night Hunt → Scoring Config
Name: ScoringConfig.asset
```

**Step 2: Configure Scoring Rules**

Open ScoringConfig inspector:

```
Scoring Config
│
├── Base Scores
│   ├── Kill Score: 1
│   ├── Assist Score: 0 (actually 0.5x)
│   ├── Boss Kill Score: 10
│   ├── Objective Capture: 5
│   └── Survival Per Minute: 1
│
├── Phase Multipliers
│   ├── Lobby: 0.5
│   ├── Hunt: 2.0
│   └── Endgame: 1.0
│
├── Assist Rules
│   ├── Assist Timeout: 10 (seconds)
│   ├── Minimum Damage: 25 (damage dealt before assist)
│   └── Max Assistants Per Kill: 3
│
└── Survival Scoring
    ├── Enabled: true
    ├── Interval: 60 (seconds between awards)
    └── Points Per Interval: 1
```

**Step 3: Add ScoringSystem to GameplayBootstrap**

```
Scene: GameplayScene
GameObject: GameplayBootstrap (or create it)

Add Component: ScoringSystem.cs

Inspector:
├── Scoring Config: ScoringConfig.asset
├── Phase Manager: MatchPhaseManager (auto-find)
└── Show Debug UI: true
```

**Step 4: Hook Events to UI**

```
ScoreHUDPanel (UI Canvas child)
├── Add Component: ScoreDisplayUI.cs
└── In Start():
    ScoringSystem.OnScoreEvent += UpdateScoreDisplay;
    ScoringSystem.OnScoreDataSynced += UpdateTeamScores;
```

### 8.4 Awarding Scores (From Combat Scripts)

**In WeaponSystem.OnPlayerKill():**

```csharp
public void OnPlayerKilled(uint targetPlayerId, uint killerPlayerId)
{
    // Server-only
    ScoringSystem.AwardKill(
        playerId: killerPlayerId,
        baseScore: 1,
        multiplier: 1.0f  // Already applies phase multiplier internally
    );
    
    // Check for assists
    ScoringSystem.CheckForAssists(
        targetPlayerId: targetPlayerId,
        killTime: Time.time
    );
}
```

**In BossController.OnDeath():**

```csharp
private void OnBossDeath()
{
    // Award boss kill to last attacker
    ScoringSystem.AwardBossKill(
        playerId: lastAttackerId,
        bossReward: 10
    );
}
```

---

## 9. Setup Boss System

### 9.1 Boss Architecture

```
BossController (NetworkBehaviour - Server Authority)
│
├── State Machine
│   ├── Idle → Waiting for player
│   ├── Aggro → Player detected, moving to player
│   ├── Attack → In melee range, attacking
│   └── Dead → Destroyed, loot spawned
│
├── AI Movement (NavMeshAgent)
│   ├── Aggro Radius: 20 units
│   ├── Attack Radius: 3 units
│   ├── Movement Speed: config.BossSpeed
│   └── Position SyncVar (clients see position)
│
├── Health (SyncVar<float>)
│   ├── Current HP
│   ├── Max HP (from config)
│   ├── Armor calculation
│   └── Damage reduction
│
├── Attack System
│   ├── Attack Damage: 50
│   ├── Attack Cooldown: 2 seconds
│   ├── Attack Range: 3 units
│   └── Detected via raycast in attack radius
│
└── Loot Drop (On Death)
    ├── Spawn BossChest prefab
    ├── Use ItemDropTable for weighted loot
    └── Chest expires after 5 minutes
```

### 9.2 Boss Configuration

**BossSpawnConfigData:**

```csharp
[System.Serializable]
public class BossSpawnConfigData
{
    public string BossId;              // "boss_alpha", "boss_elite"
    public string SpawnPointTag;       // Tag to find spawn location
    public int MaxHealth;              // 5000
    public float AggroRadius;          // 20
    public float AttackRadius;         // 3
    public float AttackDamage;         // 50
    public float AttackCooldown;       // 2
    public float BossSpeed;            // 5
    public string LootTableId;         // Reference to ItemDropTable
}
```

### 9.3 Setup Steps

**Step 1: Create Boss Prefab**

```
Create GameObject: Boss_Alpha

Components:
├── MeshRenderer (boss mesh)
├── Collider (CapsuleCollider, not trigger)
├── NavMeshAgent
│   ├── Agent Type: Humanoid
│   ├── Speed: 5
│   └── Stopping Distance: 2
├── NetworkObject (FishNet)
└── BossController.cs

Inspector Setup:
├── Boss Id: "boss_alpha"
├── Max HP: 5000
├── Aggro Radius: 20
├── Attack Radius: 3
├── Attack Damage: 50
├── Attack Cooldown: 2
├── Player Layer Mask: "Player"
├── Boss Chest Prefab: BossChest.prefab
└── Show Debug: true
```

**Step 2: Create ItemDropTable Asset**

```
Right-click Assets/_Night_Hunt/Resources/Configs/
→ Create → Night Hunt → Item Drop Table
Name: BossLootTable.asset
```

**Step 3: Configure Loot Drops**

Open BossLootTable inspector:

```
Item Drop Table
│
├── Size: 4
│
├── [0] Weapon Drop
│   ├── Item Definition: AK-47 (from ItemDatabase)
│   ├── Drop Weight: 30 (30% chance)
│   ├── Quantity: 1
│   └── Durability: 75
│
├── [1] Ammo Drop
│   ├── Item Definition: Ammo Box (5.56mm)
│   ├── Drop Weight: 40 (40% chance)
│   ├── Quantity: 3
│   └── Durability: 100
│
├── [2] Medical Kit
│   ├── Item Definition: Medical Kit
│   ├── Drop Weight: 20 (20% chance)
│   ├── Quantity: 2
│   └── Durability: 100
│
└── [3] Armor Part
    ├── Item Definition: Armor Plate
    ├── Drop Weight: 10 (10% chance)
    ├── Quantity: 1
    └── Durability: 100
```

**Step 4: Create BossChest Prefab**

```
Create GameObject: BossChest

Components:
├── MeshRenderer (chest visual)
├── BoxCollider (Trigger)
├── NetworkObject (FishNet)
└── BossChest.cs

Inspector:
├── Loot Table: BossLootTable.asset
├── Open Duration: 5 (seconds animation)
├── Despawn After: 300 (5 minutes)
└── Particle Effect: (optional)
```

**Step 5: Configure BossSpawnManager**

```
Scene: GameplayScene
Create GameObject: BossSpawner

Add Component: BossSpawnManager.cs

Inspector:
├── Boss Prefab: Boss_Alpha.prefab
├── Spawn Point Tag: "BossSpawn"
├── Max Bosses: 1
├── Auto Spawn On Hunt Phase: true
├── Respawn Delay: 30 (if boss dies before hunt ends)
└── Loot Table: BossLootTable.asset
```

**Step 6: Scene Setup - Spawn Points**

```
In GameplayScene:
Create empty GameObject: BossSpawnPoint_1
├── Tag: "BossSpawn"
├── Position: (100, 1, 100)  // Center of arena
└── (BossSpawnManager will find this by tag)
```

### 9.4 Events Published

```csharp
public class BossKilledEvent
{
    public string BossId;
    public uint LastAttackerId;
    public Vector3 DeathPosition;
    public int LootReward;
}

// Subscribed by:
- ScoringSystem (award points)
- UIManager (show kill message)
- GameplayEventBus (broadcast to all systems)
```

---

## 10. Setup Match Management

### 10.1 Match Phase System

```
MatchPhaseManager (Orchestrator)
│
├── Phase 1: LOBBY (Duration: 30 seconds)
│   ├── Players spawn
│   ├── Score multiplier: 0.5x
│   ├── Boss: Not spawned
│   └── Events: OnLobbyStarted, OnLobbyEnded
│
├── Phase 2: HUNT (Duration: 720 seconds - 12 minutes)
│   ├── Boss spawns at start
│   ├── Score multiplier: 2.0x
│   ├── Prediction active
│   ├── Beacons active
│   ├── Zone: Growing
│   └── Events: OnHuntStarted, BossSpawned, OnHuntEnded
│
└── Phase 3: ENDGAME (Duration: 180 seconds - 3 minutes)
    ├── Boss may despawn (configurable)
    ├── Score multiplier: 1.0x
    ├── Respawns allowed
    ├── Zone: Shrinking
    └── Events: OnEndgameStarted, MatchEnded
```

### 10.2 Phase Transition Logic

**MatchPhaseManager.cs:**

```csharp
private IEnumerator PhaseTransitionRoutine()
{
    // Phase 1: Lobby
    CurrentPhase = MatchPhase.Lobby;
    OnPhaseChanged?.Invoke(MatchPhase.Lobby);
    yield return new WaitForSeconds(lobbySec);
    
    // Phase 2: Hunt
    CurrentPhase = MatchPhase.Hunt;
    OnPhaseChanged?.Invoke(MatchPhase.Hunt);
    BossSpawnManager.SpawnBoss();  // Triggers boss spawn
    yield return new WaitForSeconds(huntSec);
    
    // Phase 3: Endgame
    CurrentPhase = MatchPhase.Endgame;
    OnPhaseChanged?.Invoke(MatchPhase.Endgame);
    yield return new WaitForSeconds(endgameSec);
    
    // Match Over
    MatchEndManager.EndMatch();
}
```

### 10.3 Elimination & Tie-Break

**MatchEndManager.cs:**

```csharp
public class TeamElimination
{
    bool IsTeamEliminated(int teamId)
    {
        // Team eliminated when:
        // 1. ALL players dead
        // 2. AND no active beacons
        
        bool allDead = GetTeamPlayerCount(teamId) == 0;
        bool noBeacons = BeaconSystem.GetActiveBeaconCount(teamId) == 0;
        
        return allDead && noBeacons;
    }
}

public class TieBreaker
{
    // Winner determined by:
    // 1. Most alive players
    int aliveDiff = teamA.AliveCount - teamB.AliveCount;
    if (aliveDiff != 0) return aliveDiff > 0 ? teamA : teamB;
    
    // 2. Higher total score
    int scoreDiff = teamA.TotalScore - teamB.TotalScore;
    if (scoreDiff != 0) return scoreDiff > 0 ? teamA : teamB;
    
    // 3. Draw
    return null;
}
```

### 10.4 Setup Steps

**Step 1: Create MatchPhaseManager in Scene**

```
Scene: GameplayScene
Create GameObject: MatchPhaseManager

Add Component: MatchPhaseManager.cs

Inspector:
├── Player Input Manager: (reference)
├── Lobby Duration: 30 (seconds)
├── Hunt Duration: 720 (12 minutes)
├── Endgame Duration: 180 (3 minutes)
└── Auto Start On Awake: true
```

**Step 2: Create MatchEndManager**

```
Same scene, create: MatchEndManager

Add Component: MatchEndManager.cs

Inspector:
├── Phase Manager: MatchPhaseManager (reference)
├── Show Results UI: true
└── Results UI Prefab: MatchResultsUI.prefab
```

**Step 3: Hook Events in ScoringSystem**

```csharp
public void OnPhaseChanged(MatchPhase newPhase)
{
    // Called by MatchPhaseManager.OnPhaseChanged event
    
    // Update multipliers
    _currentPhaseMultiplier = 
        ScoreMultiplier.GetPhaseMultiplier(newPhase);
    
    // Reset survival timer
    if (newPhase == MatchPhase.Hunt)
    {
        _survivalTickTimer = 0f;
    }
}
```

**Step 4: Create Match Results UI**

```
Canvas/MatchResults
├── Panel (background)
├── Winner Text
│   ├── "Team Blue Wins!"
│   ├── "Final Score: 1250"
│   └── "Alive Members: 3"
├── Team Stats
│   ├── TeamA_Score
│   ├── TeamA_Kills
│   ├── TeamA_Deaths
│   ├── TeamB_Score
│   ├── TeamB_Kills
│   └── TeamB_Deaths
└── Button
    ├── Return to Lobby (OnClick)
    └── OnClick → Restart match or return to menu
```

---

## 11. Event Bus Integration

### 11.1 GameplayEventBus Architecture

**Central Hub for All Systems:**

```
GameplayEventBus (Singleton)
│
├── Published Events
│   ├── BossSpawnedEvent
│   ├── BossKilledEvent
│   ├── BossDamagedEvent
│   ├── ScoreEvent
│   ├── ScoreDataSyncedEvent
│   ├── ItemAcquiredEvent
│   ├── ItemUsedEvent
│   ├── PlayerKilledEvent
│   ├── PhaseChangedEvent
│   ├── MatchEndedEvent
│   └── (others)
│
└── Subscribers
    ├── UI Systems (update display)
    ├── Audio Manager (play sounds)
    ├── VFX Manager (play effects)
    ├── Analytics (log events)
    └── Network Manager (broadcast)
```

### 11.2 Event Examples

**BossSpawnedEvent:**

```csharp
public class BossSpawnedEvent
{
    public string BossId { get; set; }
    public Vector3 SpawnPosition { get; set; }
    public float MaxHealth { get; set; }
    public float Timestamp { get; set; }
}

// Published by BossSpawnManager when boss spawns
// Subscribed by:
// - UI to show "Boss Spawned!" message
// - Audio to play boss music
// - Minimap to add boss marker
```

**ScoreEvent:**

```csharp
public class ScoreEvent
{
    public uint PlayerId { get; set; }
    public int TeamId { get; set; }
    public string Action { get; set; }  // "Kill", "Assist", "BossKill"
    public int PointsAwarded { get; set; }
    public float Timestamp { get; set; }
}

// Published by ScoringSystem on every score
// Subscribed by:
// - UI ScoreBoard to show score updates
// - Floating text above player ("+ 10 points")
// - Analytics to log player performance
```

### 11.3 Subscribe to Events

**In any system:**

```csharp
public class UIScoreDisplay : MonoBehaviour
{
    private void OnEnable()
    {
        GameplayEventBus.Instance.OnScoreEvent += HandleScoreEvent;
        GameplayEventBus.Instance.OnBossSpawnedEvent += HandleBossSpawned;
    }
    
    private void OnDisable()
    {
        GameplayEventBus.Instance.OnScoreEvent -= HandleScoreEvent;
        GameplayEventBus.Instance.OnBossSpawnedEvent -= HandleBossSpawned;
    }
    
    private void HandleScoreEvent(ScoreEvent scoreEvent)
    {
        // Update UI with score
        scoreText.text = $"+ {scoreEvent.PointsAwarded}";
        scoreText.color = GetColorForAction(scoreEvent.Action);
    }
    
    private void HandleBossSpawned(BossSpawnedEvent bossEvent)
    {
        // Show boss message
        bossSpawnedUI.SetActive(true);
        bossNameText.text = $"Boss: {bossEvent.BossId}";
    }
}
```

### 11.4 Publish Events

**From ScoringSystem:**

```csharp
[Server]
public void AwardKill(uint playerId, int baseScore, float multiplier)
{
    // Calculate final score
    int finalScore = Mathf.RoundToInt(baseScore * _currentPhaseMultiplier);
    
    // Update data
    playerScores[playerId].Score += finalScore;
    
    // Publish event
    var scoreEvent = new ScoreEvent
    {
        PlayerId = playerId,
        TeamId = GetTeamId(playerId),
        Action = "Kill",
        PointsAwarded = finalScore,
        Timestamp = Time.time
    };
    
    GameplayEventBus.Instance.PublishScoreEvent(scoreEvent);
}
```

---

## 12. Final Integration Checklist

### Master Setup (Do in this order)

#### Phase A: Data & Configs

- [ ] **Step 1:** Create ItemDatabase.asset with all items (weapons, ammo, consumables)
- [ ] **Step 2:** Create ScoringConfig.asset with score rules and multipliers
- [ ] **Step 3:** Create ItemDropTable.asset for boss loot
- [ ] **Step 4:** Create BossSpawnConfigData (in GameplayConfig)

#### Phase B: Game Objects & Scene

- [ ] **Step 5:** Create ProjectileWeapon prefab (with FirePoint)
- [ ] **Step 6:** Create Projectile prefab (visual + ProjectileComponent)
- [ ] **Step 7:** Setup Player prefab with all gameplay components
- [ ] **Step 8:** Add InventorySystem component to Player
- [ ] **Step 9:** Create Boss_Alpha prefab with BossController
- [ ] **Step 10:** Create BossChest prefab with BossChest script

#### Phase C: Scene Setup

- [ ] **Step 11:** Create BossSpawnPoint tagged "BossSpawn" in scene
- [ ] **Step 12:** Add BossSpawnManager to scene
- [ ] **Step 13:** Add ScoringSystem to scene (or GameplayBootstrap)
- [ ] **Step 14:** Add MatchPhaseManager to scene
- [ ] **Step 15:** Add MatchEndManager to scene

#### Phase D: UI Setup

- [ ] **Step 16:** Create Canvas with GameHUD orchestrator
- [ ] **Step 17:** Create WeaponSlotButtons (Primary, Secondary, Melee)
- [ ] **Step 18:** Create QuickSlotButtons (4 slots)
- [ ] **Step 19:** Add event listeners to UI elements
- [ ] **Step 20:** Create Score Display UI
- [ ] **Step 21:** Create Match Results UI

#### Phase E: Connections

- [ ] **Step 22:** Assign configs to all components
- [ ] **Step 23:** Verify GameplayEventBus subscriptions
- [ ] **Step 24:** Test: Play scene and verify systems initialize
- [ ] **Step 25:** Test: Fire weapon → projectile appears
- [ ] **Step 26:** Test: Score awarded → UI updates
- [ ] **Step 27:** Test: Boss spawns at Hunt phase
- [ ] **Step 28:** Test: Boss death → chest spawned, loot visible

#### Phase F: Network Sync (If using Dedicated Server)

- [ ] **Step 29:** Verify all NetworkBehaviours properly replicate
- [ ] **Step 30:** Test score sync from server to clients
- [ ] **Step 31:** Test inventory sync across network
- [ ] **Step 32:** Test boss health bar visible to all clients
- [ ] **Step 33:** Test loot drop synchronized

### Component Dependency Graph

```
Player (NetworkObject)
├── PlayerStatSystem ✓
├── InventorySystem (depends on: ItemDatabase)
├── WeaponSystem (depends on: InventorySystem, ProjectileWeapon prefab)
├── EquipmentSystem
├── QuickSlotSystem
└── AttachmentSystem

GameplayScene
├── ScoringSystem (depends on: ScoringConfig, MatchPhaseManager)
├── MatchPhaseManager (standalone)
├── MatchEndManager (depends on: MatchPhaseManager)
├── BossSpawnManager (depends on: BossController prefab, ItemDropTable)
└── GameplayEventBus ✓

Canvas
├── GameHUD (depends on: Player, all UI panels)
├── CombatHUDPanel (depends on: WeaponSystem)
├── PlayerHUDPanel (depends on: PlayerStatSystem)
├── ScoreDisplay (depends on: ScoringSystem)
└── MatchResultsUI (depends on: MatchEndManager)
```

### Verification Steps

**Run the following tests:**

1. **Inventory Test**
   ```
   1. Start scene
   2. Check Player has InventorySystem
   3. Debug: AwardItemToPlayer(itemId=1001, qty=1)
   4. Verify item appears in inventory
   5. Test add/remove/swap operations
   ```

2. **Scoring Test**
   ```
   1. Start scene (verify phase = Lobby)
   2. Debug: SimulateKill(playerId, killerPlayerId)
   3. Verify score increases (should be 0.5x multiplier in Lobby)
   4. Wait for phase change to Hunt
   5. Simulate kill again
   6. Verify score increases 2.0x
   ```

3. **Boss Test**
   ```
   1. Start scene
   2. Wait for phase = Hunt
   3. Observe boss spawn at BossSpawnPoint
   4. Verify NavMeshAgent pathfinding works
   5. Debug: DamageBoss(amount=1000)
   6. Verify boss dies and chest spawns
   7. Check chest contains loot items
   ```

4. **Match Phase Test**
   ```
   1. Start scene
   2. Observe 30 sec lobby (no boss, 0.5x score)
   3. Phase changes to Hunt (boss spawns, 2.0x score)
   4. Observe 12 min hunt phase
   5. Phase changes to Endgame (1.0x score)
   6. Observe 3 min endgame
   7. Match ends, results screen shown
   ```

5. **Event Bus Test**
   ```
   1. Add debug subscribers to GameplayEventBus
   2. Trigger events manually
   3. Verify all subscribers receive events
   4. Check message logs in console
   ```

---

## 13. Setup Checklist (Original Section - Expanded)

### Original Combat & Weapon Setup Checklist

#### Phase 1: Create Configs (Editor Only)

- [ ] Menu: `Tools → GameplaySystems → Setup Tool`
- [ ] Create all required configs:
  - [ ] PlayerStatConfig
  - [ ] GameplayConfig
  - [ ] InventoryConfig
  - [ ] WeaponStatConfigs (Pistol, Rifle, etc.)
  - [ ] QuickSlotConfig

#### Phase 2: Create Projectile Prefab

- [ ] Create GameObject named "Bullet"
- [ ] Add MeshRenderer (Sphere)
- [ ] Add Collider (Capsule, isTrigger=true)
- [ ] Add **ProjectileComponent** script
- [ ] Set Layer to "Projectile"
- [ ] Save as Prefab: `Assets/_Night_Hunt/Prefabs/Items/Projectiles/Bullet.prefab`

#### Phase 3: Create Weapon Prefab

**For ProjectileWeapon:**
- [ ] Create GameObject named "AK47" (or weapon name)
- [ ] Add MeshRenderer (rifle mesh)
- [ ] Add NetworkObject (FishNet)
- [ ] Add **ProjectileWeapon** script
  - [ ] Set `Weapon Config` → weapon config asset
  - [ ] Set `Fire Point` → create empty child at muzzle
  - [ ] Set `Visual Projectile Prefab` → Bullet prefab
  - [ ] Set `Use Hitscan For Logic` → false (for now)
  - [ ] Set `Hit Layers` → -1 (all)
- [ ] Verify **ProjectileSpawner** auto-added
  - [ ] Set `Projectile Prefab` → Bullet prefab
- [ ] Save as Prefab: `Assets/_Night_Hunt/Prefabs/Items/Weapons/AK47.prefab`

#### Phase 4: Setup Player Prefab

- [ ] Select Player prefab in scene
- [ ] Menu: `Tools → GameplaySystems → Setup Player Prefab`
- [ ] Select your Player prefab
- [ ] Click "Setup Player Prefab" button
- [ ] Verify all components added:
  - [ ] PlayerStatSystem ✓
  - [ ] InventorySystem ✓
  - [ ] WeaponSystem ✓
  - [ ] EquipmentSystem ✓
  - [ ] QuickSlotSystem ✓
  - [ ] AttachmentSystem ✓

#### Phase 5: Setup UI Canvas

- [ ] Create Canvas (if not exists)
- [ ] Add **GameHUD** script

**Weapon Slot Buttons:**
- [ ] Create 3 Buttons (Primary, Secondary, Melee)
- [ ] Add **WeaponSlotButton** to each
- [ ] Set Slot Type correctly
- [ ] Link UI elements (_selectedBorder, _ammoText, _emptySlotOverlay)

**Quick Slot Buttons:**
- [ ] Create 4 Buttons
- [ ] Add **QuickSlotHUDButton** to each
- [ ] Assign in array order

**Other UI:**
- [ ] **CombatHUDPanel**: Link weapon buttons, quick buttons, ammo text, reload indicator
- [ ] **PlayerHUDPanel**: Link HP/Stamina/Armor bars
- [ ] **CrosshairUI**: Link crosshair image
- [ ] **InteractionPromptUI**: Link prompt text
- [ ] **DeathScreen**: Setup respawn button

#### Phase 6: Scene Setup

- [ ] In scene, find or create Player instance
- [ ] Verify it has:
  - [ ] NetworkObject
  - [ ] All GameplaySystems components
  - [ ] Canvas with GameHUD
- [ ] Call `gameHUD.Initialize(localPlayer)` where Player spawns

#### Phase 7: Test Gameplay

- [ ] Play scene
- [ ] Verify:
  - [ ] Player loads
  - [ ] UI appears
  - [ ] Weapon buttons visible with correct slot labels
  - [ ] Ammo display shows
  - [ ] Firing creates projectiles
  - [ ] Projectiles travel and despawn
  - [ ] Reload indicator works
  - [ ] Quick slots respond

---

## 📝 QUICK REFERENCE - Inspector Setup Strings

Copy-paste these paths khi cần assign configs:

```
PlayerStatConfig: Assets/Resources/Configs/PlayerStatConfig.asset
GameplayConfig: Assets/Resources/Configs/GameplayConfig.asset
InventoryConfig: Assets/Resources/Configs/InventoryConfig.asset
QuickSlotConfig: Assets/Resources/Configs/QuickSlotConfig.asset
WeaponStatConfig_Rifle: Assets/Resources/Configs/WeaponStatConfig_Rifle.asset
WeaponStatConfig_Pistol: Assets/Resources/Configs/WeaponStatConfig_Pistol.asset
```

---

## 🎯 Important Notes

1. **Slot Type Enumeration:**
   - `Primary` → Main weapon
   - `Secondary` → Backup weapon
   - `Melee` → Melee weapon

2. **Projectile Lifecycle:**
   - Created at FirePoint
   - Moves with speed from config
   - Destroyed by lifetime or distance
   - No server validation (local client test)

3. **UI Event Flow:**
   - WeaponSystem fires events
   - CombatHUDPanel listens
   - WeaponSlotButton updates visuals
   - All event-driven, no polling

4. **Network Setup (Later):**
   - Use ProjectileSpawner ServerRpc/ObserversRpc
   - Validate on server before broadcast
   - Clients show visual, server does logic

---

## 🐛 Common Issues & Fixes

### Issue 1: Ammo text not updating
**Fix:** Verify `OnAmmoChanged` event hooked in CombatHUDPanel.Initialize()

### Issue 2: Weapon button not appearing
**Fix:** Check WeaponSlotButton has Button component + WeaponSlotButton script

### Issue 3: Projectile not visible
**Fix:** Verify Visual Projectile Prefab assigned in ProjectileWeapon inspector

### Issue 4: Setup Tool says "Configs not found"
**Fix:** Run `Tools → GameplaySystems → Setup Tool` first to create configs

### Issue 5: ProjectileSpawner not auto-added
**Fix:** Delete weapon and re-add ProjectileWeapon script, or manually add ProjectileSpawner

---

**Last Updated:** March 2026
**Version:** 1.0
