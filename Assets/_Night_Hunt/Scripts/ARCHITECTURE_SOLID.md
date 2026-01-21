# Architecture Documentation - SOLID Principles

## Overview
Hệ thống interaction/inventory/equipment được thiết kế theo nguyên tắc SOLID để dễ dàng scale và mở rộng.

## SOLID Principles Applied

### Single Responsibility Principle (SRP)
Mỗi class chỉ có một trách nhiệm duy nhất:

- **InventorySystem**: Quản lý inventory data (grid, weight, slots)
- **InventoryUIPresenter**: Xử lý logic presentation (MVP pattern)
- **InteractionHandlerFactory**: Tạo interaction handlers
- **PickupInteractionHandler**: Xử lý pickup logic
- **PickupCalculator**: Tính toán số lượng có thể pickup
- **InteractionValidator**: Validate interaction requests

### Open/Closed Principle (OCP)
Có thể mở rộng mà không cần sửa code hiện có:

- **InteractionHandlerFactory**: Dễ dàng thêm handler mới bằng `RegisterHandler()`
- **BaseItemConfig + Derived Classes**: Thêm item type mới bằng cách extend BaseItemConfig
- **IInteractionHandler**: Thêm handler mới implement interface này

### Liskov Substitution Principle (LSP)
Derived classes có thể thay thế base class:

- **BaseItemConfig** → **WeaponItemConfig**, **ConsumableItemConfig**, etc.
- Tất cả đều có thể dùng như BaseItemConfig trong code

### Interface Segregation Principle (ISP)
Interfaces nhỏ và focused:

- **IInventoryProvider**: Chỉ methods cần thiết cho inventory operations
- **IInventoryUIView**: Chỉ methods cần thiết cho UI display
- **IInteractionTarget**: Chỉ methods cần thiết cho interaction targeting
- **IInteractionHandler**: Chỉ method handle interaction

### Dependency Inversion Principle (DIP)
Depend on abstractions, not concretions:

- **NetworkInteractionController** depend on **IInventoryProvider** (interface), không phải **InventorySystem** (concrete)
- **PickupInteractionHandler** depend on **IPickupCalculator** (interface)
- **InventoryUIPresenter** depend on **IInventoryProvider** và **IInventoryUIView** (interfaces)

## Architecture Layers

### Data Layer
- **BaseItemConfig** + Derived Classes: Item configuration data
- **ItemInstance**: Runtime item instance với sockets
- **SocketDefinition**: Socket configuration

### Business Logic Layer
- **InventorySystem**: Inventory management (implements IInventoryProvider)
- **EquipmentManager**: Equipment management với nested sockets
- **NetworkInteractionController**: Server-authoritative interaction handling
- **NetworkItemUsageController**: Server-authoritative item usage

### Presentation Layer (MVP)
- **InventoryUIPresenter**: Business logic cho UI
- **InventoryUI**: View implementation (implements IInventoryUIView)
- **EquipmentUI**: Equipment panel UI

### Handler Layer (Strategy Pattern)
- **InteractionHandlerFactory**: Factory để tạo handlers
- **PickupInteractionHandler**: Pickup logic
- **ChestInteractionHandler**: Chest interaction logic
- Dễ dàng thêm handlers mới

## Flow Diagrams

### Interaction Flow (Pickup)
```
Client (InteractionSystem)
  ↓ Raycast detect NetworkLootItem
  ↓ Check AutoLoot setting
  ↓ ServerRpc_RequestInteract(targetNetId, "Pickup", position)
Server (NetworkInteractionController)
  ↓ Validate (InteractionValidator)
  ↓ Create Handler (InteractionHandlerFactory)
  ↓ HandleInteraction (PickupInteractionHandler)
  ↓ Calculate Pickup Amount (PickupCalculator)
  ↓ Add to Inventory (IInventoryProvider)
  ↓ Update Loot Item
  ↓ Sync Inventory
```

### Drop Flow
```
Client (InventoryUI)
  ↓ User clicks Drop button
  ↓ Show DropAmountSelector
  ↓ User selects amount
  ↓ ServerRpc_RequestDrop(itemId, dropQty, position)
Server (InventorySync)
  ↓ Validate quantity
  ↓ Remove from Inventory
  ↓ Spawn NetworkLootItem
  ↓ Sync Inventory
```

### Equipment Flow
```
Client (EquipmentUI)
  ↓ User selects slot/socket
  ↓ Show compatible items
  ↓ User clicks attach
  ↓ ServerRpc_RequestEquip/Attach
Server (EquipmentManager)
  ↓ Validate slot/socket compatibility
  ↓ Equip/Attach item
  ↓ Sync Equipment State
Client
  ↓ Receive sync
  ↓ Spawn prefab at MountPoint
```

## Extension Points

### Thêm Interaction Type Mới
1. Tạo class implement `IInteractionHandler`
2. Register trong `InteractionHandlerFactory.RegisterHandler()`
3. Không cần sửa code hiện có

### Thêm Item Type Mới
1. Tạo class extend `BaseItemConfig`
2. Update `ItemConfigLoader.ConvertFromLegacy()` nếu cần
3. Không cần sửa code hiện có

### Thêm UI Panel Mới
1. Tạo Presenter class
2. Tạo View class implement interface tương ứng
3. Inject dependencies qua constructor

## Testing & Mocking
Nhờ Dependency Inversion, dễ dàng mock:
- Mock `IInventoryProvider` để test UI
- Mock `IInteractionHandler` để test interaction flow
- Mock `IPickupCalculator` để test pickup logic

