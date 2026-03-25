# NightHunt — Master Refactoring Plan
> Ngày lập: 25-03-2026 | Senior Unity Dev Review  
> Scope: Inventory · Equipment · Weapon · Attachment · Stat · UI drag-drop · Input sync · FishNet sync

---

## TL;DR – 17 vấn đề tìm thấy

| # | Mức độ | Nhóm | Tóm tắt |
|---|--------|------|---------|
| 1 | 🔴 Critical | Stat | `ItemStatSystem` vs `ItemStatComputer` — TRÙNG LẶP hoàn toàn logic tính stat |
| 2 | 🔴 Critical | Network | `EquipItem()` / `EquipWeapon()` thiếu ServerRpc fallback — client owner không gọi được |
| 3 | 🔴 Critical | Network | `ItemInstanceData.Equals()` bỏ sót `CurrentMagazine`, `AttachedItems` → FishNet không dirty |
| 4 | 🟠 High | Architecture | `IAttachmentSystem` không có trong DI Bridge — resolve runtime mỗi lần unequip |
| 5 | 🟠 High | Dead Code | `ApplyWeaponModifiers()` empty no-op + `RemoveWeaponModifiers()` double-remove với SAO |
| 6 | 🟠 High | Dead Code | Commented-out `ApplyEquipmentModifiers` / `RemoveEquipmentModifiers` trong `EquipmentSystem` |
| 7 | 🟠 High | Performance | `ComponentResolver.Find<IAttachmentSystem>()` gọi 2 lần trong method body mỗi unequip |
| 8 | 🟡 Medium | Architecture | `StatApplyOrchestrator` không trong Bridge DI — SerializeField riêng, lifecycle tách rời |
| 9 | 🟡 Medium | Design | `MagazineSize` stat dùng cho cả current ammo lẫn max capacity — semantic conflict |
| 10 | 🟡 Medium | Design | `GameplaySystemsBridge` có ~20 passthrough wrapper methods — zero value added |
| 11 | 🟡 Medium | Architecture | `InventoryConfig` SerializeField ở EquipmentSystem, WeaponSystem, AttachmentSystem, DragDropController — risk config drift |
| 12 | 🟡 Medium | Architecture | `_batchWeightUpdates` + `_pendingWeightUpdate` trong InventorySystem — half-implemented |
| 13 | 🟡 Medium | UI | DragDropController: optimistic update không có rollback mechanism nếu server reject |
| 14 | 🟡 Medium | Event | Event chain 5 layers: System→Bridge→UIDomainBridge→SlotView — Bridge relay không transform |
| 15 | 🟡 Medium | Architecture | `StatApplyOrchestrator` wires events trực tiếp vào concrete MB refs — phải qua Bridge |
| 16 | 🟢 Low | Code Quality | `FindNextAvailableInventoryIndex()` duplicate trong EquipmentSystem & WeaponSystem |
| 17 | 🟢 Low | Code Quality | Fire mode persistance via `PlayerPrefs` trong game loop — không phù hợp production |

---

## PHẦN 1 — KIẾN TRÚC TỔNG QUAN HIỆN TẠI

```
NetworkPlayer (Prefab root)
├── PlayerStatSystem        NetworkBehaviour + SyncList<StatData>
├── InventorySystem         NetworkBehaviour + SyncList<ItemInstanceData>
├── EquipmentSystem         NetworkBehaviour + SyncDictionary<EquipmentSlotType, string>
├── AttachmentSystem        NetworkBehaviour
├── ItemSelectionSystem     NetworkBehaviour + SyncVar<string>
├── ItemUseSystem           NetworkBehaviour
└── [Child: WeaponSystem]   NetworkBehaviour + SyncDictionary + SyncVar<WeaponSlotType?>
    ├── WeaponSystem.Fire.cs
    ├── WeaponSystem.Reload.cs
    ├── WeaponSystem.UnEquip.cs
    └── WeaponSystem.NetworkSync.cs

GameplaySystemsBridge      Plain C#, created in NetworkPlayer.Awake()
  → Holds refs to all 6 systems (vắng mặt: AttachmentSystem)
  → Re-publishes 20+ events từ tất cả systems
  → 20+ passthrough wrapper methods

StatApplyOrchestrator      MonoBehaviour (Player prefab)
  → [SerializeField] trực tiếp PlayerStatSystem, EquipmentSystem, WeaponSystem, AttachmentSystem
  → KHÔNG qua Bridge DI
  → Subscribe events trong OnEnable/OnDisable riêng

ItemStatSystem             Static class (StatSystem/Systems/)
  → Tính stat item + attachment modifiers
  → Có riêng _statCache: string→Dict<ItemStatType,float>

ItemStatComputer           Static class (GameplaySystems/Systems/Stat/)
  → Tính CÙNG stat item + attachment modifiers  ← TRÙNG LẶP
  → Ghi vào instance._computedStats

UI Layer (Client-only):
UIRootController
  └── UIDomainBridge         (per-player adapter)
  └── InventoryScreen        (slot views, toolbar)
      ├── ItemSlotView[]     (pure view)
      ├── ItemSlotInput[]    (Unity EventSystem → DragDropController)
      ├── AttachmentPanel
      ├── ItemTooltip        ← dùng ItemStatSystem
      └── PlayerStatUIPanel

DragDropController         Singleton, ghost, optimistic update
DragDropValidator          Pure rules, stateless
InputLayerManager          Singleton, SSOT ActionMap enable/disable
```

---

## PHẦN 2 — PHÂN TÍCH CHI TIẾT TỪNG VẤN ĐỀ

---

### #1 🔴 TRÙNG LẶP: ItemStatSystem vs ItemStatComputer

**Vị trí:**
- `Scripts/Gameplay/StatSystem/Systems/ItemStatSystem.cs`
- `Scripts/Gameplay/GameplaySystems/Systems/Stat/ItemStatComputer.cs`

**Vấn đề:**
Cả hai làm ĐÚNG một việc: tính `base stats + attachment modifiers` cho một ItemInstance.

```
ItemStatSystem.CalculateItemStat(item, statType):
  1. Kiểm tra _statCache
  2. GetDefinition → GetBaseStatFromDefinition (switch on WeaponDef/EquipmentDef/...)
  3. ApplyAttachmentModifiers (iterate AttachedItems → collect ItemStatModifiers → Flat→Pct)
  4. Cache vào _statCache[instanceID][statType]

ItemStatComputer.Compute(instance):
  1. SeedBaseStats (switch on WeaponDef/EquipmentDef/AttachmentDef)
  2. Collect attachment modifiers (iterate AttachedItems → collect ItemStatModifiers)
  3. Apply Flat→Pct (additive pct accumulation)
  4. instance.SetComputedStats(newStats)
```

**Logic gần như copy nhau.** Điểm khác duy nhất:
- `ItemStatSystem`: lazy per-stat, có cache riêng, dùng bởi UI Tooltip
- `ItemStatComputer`: eager all-stats, ghi vào `instance._computedStats`, dùng bởi SAO

**Hậu quả:**
- `AttachmentSystem` gọi `ItemStatSystem.InvalidateCache(parentID)` → flush _statCache
- SAO.Recalculate() gọi `ItemStatComputer.Compute(item)` → rewrite instance._computedStats
- Hai cache sống song song, có thể lệch nhau (race condition)
- `ItemTooltip` đọc từ `ItemStatSystem` → có thể show giá trị khác với gameplay thực tế

**Fix:**
```
Xóa ItemStatSystem.
Refactor tất cả callers (ItemTooltip, AttachmentPanel...) dùng instance.GetComputedStat().
Đảm bảo SAO.Recalculate() luôn gọi ItemStatComputer.Compute() trước khi UI đọc.
Thêm ItemStatComputer.GetOrCompute(item, statType) cho lazy access nếu cần.
```

---

### #2 🔴 THIẾU ServerRpc FALLBACK cho EquipItem và EquipWeapon

**Vị trí:**
- `EquipmentSystem.cs:EquipItem()` — chỉ gọi được từ server
- `WeaponSystem.UnEquip.cs:EquipWeapon()` — chỉ gọi được từ server

**Vấn đề:**
```csharp
// EquipmentSystem.cs
public void EquipItem(string instanceID)
{
    if (!IsServerInitialized)
    {
        Debug.LogWarning("[EquipmentSystem] EquipItem: server-only!");
        return;  // ← CLIENT OWNER BỊ TỪ CHỐI
    }
    EquipItemServer(instanceID);
}
```

Nhưng `DragDropController.ApplyBackendAction()` chạy trên client owner, gọi:
```csharp
bridge.EquipItem(instanceID);        // → EquipmentSystem.EquipItem() → BLOCKED
bridge.EquipWeaponToSlot(id, slot);  // → WeaponSystem.EquipWeaponToSlot() → BLOCKED
```

Tương tự `UnequipItem()` CÓ RPC nhưng `EquipItem()` KHÔNG CÓ → inconsistent.

**Fix:**
```csharp
public void EquipItem(string instanceID)
{
    if (IsServerInitialized) { EquipItemServer(instanceID); return; }
    if (IsOwner) EquipItemServerRpc(instanceID);
}

[ServerRpc(RequireOwnership = true)]
private void EquipItemServerRpc(string instanceID) => EquipItemServer(instanceID);
```
Tương tự cho `EquipWeapon()` và `EquipWeaponToSlot()`.

---

### #3 🔴 ItemInstanceData.Equals() BỎ SÓT FIELDS QUAN TRỌNG

**Vị trí:** `Scripts/Gameplay/GameplaySystems/Core/Data/ItemInstanceData.cs`

**Vấn đề:**
```csharp
public override bool Equals(object obj)
{
    return InstanceID   == other.InstanceID
        && DefinitionID == other.DefinitionID
        && Quantity     == other.Quantity;   // ← CHỈ 3 FIELDS
    // THIẾU: CurrentMagazine, CurrentResource, AttachedItems, InventoryIndex
}
```

FishNet SyncList gọi `Equals()` để quyết định có dirty item hay không. Khi `_items[i] = updatedData` được gọi từ `SyncItemState()`:
- Nếu chỉ `CurrentMagazine` thay đổi → `Equals()` trả về `true` → FishNet KHÔNG ghi vào dirty set → clients KHÔNG nhận update

**Hậu quả thực tế:**
- Client HUD có thể show ammo sai (stale value)
- Khi attach/detach item, `AttachedItems[]` thay đổi nhưng clients không được notify
- `InventoryIndex` = -1 khi equip không được sync (tuy nhiên Equipment SyncDict đã handle case này)

**Fix:**
```csharp
public override bool Equals(object obj)
{
    if (!(obj is ItemInstanceData other)) return false;
    return InstanceID      == other.InstanceID
        && DefinitionID    == other.DefinitionID
        && Quantity        == other.Quantity
        && CurrentMagazine == other.CurrentMagazine
        && Mathf.Approximately(CurrentResource, other.CurrentResource)
        && InventoryIndex  == other.InventoryIndex
        && AttachedItemsEqual(AttachedItems, other.AttachedItems);
}

private static bool AttachedItemsEqual(string[] a, string[] b)
{
    if (a == b) return true;
    if (a == null || b == null) return false;
    if (a.Length != b.Length) return false;
    for (int i = 0; i < a.Length; i++)
        if (a[i] != b[i]) return false;
    return true;
}
```

---

### #4 🟠 IAttachmentSystem KHÔNG TRONG DI BRIDGE

**Vị trí:** `NetworkPlayer.cs` + `GameplaySystemsBridge.cs`

**Vấn đề:**
```csharp
// NetworkPlayer.Awake()
GamePlaySystemBridge = new GameplaySystemsBridge(
    inventory, equipment, weapon, itemSelection, statSystem, itemUse);
// ← AttachmentSystem bị bỏ qua
```

Consequence:
- `EquipmentSystem.UnequipItemServer()` phải gọi `ComponentResolver.Find<IAttachmentSystem>()` tại runtime mỗi khi unequip
- Gọi 2 lần (bug: duplicate resolution với khác pattern)
- `WeaponSystem.UnequipWeaponServer()` gọi `ComponentResolver.Find<IAttachmentSystem>()` mỗi lần
- `StatApplyOrchestrator` cũng giữ ref riêng qua `[SerializeField]`

**Fix:**
```csharp
// GameplaySystemsBridge.cs
public class GameplaySystemsBridge : IGameplayBridge, IDisposable
{
    private readonly IAttachmentSystem _attachment; // THÊM

    public IAttachmentSystem Attachment => _attachment; // THÊM
    
    public GameplaySystemsBridge(
        IInventorySystem inventory, IEquipmentSystem equipment,
        IWeaponSystem weapon, IAttachmentSystem attachment,   // THÊM
        IItemSelectionSystem itemSelection, IPlayerStatSystem statSystem,
        IItemUseSystem itemUse)
    { ... }
}

// NetworkPlayer.Awake()
var attachment = ComponentResolver.Find<IAttachmentSystem>(this).OnSelf().InChildren().Resolve();
GamePlaySystemBridge = new GameplaySystemsBridge(
    inventory, equipment, weapon, attachment, itemSelection, statSystem, itemUse);
```

Sau đó inject attachment vào `EquipmentSystem` và `WeaponSystem` qua constructor/setter thay vì runtime resolve.

---

### #5 🟠 DEAD CODE: ApplyWeaponModifiers / RemoveWeaponModifiers

**Vị trí:** `WeaponSystem.UnEquip.cs`

**Vấn đề:**
```csharp
[Server]
private void ApplyWeaponModifiers(string instanceID, WeaponDefinition weaponDef)
{
    if (_statSystem == null) return;
    foreach (var m in (IEnumerable)weaponDef.GetPlayerModifiers() ?? System.Array.Empty<object>())
    {
        // Cast to your modifier type here — kept generic to compile without full type refs.
        // _statSystem.AddModifier(m.StatType, new StatModifier { ... });
        // ← THÂN HÀM TRỐNG, CHƯA IMPLEMENT
    }
}

[Server]
private void RemoveWeaponModifiers(string instanceID)
{
    _statSystem?.RemoveAllModifiersFromSource(instanceID);
    // ← SAO đã làm với SOURCE_PREFIX+"sao:"+instanceID
    // ← Xóa bare instanceID source — có thể không khớp nhưng là dead code
}
```

`ApplyWeaponModifiers` được gọi từ `AssignToSlot()` — empty no-op.
`RemoveWeaponModifiers` được gọi từ `UnequipWeaponServer()` — xóa source `instanceID` nhưng SAO dùng `"sao:" + instanceID`. Không trùng nhưng gây confusion.

SAO là SSOT cho modifier management. Methods này là remnants từ trước khi có SAO.

**Fix:** Xóa cả 2 methods và tất cả call sites.

---

### #6 🟠 DEAD CODE: Commented-out Modifier Methods trong EquipmentSystem

**Vị trí:** `EquipmentSystem.cs`

**Vấn đề:**
```csharp
// Trong EquipItemServer():
// [SAO] Handled by StatApplyOrchestrator
// ApplyEquipmentModifiers(instanceID, equipmentDef);

// Trong UnequipItemServer():
// [SAO] StatApplyOrchestrator handles modifier removal — disabled to prevent double-apply
// RemoveEquipmentModifiers(instanceID);
```

Plus các methods commented-out hoặc unreachable code về `ApplyEquipmentModifiers` / `RemoveEquipmentModifiers`.

**Fix:** Xóa toàn bộ commented-out code liên quan. Code đã xác nhận SAO handles it.

---

### #7 🟠 ComponentResolver RUNTIME trong Method Body

**Vị trí:** `EquipmentSystem.UnequipItemServer()` và `WeaponSystem.UnequipWeaponServer()`

**Vấn đề:**
```csharp
// EquipmentSystem.UnequipItemServer() — gọi mỗi lần unequip:
var attachmentSystem = ComponentResolver.Find<IAttachmentSystem>(this)
    .OnSelf().InChildren()
    .OrLogWarning("[Auto] ...").Resolve();

if (attachmentSystem == null)  // ← NULL CHECK sau khi đã resolve
{
    attachmentSystem = ComponentResolver.Find<IAttachmentSystem>(this)  // ← GỌI LẦN 2
        .InParent().InRootChildren()
        .OrLogWarning("[Auto] ...").Resolve();
}
```

Hai lần ComponentResolver scan trong cùng một flow. Đây là O(hierarchy) operation mỗi lần unequip. Không cache.

**Fix:**
```csharp
// Awake()
private IAttachmentSystem _attachmentSystem; // cached field

private void Awake()
{
    _attachmentSystem = ComponentResolver.Find<IAttachmentSystem>(this)
        .OnSelf().InChildren().InParent().InRootChildren()
        .OrLogWarning("[EquipmentSystem] IAttachmentSystem not found")
        .Resolve();
}

// UnequipItemServer() — giờ chỉ:
if (_inventoryConfig.DetachAttachmentsOnUnequip)
    _attachmentSystem?.DetachAllFromItem(instanceID);
```

---

### #8 🟡 StatApplyOrchestrator Không Trong DI Chain

**Vị trí:** `StatApplyOrchestrator.cs`

**Vấn đề:**
SAO là `MonoBehaviour` với 4 `[SerializeField]` trỏ vào MB concrete types:
```csharp
[SerializeField] private PlayerStatSystem _playerStatSystemMB;
[SerializeField] private EquipmentSystem _equipmentSystemMB;
[SerializeField] private WeaponSystem _weaponSystemMB;
[SerializeField] private AttachmentSystem _attachmentSystemMB;
```

SAO subscribe events trong `OnEnable/OnDisable`, hoàn toàn độc lập với Bridge lifecycle.
Nếu Bridge bị Dispose() (player disconnect, spectate switch), SAO vẫn còn subscribe tới old system events.

**Fix phương án A (nhỏ):** SAO subscribe qua Bridge events thay vì system events trực tiếp:
```csharp
// StatApplyOrchestrator.OnEnable()
_bridge = GetComponentInParent<NetworkPlayer>()?.GamePlaySystemBridge;
if (_bridge != null)
{
    _bridge.OnItemEquipped   += _ => ScheduleRecalc();
    _bridge.OnItemUnequipped += _ => ScheduleRecalc();
    _bridge.OnWeaponEquipped += _ => ScheduleRecalc();
    // ... etc
}
```

**Fix phương án B (lớn):** Promote SAO vào Bridge: `IStatApplyOrchestrator` là một thành phần của `IGameplayBridge`, lifecycle gắn với Bridge.

---

### #9 🟡 MagazineSize Semantic Conflict

**Vị trí:** `WeaponSystem.Reload.cs`, `WeaponSystem.Fire.cs`

**Vấn đề:**
`ItemStatType.MagazineSize` dùng để chỉ HAI thứ khác nhau:
```csharp
// Dùng như MAX capacity (từ definition):
int magCap = Mathf.RoundToInt(weaponDef.GetStatValue(ItemStatType.MagazineSize));

// Dùng như CURRENT ammo (từ instance):
int currentAmmo = (int)inst.GetCurrentValue(ItemStatType.MagazineSize);

// TRONG RELOAD:
float magCap = inst.GetComputedStat(ItemStatType.MagazineSize);     // ← max
int current  = (int)inst.GetCurrentValue(ItemStatType.MagazineSize); // ← current
```

Cùng một `ItemStatType.MagazineSize` được đọc qua `GetComputedStat()` (trả về max) VÀ `GetCurrentValue()` (trả về current). Đây là intentional design của `ItemInstance` (current values track actual state, computed stats track max), nhưng naming `MagazineSize` cho cả hai gây confusion.

**Fix:**
```csharp
// Rename trong ItemStatType enum:
MagazineCapacity,  // max rounds in magazine (was: MagazineSize) — in definition
CurrentMagazine,   // current loaded rounds (was: MagazineSize in instance current values)
```

Hoặc nếu không muốn rename enum: đổi tên method calls và add comments rõ ràng:
```csharp
// Naming helper extension:
public static int GetCurrentAmmo(this ItemInstance inst)   => (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
public static int GetMagazineCapacity(this ItemInstance inst) => (int)inst.GetComputedStat(ItemStatType.MagazineSize);
```

---

### #10 🟡 GameplaySystemsBridge — 20+ Passthrough Wrappers

**Vị trí:** `GameplaySystemsBridge.cs`

**Vấn đề:**
```csharp
// Đây là zero-value wrappers:
public void AddItem(string defID, int qty)           => _inventory.AddItem(defID, qty);
public void RemoveItem(string instanceID)             => _inventory.RemoveItem(instanceID);
public void DropItem(string instanceID)               => _inventory.DropItem(instanceID);
public void EquipItem(string instanceID)              => _equipment.EquipItem(instanceID);
public void UnequipItem(EquipmentSlotType slot)       => _equipment.UnequipItem(slot);
public void EquipWeapon(string instanceID)            => _weapon.EquipWeapon(instanceID);
// ... ~15 more
```

Bridge đã expose `Inventory`, `Equipment`, `Weapon` trực tiếp. Callers có thể dùng:
```csharp
bridge.Inventory.AddItem(defID, qty);   // vs bridge.AddItem(defID, qty)
```

**Giữ lại** các non-trivial bridge methods:
- `AddAndEquip(defID)` — cross-system operation
- `GetAllItems()` — sugar
- `GetWeightPercent()` — tính toán từ stat
- `Refresh()` (nếu có)

**Xóa** tất cả pure single-system delegates.

---

### #11 🟡 InventoryConfig Mất Kiểm Soát

**Vị trí:** SerializeField trong EquipmentSystem, WeaponSystem, AttachmentSystem, DragDropController

**Vấn đề:** 4 components riêng biệt mỗi cái cần được assign cùng `InventoryConfig` trong Inspector. Risk of stale/wrong config being assigned on one of them.

**Fix Option A:** `InventoryConfig` là `ScriptableObjectSingleton` — truy cập qua `InventoryConfig.Instance`.

**Fix Option B:** Inject qua `IGameplayBridge` — Bridge nhận InventoryConfig một lần, expose qua property.

**Fix Option C (đơn giản nhất):** Tạo static accessor trong InventoryConfig:
```csharp
[CreateAssetMenu(...)]
public class InventoryConfig : ScriptableObject
{
    private static InventoryConfig _instance;
    public static InventoryConfig Instance => _instance;  // set khi SO loaded
    
    private void OnEnable() => _instance = this;
}
```

---

### #12 🟡 BatchWeightUpdates Half-Implemented

**Vị trí:** `InventorySystem.cs`

**Vấn đề:**
```csharp
[SerializeField] private bool _batchWeightUpdates = true;
private bool _isUpdatingWeight = false;
private float _pendingWeightUpdate = 0f;  // ← CHƯA BAO GIỜ được dùng
```

`_pendingWeightUpdate` float không có trong bất kỳ logic nào. `ScheduleWeightUpdate()` sử dụng `_isUpdatingWeight` bool như một frame-deferred gate, nhưng mechanism thực sự chưa hoàn chỉnh.

**Fix:** 
```csharp
// Bỏ _pendingWeightUpdate nếu không dùng.
// Implement proper batch:
private bool _weightDirty;
private void LateUpdate() {
    if (_weightDirty) { RecalculateTotalWeight(); _weightDirty = false; }
}
private void ScheduleWeightUpdate() => _weightDirty = true;
```

---

### #13 🟡 DragDropController — Không Có Rollback

**Vị trí:** `DragDropController.cs`

**Vấn đề:**
Flow hiện tại:
1. `NotifyDropTarget` → `ValidateDrop()` pass → `ApplyLocalAction()` (optimistic visual update)
2. → `ApplyBackendAction()` (gửi RPC/call system) → NO ACK

Nếu server reject (item đã bị xóa bởi loot, hết slot trên server, concurrent action):
- `_items.OnChange` callback trong `InventorySystem` sẽ cuối cùng correct the UI
- **Nhưng** trong khoảng 1-2 frames giữa optimistic update và server correction, UI sẽ hiện sai
- Không có explicit "revert" path
- Nếu SyncList correction không trigger UI event đúng cách → permanent UI desync

**Fix:**
```csharp
// Option A (recommended): Subscribe InventorySystem.SyncList.OnChange → refresh UI slot
// Đây là cách FishNet-native: server is truth, UI follows SyncList.
// Remove optimistic update entirely — lag is acceptable given LAN/low RTT gameplay.

// Option B: Add server ACK
[ServerRpc] void RequestDropServerRpc(UISlotId src, UISlotId dst);
[TargetRpc] void ConfirmDropTargetRpc(NetworkConnection conn, bool success, UISlotId src, UISlotId dst);
// On failure: DragDropController.Rollback(src, srcState, dst, dstState)
```

---

### #14 🟡 Event Chain 5 Layers

**Vị trí:** InventorySystem → GameplaySystemsBridge → UIDomainBridge → ItemSlotView

**Flow hiện tại:**
```
1. InventorySystem._items.OnChange → OnItemsChanged()
2. → OnItemAdded.Invoke(item)           [System event]
3. → GameplaySystemsBridge.HandleItemAdded(item)
4. → Bridge.OnItemAdded.Invoke(item)   [Bridge relay — NO TRANSFORM]
5. → UIDomainBridge.HandleItemChangedInventory(item)
6. → BuildSlotStateFromItem(item) → OnInventorySlotChanged.Invoke(id, state)  [TRANSFORM]
7. → ItemSlotView.SetState(state)
```

Steps 3-4 là pure relay không có transformation hay logic. Bridge chỉ re-fire.

**Fix (nhỏ):** Giữ nguyên nhưng document rõ. Bridge event surface có value cho external subscribers.

**Fix (lớn):** UIDomainBridge subscribe trực tiếp vào system events qua `bridge.Inventory.OnItemAdded` (không qua Bridge's re-published event):
```csharp
// UIDomainBridge.WireEvents():
_bridge.Inventory.OnItemAdded += HandleItemChangedInventory; // direct system event
// vs:
_bridge.OnItemAdded += HandleItemChangedInventory;            // bridge relay (current)
```

Giảm 1 layer relay. Bridge events vẫn tồn tại cho external consumers.

---

### #15 🟡 StatApplyOrchestrator — Direct SerializeField Refs

Đã cover ở #8. SAO cần được refactor để subscribe qua Bridge hoặc interface layer, không serialize concrete MB refs.

---

### #16 🟢 FindNextAvailableInventoryIndex Duplicate

**Vị trí:** `EquipmentSystem.cs`, `WeaponSystem.UnEquip.cs`

```csharp
// EquipmentSystem:
private int FindNextAvailableInventoryIndex()
    => _inventorySystem.GetNextFreeInventoryIndex();

// WeaponSystem:
internal int FindNextAvailableInventoryIndex()
    => _inventorySystem?.GetNextFreeInventoryIndex() ?? 0;
```

Pure duplicate 1-liners. Method gọi không cần wrapper — gọi trực tiếp `_inventorySystem.GetNextFreeInventoryIndex()`.

**Fix:** Xóa cả 2 wrapper methods, gọi trực tiếp.

---

### #17 🟢 PlayerPrefs Trong Game Loop

**Vị trí:** `WeaponSystem.Fire.cs`

```csharp
public FireMode GetCurrentFireMode()
{
    int saved = PlayerPrefs.GetInt($"firemode_{(int)slot.Value}", -1);  // ← DISK READ
    if (saved >= 0) { _fireModes[slot.Value] = (FireMode)saved; ... }
}

public void SetFireMode(FireMode mode)
{
    PlayerPrefs.SetInt($"firemode_{(int)slot.Value}", (int)mode);      // ← DISK WRITE
}
```

`PlayerPrefs.GetInt/SetInt` là synchronous I/O, không nên trong hot path `GetCurrentFireMode()`. `GetCurrentFireMode()` được gọi nhiều lần (trong auto-fire coroutine mỗi shot).

**Fix:**
```csharp
// Load once in Awake/OnStartClient, save only on change
private void LoadSavedFireModes()
{
    foreach (var slot in _slotPriority)
    {
        int saved = PlayerPrefs.GetInt($"firemode_{(int)slot}", -1);
        if (saved >= 0) _fireModes[slot] = (FireMode)saved;
    }
}

// GetCurrentFireMode(): chỉ đọc từ _fireModes dict (no PlayerPrefs)
// SetFireMode(): set dict + PlayerPrefs.SetInt (acceptable on user action, not per-frame)
```

---

## PHẦN 3 — KẾ HOẠCH TRIỂN KHAI (Theo thứ tự ưu tiên)

### SPRINT 1 — Critical Bug Fixes (Làm trước, không break thêm)

#### Task 1.1: Fix `ItemInstanceData.Equals()` [#3]
- **File:** `ItemInstanceData.cs`
- **Action:** Thêm `CurrentMagazine`, `CurrentResource`, `InventoryIndex`, `AttachedItems` vào Equals()
- **Risk:** Thấp — chỉ ảnh hưởng FishNet dirty detection, không thay đổi logic
- **Test:** Verify ammo sync trên client sau khi bắn; verify attachment sync sau attach/detach

#### Task 1.2: Add ServerRpc fallback cho EquipItem/EquipWeapon [#2]
- **Files:** `EquipmentSystem.cs`, `WeaponSystem.UnEquip.cs`
- **Action:** Thêm `[ServerRpc(RequireOwnership=true)]` pattern như UnequipItem đã làm
- **Risk:** Thấp — thêm code path, không xóa
- **Test:** DragDropController equip từ client → verify server processes it

#### Task 1.3: Cache IAttachmentSystem trong Awake, xóa double ComponentResolver [#7]
- **Files:** `EquipmentSystem.cs`, `WeaponSystem.UnEquip.cs`
- **Action:** Add `private IAttachmentSystem _attachmentSystem;` field, resolve once in `Awake()` / `ValidateReferences()`
- **Risk:** Thấp — chỉ là caching, logic giống nhau
- **Test:** Unequip weapon/equipment với attachment → verify attach still detaches

---

### SPRINT 2 — Architecture Cleanup (Xóa dead code, đơn giản hóa)

#### Task 2.1: Xóa dead modifier methods trong WeaponSystem [#5]
- **File:** `WeaponSystem.UnEquip.cs`
- **Action:** Delete `ApplyWeaponModifiers()`, `RemoveWeaponModifiers()`, và calls từ `AssignToSlot()` / `UnequipWeaponServer()`
- **Risk:** Thấp — methods là no-op anyway, SAO handles stats
- **Test:** Equip weapon → check player stats via PlayerStatSystem.GetStat()

#### Task 2.2: Xóa dead commented code trong EquipmentSystem [#6]
- **File:** `EquipmentSystem.cs`
- **Action:** Remove all commented-out `ApplyEquipmentModifiers` / `RemoveEquipmentModifiers` code blocks
- **Risk:** Rất thấp — comments only
- **Test:** N/A (no behavioral change)

#### Task 2.3: Xóa FindNextAvailableInventoryIndex wrappers [#16]
- **Files:** `EquipmentSystem.cs`, `WeaponSystem.UnEquip.cs`
- **Action:** Inline `_inventorySystem.GetNextFreeInventoryIndex()` tại call sites
- **Risk:** Rất thấp
- **Test:** N/A

#### Task 2.4: Fix FireMode PlayerPrefs in hot path [#17]
- **File:** `WeaponSystem.Fire.cs`
- **Action:** Move PlayerPrefs.GetInt to `Awake()`, remove from `GetCurrentFireMode()`
- **Risk:** Thấp
- **Test:** Fire mode persist across equip/unequip

---

### SPRINT 3 — Stat System Consolidation (Xóa ItemStatSystem)

#### Task 3.1: Audit tất cả callers của ItemStatSystem
- **Action:** `grep -r "ItemStatSystem\." --include="*.cs"`
- Xác định: ItemTooltip, AttachmentPanel, UI components gọi `CalculateItemStat()`, `GetAllItemStats()`, `HasStat()`, `InvalidateCache()`

#### Task 3.2: Refactor ItemTooltip dùng instance.GetComputedStat() [#1]
- **File:** `ItemTooltip.cs`
- **Action:** 
  ```csharp
  // Trước:
  float val = ItemStatSystem.CalculateItemStat(item, statType);
  // Sau:
  float val = item.GetComputedStat(statType);
  ```
- **Dependency:** Item phải đã được Compute() trước khi Tooltip show. SAO.Recalculate() đảm bảo điều này khi player equips item. Cho hover tooltip trên items trong inventory (chưa equipped), cần trigger:
  ```csharp
  // Trong InventorySystem.AddItem hoặc ItemDatabase.RegisterInstance:
  ItemStatComputer.Compute(newInstance); // compute on add
  ```

#### Task 3.3: Refactor AttachmentSystem.InvalidateCache [#1]
- **File:** `AttachmentSystem.cs`
- **Action:** 
  ```csharp
  // Trước:
  ItemStatSystem.InvalidateCache(parentID);
  // Sau:
  ItemStatComputer.Compute(parentInstance); // recompute immediately
  _statApplyOrchestrator?.ScheduleRecalc(); // re-apply player modifiers
  ```

#### Task 3.4: Xóa ItemStatSystem.cs [#1]
- **Action:** Delete file sau khi 3.1-3.3 hoàn thành và test pass
- **Risk:** Medium — requires thorough caller audit
- **Test:** ItemTooltip shows correct stats; attachment bonuses reflected in tooltip

---

### SPRINT 4 — Bridge và DI Cleanup

#### Task 4.1: Add IAttachmentSystem vào GameplaySystemsBridge [#4]
- **Files:** `IGameplayBridge.cs`, `GameplaySystemsBridge.cs`, `NetworkPlayer.cs`
- **Action:**
  ```csharp
  // IGameplayBridge.cs:
  IAttachmentSystem Attachment { get; }
  
  // GameplaySystemsBridge.cs — thêm param constructor:
  public GameplaySystemsBridge(IInventorySystem inv, IEquipmentSystem equip,
      IWeaponSystem weapon, IAttachmentSystem attachment, ...)
  
  // NetworkPlayer.Awake():
  var attachment = ComponentResolver.Find<IAttachmentSystem>(this)
      .OnSelf().InChildren().OrLogWarning(...).Resolve();
  bridge = new GameplaySystemsBridge(inv, equip, weapon, attachment, ...);
  ```
- **Then:** Remove `[SerializeField] private AttachmentSystem _attachmentSystemMB` từ SAO; access qua `_bridge.Attachment`

#### Task 4.2: Refactor StatApplyOrchestrator → subscribe qua Bridge [#8, #15]
- **File:** `StatApplyOrchestrator.cs`
- **Action:** 
  - Remove 4 `[SerializeField]` concrete MB refs
  - Inject `IGameplayBridge` (từ NetworkPlayer)
  - Subscribe `bridge.OnItemEquipped`, `bridge.OnItemUnequipped`, etc.
  - `Recalculate()` reads from `bridge.Equipment.GetAllEquippedItems()`, `bridge.Weapon.GetActiveWeapon()`
  - `ItemStatComputer.Compute()` reads via `bridge.Attachment` để get attachment system
- **Risk:** Medium — SAO là central piece
- **Test:** All stat changes (equip, unequip, select weapon, attach) still apply correctly

#### Task 4.3: Remove passthrough wrapper methods từ Bridge [#10]
- **File:** `IGameplayBridge.cs`, `GameplaySystemsBridge.cs`
- **Action:** Remove tất cả pure single-system delegates. Giữ: `AddAndEquip`, `GetWeightPercent`, `GetAllItems`, `GetAllEquipped`, `GetAllWeapons`, `Refresh`
- **Risk:** Medium — cần update tất cả callers (UIDomainBridge, DragDropController, UIContextMenu...)
- **Test:** All UI operations work correctly

---

### SPRINT 5 — Config và các vấn đề còn lại

#### Task 5.1: InventoryConfig Singleton hoặc Static accessor [#11]
- **File:** `InventoryConfig.cs`
- **Action:** Add static `Instance` property, set trong `OnEnable()`
- Remove `[SerializeField] InventoryConfig` từ Equipment/Weapon/Attachment/DragDropController
- **Test:** Config values đúng trong tất cả systems

#### Task 5.2: Fix BatchWeightUpdates [#12]
- **File:** `InventorySystem.cs`
- **Action:** Remove `_pendingWeightUpdate`, implement proper LateUpdate-based dirty flag
- **Test:** Weight updates exactly once per frame khi nhiều items thêm cùng lúc

#### Task 5.3: DragDropController Rollback [#13]
- **Decision point:** Remove optimistic update (LAN-only game, 1-frame lag fine) vs implement ACK
- **Recommended:** Remove optimistic visual update, rely on SyncList.OnChange for truth
- **Alternative (keep optimistic):** Add explicit revert method called from SyncList.OnChange nếu item state không khớp expectation

#### Task 5.4: MagazineSize Rename [#9]
- **File:** `ItemStatType.cs`, all callers
- **Action:** Thêm `MagazineCapacity` enum member; migrate `MagazineSize` usages:
  - Definition-time (max): → `MagazineCapacity`
  - Current instance value: → tiếp tục dùng `MagazineSize` OR thêm extension method `GetCurrentAmmo()`
- **Risk:** High impact — nhiều files. Dùng rename tool.

---

## PHẦN 4 — KIẾN TRÚC SAU REFACTOR

```
NetworkPlayer (Prefab root)
├── PlayerStatSystem        NetworkBehaviour + SyncList<StatData>
├── InventorySystem         NetworkBehaviour + SyncList<ItemInstanceData>
│     └── ItemStatComputer.Compute() called on item add
├── EquipmentSystem         NetworkBehaviour + SyncDictionary
│     └── IAttachmentSystem _attachSystem; (Awake-cached)
│     └── EquipItem()+EquipItemServerRpc (consistent pattern)
├── AttachmentSystem        NetworkBehaviour
├── ItemSelectionSystem     NetworkBehaviour + SyncVar<string>
├── ItemUseSystem           NetworkBehaviour
└── [Child: WeaponSystem]   NetworkBehaviour + SyncDictionary + SyncVar
    └── IAttachmentSystem _attachSystem; (Awake-cached)

GameplaySystemsBridge      Plain C#, created in NetworkPlayer.Awake()
  → Holds refs to ALL 7 systems (includes Attachment)
  → Re-publishes events (keeps as event aggregator)
  → Only non-trivial wrapper methods remain
  → IStatApplyOrchestrator accessible via Bridge

StatApplyOrchestrator      MonoBehaviour, subscribes via Bridge events
  → IGameplayBridge reference (no SerializeField concrete refs)
  → Calls ItemStatComputer.Compute() for all active items
  → Sole manager of item→player stat modifiers

ItemStatComputer           Static utility (unchanged)
  → Sole stat calculation engine
  → instance.GetComputedStat() is UI read path

[DELETED] ItemStatSystem   ← REMOVED

ItemInstanceData.Equals()  Full field comparison including CurrentMagazine, 
                           CurrentResource, InventoryIndex, AttachedItems[]

InventoryConfig            ScriptableObject Singleton (static Instance accessor)
  → Accessed directly, no per-component SerializeField

UI Layer (unchanged architecture):
UIRootController → UIDomainBridge → InventoryScreen → ItemSlotView
DragDropController: optimistic update → removed OR with ACK pattern
InputLayerManager: SSOT for ActionMap (unchanged)
```

---

## PHẦN 5 — CÁC FILE CẦN SỬA/XÓA

### Files cần xóa hoàn toàn:
- [ ] `Scripts/Gameplay/StatSystem/Systems/ItemStatSystem.cs` — sau Sprint 3

### Files cần thay đổi lớn (rewrite một phần):
- [ ] `Scripts/Gameplay/GameplaySystems/Core/Data/ItemInstanceData.cs` — Equals() #3
- [ ] `Scripts/Gameplay/GameplaySystems/Systems/Equipment/EquipmentSystem.cs` — #2, #5(commented cleanup), #7
- [ ] `Scripts/Gameplay/GameplaySystems/Systems/Weapon/WeaponSystem.UnEquip.cs` — #2, #5, #7, #16
- [ ] `Scripts/Gameplay/GameplaySystems/Systems/Weapon/WeaponSystem.Fire.cs` — #17
- [ ] `Scripts/Gameplay/GameplaySystems/Systems/Stat/StatApplyOrchestrator.cs` — #8, #15
- [ ] `Scripts/Gameplay/GameplaySystems/Core/Bridge/GameplaySystemsBridge.cs` — #4, #10
- [ ] `Scripts/Gameplay/GameplaySystems/Core/Bridge/IGameplayBridge.cs` — #4, #10
- [ ] `Scripts/Networking/Player/NetworkPlayer.cs` — #4 (thêm IAttachmentSystem vào DI)

### Files cần thay đổi nhỏ (update callers):
- [ ] `Scripts/Gameplay/GameplaySystems/UI/Inventory/ItemTooltip.cs` — dùng GetComputedStat()
- [ ] `Scripts/Gameplay/GameplaySystems/Systems/Attachment/AttachmentSystem.cs` — xóa ItemStatSystem.InvalidateCache()
- [ ] `Scripts/Gameplay/GameplaySystems/Systems/Inventory/InventorySystem.cs` — batch weight fix
- [ ] `Scripts/Gameplay/StatSystem/Configs/ItemStatConfig.cs` — nếu rename MagazineSize
- [ ] `Scripts/Gameplay/StatSystem/Core/Types/ItemStatType.cs` — nếu rename MagazineSize

### Files cần review (có thể ổn):
- [ ] `DragDropController.cs` — cần quyết định rollback strategy (#13)
- [ ] `WeaponSystem.NetworkSync.cs` — architecture OK, chỉ review comments
- [ ] `PlayerStatSystem.cs` — architecture OK, minor: Update() polling for dirty stats
- [ ] `UIDomainBridge.cs` — event chain OK cho production, có thể optimize tới Sprint 5

---

## PHẦN 6 — CHECKLIST REVIEW TRƯỚC KHI BẮT ĐẦU CODE

Trước mỗi Sprint, đảm bảo:
- [ ] Có test scene với NetworkPlayer spawn đầy đủ (server + client)
- [ ] Có cách verify ammo sync (client HUD hiển thị sau khi bắn)
- [ ] Có cách verify stat changes (UI stat panel update khi equip/unequip)
- [ ] Console không có unexpected NullRef warnings sau mỗi Sprint
- [ ] Không có "Multiple `OnChange` subscriptions" warnings từ FishNet

---

## PHẦN 7 — QUYẾT ĐỊNH KIẾN TRÚC CẦN CONFIRM

1. **Rollback strategy cho DragDropController**: Remove optimistic update vs ACK pattern?
   - **Đề xuất**: Remove optimistic (game là LAN/low-latency) — simpler code, no UI desync risk

2. **MagazineSize rename**: Có đủ thời gian để rename + test toàn bộ callers?
   - **Đề xuất**: Thêm extension methods làm alias trước, rename sau trong separate PR

3. **SAO và Bridge lifecycle**: SAO subscribe qua Bridge hoặc inject qua constructor?
   - **Đề xuất**: Subscribe qua Bridge events — ít coupling hơn

4. **IAttachmentSystem trong Bridge**: Add constructor param hay dùng IGameplayBridge.SetAttachment()?
   - **Đề xuất**: Constructor param (consistent với existing pattern)

5. **InventoryConfig singleton**: Static property trong SO hay ScriptableObjectSingleton base?
   - **Đề xuất**: Simple static `_instance` field set trong `OnEnable()` — không cần base class

---

*Tài liệu này là nguồn chân lý duy nhất cho toàn bộ refactor. Cập nhật checkbox khi hoàn thành mỗi task.*
