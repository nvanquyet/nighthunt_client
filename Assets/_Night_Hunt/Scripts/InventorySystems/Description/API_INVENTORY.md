Dưới đây là phiên bản **chuẩn hoá thành file Markdown (.md)** cho toàn bộ API contract của hệ thống Inventory / Equipment / Weapon / Network.

Bạn có thể copy toàn bộ nội dung này lưu thành:

```
Gameplay_API_Contract.md
```

---

# 🎮 Gameplay & Network API Contract

> Đây là **hợp đồng chính thức (Final Contract)** cho tầng Gameplay / Orchestrator.
> Mọi hệ thống khác (UI, World, Interaction, Network, AI…) **phải đi qua các API này**.
> Không được thao tác trực tiếp vào InventorySystem, EquipmentSystem, WeaponSystem.

---

# 1️⃣ PlayerInventoryController – Inventory API

## ➤ Core Inventory Operations

```csharp
OperationResult AddItem(ItemInstance item, out int assignedSlot);

OperationResult RemoveItem(string instanceId);

OperationResult RemoveItemAtSlot(int slotIndex, out ItemInstance removedItem);

OperationResult MoveItem(int fromSlot, int toSlot);

OperationResult SwapItems(int slotA, int slotB);

OperationResult SplitStack(int slotIndex, int amount, out ItemInstance splitItem);

OperationResult MergeStacks(int sourceSlot, int targetSlot);

OperationResult RemoveItemAmount(string instanceId, int amount);

OperationResult SplitStack(string instanceId, int amount);

OperationResult MergeStacks(string sourceInstanceId, string targetInstanceId);
```

---

## ➤ Query Helpers

```csharp
ItemInstance GetItemAtSlot(int slotIndex);

ItemInstance FindItem(string instanceId);

List<ItemInstance> GetAllItems();

List<ItemInstance> GetItemsByType(ItemType type);

List<ItemInstance> GetAllConsumables();
```

---

## ➤ Weight

```csharp
float GetCurrentWeight();

float GetMaxWeight(); // luôn ưu tiên PlayerStats.WeightCapacity

bool IsOverweight();
```

---

## 📌 Mô tả

* Toàn bộ Add / Remove / Move / Swap / Split / Merge
  ➜ Ủy thác hoàn toàn cho `InventorySystem`
* Bắn `InventoryEvents` tương ứng
* API theo `InstanceId` giúp UI không cần biết slot index

---

# 2️⃣ PlayerInventoryController – Equipment API

## ➤ Core

```csharp
OperationResult EquipFromInventory(int inventorySlot, EquipmentSlotType equipSlot);

OperationResult UnequipToInventory(EquipmentSlotType equipSlot);

OperationResult ValidateItemEquippable(string instanceId, EquipmentSlotType equipSlot);
```

---

## ➤ Query

```csharp
ItemInstance GetEquippedItem(EquipmentSlotType slotType);

List<ItemInstance> GetAllEquippedItems();
```

---

## ➤ Durability / Weight

```csharp
void DamageEquippedItem(EquipmentSlotType slotType, float damage);

float GetEquippedWeight();
```

---

# 3️⃣ PlayerInventoryController – Weapon API

## ➤ Equip / Switch

```csharp
OperationResult EquipWeaponFromInventory(int inventorySlot, WeaponSlotType weaponSlot);

OperationResult UnequipWeaponToInventory(WeaponSlotType weaponSlot);

OperationResult SwitchWeapon(WeaponSlotType slotType);

OperationResult SwitchToNextWeapon();

OperationResult SwitchToPreviousWeapon();
```

---

## ➤ Query

```csharp
ItemInstance GetActiveWeapon();

WeaponStats GetWeaponStats();
```

---

## ➤ Ammo & Fire

```csharp
OperationResult ReloadWeapon(int ammoAmount);

bool ConsumeAmmo(int amount);

int GetCurrentAmmo();

int GetMaxAmmo();
```

---

## ➤ Durability

```csharp
void DamageWeapon(WeaponSlotType slotType, float damage);
```

---

# 4️⃣ PlayerInventoryController – Attachment API

## ➤ Core

```csharp
OperationResult AttachFromInventory(int inventorySlot, ItemInstance parentItem);

OperationResult DetachToInventory(ItemInstance parentItem, AttachmentSlotType slotType);

OperationResult SwapAttachmentsBetweenItems(
    ItemInstance sourceItem, AttachmentSlotType sourceSlot,
    ItemInstance targetItem, AttachmentSlotType targetSlot);

OperationResult MoveAttachmentToItem(
    ItemInstance sourceItem, AttachmentSlotType sourceSlot,
    ItemInstance targetItem);
```

---

## ➤ Query

```csharp
ItemInstance[] GetAttachments(ItemInstance parentItem);

ItemInstance GetAttachment(ItemInstance parentItem, AttachmentSlotType slotType);

ItemInstance GetParentItemForAttachment(ItemInstance attachment);
```

---

# 5️⃣ PlayerInventoryController – Quick Slots API

## ➤ Core

```csharp
OperationResult AssignQuickSlot(int inventorySlot, int quickSlotIndex);

OperationResult UseQuickSlot(int quickSlotIndex);

void ClearQuickSlot(int quickSlotIndex);
```

---

## ➤ Query

```csharp
ItemInstance GetQuickSlotItem(int quickSlotIndex);

bool IsQuickSlotAssigned(int quickSlotIndex);

int GetQuickSlotCount();
```

---

## ➤ Maintenance

```csharp
void ValidateQuickSlots();
```

---

# 6️⃣ PlayerInventoryController – World Interaction

```csharp
OperationResult PickupItem(ItemInstance item);

OperationResult DropItem(int inventorySlot);

OperationResult DropEquippedItem(EquipmentSlotType slotType);
```

---

# 7️⃣ PlayerStats / WeaponStats API

---

## ➤ PlayerStats

```csharp
float GetWeightCapacity();

float GetCurrentHealth();
float GetMaxHealth();

float GetCurrentStamina();
float GetMaxStamina();

float GetMoveSpeed();

float GetVisionRadius();
```

### Events

* OnAddModifier
* OnRemoveModifier
* OnStatChanged

---

## ➤ WeaponStats

```csharp
void Initialize(ItemInstance weaponInstance);
```

### Getters (tuỳ design)

* Magazine size
* Damage
* Fire rate
* Recoil
* Accuracy
* Range

---

# 🌐 Network Layer – Public API Contract

---

# 8️⃣ InventoryNetworkSync (Client Public API)

```csharp
void RequestPickup(string itemDefinitionId, int stackSize = 1);

void RequestDrop(string instanceId);

void RequestEquipFromInventory(string instanceId, EquipmentSlotType slotType);

void RequestUnequipToInventory(EquipmentSlotType slotType);

void RequestEquipWeaponFromInventory(string instanceId, WeaponSlotType slotType);

void RequestAssignQuickSlot(string instanceId, int quickSlotIndex);

void RequestMoveItem(int fromSlot, int toSlot);

void RequestSwapItems(int slotA, int slotB);

void RequestSplitStack(int slotIndex, int amount);

void RequestMergeStacks(int sourceSlot, int targetSlot);
```

---

## ➤ OperationId Convention

```
add_{itemDefinitionId}
add_{InstanceId}

remove_{InstanceId}

move_{from}_{to}

swap_{a}_{b}

split_{slot}_{amount}

merge_{source}_{target}

equip_{instanceId}_{slotType}

unequip_{slotType}

equipWeapon_{instanceId}_{slotType}

assignQuickSlot_{instanceId}_{quickSlotIndex}
```

---

# 9️⃣ EquipmentNetworkSync

```csharp
void RequestEquipFromInventory(string instanceId, EquipmentSlotType slotType);

void RequestUnequipToInventory(EquipmentSlotType slotType);
```

---

# 🔟 WeaponNetworkSync

```csharp
void RequestEquipWeaponFromInventory(string instanceId, WeaponSlotType slotType);

void RequestUnequipWeaponToInventory(WeaponSlotType slotType);

void RequestSwitchWeapon(WeaponSlotType slotType);

void RequestReload(int ammoAmount);

void RequestConsumeAmmo(int amount);
```

---

# 1️⃣1️⃣ QuickSlotNetworkSync

```csharp
void RequestAssignQuickSlot(string instanceId, int quickSlotIndex);

void RequestClearQuickSlot(int quickSlotIndex);
```

---

# 1️⃣2️⃣ AttachmentNetworkSync

```csharp
void RequestAttach(string parentItemId, string attachmentInstanceId);

void RequestDetach(string parentItemId, AttachmentSlotType slotType);

void RequestSwapAttachments(
    string sourceItemId, AttachmentSlotType sourceSlot,
    string targetItemId, AttachmentSlotType targetSlot);

void RequestMoveAttachment(
    string sourceItemId, AttachmentSlotType sourceSlot,
    string

# 1️⃣3️⃣ WorldItemDropNetworkSync

```csharp
// Server side
void InitializeOnServer(ItemInstance item);

// Client side
void RequestPickup(PlayerInventoryController inventory);
```

---
