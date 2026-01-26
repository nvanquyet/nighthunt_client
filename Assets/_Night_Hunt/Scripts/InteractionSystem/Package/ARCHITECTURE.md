# Package Architecture

## Design Philosophy

This package follows **separation of concerns** and **loose coupling** principles:

- **Package provides**: Core logic, data structures, event system
- **Game provides**: UI implementation, visual styling, game-specific features

## Why UI is NOT in the Package?

### 1. **Flexibility**
- Each game has different UI styles (realistic, stylized, minimalist, etc.)
- Different screen sizes and resolutions
- Different input methods (mouse, touch, gamepad)
- Different accessibility requirements

### 2. **Maintainability**
- Package stays focused on core functionality
- UI changes don't require package updates
- Easier to update package without breaking game UI

### 3. **Reusability**
- Package can be used across multiple projects
- No UI dependencies in package code
- Game developers have full control

### 4. **Performance**
- Game can optimize UI for their specific needs
- No unnecessary UI code in package
- Smaller package size

## Architecture Layers

```
┌─────────────────────────────────────┐
│         Game UI Layer               │  ← Game implements
│  (Custom UI, Styling, Animations)   │
└──────────────┬──────────────────────┘
               │ Subscribes to Events
┌──────────────▼──────────────────────┐
│      Event System (InventoryEvents) │  ← Package provides
│  (Decoupled communication layer)     │
└──────────────┬──────────────────────┘
               │ Invoked by Logic
┌──────────────▼──────────────────────┐
│      Core Logic Layer                │  ← Package provides
│  (Inventory, Equipment, Loot, etc.) │
└──────────────────────────────────────┘
```

## What the Package Provides

### ✅ Core Logic Components
- `InventoryComponentBase`, `GridInventoryComponent`, `ListInventoryComponent`
- `EquipmentManager`, `EquipmentHandler`
- `PickupHandler`, `InteractionHandler`
- `LootContainer`, `LootSpawnPoint`
- `ItemDropHandler`

### ✅ Event System
- `InventoryEvents` static class with `Action` delegates
- Events for: item added/removed, weight changed, equipped/unequipped, etc.
- **No UI dependencies** - pure C# events

### ✅ Data Structures
- `ItemInstance`, `ItemDataBase`, `EquipmentDataBase`
- `LootTable`, `LootItemDefinition`
- ScriptableObject data classes

### ✅ Helper Components (Optional)
- `InventoryUIListener` - Example listener (game can use or replace)
- `LootContainerUI` - Basic controller (game implements grid rendering)
- `InteractionUIController` - Basic prompt controller

### ❌ NOT Provided
- UI prefabs (game-specific)
- UI styling/theme
- Grid/item slot rendering
- Drag-and-drop UI implementation
- Animations/transitions

## How to Implement UI

### Option 1: Subscribe to Events (Recommended)

```csharp
public class MyInventoryUI : MonoBehaviour
{
    private void OnEnable()
    {
        InventoryEvents.OnItemAdded += HandleItemAdded;
        InventoryEvents.OnWeightChanged += HandleWeightChanged;
        // ... subscribe to other events
    }

    private void OnDisable()
    {
        InventoryEvents.OnItemAdded -= HandleItemAdded;
        InventoryEvents.OnWeightChanged -= HandleWeightChanged;
        // ... unsubscribe
    }

    private void HandleItemAdded(ItemInstance item)
    {
        // Update your UI grid, show notification, play sound, etc.
    }

    private void HandleWeightChanged(float current, float max)
    {
        // Update weight bar, show warning, etc.
    }
}
```

### Option 2: Use Helper Components (For Quick Testing)

- Use `InventoryUIListener` as starting point
- Customize or replace with your own implementation
- Helper components are **examples only**, not required

### Option 3: Use UI Wizard (For Testing)

- Menu: `NightHunt/InteractionSystem/UI Wizard`
- Generates basic test UI prefabs
- **Marked as "example only"** - customize for production

## Example: Custom Inventory Grid UI

```csharp
public class MyInventoryGridUI : MonoBehaviour
{
    [SerializeField] private Transform gridParent;
    [SerializeField] private GameObject slotPrefab;

    private void OnEnable()
    {
        InventoryEvents.OnInventoryChanged += RefreshGrid;
    }

    private void OnDisable()
    {
        InventoryEvents.OnInventoryChanged -= RefreshGrid;
    }

    private void RefreshGrid()
    {
        // Clear existing slots
        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }

        // Get inventory items
        var inventory = FindObjectOfType<GridInventoryComponent>();
        if (inventory == null) return;

        // Create slots for each item
        foreach (var item in inventory.Items)
        {
            GameObject slot = Instantiate(slotPrefab, gridParent);
            // Configure slot with item data
            slot.GetComponent<ItemSlotUI>().SetItem(item);
        }
    }
}
```

## Benefits of This Architecture

1. **Game developers have full control** over UI appearance and behavior
2. **Package stays lightweight** and focused on core functionality
3. **Easy to test** - can test logic without UI
4. **Easy to update** - package updates don't break game UI
5. **Reusable** - same package works for different game styles

## Migration Path

If you need to change UI:
1. Keep event subscriptions
2. Replace UI implementation
3. No changes needed in package code

## Conclusion

**Package = Logic + Events**  
**Game = UI Implementation**

This separation ensures the package remains flexible, maintainable, and reusable across different projects.
