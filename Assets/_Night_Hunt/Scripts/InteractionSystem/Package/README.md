# NightHunt Interaction System

Comprehensive interaction and pickup system for Unity 6 with Fish-Networking Pro V4.

## Features

- **Separated Pickup vs Interaction Systems**: Clear separation between picking up items and interacting with objects
- **Auto Pickup System**: Configurable auto-pickup with category filtering
- **Universal Attachment System**: Support attachments for ALL equipment types (weapons, armor, helmets, backpacks)
- **Container & Loot System**: Full container system with drag-drop UI for chests and player corpses
- **Event-Driven Architecture**: Decoupled UI system using `InventoryEvents` - implement your own UI that subscribes to events
- **Editor Tools**: Comprehensive debug tools including raycast visualization, interaction debug panels, and attachment debug windows
- **UI Wizard**: Optional tool to generate basic test UI prefabs (example only, customize for your game)

## Requirements

- Unity 6.0+
- Fish-Networking Pro V4
- DOTween (for animations)
- Unity Input System (optional, for input routing)

## Quick Start

1. Add the package to your project
2. Set up a player with `PickupDetector` and `InteractionDetector` components
3. Configure `PickupSettings` ScriptableObject for auto-pickup preferences
4. Create interactable objects by extending `InteractableBase`
5. Create pickupable items by implementing `IPickupable` interface

## Documentation

See the package documentation for detailed usage instructions.
