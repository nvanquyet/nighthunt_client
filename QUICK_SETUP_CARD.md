# ⚡ QUICK SETUP CARD - 5 MINUTES TO GAMEPLAY

**Dành cho những ai muốn setup nhanh chóng!**

---

## 🚀 FAST TRACK SETUP (5 Phút)

### Step 1: Create Configs (1 Min)

```
Menu → Tools → GameplaySystems → Setup Tool
└─ Click "Setup All" button
└─ Wait... Done!
```

### Step 2: Create Projectile Prefab (1 Min)

```
Create new GameObject: "Bullet"
├─ Add: Sphere (mesh renderer)
├─ Add: Collider (Capsule, isTrigger ✓)
├─ Add: ProjectileComponent script
├─ Layer: "Projectile"
└─ Drag to: Assets/_Night_Hunt/Prefabs/Items/Projectiles/
```

### Step 3: Create Weapon Prefab (1 Min)

```
Create new GameObject: "Rifle"
├─ Add: Cube/Model (weapon mesh)
├─ Add: NetworkObject (FishNet)
├─ Add: ProjectileWeapon script
│  ├─ weaponConfig: [Rifle config]
│  ├─ firePoint: [Create empty child at muzzle]
│  └─ visualProjectilePrefab: [Bullet.prefab]
└─ Drag to: Assets/_Night_Hunt/Prefabs/Items/Weapons/
```

### Step 4: Setup Player (1 Min)

```
In scene: Select Player prefab
Menu → Tools → GameplaySystems → Setup Player Prefab
├─ Select: Your player prefab
└─ Click: "Setup Player Prefab"
```

### Step 5: Create UI (1 Min)

```
Create Canvas
├─ Add: GameHUD script
└─ Add: CombatHUDPanel with children
    ├─ 3 WeaponSlotButton (Primary/Secondary/Melee)
    ├─ 4 QuickSlotHUDButton
    └─ AmmoLabel (TextMeshProUGUI)
```

---

## 📋 ULTRA-QUICK REFERENCE TABLE

| Task | Where | Script | Key Field |
|------|-------|--------|-----------|
| **Weapon Prefab** | Scene | ProjectileWeapon | visualProjectilePrefab |
| **Projectile Prefab** | Scene | ProjectileComponent | (auto init) |
| **Player Setup** | Menu Tool | - | Setup auto |
| **UI Weapon Slot** | Inspector | WeaponSlotButton | _slotType |
| **UI Ammo Counter** | Inspector | CombatHUDPanel | _ammoLabel |
| **Initialize Game** | Code | GameHUD | .Initialize() |

---

## ⚙️ MUST-ASSIGN INSPECTOR FIELDS

### ProjectileWeapon (on Weapon)
```csharp
[SerializeField] private GameObject visualProjectilePrefab;  // ← Bullet.prefab
[SerializeField] private bool useHitscanForLogic = false;    // ← false
[SerializeField] private LayerMask hitLayers = -1;           // ← -1
```

### ProjectileSpawner (auto-added)
```csharp
[SerializeField] private GameObject projectilePrefab;        // ← Bullet.prefab
```

### WeaponSlotButton (on UI Button)
```csharp
[SerializeField] private WeaponSlotType _slotType;           // ← Primary/Secondary/Melee
[SerializeField] private Image _selectedBorder;              // ← Border Image
[SerializeField] private TextMeshProUGUI _ammoText;           // ← Ammo text
[SerializeField] private Image _emptySlotOverlay;            // ← Empty overlay
```

### CombatHUDPanel (on UI Panel)
```csharp
[SerializeField] private WeaponSlotButton _primaryButton;    // ← PrimarySlotButton
[SerializeField] private WeaponSlotButton _secondaryButton;  // ← SecondarySlotButton
[SerializeField] private WeaponSlotButton _meleeButton;      // ← MeleeSlotButton
[SerializeField] private TextMeshProUGUI _ammoLabel;          // ← AmmoLabel text
[SerializeField] private GameObject _reloadingIndicator;     // ← Reload Image
```

---

## 🔥 COPY-PASTE INITIALIZATION CODE

**Add to your GameBoot/GameplayBootstrap:**

```csharp
// After player spawned
public void OnPlayerSpawned(NetworkPlayer player)
{
    // Get UI references
    var gameHUD = FindObjectOfType<GameHUD>();
    var combatPanel = gameHUD.CombatHUDPanel;
    
    // Get system references from player
    var weaponSystem = player.WeaponSystem;
    var quickSlotSystem = player.QuickSlotSystem;
    
    // Initialize UI
    gameHUD.Initialize(player);
    combatPanel.Initialize(weaponSystem, quickSlotSystem);
    
    Debug.Log("✓ Gameplay initialized!");
}
```

---

## 🎯 FIRING FLOW (5 Steps)

1. **Input:** Player presses LMB → InputHandler triggered
2. **Command:** InputHandler calls `weaponSystem.Fire(direction)`
3. **Fire:** ProjectileWeapon.Fire() checks CanFire()
4. **Spawn:** ProjectileSpawner.SpawnLocal() instantiates Bullet
5. **Network:** SendProjectileToServer() broadcasts to all clients

---

## 📊 UI UPDATE FLOW (4 Steps)

1. **Event:** WeaponSystem fires `OnAmmoChanged` event
2. **Handler:** CombatHUDPanel.HandleAmmoChanged() receives it
3. **Update:** WeaponSlotButton.RefreshAmmo() updates text
4. **Display:** `_ammoText.text = "30 / 90"` ← User sees update

---

## ✅ MINIMAL WORKING SETUP

### Scene Requirements
- [ ] Player with NetworkObject
- [ ] Player with all GameplaySystems (use Setup Tool)
- [ ] Weapon in Primary slot with ProjectileWeapon

### Prefab Requirements
- [ ] Bullet prefab with ProjectileComponent and Collider
- [ ] Weapon prefab with ProjectileWeapon and FirePoint

### UI Requirements
- [ ] Canvas with GameHUD
- [ ] CombatHUDPanel with 3 WeaponSlotButtons
- [ ] All fields assigned

### Code Requirements
- [ ] `gameHUD.Initialize(player)` called once
- [ ] `combatHUDPanel.Initialize(weaponSystem, quickSlotSystem)` called once

---

## 🔍 LAYER SETUP

**Create these layers if missing:**

```
Layers
├─ Default
├─ Player
├─ Enemy
├─ Projectile       ← New: for bullets
├─ Weapon           ← New: for equipment
├─ Environment      ← New: for walls/objects
└─ UI
```

**Collider Settings:**

```
Player:
  └─ Layer: Player
  └─ Collision: Layer everything except Projectile

Enemy:
  └─ Layer: Enemy
  └─ Collision: Layer everything

Projectile:
  └─ Layer: Projectile
  └─ Is Trigger: ✓
  └─ Collision Detection: Continuous

Weapon:
  └─ Layer: Weapon
  └─ Is Trigger: ✓ (if pickup)
```

---

## 🧪 QUICK TEST

1. Play scene
2. Look at console: Should see `✓ Gameplay initialized!`
3. Press LMB → Should spawn bullet
4. Watch bullet travel
5. UI ammo counter should decrease
6. Bullet disappears after range/time

**If any step fails → Check corresponding Setup section**

---

## 🚨 EMERGENCY FIXES (Copy-Paste)

### Ammo text blank?
```csharp
// Verify in CombatHUDPanel
if (_ammoLabel != null)
    _ammoLabel.text = $"{currentAmmo} / {reserveAmmo}";
```

### Weapon not firing?
```csharp
// Check in ProjectileWeapon.Fire()
if (!CanFire()) {
    Debug.Log("Cannot fire - ammo:" + currentAmmo + " reloading:" + isReloading);
    return;
}
```

### UI not updating?
```csharp
// Verify event hook in CombatHUDPanel.Initialize()
if (_weaponSystem != null)
    _weaponSystem.OnAmmoChanged += HandleAmmoChanged;
```

### Projectile not visible?
```csharp
// Check in ProjectileWeapon inspector
// visualProjectilePrefab must NOT be null
if (visualProjectilePrefab == null)
    Debug.LogError("Visual projectile prefab not assigned!");
```

---

## 📝 ABBREVIATIONS KEY

| Abbr | Meaning |
|------|---------|
| LMB | Left Mouse Button |
| UI | User Interface |
| RPC | Remote Procedure Call (Network) |
| IWeaponSystem | Weapon system interface |
| IQuickSlotSystem | Quick slot system interface |
| Prefab | Pre-made GameObject template |
| Scene | Current Unity scene |
| Collider | Physics collision component |

---

## 🎓 LEARNING PATH (If stuck)

1. **Read:** GAMEPLAY_SETUP_GUIDE.md (detailed)
2. **View:** GAMEPLAY_VISUAL_REFERENCE.md (diagrams)
3. **Code:** Read ProjectileWeapon.cs
4. **Code:** Read CombatHUDPanel.cs
5. **Test:** Play scene step-by-step

---

## 🟢 SUCCESS INDICATORS

✓ Player loads  
✓ UI appears with weapon buttons  
✓ Ammo shows "30 / 90"  
✓ LMB fires projectile  
✓ Projectile travels  
✓ Ammo decrements  
✓ Projectile despawns  

**All green? Gameplay working!** ✅

---

**Last Updated:** March 2026
**Time Estimate:** 5 minutes from scratch to working gameplay
