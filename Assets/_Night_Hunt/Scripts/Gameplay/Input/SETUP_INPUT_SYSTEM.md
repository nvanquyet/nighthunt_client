# Hướng dẫn Setup Input System (Nhất quán - Inventory/OpenInventory)

## Tổng quan kiến trúc

**Flow nhất quán:**
```
InputAction (Inventory/OpenInventory) 
  → InventoryInputBridge 
  → InputLayerManager.TransitionToState() 
  → InputManager.SetInventoryMode() / SetPlayerAliveMode()
  → UIRootController.ToggleInventory() (show/hide UI)
```

## Bước 1: Setup InputConfig

1. Tạo ScriptableObject `InputConfig`:
   - Menu: `Assets > Create > Night Hunt > Input > Input Config`
   - Gán `InputSystem_Actions.inputactions` vào field **Input Action Asset**
   - Kiểm tra các map names đúng:
     - Player Map Name = `"Player"`
     - Combat Map Name = `"Combat"`
     - Inventory Map Name = `"Inventory"`
     - Camera Map Name = `"Camera"`
     - UI Map Name = `"UI"`
     - Spectator Map Name = `"Spectator"`
     - Team Map Name = `"Team"`

## Bước 2: Setup GameObject trong Scene

Tạo 1 GameObject (ví dụ `InputSystemRoot`) trong scene gameplay, add các components:

### Components cần thiết:

1. **InputLayerManager**
   - Gán `InputConfig` asset vào field **Input Config**

2. **InputManager**
   - Các handler sẽ được auto-create nếu chưa assign

3. **MovementInputHandler** (auto-created nếu chưa có)
4. **CombatInputHandler** (auto-created nếu chưa có)
5. **CameraInputHandler** (auto-created nếu chưa có)
6. **UIInputHandler** (auto-created nếu chưa có)

7. **InventoryInputBridge** (MỚI - quan trọng!)
   - Field **Input Layer Manager**: drag component `InputLayerManager` trên cùng object
   - Field **UI Root Controller**: drag `UIRootController` từ scene (hoặc để null, sẽ tự tìm)

## Bước 3: Kiểm tra InputSystem_Actions.inputactions

Đảm bảo trong asset `InputSystem_Actions.inputactions`:

- **Map `Inventory`** có action `OpenInventory` (bind phím Tab hoặc I)
- **Map `UI`** có actions:
  - `QuickSlot1`, `QuickSlot2`, `QuickSlot3`, `QuickSlot4`
  - `Cancel`

## Bước 4: Setup UIRootController

Trong scene, đảm bảo có `UIRootController`:
- Gán `InventoryScreen` và `PlayerHUDPanel`
- Gán `_inventoryRootObject` (GameObject chứa inventory UI panel)

## Bước 5: Test

1. Chạy game
2. Nhấn phím bind với `Inventory/OpenInventory` (Tab hoặc I)
3. Kiểm tra:
   - Inventory UI mở/đóng
   - Gameplay input (move, combat) bị disable khi inventory mở
   - Chỉ UI input hoạt động khi inventory mở

## Lưu ý quan trọng

- **KHÔNG dùng `UIInputHandler.OnInventoryToggled` nữa** - đã bị remove
- **Dùng `InventoryInputBridge`** để toggle inventory - đây là cách nhất quán
- `InventoryInputBridge` tự động subscribe vào `Inventory/OpenInventory` action
- Flow state: `PlayerAlive` ↔ `InventoryOpen` được quản lý bởi `InputLayerManager`

## Troubleshooting

### Inventory không mở khi nhấn Tab/I:
- Kiểm tra `InventoryInputBridge` có được add vào `InputSystemRoot` chưa
- Kiểm tra `InputLayerManager` có gán `InputConfig` chưa
- Kiểm tra action `Inventory/OpenInventory` có bind đúng phím chưa

### Gameplay vẫn hoạt động khi inventory mở:
- Kiểm tra `InputLayerManager.TransitionToState()` có được gọi không
- Kiểm tra `InputManager.SetInventoryMode()` có disable đúng handlers không

### UI không show/hide:
- Kiểm tra `InventoryInputBridge` có reference tới `UIRootController` chưa
- Kiểm tra `UIRootController._inventoryRootObject` có được gán chưa
