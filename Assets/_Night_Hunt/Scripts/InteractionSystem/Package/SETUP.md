# Setup Guide (Unity 6 + FishNet Pro V4)

## Requirements

- Unity 6
- FishNet Pro V4
- Unity **New Input System** enabled (this package is **New Input only**, no `Input.GetKeyDown`)

## 0) Project Settings (New Input System)

1. `Edit > Project Settings > Player > Active Input Handling` → **Input System Package (New)**
2. Ensure package `com.unity.inputsystem` is installed.

## 1) Create Test Data (recommended)

This creates default ScriptableObjects for quick testing and puts them here:
`Assets/_Night_Hunt/Data/Interaction/`

- Menu: `NightHunt/InteractionSystem/Create Test Data`
- Click: **Create All Selected Data**

## 2) Player Setup (New Input System + child objects)

### Option A: Setup Wizard (recommended)

1. Menu: `NightHunt/InteractionSystem/Setup Wizard`
2. Select your Player root GameObject
3. Click **Setup All**

Wizard will create child objects:
- `PickupSystem`
- `InteractionSystem`
- `InventorySystem`
- `EquipmentSystem`

### Option B: Manual (exact)

#### Root Player (required)

- **FishNet**: `NetworkObject` (and your existing movement/network scripts)
- **New Input**: `PlayerInput`
- `InteractionInputHandler` (routes input actions to detectors)

#### Child: `PickupSystem`

- `PickupDetector`
- `PickupHandler` (**NetworkBehaviour**: add on prefab or at runtime)
- `AutoPickupTrigger` (+ `SphereCollider` auto-added)
- `PickupAnimator` (optional)

#### Child: `InteractionSystem`

- `InteractionDetector`
- `InteractionHandler`
- `HoldInteractionHandler`
- `InteractionUIController` (optional)

#### Child: `InventorySystem`

Add ONE:
- `GridInventoryComponent` (recommended)
- `ListInventoryComponent`

**Drop Item Support:**
- `ItemDropHandler` (**NetworkBehaviour**: handles dropping items from inventory to world)

#### Child: `EquipmentSystem`

- `EquipmentManager`
- `EquipmentVisualController`
- `EquipmentHandler` (**NetworkBehaviour**: equip/unequip + attach/detach on server)

## 3) PlayerInput Actions (required)

`InteractionInputHandler` supports either:

### Option A (recommended): assign `InputActionReference`

Create an Input Actions asset, then create 3 actions:
- **Interact** (Button)
- **Pickup** (Button)
- **Inventory** (Button)

Bind keys for testing (example):
- Interact: `E`
- Pickup: `F` (recommend different from Interact)
- Inventory: `Tab`

Assign those action references into `InteractionInputHandler`:
- `interactAction`
- `pickupAction`
- `inventoryAction`

### Option B: rely on `PlayerInput.actions["ActionName"]`

If you don’t use references, your `PlayerInput` must contain actions named exactly:
- `Interact`
- `Pickup`
- `Inventory`

## Component Configuration

### PickupDetector
- Set `playerCamera` reference
- Configure `detectionRange` (default: 5m)
- Set `pickupLayers` layer mask
- Assign UI references (optional)

### InteractionDetector
- Set `playerCamera` reference
- Configure `detectionRange` (default: 5m)
- Set `interactionLayers` layer mask
- Assign UI references (optional)

### GridInventoryComponent
- Set `gridWidth` and `gridHeight` (default: 4x3)
- Configure `maxWeight` (default: 20kg)
- Adjust `maxSlots` if needed

### AutoPickupTrigger
- Configure `autoPickupRadius` (default: 2m)
- Set `autoPickupCategories` (Ammo, Consumables, etc.)

### ItemDropHandler
- Assign `lootItemPrefab`: Generic `NetworkLootItem` prefab (same as used by `LootSpawnPoint`)
- Configure `dropOffset`: Position offset from player when dropping (default: 0.5m up, 1m forward)
- Configure `dropSpreadRadius`: Random spread radius (default: 0.5m)

## 4) Create Items + World Loot

### Create Item Data
1. Right-click in Project window
2. Create > NightHunt > InteractionSystem > [ItemType]Data
   - WeaponData
   - ArmorData
   - HelmetData
   - BackpackData
   - AttachmentData
3. Configure item properties
4. For equipment, configure attachment slots

### Create Loot Item (World Spawn)
1. Create empty GameObject
2. Add `NetworkLootItem` component
3. **For Testing**: Assign `testDefinition` (LootItemDefinition) - this will be used when spawned
4. **For Production**: Leave `testDefinition` empty - spawner will inject definition at runtime
5. Configure visual settings (rotation, float effects)
6. Add **FishNet** `NetworkObject`
7. Save as prefab (this is your generic loot item prefab)

**Note**: In production, `NetworkLootItem` prefab should be generic. The spawner will inject `LootItemDefinition` at runtime via `ServerInitialize()`.

**Important**: This same prefab is also used by `ItemDropHandler` when players drop items from inventory.

## 5) Drop Items from Inventory (State Preservation)

**ItemDropHandler** allows players to drop items from inventory to the world. **All item state is preserved** (durability, attachments, ammo, etc.).

### Setup ItemDropHandler

1. On your Player root GameObject (or `InventorySystem` child):
   - Add `ItemDropHandler` component (**NetworkBehaviour**)
   - Assign `lootItemPrefab`: Generic `NetworkLootItem` prefab (same as used by `LootSpawnPoint`)
   - Configure `dropOffset`: Position offset from player (default: `0.5m up, 1m forward`)
   - Configure `dropSpreadRadius`: Random spread radius (default: `0.5m`)

### Create LootItemDefinition for Dropped Items

**Important**: For items to drop properly with world models, create `LootItemDefinition` assets:

1. Right-click in Project window
2. Create > **NightHunt > InteractionSystem > Loot > LootItemDefinition**
3. Configure:
   - `definitionId`: Unique ID (e.g., `"loot_ak47"`)
   - `itemData`: Reference to `WeaponData`, `ArmorData`, etc.
   - `worldPrefab`: 3D model/prefab to show in world
   - `pickupRange`: Distance to pickup (default: 3m)
   - Visual settings: `rotationSpeed`, `floatSpeed`, `floatAmount`

4. Add all `LootItemDefinition`s to `LootItemDefinitionDatabase`:
   - Create asset: `Assets/Resources/LootItemDefinitionDatabase.asset`
   - Add all definitions to the list

**Note**: If `LootItemDefinition` is missing, item will still drop but may not have proper world model.

### Drop Item Flow

**From Code:**
```csharp
// Get ItemDropHandler
ItemDropHandler dropHandler = player.GetComponent<ItemDropHandler>();

// Drop by ItemInstance
ItemInstance item = /* get from inventory */;
dropHandler.DropItem(item);

// Drop by instance ID (for UI drag-drop)
dropHandler.DropItemByInstanceId(item.instanceId);
```

**From UI (Drag-Drop):**
- When player drags item from inventory UI to world
- Call `ItemDropHandler.DropItemByInstanceId(instanceId)`

### State Preservation

When dropping items, **all state is preserved**:
- ✅ **Durability**: Weapon/armor condition
- ✅ **Custom Data**: Ammo count, attachments (stored as JSON in `customData`)
- ✅ **Instance ID**: Original instance ID (for tracking)
- ✅ **Quantity**: Item stack size

When picking up dropped items:
- ✅ **State is restored**: Durability, attachments, ammo, etc. are preserved
- ✅ **Attachments**: If item had attachments, they are restored from `customData`

**Example Flow:**
1. Player has weapon with 50% durability + 2 attachments + 30 ammo
2. Player drops weapon → `NetworkLootItem` spawns with all state preserved
3. Another player picks up weapon → Gets weapon with 50% durability + 2 attachments + 30 ammo

## 6) Loot Tables (Config for Spawning)

### Create LootTable ScriptableObject

**LootTable** is a reusable config that defines:
- Which items can spawn (with weighted probability)
- Rarity levels
- Min/max quantity per item
- Min/max total items per spawn cycle

**Steps:**
1. Right-click in Project window
2. Create > **NightHunt > InteractionSystem > Loot Table**
3. Configure:
   - **Entries**: Add `LootTableEntry` for each item
     - `loot`: Reference to `LootItemDefinition` (contains item data + world config)
     - `weight`: Probability weight (0-100, higher = more likely)
     - `rarity`: Common, Uncommon, Rare, Epic, Legendary (for UI/visual)
     - `minQuantity` / `maxQuantity`: Quantity range when this item spawns
     - `canSpawnMultiple`: Can spawn multiple times in same cycle?
   - **Spawn Count**: `minItemsPerSpawn` / `maxItemsPerSpawn` (total items per cycle)

**Example:**
- `LootTable_Chest_Common` (Common chest loot)
- `LootTable_Chest_Rare` (Rare chest loot)
- `LootTable_World_Spawn` (World spawn points)

**Reusability**: One `LootTable` can be used by multiple `LootSpawnPoint`s or `LootContainer`s.

## 7) Containers, Corpse Loot, Loot Spawning

### Create Container (Chest/Crate) with Auto-Generate Loot

**Option A: Container with LootTable (Auto-Generate on First Open)**
1. Create empty GameObject
2. Add `LootContainer` component
3. Configure:
   - `maxSlots`: Maximum items container can hold
   - `lootTable`: Reference to `LootTable` ScriptableObject (optional)
   - `generateLootOnFirstOpen`: **Enable this** to auto-generate items when first opened (if empty)
4. Add `ContainerInteractable` component
5. Configure interaction:
   - `interactionType`: **Immediate** (instant open) or **Hold** (hold to open)
   - `requiredHoldTime`: If Hold type, set hold duration (e.g., 2 seconds)
6. Add visual model (optional)
7. Add **FishNet** `NetworkObject`

**Flow:**
- Player interacts → Opens container UI
- If `generateLootOnFirstOpen = true` + container empty + has `lootTable` → Server generates items from `LootTable` and adds to container
- Player can transfer items between container and inventory

**Option B: Container with Pre-Placed Items**
- Same setup but leave `lootTable` empty
- Manually add items via `LootContainer.AddItem()` (server-side)

### Create Corpse Loot (Loot Other Players When They Die)

**Corpse loot allows players to loot items from dead players.**

**Setup:**
1. Create empty GameObject (or use player death spawn system)
2. Add `LootContainer` component
3. Add `PlayerCorpseLoot` component
4. Add `CorpseInteractable` component
5. Configure:
   - `CorpseInteractable.interactionType`: **Immediate** (instant) or **Hold** (hold to loot)
   - `CorpseInteractable.requiredHoldTime`: If Hold type, set duration
6. Add visual model (corpse mesh/model)
7. Add **FishNet** `NetworkObject`

**Server-Side Initialization (When Player Dies):**
```csharp
// In your death handler (server-side)
PlayerCorpseLoot corpse = /* spawn corpse */;
GridInventoryComponent playerInventory = /* get dead player's inventory */;
corpse.InitializeWithInventory(playerInventory);
```

**Flow:**
1. Player dies → Server spawns corpse with `PlayerCorpseLoot`
2. Server calls `InitializeWithInventory()` → Transfers all items from dead player's inventory to corpse container
3. Other players approach corpse → See interaction prompt
4. Player interacts (hold or instant, based on config) → Opens container UI
5. Player can transfer items from corpse to their inventory (one-way: corpse → player)
6. Corpse auto-despawns when empty (after 5 seconds) or after `despawnDelay` (default: 5 minutes)

**Note**: `CorpseInteractable` automatically sets `interactionType = Container`, but you can override it in Inspector to use **Hold** interaction.

### Loot Spawn Points (Weighted Random Spawning)

**LootSpawnPoint** spawns items in the world based on a `LootTable`.

**Setup:**
1. Create empty GameObject in scene
2. Add `LootSpawnPoint` component
3. Configure:
   - **Loot Table**: Reference to `LootTable` ScriptableObject (contains all spawn config)
   - **Prefab**: Generic `NetworkLootItem` prefab (will be initialized with loot data at runtime)
   - **Spawn Settings**:
     - `spawnRadius`: Random spawn radius around point
   - **Spawn Timing**:
     - `spawnMode`: 
       - `Once`: Spawn once when scene starts
       - `Interval`: Respawn every `respawnInterval` seconds (after all items picked up)
       - `OnDemand`: Spawn manually via code (`TriggerSpawn()`)
     - `respawnInterval`: Time between respawns (if Interval mode)
     - `initialDelay`: Delay before first spawn
4. Add **FishNet** `NetworkObject` (to the spawn point, not the loot prefab)

**Flow:**
- Server spawns items based on `LootTable.GenerateLoot()` (weighted random)
- Each item spawns as `NetworkLootItem` prefab
- Server calls `lootItem.ServerInitialize(lootDefinition, quantity)` to inject data
- Items sync to clients automatically

**Optional Manager:**
- Add `LootSpawnManager` to a scene object
- Enable `spawnOnStart` to auto-spawn all `LootSpawnPoint`s on scene start

### Create Custom Interactable (Door, Button, etc.)

**Interaction Types:**
- **Immediate**: Click once → Instant action (default)
- **Hold**: Hold button for `requiredHoldTime` seconds → Action
- **Toggle**: Click to toggle state (on/off)
- **Container**: Opens container UI (used by `ContainerInteractable`, `CorpseInteractable`)

**Setup:**
1. Create empty GameObject
2. Add component extending `InteractableBase` (or use existing like `ContainerInteractable`)
3. Configure in Inspector:
   - `interactionType`: Choose **Immediate**, **Hold**, **Toggle**, or **Container**
   - `requiredHoldTime`: If Hold type, set duration (e.g., 2.0 seconds)
   - `interactionRange`: Maximum distance to interact (default: 3m)
   - `interactionText`: Text shown in UI prompt
4. Implement `Interact(GameObject interactor)` method

**Example (Hold Interaction):**
```csharp
public class DoorInteractable : InteractableBase
{
    protected override void Awake()
    {
        base.Awake();
        interactionType = InteractionType.Hold;  // Hold to open
        requiredHoldTime = 2f;  // Hold for 2 seconds
        interactionText = "Hold E to open door";
    }

    public override void Interact(GameObject interactor)
    {
        // Door opens (server-side logic)
        Debug.Log("Door opened!");
    }
}
```

**Note**: `HoldInteractionHandler` automatically handles hold progress tracking and UI updates.

## Testing

### UI Setup (Quick Start)

**Option A: Use UI Wizard (Recommended for Testing)**
1. Menu: `NightHunt/InteractionSystem/UI Wizard`
2. Configure save path (default: `Assets/_Night_Hunt/UI/InteractionSystem/`)
3. Select which UI to generate:
   - **Inventory UI**: Weight display, slot count, basic panel
   - **Container/Loot UI**: Dual-grid for containers and player inventory
   - **Interaction Prompt UI**: Interaction prompts and progress bars
4. Click **Generate UI Prefabs**
5. Drag prefabs into scene
6. Assign references to components (`InventoryUIListener`, `LootContainerUI`, `InteractionUIController`)

**Option B: Create Custom UI**
- Create your own UI prefabs
- Subscribe to `InventoryEvents` for data updates
- Assign UI references to components

### Event/Observer testing (UI decoupled)

UI should not be referenced by gameplay logic. Subscribe to `InventoryEvents` instead.

For a quick test listener:
- Add `InventoryUIListener` to any UI/GameObject in scene
- Assign optional text/slider references (or just watch Console logs)

**Note**: The generated UI prefabs are basic placeholders. You'll need to implement:
- Grid/item slot rendering (for inventory and container grids)
- Item icons/images
- Drag-and-drop functionality (for transferring items)
- Custom styling to match your game

### Editor Tools
- `NightHunt/InteractionSystem/Create Test Data` - Generate test ScriptableObject data
- `NightHunt/InteractionSystem/Setup Wizard` - Auto-setup player components
- `NightHunt/InteractionSystem/UI Wizard` - **Generate basic UI prefabs for testing**
- `NightHunt/InteractionSystem/Inventory Grid Visualizer` - Debug inventory grid
- `NightHunt/InteractionSystem/Attachment Debug Window` - Debug attachments

### Debug Features
- Raycast visualization in Scene View
- Interaction range gizmos
- Attachment point gizmos
- Pickup radius gizmos

## Troubleshooting

### Items not picking up
- Check `PickupDetector` is on `PickupSystem` child
- Verify `PickupHandler` exists (NetworkBehaviour: prefab/runtime)
- Verify `InteractionInputHandler` has `pickupAction` bound
- Check item implements `IPickupable`
- Verify layer masks are correct
- **State not preserved**: Ensure `PickupHandler` is using latest version that calls `networkLootItem.GetItemInstance()`

### Items not dropping
- Check `ItemDropHandler` is on player (or `InventorySystem` child)
- Verify `lootItemPrefab` is assigned (generic `NetworkLootItem` prefab)
- Verify `LootItemDefinitionDatabase` exists in `Resources/` folder
- Check `LootItemDefinition` exists for the item you're trying to drop
- **Item drops but no model**: Create `LootItemDefinition` with `worldPrefab` assigned

### Interactions not working
- Check `InteractionDetector` is on `InteractionSystem` child
- Verify `InteractionInputHandler` has `interactAction` bound
- Check object implements `IInteractable`
- Verify interaction range

### Inventory not working
- Check `GridInventoryComponent` or `ListInventoryComponent` is present
- Verify weight capacity settings
- Check item data is assigned correctly

### Input not working
- Ensure `Active Input Handling` is **New Input System**
- Ensure `PlayerInput` is present
- Ensure actions are named `Interact` / `Pickup` / `Inventory` or assigned via references

## Next Steps

1. Plug your real UI into `InventoryEvents` (no direct refs from gameplay)
2. Implement your real item database loading (currently item-data lookup is placeholder)
3. Wire death → spawn corpse → `InitializeWithInventory()`
4. Add proper equip/attachment item data loading (see `EquipmentHandler` TODOs)
