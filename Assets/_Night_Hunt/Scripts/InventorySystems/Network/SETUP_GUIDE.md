# Inventory Network Sync Setup Guide

## Tổng quan

Sau khi implement các NetworkSync components, bạn cần setup chúng trên Player prefab để chúng hoạt động trong multiplayer game.

## Các Components cần Setup

1. **InventoryNetworkSync** - Đồng bộ inventory
2. **EquipmentNetworkSync** - Đồng bộ equipment
3. **WeaponNetworkSync** - Đồng bộ weapons
4. **QuickSlotNetworkSync** - Đồng bộ quick slots
5. **AttachmentNetworkSync** - Đồng bộ attachments

## Bước 1: Thêm Components vào Player Prefab

### 1.1. Mở Player Prefab
- Tìm Player prefab trong project
- Mở prefab trong Prefab Mode hoặc Scene

### 1.2. Thêm NetworkSync Components
Thêm các component sau vào GameObject chứa `PlayerInventoryController`:

```
Player GameObject
├── PlayerInventoryController
├── InventoryNetworkSync (NEW)
├── EquipmentNetworkSync (NEW)
├── WeaponNetworkSync (NEW)
├── QuickSlotNetworkSync (NEW)
└── AttachmentNetworkSync (NEW)
```

**Lưu ý:** Tất cả các NetworkSync components phải là **NetworkBehaviour** và được thêm vào cùng GameObject có **NetworkObject**.

## Bước 2: Gán References trong Inspector

### 2.1. InventoryNetworkSync

**References:**
- `Inventory System` → Gán `InventorySystem` component
- `Config` → Gán `InventoryConfig` ScriptableObject

**Cross-System References:**
- `Equipment System` → Gán `EquipmentSystem` component
- `Weapon System` → Gán `WeaponSystem` component
- `Quick Slot System` → Gán `QuickSlotSystem` component

**Network Settings:**
- `Enable Client Prediction` → ✅ (true) - Cho UI responsive
- `Reconciliation Timeout` → 2.0 (seconds)

**Anti-Cheat:**
- `Enable Validation` → ✅ (true)
- `Max Operations Per Second` → 20
- `Log Suspicious Activity` → ✅ (true)

### 2.2. EquipmentNetworkSync

**References:**
- `Equipment System` → Gán `EquipmentSystem` component
- `Inventory System` → Gán `InventorySystem` component
- `Inventory Sync` → Gán `InventoryNetworkSync` component (optional, for backward compatibility)

**Network Settings:**
- `Enable Client Prediction` → ✅
- `Reconciliation Timeout` → 2.0

**Anti-Cheat:**
- `Enable Validation` → ✅
- `Max Operations Per Second` → 20
- `Log Suspicious Activity` → ✅

**Visual Sync:**
- `Equipment Model Slots` → Array of Transform (nếu có visual models)

### 2.3. WeaponNetworkSync

**References:**
- `Weapon System` → Gán `WeaponSystem` component
- `Inventory System` → Gán `InventorySystem` component
- `Inventory Sync` → Gán `InventoryNetworkSync` component (optional)

**Network Settings:**
- `Enable Client Prediction` → ✅
- `Reconciliation Timeout` → 2.0

**Anti-Cheat:**
- `Enable Validation` → ✅
- `Max Operations Per Second` → 20
- `Log Suspicious Activity` → ✅

**Visual Sync:**
- `Weapon Holder` → Transform để spawn weapon models

### 2.4. QuickSlotNetworkSync

**References:**
- `Quick Slot System` → Gán `QuickSlotSystem` component
- `Inventory System` → Gán `InventorySystem` component

**Network Settings:**
- `Enable Client Prediction` → ✅
- `Reconciliation Timeout` → 2.0

**Anti-Cheat:**
- `Enable Validation` → ✅
- `Max Operations Per Second` → 20
- `Log Suspicious Activity` → ✅

### 2.5. AttachmentNetworkSync

**References:**
- `Attachment System` → Gán `AttachmentSystem` component
- `Inventory System` → Gán `InventorySystem` component
- `Equipment System` → Gán `EquipmentSystem` component
- `Weapon System` → Gán `WeaponSystem` component

**Network Settings:**
- `Enable Client Prediction` → ✅
- `Reconciliation Timeout` → 2.0

**Anti-Cheat:**
- `Enable Validation` → ✅
- `Max Operations Per Second` → 20
- `Log Suspicious Activity` → ✅

## Bước 3: Verify NetworkObject Setup

### 3.1. Kiểm tra NetworkObject
- Đảm bảo Player GameObject có **NetworkObject** component
- **NetworkObject** phải được setup đúng với FishNet

### 3.2. Kiểm tra Ownership
- Tất cả NetworkSync components sẽ tự động check `IsOwner` trong Public API
- Chỉ owner mới có thể gọi các operations

## Bước 4: Testing

### 4.1. Local Testing (Single Player)
1. Chạy game trong Play Mode
2. Test các operations:
   - Add item to inventory
   - Equip item from inventory
   - Unequip item to inventory
   - Assign quick slot
   - Attach item to weapon/equipment

### 4.2. Multiplayer Testing
1. Build game hoặc dùng Unity Multiplayer Play Mode
2. Start Server
3. Connect 2+ clients
4. Test synchronization:
   - Client 1 equips item → Client 2 should see it
   - Client 1 attaches attachment → All clients should see it
   - Verify state is consistent across all clients

### 4.3. Debug Logs
Enable debug logs trong Inspector để xem:
- `[SERVER]` messages - Server-side operations
- `[CLIENT]` messages - Client-side operations
- `[ANTI-CHEAT]` messages - Suspicious activity

## Bước 5: UI Integration

### 5.1. Gọi Public API từ UI
UI code nên gọi Public API methods, không gọi ServerRpc trực tiếp:

```csharp
// ✅ CORRECT - Gọi Public API
inventoryNetworkSync.RequestEquipFromInventory(itemId, EquipmentSlotType.Helmet);

// ❌ WRONG - Không gọi ServerRpc trực tiếp
inventoryNetworkSync.RequestEquipFromInventory_ServerRpc(itemId, EquipmentSlotType.Helmet);
```

### 5.2. Lắng nghe Events
UI nên subscribe vào Events để update khi state thay đổi:

```csharp
// Inventory events
InventoryEvents.OnItemAdded += OnItemAdded;
InventoryEvents.OnItemRemoved += OnItemRemoved;

// Equipment events
EquipmentEvents.OnItemEquipped += OnItemEquipped;
EquipmentEvents.OnItemUnequipped += OnItemUnequipped;

// Attachment events
AttachmentEvents.OnAttachmentAttached += OnAttachmentAttached;
```

## Bước 6: Spectate Mode Setup

### 6.1. SpectateManager Integration
Khi implement Spectate Mode, UI sẽ lấy data qua `SpectateManager`:

```csharp
// Get current player inventory
var inventory = SpectateManager.GetCurrentPlayerInventory();

// Get current player equipment
var equipment = SpectateManager.GetCurrentPlayerEquipment();
```

## Troubleshooting

### Lỗi: "NullReferenceException" khi gọi Public API
**Nguyên nhân:** References chưa được gán trong Inspector
**Giải pháp:** Kiểm tra lại tất cả references trong Inspector

### Lỗi: "Operation rejected" trên client
**Nguyên nhân:** 
- Rate limit exceeded
- Item ownership validation failed
- Item không tồn tại

**Giải pháp:** 
- Check debug logs để xem lý do cụ thể
- Verify item ownership trước khi gọi operation

### Lỗi: "Sync state không update"
**Nguyên nhân:** 
- Event subscription chưa đúng
- Local System không fire events

**Giải pháp:**
- Verify Event subscription trong `OnStartServer()`
- Check Local System có fire events đúng không

### Lỗi: "Client prediction không hoạt động"
**Nguyên nhân:**
- `Enable Client Prediction` = false
- `IsOwner` = false

**Giải pháp:**
- Enable Client Prediction trong Inspector
- Verify NetworkObject ownership

## Best Practices

1. **Luôn gọi Public API**, không gọi ServerRpc trực tiếp
2. **Enable Validation** trong production để prevent cheating
3. **Log Suspicious Activity** để track potential hacks
4. **Test với nhiều clients** để verify synchronization
5. **Monitor rate limits** - nếu quá nhiều operations, có thể là spam/hack

## Architecture Flow

```
UI Layer
  ↓ (calls Public API)
NetworkSync Public API
  ↓ (checks IsOwner, optional prediction)
ServerRpc
  ↓ (validation layer)
Local System (InventorySystem, EquipmentSystem, etc.)
  ↓ (fires Events)
Event System
  ↓ (NetworkSync subscribes)
Sync State (SyncList/SyncDictionary)
  ↓ (FishNet auto-syncs)
Client Sync Handler
  ↓ (updates Local System)
Local Events (for UI)
```

## Summary Checklist

- [ ] Thêm tất cả NetworkSync components vào Player prefab
- [ ] Gán tất cả references trong Inspector
- [ ] Setup NetworkObject đúng cách
- [ ] Enable Validation và Anti-Cheat
- [ ] Test local operations
- [ ] Test multiplayer synchronization
- [ ] Integrate với UI layer
- [ ] Setup SpectateManager (nếu cần)

Sau khi hoàn thành checklist, hệ thống inventory network sync sẽ sẵn sàng cho multiplayer game!
