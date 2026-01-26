# Migration Guide

## Overview

This guide helps migrate from the old interaction/inventory systems to the new Interaction System package.

## Migration Steps

### 1. Replace InteractionSystem

**Old:** `NightHunt.Gameplay.Interaction.InteractionSystem`
**New:** Use `PickupDetector` and `InteractionDetector` separately

**Steps:**
- Remove old `InteractionSystem` component
- Add `PickupDetector` component for item pickup
- Add `InteractionDetector` component for object interaction
- Add `PickupHandler` component for server-side pickup logic
- Add `InteractionHandler` component for interaction routing

### 2. Replace InventorySystem

**Old:** `NightHunt.Gameplay.Inventory.InventorySystem`
**New:** `GridInventoryComponent` or `ListInventoryComponent`

**Steps:**
- Remove old `InventorySystem` component
- Add `GridInventoryComponent` (for grid-based inventory)
- Or add `ListInventoryComponent` (for simple list-based inventory)
- Update all references to use new component

### 3. Update LootItem

**Old:** `NightHunt.Gameplay.Loot.LootItem` (implements IInteractable)
**New:** `NetworkLootItem` (implements IPickupable)

**Steps:**
- Replace `LootItem` with `NetworkLootItem`
- Ensure it implements `IPickupable` (not `IInteractable`)
- Remove `LootInteractable` component (no longer needed)

### 4. Setup Pickup Settings

**New:** Create `PickupSettings` ScriptableObject

**Steps:**
- Create `PickupSettings` asset via menu: `NightHunt/InteractionSystem/PickupSettings`
- Configure auto-pickup settings
- Assign to player or scene

### 5. Update Equipment System

**New:** Use `EquipmentManager` and `AttachmentManager`

**Steps:**
- Add `EquipmentManager` to player
- Add `AttachmentManager` to equipment GameObjects
- Create equipment data ScriptableObjects (WeaponData, ArmorData, etc.)
- Setup attachment slots in equipment data

## Key Changes

1. **Pickup vs Interaction Separation:**
   - Items use `IPickupable` → go to inventory
   - Objects use `IInteractable` → perform actions

2. **Universal Attachment System:**
   - All equipment types support attachments
   - Use `AttachmentManager` for any equipment
   - Configure slots in equipment data

3. **Container System:**
   - Use `LootContainer` for chests/crates
   - Use `PlayerCorpseLoot` for player corpses
   - Use `LootContainerUI` for dual-grid interface

## Notes

- All networking uses Fish-Networking Pro V4 patterns
- Editor tools are available for debugging
- See README.md for detailed usage instructions
