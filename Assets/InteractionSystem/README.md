# NightHunt Interaction System

Complete multiplayer interaction system for Unity with FishNet.

## Features

### ✅ Pickup System
- Manual pickup (raycast + input)
- Auto pickup (trigger-based)
- Per-player settings
- Network synchronized

### ✅ Interaction System
- Multiple interaction types (Immediate, Hold, Toggle, Container)
- Distance validation
- Line of sight checking
- Progress UI

### ✅ Loot & Container System
- Chest/crate containers
- Player corpse looting
- Drag & drop UI
- Network synchronized

### ✅ Universal Attachment System
- Works with ALL equipment types (weapons, armor, helmets, backpacks)
- Stat modification system
- Visual attachment spawning
- Network synchronized

### ✅ Inventory System
- Grid-based (Tetris-style)
- List-based (simple)
- Auto-stacking
- Item dropping

### ✅ Equipment & Combat
- Equipment manager
- Visual controller
- Health system
- Armor system
- Damage calculation

### ✅ Editor Tools
- Raycast visualizer
- Interaction debugger
- Inventory grid visualizer
- Attachment debug window
- Custom gizmos
- Setup wizard

## Quick Start

1. **Run Setup Wizard**
```
   NightHunt > Setup > Interaction System Wizard
```

2. **Add to Player**
    - Select your player prefab in wizard
    - Click "Add Components to Player"

3. **Create Item Database**
    - Click "Create Item Database Manager" in wizard
    - Place in persistent scene

4. **Test with Examples**
    - Enable "Create Example Content"
    - Press Play and test!

## Documentation

Full documentation: https://docs.nighthunt.dev/interaction-system

### Core Components

#### PickupDetector
Raycast-based detection for pickupable items.

#### InteractionDetector
Raycast-based detection for interactable objects.

#### PickupHandler
Server-side pickup logic with validation.

#### EquipmentManager
Manages equipped items and attachments.

### Creating Items
```csharp
[CreateAssetMenu(fileName = "NewWeapon", menuName = "NightHunt/Items/Weapon")]
public class CustomWeapon : WeaponData
{
    // Your custom weapon logic
}
```

### Creating Interactables
```csharp
public class CustomInteractable : InteractableBase
{
    public override void OnInteract(NetworkConnection player)
    {
        // Your interaction logic
    }
}
```

## Architecture
```
Player
├── PickupDetector (raycast)
├── InteractionDetector (raycast)
├── PickupHandler (server logic)
├── InputRouter (input handling)
├── GridInventoryComponent (storage)
├── EquipmentManager (equipped items)
│   └── AttachmentManager (per equipment)
├── PlayerHealthComponent (health)
└── ArmorComponent (damage reduction)
```

## Network Architecture

- **Server-Authoritative**: All gameplay logic runs on server
- **Client Prediction**: Immediate visual feedback
- **SyncVars/SyncLists**: Automatic state synchronization
- **RPCs**: Server→Client communication

## Performance

- Raycast detection: <0.5ms/frame
- Inventory operations: <0.1ms
- Attachment calculations: <0.05ms
- Network bandwidth: ~5KB/s per player

## Requirements

- Unity 2021.3+
- FishNet 4.0+
- Unity Input System

## License

MIT License - See LICENSE file

## Support

Discord: https://discord.gg/nighthunt
Email: support@nighthunt.dev