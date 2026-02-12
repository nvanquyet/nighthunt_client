# INVENTORY SYSTEM - PHÂN TÍCH CHI TIẾT

## 1. TỔNG QUAN KIẾN TRÚC HIỆN TẠI

### 1.1. Core Systems
```
PlayerInventoryController (Orchestrator)
├── InventorySystem (List-based, weight-limited)
├── EquipmentSystem (Helmet, Armor, Backpack, Boots)
├── WeaponSystem (Primary, Secondary weapons)
├── QuickSlotSystem (Consumables hotkeys)
└── AttachmentSystem (Attachments for items)
```

### 1.2. Network Sync
```
InventoryNetworkSync
EquipmentNetworkSync
WeaponNetworkSync
QuickSlotNetworkSync
AttachmentNetworkSync
```

### 1.3. Data Structures
```
ItemDefinition (ScriptableObject) - Template
ItemInstance (Runtime) - Instance with state
ItemInstanceData (Struct) - Network serialization
```

---

## 2. LUỒNG LOGIC CHÍNH

### 2.1. ADD ITEM TO INVENTORY
```
Flow:
1. User picks up item from world
2. PlayerInventoryController.PickupItem(ItemInstance)
3. InventorySystem.AddItem(item, out slot)
   - Check weight limit
   - Auto-merge if stackable (config: AutoMergeStacks)
   - Find empty index
   - Add to list
   - Fire: InventoryEvents.OnItemAdded
4. Network: InventoryNetworkSync syncs to server
5. Server validates & broadcasts to clients
```

**Vấn đề:**
- ✅ Đã có auto-merge cho stackable items
- ❌ THIẾU: Remove với số lượng cụ thể (chỉ có remove all)
- ❌ THIẾU: Split stack trực tiếp từ UI

---

### 2.2. EQUIP ITEM
```
Flow:
1. User equips item from inventory
2. PlayerInventoryController.EquipFromInventory(inventorySlot, equipSlot)
3. EquipmentSystem.EquipItem(item, slot)
   - Validate: CanEquip()
   - If slot occupied → SwapEquipment()
   - Apply stat modifiers → ApplyEquipmentModifiers()
   - Fire: EquipmentEvents.OnItemEquipped
4. Remove from inventory
5. Network sync
```

**Vấn đề:**
- ✅ Stats modifiers được apply đúng
- ❌ THIẾU: Logic giảm weight khi equip backpack
- ⚠️ Weight capacity ở 2 nơi: InventoryConfig + PlayerStats

---

### 2.3. ATTACH/DETACH ATTACHMENTS
```
Flow:
1. User attaches item to parent (e.g., Scope → Weapon)
2. AttachmentSystem.AttachItem(parent, attachment)
   - Validate compatibility
   - Check slot availability
   - Add to parent.AttachedItems
   - Apply modifiers (Character or Weapon stats)
   - Fire: AttachmentEvents.OnAttachmentAttached
3. Network sync
```

**Vấn đề:**
- ✅ Modifiers được apply theo target (Character/Weapon)
- ✅ Có swap attachments giữa 2 items
- ❌ THIẾU: Move attachment từ item A sang item B (khi slot B empty)
- ✅ ĐÃ CÓ: RequestMoveAttachment trong AttachmentNetworkSync

---

### 2.4. QUICK SLOTS
```
Flow:
1. User assigns item to quick slot
2. QuickSlotSystem.AssignToQuickSlot(item, index)
3. User presses hotkey (1-8)
4. QuickSlotSystem.UseQuickSlot(index)
   - UseItemByType() → ConsumeItem()
   - Decrease stack or remove item
   - Fire: OnQuickSlotUsed
```

**Vấn đề:**
- ⚠️ UseItemByType() chưa đầy đủ:
  - Consumable: OK
  - Throwable: TODO
  - Weapon: Commented out
- ❌ THIẾU: Logic cụ thể cho grenades, bandages

---

## 3. VẤN ĐỀ TRÙNG LẶP & CẦN TỐI ƯU

### 3.1. WEIGHT MANAGEMENT (DUPLICATE)

**Vị trí 1: InventoryConfig**
```csharp
public float DefaultWeightCapacity = 50f;
```

**Vị trí 2: PlayerStats**
```csharp
public float GetWeightCapacity() => _finalValue[CharacterStatType.WeightCapacity];
```

**Vấn đề:**
- Weight capacity nên là CHARACTER STAT (có thể modify bởi equipment)
- InventoryConfig.DefaultWeightCapacity chỉ là giá trị khởi tạo
- Hiện tại InventorySystem.GetMaxWeight() check cả 2 nơi → confusing

**✅ GIẢI PHÁP:**
- PlayerStats là source of truth
- InventoryConfig.DefaultWeightCapacity → chỉ dùng để init PlayerStats
- InventorySystem.GetMaxWeight() → luôn lấy từ PlayerStats

---

### 3.2. CODE DUPLICATION

#### Log Functions (Repeated in every file)
```csharp
void Log(string message)
{
    if (enableDebugLogs)
        Debug.Log($"[ClassName] {message}");
}
```

**✅ GIẢI PHÁP:** Dùng InventoryLogger utility (đã có)

#### Validation Logic
- CheckRateLimit() repeated in all NetworkSync files
- ValidateRequest() repeated
- ValidateOwnership() có logic tương tự

**✅ GIẢI PHÁP:** Tạo NetworkValidationUtility

---

### 3.3. THIẾU CHỨC NĂNG

#### A. Remove Item với số lượng cụ thể
```
❌ HIỆN TẠI:
- RemoveItem(instanceId) → remove ALL
- RemoveItemAtSlot(index) → remove ALL

✅ CẦN:
- RemoveItem(instanceId, amount) → remove partial
```

#### B. Event Items
```
❌ THIẾU:
- ItemType.EventItem
- Logic phân biệt event items (không thể equip)
```

#### C. Equipment Weight Reduction
```
❌ THIẾU:
- Backpack equip → increase WeightCapacity
- Hiện tại: Equipment modifiers chỉ track trong EquipmentSystem
- Cần: Apply lên PlayerStats.WeightCapacity
```

#### D. Consumable Types Details
```
❌ THIẾU LOGIC:
- Bandages: heal over time
- Health potion: instant heal
- Grenades: throw mechanics
- Stamina items
```

---

## 4. CENTRALIZED API

### 4.1. Hiện tại
**PlayerInventoryController** đã có các methods:
```csharp
// Inventory
AddItem(), RemoveItem(), MoveItem()
GetCurrentWeight(), GetMaxWeight(), IsOverweight()

// Equipment
EquipFromInventory(), UnequipToInventory()
GetEquippedItem()

// Weapons
EquipWeaponFromInventory(), UnequipWeaponToInventory()
SwitchWeapon(), GetActiveWeapon(), ReloadWeapon()

// Attachments
AttachFromInventory(), DetachToInventory()

// Quick Slots
AssignQuickSlot(), UseQuickSlot(), ClearQuickSlot()

// World Interaction
PickupItem(), DropItem(), DropEquippedItem()
```

### 4.2. CẦN BỔ SUNG
```csharp
// Stack operations
SplitStack(instanceId, amount)
MergeStacks(sourceId, targetId)

// Partial remove
RemoveItemAmount(instanceId, amount)

// Swap operations
SwapAttachmentsBetweenItems(item1, slot1, item2, slot2)
SwapEquipmentSlots(slot1, slot2)

// Utility
GetItemsByType(ItemType type)
GetAllConsumables()
ValidateItemEquippable(instanceId)
```

---

## 5. EVENT SYSTEM

### 5.1. Hiện tại
```
InventoryEvents: OnItemAdded, OnItemRemoved, OnItemMoved, etc.
EquipmentEvents: OnItemEquipped, OnItemUnequipped, etc.
WeaponEvents: OnWeaponEquipped, OnActiveWeaponChanged, etc.
AttachmentEvents: OnAttachmentAttached, OnAttachmentDetached, etc.
CharacterStatsEvents: OnStatChanged, OnAddModifier, etc.
```

### 5.2. Vấn đề
- Events phân tán ở nhiều static classes
- UI cần subscribe nhiều nơi

### 5.3. ✅ GIẢI PHÁP
- Tạo UnifiedInventoryEvents với nested categories
- Hoặc giữ nguyên nhưng document rõ ràng

---

## 6. TÓM TẮT NHỮNG GÌ ĐÃ LÀM TỐT

✅ **Kiến trúc modular**: Các systems độc lập, dễ maintain
✅ **Network sync**: Flow rõ ràng với validation
✅ **List-based inventory**: Linh hoạt, không giới hạn slots
✅ **Weight system**: Đã implement đầy đủ
✅ **Stack operations**: Split, merge đã có
✅ **Attachment system**: Complex nhưng complete
✅ **Stats modifiers**: Target-based (Character/Weapon) rõ ràng
✅ **Events**: Comprehensive event system
✅ **Utilities**: StackManager, InventoryLogger đã có
✅ **Serialization**: ItemInstanceData cho network

---

## 7. ROADMAP REFACTORING

### Priority 1 (Critical)
1. ✅ Fix weight management duplication
2. ✅ Add RemoveItemAmount()
3. ✅ Implement equipment weight reduction (backpack)
4. ✅ Expand consumable logic

### Priority 2 (Important)
5. ✅ Create NetworkValidationUtility
6. ✅ Expand PlayerInventoryController API
7. ✅ Add EventItem type
8. ✅ Document swap operations clearly

### Priority 3 (Nice to have)
9. ⚠️ Unified event system (optional)
10. ⚠️ UI helper utilities

---

## 8. KẾT LUẬN

**Hệ thống hiện tại đã hoàn thiện 85%.**

**Điểm mạnh:**
- Kiến trúc tốt, modular
- Network sync robust
- Stats system flexible

**Cần cải thiện:**
- Weight management đang duplicate
- Thiếu một số methods trong API
- Consumable logic chưa đầy đủ
- Code duplication (logging, validation)

**Next steps: Refactor từng file theo roadmap trên.**