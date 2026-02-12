## 1. Overview

Hệ thống inventory được chia làm 3 tầng chính:

- **Core & Systems (logic local)**
  - `InventorySystem`: quản lý list ItemInstance, weight, stack.
  - `EquipmentSystem`: quản lý trang bị (helmet, armor, backpack, boots…).
  - `WeaponSystem`: quản lý vũ khí (primary, secondary, ammo, active weapon).
  - `AttachmentSystem`: quản lý attachments trên item.
  - `QuickSlotSystem`: quản lý quick slots (phím 1–8).
  - `PlayerStats`, `WeaponStats`: quản lý chỉ số nhân vật & vũ khí.

- **Orchestrator**
  - `PlayerInventoryController`: API trung tâm cho gameplay sử dụng, wrap các system trên và xử lý tương tác với world.

- **Network**
  - `InventoryNetworkSync`, `EquipmentNetworkSync`, `WeaponNetworkSync`,
    `QuickSlotNetworkSync`, `AttachmentNetworkSync`, `WorldItemDropNetworkSync`:
    xử lý RPC, validation, đồng bộ state với FishNet.

UI **không** gọi trực tiếp Systems hay ServerRpc, mà luôn đi qua:
- Public API của `PlayerInventoryController` (logic offline).
- Public API của các `*NetworkSync` (logic network).

---

## 2. Data & Config (tóm tắt)

- **`ItemDefinition`**
  - Template: `ItemId`, `ItemType`, `Weight`, `IsStackable`, `MaxStackSize`, `AllowedSlotLocations`, `EquipmentSlot`,
    `AttachmentSlots`, `AttachmentType`, `StatModifiers`, `WorldModelPrefab`, `EquippedModelPrefab`, v.v.

- **`ItemInstance`**
  - Runtime: `InstanceId`, `Definition`, `StackSize`, `Durability`, `IsEquipped`, `EquippedLocation`, `InventoryIndex`,
    `AttachedItems`, `CustomData`, `AcquiredTime`.
  - Hàm quan trọng: `GetTotalWeight()`, `ModifyDurability()`, `GetDurabilityPercent()`, `IsBroken()`,
    `Serialize()`, `Deserialize()`, `Clone()`, `IsValid()`.

- **`ItemInstanceData`**
  - Struct dùng cho network sync: `InstanceId`, `ItemId`, `StackSize`, `Durability`, `IsEquipped`,
    `EquippedLocation`, `InventoryIndex`, `AcquiredTime`, `CustomData`.

- **`InventoryConfig`**
  - `EnableWeightSystem`, `DefaultWeightCapacity`, `OverweightSpeedPenalty`,
    `AllowPickupWhenOverweight`, `AutoMergeStacks`, `GlobalMaxStackSize`,
    cấu hình drop & network batching, validation.

- **`PlayerStats`**
  - `GetWeightCapacity()` đọc từ `_finalValue[CharacterStatType.WeightCapacity]`.
  - **Source of truth** cho max weight capacity.
  - `InventoryConfig.DefaultWeightCapacity` dùng như giá trị khởi tạo ban đầu.

---

## 3. Public Gameplay API – `PlayerInventoryController`

### 3.1. Inventory

---

### Nội dung đề xuất cho `Refactoring_Summary.md`

Bạn có thể copy nội dung dưới đây vào `Refactoring_Summary.md`:

## Refactoring Summary – Inventory Systems (Logic + Network, không UI)

### 1. Mục tiêu

- Chuẩn hoá API public quanh `PlayerInventoryController` và các `*NetworkSync`.
- Đảm bảo mọi thay đổi item (equip/unequip/attach/detach/consume/reload) cập nhật đúng `PlayerStats`/`WeaponStats` và được sync network rõ ràng.
- Không sửa đổi bất kỳ code UI nào; UI chỉ dùng Public API + Events.

---

### 2. Trước refactor (tình trạng cũ)

- API public phân tán, một số luồng chỉ được mô tả trong docs, không rõ ràng trong code.
- WeightCapacity bị trùng giữa `InventoryConfig.DefaultWeightCapacity` và `PlayerStats`.
- Nhiều logic validation mạng lặp lại trong từng `*NetworkSync`.
- QuickSlot chưa support đầy đủ cho Weapon/Equipment/Throwable; consumable mới chỉ giảm stack.
- Một số lớp utility (`NetworkMessages`, `NetworkValidationUtility`, `StatModifierManager`) rỗng/chưa dùng.

---

### 3. Sau refactor (mục tiêu)

- **API gameplay rõ ràng**:
  - Tất cả thao tác inventory/equipment/weapon/attachment/quickslot/world đi qua
    `PlayerInventoryController` + các `*NetworkSync` Public API.
  - Chữ ký, tham số, `OperationResult` và events liên quan được document trong `Implementation_Guide.md`.

- **Stats & Weight pipeline thống nhất**:
  - `PlayerStats.WeightCapacity` là source of truth cho max weight.
  - Backpack/equipment/attachments tác động lên stats thông qua `StatModifiers` + `CharacterStatsEvents` / `WeaponStats`.

- **Network layer chuẩn hoá**:
  - Mọi thao tác có dạng:
    - Client: `RequestXxx(...)`
    - Server: `RequestXxx_ServerRpc(...)` + `Validate*` + gọi Systems
    - Confirm: `Confirm..._TargetRpc(...)`
  - Các `*NetworkSync` sử dụng cùng pattern (Inventory, Equipment, Weapon, QuickSlot, Attachment).

- **Event contracts rõ ràng**:
  - Bảng mapping use-case → API → events được định nghĩa, để UI/gameplay chỉ cần subscribe đúng event.

---