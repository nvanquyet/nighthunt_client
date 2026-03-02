# GAMEPLAY SETUP - VISUAL REFERENCE GUIDE

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          NIGHT HUNT CLIENT GAMEPLAY                         │
└─────────────────────────────────────────────────────────────────────────────┘

RUNTIME FLOW:
═════════════════════════════════════════════════════════════════════════════

Player Input (Press LMB)
    ↓
CharacterController / InputHandler
    ↓
WeaponSystem.Fire() called
    ↓
ProjectileWeapon.Fire(direction)
    ├─→ ProjectileSpawner.SpawnLocal(pos, dir)
    │   ├─→ Instantiate(visualProjectilePrefab)
    │   ├─→ ProjectileComponent.Initialize()
    │   └─→ SendProjectileToServer (RPC)
    │
    └─→ Server validates & broadcasts to all clients
        ├─→ BroadcastProjectileToClients (ObserversRpc)
        └─→ Other clients spawn projectile


UI UPDATE FLOW:
═════════════════════════════════════════════════════════════════════════════

WeaponSystem → FireWeapon
    ↓
OnWeaponEquipped / OnAmmoChanged events
    ↓
CombatHUDPanel events handlers
    ├─→ HandleAmmoChanged(currentMag, reserve)
    ├─→ HandleReloadStateChanged(isReloading)
    └─→ HandleActiveWeaponChanged(weaponType)
    ↓
WeaponSlotButton.RefreshAmmo()
    ├─→ Update _ammoText → "30 / 90"
    ├─→ Update _selectedBorder brightness
    └─→ Update _emptySlotOverlay visibility


INSPECTOR HIERARCHY:
═════════════════════════════════════════════════════════════════════════════

┌─ Scene Root ─────────────────────────────────────────────────────────────┐
│                                                                            │
│  ┌─ Player (NetworkObject) ──────────────────────────────────────────┐   │
│  │                                                                    │   │
│  │  ✓ NetworkObject (FishNet)                                        │   │
│  │  ✓ Transform                                                      │   │
│  │  ✓ Rigidbody                                                      │   │
│  │                                                                    │   │
│  │  🔧 GAMEPLAY SYSTEMS (Added by Setup Tool)                       │   │
│  │  ├─ PlayerStatSystem                                              │   │
│  │  │  └─ _statConfig → PlayerStatConfig                            │   │
│  │  │  └─ _gameplayConfig → GameplayConfig                          │   │
│  │  │                                                                │   │
│  │  ├─ InventorySystem                                              │   │
│  │  │  ├─ _inventoryConfig → InventoryConfig                        │   │
│  │  │  └─ _statSystem → PlayerStatSystem (ref)                      │   │
│  │  │                                                                │   │
│  │  ├─ WeaponSystem                                                 │   │
│  │  │  ├─ _inventoryConfig → InventoryConfig                        │   │
│  │  │  ├─ _statSystemComponent → PlayerStatSystem (ref)             │   │
│  │  │  └─ _slotPriority → [Primary, Secondary, Melee]              │   │
│  │  │                                                                │   │
│  │  ├─ EquipmentSystem                                              │   │
│  │  ├─ QuickSlotSystem                                              │   │
│  │  └─ AttachmentSystem                                             │   │
│  │                                                                    │   │
│  │  🎯 WEAPONS (Equipped Items)                                      │   │
│  │  └─ Primary Weapon Slot                                          │   │
│  │     ├─ ProjectileWeapon (Script)                                 │   │
│  │     │  ├─ weaponConfig → WeaponConfigData                        │   │
│  │     │  ├─ firePoint → FirePoint transform                        │   │
│  │     │  ├─ visualProjectilePrefab → Bullet prefab                 │   │
│  │     │  └─ useHitscanForLogic → false                             │   │
│  │     │                                                             │   │
│  │     └─ ProjectileSpawner (Auto-added)                            │   │
│  │        └─ projectilePrefab → Bullet prefab                       │   │
│  │                                                                    │   │
│  │  📍 FirePoint (Empty GameObject, Child)                          │   │
│  │     └─ Position: Muzzle tip of weapon                            │   │
│  │                                                                    │   │
│  └────────────────────────────────────────────────────────────────────┘   │
│                                                                            │
│  ┌─ Canvas (UI) ─────────────────────────────────────────────────────┐   │
│  │                                                                    │   │
│  │  🎮 GameHUD (Script) - Orchestrator                              │   │
│  │  ├─ playerHUDPanel → ref                                         │   │
│  │  ├─ combatHUDPanel → ref                                         │   │
│  │  └─ crosshairUI → ref                                            │   │
│  │                                                                    │   │
│  │  ┌─ CombatHUDPanel ───────────────────────────────────────────┐  │   │
│  │  │                                                              │  │   │
│  │  │  🔫 WEAPON SLOTS                                            │  │   │
│  │  │  ├─ PrimarySlotButton (WeaponSlotButton)                    │  │   │
│  │  │  │  ├─ _slotType → Primary                                  │  │   │
│  │  │  │  ├─ _selectedBorder → Image                              │  │   │
│  │  │  │  ├─ _ammoText → TextMeshProUGUI                          │  │   │
│  │  │  │  └─ _emptySlotOverlay → Image                            │  │   │
│  │  │  │                                                           │  │   │
│  │  │  ├─ SecondarySlotButton (WeaponSlotButton)                  │  │   │
│  │  │  │  └─ ... (same structure)                                 │  │   │
│  │  │  │                                                           │  │   │
│  │  │  └─ MeleeSlotButton (WeaponSlotButton)                      │  │   │
│  │  │     └─ ... (same structure)                                 │  │   │
│  │  │                                                              │  │   │
│  │  │  🎯 QUICK SLOTS (4)                                          │  │   │
│  │  │  ├─ QuickSlot0Button (QuickSlotHUDButton)                   │  │   │
│  │  │  ├─ QuickSlot1Button (QuickSlotHUDButton)                   │  │   │
│  │  │  ├─ QuickSlot2Button (QuickSlotHUDButton)                   │  │   │
│  │  │  └─ QuickSlot3Button (QuickSlotHUDButton)                   │  │   │
│  │  │                                                              │  │   │
│  │  │  📊 AMMO DISPLAY                                             │  │   │
│  │  │  ├─ AmmoLabel → "30 / 90"                                   │  │   │
│  │  │  ├─ ReloadingIndicator → GameObject (hidden/shown)          │  │   │
│  │  │  └─ DepletedWarning → GameObject (hidden/shown)             │  │   │
│  │  │                                                              │  │   │
│  │  └──────────────────────────────────────────────────────────────┘  │   │
│  │                                                                    │   │
│  │  ┌─ PlayerHUDPanel ────────────────────────────────────────────┐  │   │
│  │  │ HealthBar (Slider)  ████████░░ 75 / 100                    │  │   │
│  │  │ StaminaBar (Slider) ██████░░░░ 60 / 100                    │  │   │
│  │  │ ArmorBar (Slider)   ████░░░░░░ 40 / 100                    │  │   │
│  │  └──────────────────────────────────────────────────────────────┘  │   │
│  │                                                                    │   │
│  │  💣 CrosshairUI → Crosshair Image                               │   │
│  │                                                                    │   │
│  └────────────────────────────────────────────────────────────────────┘   │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘


PREFAB STRUCTURE:
═════════════════════════════════════════════════════════════════════════════

📦 AK47.prefab (Weapon)
├─ Mesh + Renderer
├─ Collider
├─ NetworkObject
├─ 🔴 ProjectileWeapon (Script)
│  ├─ weaponConfig: [WeaponConfigData Asset]
│  ├─ firePoint: [FirePoint GameObject]
│  ├─ visualProjectilePrefab: [Bullet.prefab] ⭐
│  ├─ useHitscanForLogic: false
│  └─ hitLayers: -1 (all)
│
├─ 🟢 ProjectileSpawner (Script - auto added)
│  └─ projectilePrefab: [Bullet.prefab] ⭐
│
└─ 📍 FirePoint (Empty child)
   └─ Position: 0.5, 0, 0.5 (relative to weapon)


📦 Bullet.prefab (Projectile)
├─ Sphere Mesh
├─ MeshRenderer (white material)
├─ Capsule Collider
│  ├─ isTrigger: ✓
│  └─ Layer: "Projectile"
│
└─ 🟣 ProjectileComponent (Script)
   (No inspector setup needed - Initialize() called at runtime)


CONFIG ASSETS:
═════════════════════════════════════════════════════════════════════════════

📄 WeaponConfigData
├─ Name: "AK-47"
├─ DamageBody: 25
├─ DamageHeadMul: 2.0
├─ ProjectileSpeed: 50 (units/sec)
├─ MaxRange: 100
├─ MagazineSize: 30
├─ ReserveAmmo: 90
├─ FireRate: 0.1 (sec between shots)
├─ ReloadTime: 2.5
├─ BallisticType: "Projectile"
└─ GravityScale: 1.0

📄 PlayerStatConfig
├─ MaxHealth: 100
├─ MaxStamina: 100
├─ MaxArmor: 100
└─ ...

📄 GameplayConfig
├─ MaxInventoryWeight: 100
├─ WeaponSlotPriority: [Primary, Secondary, Melee]
└─ ...


COMPONENT SCRIPTS SUMMARY:
═════════════════════════════════════════════════════════════════════════════

GAMEPLAY SYSTEMS (Attach to Player):
┌─────────────────────────────────────┐
│ PlayerStatSystem                    │
│ InventorySystem                     │
│ WeaponSystem  ⭐                    │
│ EquipmentSystem                     │
│ QuickSlotSystem  ⭐                 │
│ AttachmentSystem                    │
└─────────────────────────────────────┘

WEAPON COMPONENTS (Attach to Weapon):
┌─────────────────────────────────────┐
│ ProjectileWeapon  ⭐                │
│ ProjectileSpawner (auto-add)        │
│ WeaponBase (parent class)           │
│ HitscanWeapon (alternative)         │
└─────────────────────────────────────┘

PROJECTILE COMPONENTS (Attach to Bullet):
┌─────────────────────────────────────┐
│ ProjectileComponent  ⭐             │
└─────────────────────────────────────┘

UI COMPONENTS (Attach to Canvas):
┌─────────────────────────────────────┐
│ GameHUD                             │
│ CombatHUDPanel  ⭐                  │
│ WeaponSlotButton (×3)  ⭐           │
│ QuickSlotHUDButton (×4)  ⭐         │
│ PlayerHUDPanel                      │
│ CrosshairUI                         │
└─────────────────────────────────────┘


COMPONENT DEPENDENCIES:
═════════════════════════════════════════════════════════════════════════════

ProjectileWeapon
  ├─ Requires: WeaponConfigData (asset)
  ├─ Requires: firePoint (Transform)
  ├─ Requires: visualProjectilePrefab (Prefab)
  └─ Auto-adds: ProjectileSpawner

ProjectileSpawner
  ├─ Requires: projectilePrefab (Prefab)
  └─ Uses: NetworkObject for RPC

ProjectileComponent
  ├─ Called by: ProjectileSpawner.SpawnLocal()
  ├─ Initializes: direction, speed, lifetime
  └─ Uses: WeaponConfigData for physics

CombatHUDPanel
  ├─ Requires: _primaryButton (WeaponSlotButton) ✓
  ├─ Requires: _secondaryButton (WeaponSlotButton) ✓
  ├─ Requires: _meleeButton (WeaponSlotButton) ✓
  ├─ Requires: _quickSlotButtons[] (QuickSlotHUDButton) ✓
  ├─ Requires: _ammoLabel (TextMeshProUGUI) ✓
  └─ Calls: Initialize(IWeaponSystem, IQuickSlotSystem)

WeaponSlotButton
  ├─ Requires: _selectedBorder (Image)
  ├─ Requires: _ammoText (TextMeshProUGUI)
  ├─ Requires: _emptySlotOverlay (Image)
  ├─ Requires: _slotType (enum)
  └─ Calls: Bind(slotType, weaponSystem)


SETUP ORDER (CRITICAL):
═════════════════════════════════════════════════════════════════════════════

1️⃣  CREATE CONFIGS
    └─ Tools → GameplaySystems → Setup Tool

2️⃣  CREATE PREFABS
    ├─ Bullet.prefab + ProjectileComponent
    └─ Weapon.prefab + ProjectileWeapon + FirePoint

3️⃣  SETUP PLAYER
    └─ Tools → GameplaySystems → Setup Player Prefab

4️⃣  CREATE UI CANVAS
    ├─ CombatHUDPanel + children
    ├─ WeaponSlotButton (×3)
    ├─ QuickSlotHUDButton (×4)
    └─ Link all references

5️⃣  INITIALIZE IN CODE
    gameHUD.Initialize(localPlayer);
    combatHUDPanel.Initialize(weaponSystem, quickSlotSystem);

6️⃣  TEST
    └─ Play scene & verify fire, ammo updates, UI


EVENT FLOW (EVENT-DRIVEN ARCHITECTURE):
═════════════════════════════════════════════════════════════════════════════

WeaponSystem.Fire()
    └─→ public event OnAmmoChanged
            └─→ CombatHUDPanel.HandleAmmoChanged()
                    └─→ WeaponSlotButton.RefreshAmmo()
                            └─→ _ammoText.text = "30 / 90"

WeaponSystem.Reload()
    └─→ public event OnReloadStateChanged
            └─→ CombatHUDPanel.HandleReloadStateChanged()
                    └─→ _reloadingIndicator.SetActive(true)
                    └─→ WeaponSlotButton.ShowCooldown()

WeaponSystem.Equip(weaponSlot)
    └─→ public event OnActiveWeaponChanged
            └─→ CombatHUDPanel.HandleActiveWeaponChanged()
                    └─→ All WeaponSlotButton.RefreshSelected()
                            └─→ Highlight active slot


TESTING CHECKLIST:
═════════════════════════════════════════════════════════════════════════════

□ Player loads in scene
□ GameHUD canvas appears
□ CombatHUDPanel visible with weapon slots
□ Weapon slot buttons show correct labels (Primary/Secondary/Melee)
□ Ammo counter displays "30 / 90"
□ Fire button blinks/shows selected slot
□ Empty slot overlay shows on unequipped slots
□ Trigger weapon fire (LMB)
□ Projectile spawns at FirePoint
□ Projectile moves in direction
□ Projectile disappears after range/time
□ Ammo counter decrements
□ Reserve ammo shows correctly
□ Reload updates UI indicators
□ Quick slots respond to input
□ Crosshair updates with weapon


COMMON MISTAKES & SOLUTIONS:
═════════════════════════════════════════════════════════════════════════════

❌ "Projectile not appearing"
✅ Check: visualProjectilePrefab assigned in ProjectileWeapon
✅ Check: Bullet.prefab has MeshRenderer
✅ Check: Camera can see instantiation point

❌ "Ammo text not updating"
✅ Check: _ammoText assigned in WeaponSlotButton
✅ Check: CombatHUDPanel.Initialize() called
✅ Check: OnAmmoChanged event hooked

❌ "Weapon button not highlighting"
✅ Check: _selectedBorder assigned
✅ Check: Image component has brightness value set
✅ Check: OnActiveWeaponChanged event firing

❌ "Configs not found in Setup Tool"
✅ Run: Tools → GameplaySystems → Setup Tool FIRST
✅ Check: Assets/Resources/Configs/ folder created
✅ Check: All assets saved (Ctrl+S)

❌ "ProjectileSpawner not auto-added"
✅ Solution: Delete and re-add ProjectileWeapon script
✅ Or: Manually add ProjectileSpawner component

❌ "Fire not working"
✅ Check: InputHandler / InputSystem correctly mapping LMB
✅ Check: Player.IsOwner = true (network check)
✅ Check: WeaponSystem has active weapon
✅ Check: CanFire() returns true (not reloading, has ammo)
