# 📋 INVENTORY SYSTEM FLOW DOCUMENTATION

## 🎯 TỔNG QUAN FLOW

### 1. **PICKUP ITEM → INVENTORY**
```
Player pickup item (NetworkLootItem)
  → ItemDropHandler hoặc PickupHandler
  → InventoryNetworkSync.AddItemServerRpc()
  → Server: AddItemServer() → AddItemObserversRpc()
  → Client: InventoryLogicEvents.OnItemAdded
  → InventoryPanel.OnItemAdded()
  → RefreshInventoryGrid() (giữ local positions)
  → Item hiển thị trong inventory UI
```

### 2. **LOOT CONTAINER (Raycast-based)**
```
Player raycast vào container (chest, corpse, etc.)
  → InteractionDetector phát hiện
  → InteractionEvents.OnInteractTargetChanged
  → LootContainerPanel.HandleInteractTargetChanged()
  → isRaycastingContainer = true
  → LoadContainer() + Show()
  → InventoryPanel.SetMode(InventoryMode.Loot)
  → Container UI hiển thị bên phải
```

**Lưu ý:**
- Container UI CHỈ hiển thị khi `isRaycastingContainer = true`
- Khi mất raycast → `HandleInteractTargetLost()` → Hide() + ClearContainer()
- Container config: `allowAddItems` / `allowRemoveItems` quyết định có thể thêm/xóa items

### 3. **MOVE ITEM: INVENTORY ↔ CONTAINER**

#### **Inventory → Container:**
```
DragDropHandler.HandleContainerInventoryMove()
  → ClearLocalItemPosition(itemId) // Xóa local position
  → InventoryPanel.MoveItemToContainer()
  → LootContainerPanel.MoveItemToContainer()
  → InventoryUIEvents.RequestMoveItemToContainer
  → InventoryEventHandler.HandleMoveItemToContainerRequested
  → ContainerNetworkSync.MoveItemToContainerServerRpc()
  → Server: GetItemInstanceFromInventory() → container.AddItem() → inventory.RemoveItemServerRpc()
  → ObserversRpc: RemoveItemObserversRpc() → InventoryPanel.OnItemRemoved()
  → Container: AddItemObserversRpc() → LootContainerPanel.RefreshLootGrid()
```

#### **Container → Inventory:**
```
DragDropHandler.HandleContainerInventoryMove()
  → InventoryPanel.MoveItemFromContainer()
  → LootContainerPanel.MoveItemFromContainer()
  → InventoryUIEvents.RequestMoveItemFromContainer
  → InventoryEventHandler.HandleMoveItemFromContainerRequested
  → ContainerNetworkSync.MoveItemFromContainerServerRpc()
  → Server: GetItemInstanceFromContainer() → container.RemoveItem() → inventory.AddItemServer()
  → ObserversRpc: AddItemObserversRpc() → InventoryPanel.OnItemAdded() + UpdateLocalItemPosition()
  → Container: RemoveItemObserversRpc() → LootContainerPanel.RefreshLootGrid()
```

**Lưu ý:**
- Khi move item, UI KHÔNG tự đóng
- Local positions được quản lý cho inventory items
- Container items không có local positions (theo server order)

### 4. **MOVE ITEM: INVENTORY ↔ EQUIPMENT/WEAPON/QUICKSLOT**

#### **Inventory → Equipment:**
```
DragDropHandler.HandleEquipmentMove()
  → Validate item type với equipment slot type
  → InventoryUIEvents.RequestEquipItem
  → InventoryEventHandler → InventoryNetworkSync.EquipItemServerRpc()
  → Server: Validate → Equip → RemoveItemServerRpc()
  → ObserversRpc: EquipmentPanel.UpdateEquipmentSlot()
```

#### **Inventory → Weapon Slot:**
```
DragDropHandler.HandleWeaponMove()
  → Validate weapon type
  → InventoryUIEvents.RequestEquipWeapon
  → WeaponNetworkSync.EquipWeaponServerRpc()
  → Server: Equip weapon → RemoveItemServerRpc()
  → ObserversRpc: WeaponSwitchingSystem.UpdateWeaponSlot()
```

#### **Inventory → Quick Slot:**
```
DragDropHandler.HandleQuickSlotMove()
  → Validate consumable item
  → InventoryUIEvents.RequestAssignQuickSlot
  → InventoryNetworkSync.AssignQuickSlotServerRpc()
  → Server: Assign → Sync
  → ObserversRpc: QuickSlotPanel.UpdateQuickSlot()
```

### 5. **DROP ITEM**
```
InventoryPanel.DropItem()
  → GetItemInstanceFromInventory() (preserve state)
  → ItemDropHandler.DropItem(itemInstance, position)
  → Client: inventory.RemoveItem() (validation)
  → ServerDropItem(itemInstance, position)
  → LootSpawnManager.SpawnItemAtPosition()
  → Spawn NetworkLootItem với ServerInitializeFromItemInstance()
  → Item spawn trong world với preserved state (durability, attachments, etc.)
```

### 6. **ATTACHMENT SYSTEM**

#### **Attachment Data Flow:**
```
EquipmentDataBase.AttachmentSlots[] (ScriptableObject)
  → Định nghĩa các slot có thể gắn attachment
  → AttachmentSlotDefinition: { slotType, displayName, compatibleTypes[] }
  
AttachmentData (Item có thể gắn)
  → compatibleSlots: AttachmentSlotType[] (các slot type có thể gắn vào)
  → statModifiers: StatModifier[] (stats khi gắn vào)
```

#### **Hover Item → Show Attachment UI:**
```
ItemCell.OnPointerEnter()
  → Check: item.ItemData is EquipmentDataBase && AttachmentSlots.Length > 0
  → InventoryPanel.ShowNestedEquipmentPanelOnHover()
  → Determine: isEquippedItem ? nestedEquipmentRight : nestedEquipmentLeft
  → NestedEquipmentPanel.ShowForItem()
  → CreateNestedSlots() từ EquipmentDataBase.AttachmentSlots[]
  → Spawn ItemCell cho MỖI attachment slot
  → Load attached items nếu có (từ ItemInstance.customData hoặc EquipmentHandler)
```

#### **Drag Attachment → Equipment:**
```
DragDropHandler.HandleAttachmentMove()
  → Validate: attachment.compatibleSlots contains targetSlotType
  → InventoryUIEvents.RequestAttachItem
  → InventoryEventHandler → EquipmentHandler.AttachToEquipmentServerRpc()
  → Server: Validate → Attach → RemoveItemServerRpc()
  → ObserversRpc: Update nested UI + EquipmentPanel
```

#### **Detach Attachment:**
```
DragDropHandler.HandleAttachmentDetach()
  → InventoryUIEvents.RequestDetachItem
  → EquipmentHandler.DetachFromEquipmentServerRpc()
  → Server: Detach → AddItemServer() (về inventory)
  → ObserversRpc: Update nested UI + InventoryPanel
```

### 7. **LOCAL UI STATE PERSISTENCE**

```
localItemPositions: Dictionary<string, int> // itemId → UI slot index

RefreshInventoryGrid():
  1. Cleanup local positions (xóa items không còn trong inventory)
  2. Create empty slots
  3. Place items theo localItemPositions (nếu có)
  4. Place remaining items vào first available slots
  5. Update localItemPositions cho items mới

UpdateLocalItemPosition(itemId, slotIndex):
  → Lưu vị trí khi user drag-drop trong inventory
  
ClearLocalItemPosition(itemId):
  → Xóa khi item được move ra ngoài inventory (container, equipment, drop)
```

---

## 🐛 CÁC LỖI CẦN FIX

### **Bug 1: Container UI tự đóng sau khi move item**
**Nguyên nhân:** `HandleContainerItemsChanged` hoặc `OnLootContainerClosed` được trigger khi item thay đổi
**Fix:** Chỉ hide UI khi `!isRaycastingContainer`, không hide khi chỉ remove item

### **Bug 2: Item về vị trí đầu khi mở lại inventory**
**Nguyên nhân:** `localItemPositions` không được persist đúng cách hoặc bị clear
**Fix:** Đảm bảo `UpdateLocalItemPosition()` được gọi khi move, và `RefreshInventoryGrid()` restore đúng

### **Bug 3: Move inventory → container: mất item UI, không hiển thị trong container**
**Nguyên nhân:** Server sync chưa đúng hoặc UI không refresh
**Fix:** Đảm bảo `ContainerNetworkSync` gọi đúng flow và `RefreshLootGrid()` được trigger

### **Bug 4: Drop item không spawn ra world**
**Nguyên nhân:** `ItemDropHandler.ServerDropItem()` hoặc `LootSpawnManager.SpawnItemAtPosition()` có vấn đề
**Fix:** Verify spawn flow và check logs

### **Bug 5: Hover weapon không hiển thị attachment UI**
**Nguyên nhân:** `ShowNestedEquipmentPanelOnHover()` không được gọi hoặc check sai
**Fix:** Đảm bảo `ItemCell.OnPointerEnter()` check đúng và gọi `ShowNestedEquipmentPanelOnHover()`

### **Bug 6: Attachment UI không spawn đủ slots**
**Nguyên nhân:** `CreateNestedSlots()` không tạo đủ slots từ `AttachmentSlots[]`
**Fix:** Verify `CreateNestedSlots()` loop qua tất cả `AttachmentSlots`

### **Bug 7: Hover empty slot không show tooltip**
**Nguyên nhân:** Tooltip logic chưa handle empty slots
**Fix:** Add tooltip cho empty equipment/weapon/attachment slots

---

## 🔒 ANTI-CHEAT & SERVER SYNC

### **Server Authority:**
- Tất cả operations phải qua `ServerRpc` → Server validate → `ObserversRpc` sync
- Client chỉ làm optimistic UI update, server quyết định cuối cùng
- Item positions trong inventory: Server quản lý list, Client quản lý UI positions (local)

### **Validation:**
- Item type vs slot type (equipment, weapon, quickslot)
- Attachment compatibility (compatibleSlots vs slotType)
- Quantity checks
- Ownership checks

---

## 📝 LOGGING STRATEGY

### **Keep Logs:**
- Error logs (warnings, errors)
- Critical flow points (server RPC entry/exit)
- Validation failures

### **Remove Logs:**
- Debug logs trong UI refresh loops
- Verbose logs trong drag-drop operations
- Redundant state logs
