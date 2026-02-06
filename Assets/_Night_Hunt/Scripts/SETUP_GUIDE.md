# 🛠️ NightHunt Gameplay System Setup Guide

Hướng dẫn chi tiết để setup và cấu hình toàn bộ gameplay system.

## 📋 Mục lục

1. [Prerequisites](#1-prerequisites)
2. [Initial Setup](#2-initial-setup)
3. [Network Setup](#3-network-setup)
4. [Inventory System Setup](#4-inventory-system-setup)
5. [UI Setup](#5-ui-setup)
6. [Spectate Mode Setup](#6-spectate-mode-setup)
7. [Input System Setup](#7-input-system-setup)
8. [Configuration Files](#8-configuration-files)
9. [Testing Checklist](#9-testing-checklist)

---

## 1. Prerequisites

### 1.1 Unity Version
- **Unity**: 6.0.0 or higher
- **FishNet**: Pro v4.0.0 or higher
- **Input System**: 1.7.0 or higher
- **Cinemachine**: Latest version

### 1.2 Required Packages
```json
{
  "com.unity.inputsystem": "1.7.0",
  "com.unity.cinemachine": "latest",
  "com.fish-networking.fishnet": "pro-v4"
}
```

### 1.3 Project Structure
```
NightHuntClient/
├── Assets/
│   └── _Night_Hunt/
│       └── Scripts/
│           ├── Core/
│           ├── Gameplay/
│           ├── InventorySystems/
│           ├── Networking/
│           └── UI/
```

---

## 2. Initial Setup

### 2.1 GameManager Setup

1. **Tạo GameManager GameObject**
   - Tạo empty GameObject: `GameManager`
   - Add component: `GameManager`
   - Tag: `GameManager`
   - DontDestroyOnLoad: ✅

2. **Setup Services**
   ```
   GameManager
   ├── BackendHttpClient (Auto-added)
   ├── AuthService (Auto-added)
   ├── RoomService (Auto-added)
   ├── GameWebSocketService (Auto-added)
   ├── LobbyController (Auto-added)
   ├── SessionState (Auto-added)
   └── RoomState (Auto-added)
   ```

3. **Configure InstanceConfig**
   - Create ScriptableObject: `Assets/Config/InstanceConfig.asset`
   - Assign to GameManager.InstanceConfig
   - Configure:
     - `ShouldRunInBackground`: true (Editor), false (Build)
     - `ShouldRefreshOnFocusReturn`: true

### 2.2 Scene Setup

**Required Scenes:**
- `01_Login` - Login scene
- `02_Home` - Home/Menu scene
- `03_Waiting` or `04_Waiting` - Lobby/Waiting room
- `Gameplay` - Main gameplay scene

**Scene Hierarchy:**
```
Gameplay Scene
├── NetworkManager (FishNet)
├── GameManager (Persistent)
├── InputManager
├── SpectateManager (Persistent)
├── ClientOnlyUIManager
└── UIRootController
    ├── PlayerHUDRoot
    └── InventoryUIRoot
```

---

## 3. Network Setup

### 3.1 FishNet NetworkManager Setup

1. **Create NetworkManager**
   - GameObject: `NetworkManager`
   - Component: `NetworkManager` (FishNet)
   - Component: `NetworkGameManager` (Custom)

2. **Configure NetworkManager**
   ```
   Transport: Tugboat or KCP
   Server Port: 7770
   Client Port: 0 (Auto)
   ```

3. **NetworkGameManager Settings**
   - Max Players: 16
   - Spawn Prefabs: Add PlayerPrefab
   - Scene Objects: Add all network objects

### 3.2 Player Prefab Setup

**PlayerPrefab Structure:**
```
PlayerPrefab
├── NetworkObject (FishNet)
├── NetworkPlayer (Custom)
├── CharacterNormalMovement
├── CharacterCombat
├── CinemachineCamera (Child)
├── InventoryManager
├── EquipmentManager
├── WeaponManager
├── QuickSlotManager
├── AttachmentManager
├── InventoryNetworkSync
├── QuickSlotNetworkSync
└── [Other Network Components]
```

**Required Components:**
- `NetworkObject`: Owner only, Observers: All
- `NetworkPlayer`: Main player controller
- All Managers: Server-authoritative

### 3.3 Network Sync Components

**For each Manager, add NetworkSync:**
```csharp
// Example: InventoryNetworkSync
[RequireComponent(typeof(InventoryManager))]
public class InventoryNetworkSync : NetworkBehaviour
{
    private InventoryManager inventoryManager;
    private readonly SyncVar<InventorySnapshotData> syncedSnapshot;
    
    // ServerRpc for client requests
    // ObserversRpc for server broadcasts
}
```

**SyncVar Configuration:**
- `syncedSnapshot`: Full snapshot backup
- Delta sync: For frequent updates
- Full sync: Every N delta updates

---

## 4. Inventory System Setup

### 4.1 Create Item Definitions

1. **Create Item Definition**
   ```
   Right-click: Create > NightHunt > Inventory > Item Definition
   ```

2. **Configure Item**
   ```csharp
   ItemId: "weapon_ak47"
   ItemName: "AK-47"
   ItemType: Weapon
   Weight: 4.5
   MaxStack: 1
   AttachmentSlots: [Scope, Grip, Muzzle, Magazine]
   ```

3. **Item Types**
   - `Weapon`: Primary/Secondary weapons
   - `Consumable`: Medkits, food, drinks
   - `Throwable`: Grenades, molotovs
   - `Equipment`: Helmet, Armor, Backpack
   - `Attachment`: Scope, Grip, Muzzle, Magazine
   - `Ammo`: Bullets, magazines
   - `Misc`: Other items

### 4.2 Inventory Manager Setup

**On PlayerPrefab:**
```csharp
InventoryManager
├── Config: InventoryConfig (ScriptableObject)
│   ├── Max Slots: 20
│   ├── Weight Capacity: 100
│   └── Max Weight: 150
└── Initial Items: [Optional]
```

**InventoryConfig Settings:**
- `Max Slots`: 20 (default)
- `Weight Capacity`: 100% (normal)
- `Max Weight`: 150% (cannot move)
- `Weight Penalty Curve`: Custom AnimationCurve

### 4.3 Equipment Manager Setup

**Create EquipmentConfig:**
```
Create > NightHunt > Inventory > Equipment Config
```

**Configure Slots:**
```csharp
EquipmentSlots:
  - Helmet: SlotType.Helmet
  - Armor: SlotType.Armor
  - Backpack: SlotType.Backpack
```

**Assign to EquipmentManager:**
- EquipmentManager.Config = EquipmentConfig

### 4.4 Weapon Manager Setup

**Create WeaponSlotConfig:**
```
Create > NightHunt > Inventory > Weapon Slot Config
```

**Configure Slots:**
```csharp
WeaponSlots:
  - Primary: SlotType.Primary
  - Secondary: SlotType.Secondary
```

**Assign to WeaponManager:**
- WeaponManager.Config = WeaponSlotConfig

### 4.5 QuickSlot Manager Setup

**Create QuickSlotConfig:**
```
Create > NightHunt > Inventory > QuickSlot Config
```

**Configure:**
```csharp
QuickSlotConfig:
  - Slot Count: 4
  - Allowed Types: [Consumable, Throwable]
  - Cooldown Enabled: true
  - Cooldown Duration: 5.0s
```

**Assign to QuickSlotManager:**
- QuickSlotManager.Config = QuickSlotConfig

### 4.6 Attachment Manager Setup

**Attachment System:**
- No config needed
- Uses ItemDefinition.AttachmentSlots
- Validates compatibility automatically

---

## 5. UI Setup

### 5.1 UIRootController Setup

**Create UI Root:**
```
Canvas (Screen Space - Overlay)
├── UIRootController
│   ├── PlayerHUDRoot
│   │   ├── HealthBar
│   │   ├── QuickSlotHUD
│   │   └── [Other HUD Elements]
│   └── InventoryUIRoot
│       ├── InventoryPanel
│       ├── EquipmentPanel
│       ├── WeaponPanel
│       ├── QuickSlotPanel
│       └── AttachmentPanel
```

**UIRootController Configuration:**
```csharp
UIRootController:
  - PlayerHUDRoot: Reference to HUD GameObject
  - InventoryUIRoot: Reference to Inventory GameObject
  - EquipmentPanelUI: Reference to EquipmentPanelUI component
  - WeaponPanelUI: Reference to WeaponPanelUI component
  - QuickSlotPanelUI: Reference to QuickSlotPanelUI component
  - QuickSlotHUDController: Reference to QuickSlotHUDController component
```

### 5.2 Inventory Panel Setup

**InventoryPanelUI:**
```
InventoryPanel
├── ScrollView
│   └── Content
│       └── [InventoryCellUI Prefabs Spawned Here]
└── InventoryUIController
```

**Cell Prefab:**
- Create: `Prefabs/UI/InventoryCell.prefab`
- Component: `InventoryCellUI`
- UI Elements:
  - ItemIcon (Image)
  - ItemName (TextMeshProUGUI)
  - StackCount (TextMeshProUGUI)
  - Background (Image)

### 5.3 Equipment Panel Setup

**EquipmentPanelUI:**
```
EquipmentPanel
├── HelmetSlot (EquipmentSlotUI)
├── ArmorSlot (EquipmentSlotUI)
└── BackpackSlot (EquipmentSlotUI)
```

**Slot Prefab:**
- Create: `Prefabs/UI/EquipmentSlot.prefab`
- Component: `EquipmentSlotUI`
- Configure SlotType in Inspector

### 5.4 QuickSlot Panel Setup

**QuickSlotPanelUI:**
```
QuickSlotPanel
├── Slot1 (QuickSlotSlotUI)
├── Slot2 (QuickSlotSlotUI)
├── Slot3 (QuickSlotSlotUI)
└── Slot4 (QuickSlotSlotUI)
```

**QuickSlotSlotUI:**
- Component: `QuickSlotSlotUI`
- Slot Index: 0, 1, 2, 3
- Parent Panel: QuickSlotPanelUI reference

### 5.5 QuickSlot HUD Setup

**QuickSlotHUDController:**
```
QuickSlotHUD
├── Container (Horizontal Layout Group)
│   └── [QuickSlotHUDButton Prefabs Spawned Here]
└── QuickSlotHUDController
```

**Button Prefab:**
- Create: `Prefabs/UI/QuickSlotHUDButton.prefab`
- Component: `QuickSlotHUDButton`
- UI Elements:
  - ItemIcon (Image)
  - SlotNumber (TextMeshProUGUI)
  - CooldownOverlay (Image)
  - ProgressBar (Image)

### 5.6 Drag & Drop Setup

**DragDropHandler:**
- Add to UIRootController GameObject
- Assign Managers:
  - InventoryManager (from local player)
  - QuickSlotManager
  - EquipmentManager
  - WeaponManager
  - AttachmentManager

**DragDropVisual:**
- Create: `Prefabs/UI/DragDropVisual.prefab`
- Component: `DragDropVisual`
- Assign to DragDropController

---

## 6. Spectate Mode Setup

### 6.1 SpectateManager Setup

**Create SpectateManager:**
```
GameManager GameObject (or separate GameObject)
└── SpectateManager Component
```

**Configuration:**
- Singleton: Auto-managed
- DontDestroyOnLoad: ✅
- No additional config needed

### 6.2 SpectateManager Integration

**NetworkPlayer Integration:**
```csharp
// Already integrated in NetworkPlayer.OnStartClient()
if (IsOwner && SpectateManager.Instance != null)
{
    SpectateManager.Instance.SetLocalPlayer(this);
}
```

**UIRootController Integration:**
```csharp
// Already integrated
SpectateManager.Instance.OnCurrentPlayerChanged += OnCurrentPlayerChanged;
```

### 6.3 Spectate Input Setup

**SpectatorInputHandler:**
- Add to InputManager
- Configure keys:
  - Next Player: `Tab` or `PageDown`
  - Previous Player: `PageUp`
  - Exit Spectate: `Escape`

**SpectatorCameraSystem:**
- Add to Camera GameObject
- Configure follow speed
- Configure camera settings

---

## 7. Input System Setup

### 7.1 InputManager Setup

**Create InputManager:**
```
GameManager GameObject (or separate GameObject)
└── InputManager Component
```

**Input Handlers:**
```
InputManager
├── MovementInputHandler
├── CombatInputHandler
├── InventoryInputHandler
├── QuickSlotInputHandler
├── UIInputHandler
├── CameraInputHandler
└── SpectatorInputHandler (if spectating)
```

### 7.2 Input Actions Setup

**Create Input Actions Asset:**
```
Assets/Input/GameplayInputActions.inputactions
```

**Action Maps:**
- `Gameplay`: Movement, Combat, Interaction
- `UI`: Inventory, Menu navigation
- `Spectator`: Spectate controls

**Key Bindings:**
```
Movement:
  - W/A/S/D: Move
  - Shift: Sprint
  - Space: Jump
  - Ctrl: Crouch

Combat:
  - Left Click: Shoot
  - Right Click: Aim
  - R: Reload

Inventory:
  - Tab: Toggle Inventory
  - E: Interact/Pickup

QuickSlot:
  - Ctrl+1: QuickSlot 1
  - Ctrl+2: QuickSlot 2
  - Ctrl+3: QuickSlot 3
  - Ctrl+4: QuickSlot 4
```

### 7.3 Input Handler Configuration

**MovementInputHandler:**
- Input Action: `Gameplay/Move`
- Input Action: `Gameplay/Sprint`
- Input Action: `Gameplay/Jump`

**CombatInputHandler:**
- Input Action: `Gameplay/Shoot`
- Input Action: `Gameplay/Aim`
- Input Action: `Gameplay/Reload`

**InventoryInputHandler:**
- Input Action: `UI/ToggleInventory`
- Input Action: `Gameplay/Interact`

**QuickSlotInputHandler:**
- Input Action: `Gameplay/QuickSlot1`
- Input Action: `Gameplay/QuickSlot2`
- Input Action: `Gameplay/QuickSlot3`
- Input Action: `Gameplay/QuickSlot4`

---

## 8. Configuration Files

### 8.1 Required Config Files

**Create these ScriptableObjects:**

1. **InstanceConfig**
   ```
   Assets/Config/InstanceConfig.asset
   ```
   - ShouldRunInBackground: true/false
   - ShouldRefreshOnFocusReturn: true

2. **InventoryConfig**
   ```
   Assets/Config/Inventory/InventoryConfig.asset
   ```
   - Max Slots: 20
   - Weight Capacity: 100
   - Max Weight: 150
   - Weight Penalty Curve

3. **EquipmentConfig**
   ```
   Assets/Config/Inventory/EquipmentConfig.asset
   ```
   - Equipment Slots definition

4. **WeaponSlotConfig**
   ```
   Assets/Config/Inventory/WeaponSlotConfig.asset
   ```
   - Weapon Slots definition

5. **QuickSlotConfig**
   ```
   Assets/Config/Inventory/QuickSlotConfig.asset
   ```
   - Slot Count: 4
   - Allowed Types
   - Cooldown Settings

6. **WeightPenaltyConfig**
   ```
   Assets/Config/Inventory/WeightPenaltyConfig.asset
   ```
   - Speed Curve
   - Stamina Penalties

### 8.2 Config Assignment Checklist

- [ ] InstanceConfig → GameManager
- [ ] InventoryConfig → InventoryManager (on PlayerPrefab)
- [ ] EquipmentConfig → EquipmentManager
- [ ] WeaponSlotConfig → WeaponManager
- [ ] QuickSlotConfig → QuickSlotManager
- [ ] WeightPenaltyConfig → InventoryManager

---

## 9. Testing Checklist

### 9.1 Network Testing

- [ ] Server starts successfully
- [ ] Client connects to server
- [ ] Player spawns correctly
- [ ] Network sync works (movement, inventory)
- [ ] Multiple clients can join
- [ ] Server-authoritative validation works

### 9.2 Inventory Testing

- [ ] Add item to inventory
- [ ] Remove item from inventory
- [ ] Drag & drop between slots
- [ ] Stack items correctly
- [ ] Weight system works
- [ ] Equipment slots work
- [ ] Weapon slots work
- [ ] QuickSlot assignment works
- [ ] Attachment system works

### 9.3 UI Testing

- [ ] Inventory panel opens/closes
- [ ] Drag & drop visual feedback
- [ ] Tooltip shows correctly
- [ ] Slot states update (Empty, Occupied, Hover, Selected)
- [ ] QuickSlot HUD displays correctly
- [ ] Progress bar for consumables works

### 9.4 Spectate Mode Testing

- [ ] Spectate mode activates on death
- [ ] Can switch between players
- [ ] UI shows spectated player's data
- [ ] Drag-drop blocked when spectating
- [ ] Hover/tooltip works when spectating
- [ ] Prompts disabled when spectating
- [ ] Can exit spectate mode

### 9.5 QuickSlot Testing

- [ ] Ctrl+1/2/3/4 selects slots
- [ ] Fast press (< 0.3s) uses item immediately
- [ ] Slow press (> 0.3s) only selects
- [ ] Double-click on UI uses item
- [ ] Cooldown system works
- [ ] Progress bar shows during usage
- [ ] Network sync works for QuickSlot

### 9.6 Interaction Testing

- [ ] Raycast detects interactables
- [ ] Prompt shows correctly
- [ ] Hold to interact works
- [ ] Container opens correctly
- [ ] Can loot from containers
- [ ] Can pickup world items

---

## 10. Troubleshooting

### 10.1 Common Issues

**Issue: Inventory not syncing**
- Check NetworkSync components are added
- Verify ServerRpc/ObserversRpc are correct
- Check server logs for validation errors

**Issue: UI not updating**
- Verify event subscriptions
- Check UIRootController setup
- Ensure managers are injected correctly

**Issue: Spectate mode not working**
- Check SpectateManager exists
- Verify NetworkPlayer calls SetLocalPlayer
- Check UIRootController subscribes to events

**Issue: Drag-drop not working**
- Check DragDropHandler is assigned
- Verify managers are injected
- Check IsCurrentPlayerLocal() check

**Issue: QuickSlot not responding**
- Check QuickSlotInputHandler is enabled
- Verify Input Actions are configured
- Check QuickSlotManager is assigned

### 10.2 Debug Settings

**Enable Debug Logs:**
- Set `enableDebugLogs = true` on:
  - InventoryManager
  - QuickSlotManager
  - UIRootController
  - DragDropHandler
  - SpectateManager

**Network Debug:**
- Enable FishNet logging
- Check server console for errors
- Use NetworkManager debug tools

---

## 11. Performance Optimization

### 11.1 Network Optimization

- Use Delta Sync for frequent updates
- Full Sync only every N updates
- Limit update frequency
- Use object pooling for UI elements

### 11.2 UI Optimization

- Pool InventoryCellUI instances
- Limit tooltip updates
- Use object pooling for drag visuals
- Disable unused UI panels

### 11.3 Inventory Optimization

- Cache item lookups
- Limit validation checks
- Use efficient data structures
- Optimize weight calculations

---

## 📝 Notes

- All managers should be on PlayerPrefab
- NetworkSync components must be on same GameObject as Managers
- UI components should be on Canvas (Screen Space)
- SpectateManager should be persistent (DontDestroyOnLoad)
- InputManager should be persistent

---

**Version**: 1.0.0  
**Last Updated**: 2024  
**Unity Version**: 6.0+  
**FishNet Version**: Pro v4+
