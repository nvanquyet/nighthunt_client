# 📋 Hướng Dẫn Setup Inventory System - Chi Tiết

## 🎯 Mục tiêu
Setup đầy đủ inventory system để có thể test ngay trong Unity Editor.

---

## 📦 BƯỚC 1: Setup PlayerInventoryCache (Global)

### 1.1 Tạo GameObject riêng cho Cache
1. Trong Hierarchy, tạo GameObject mới (không phải child của player)
2. Đặt tên: **"PlayerInventoryCache"**
3. Vị trí: Root level của scene (không phải child của bất kỳ object nào)

### 1.2 Attach Component
1. Select GameObject "PlayerInventoryCache"
2. Add Component → `PlayerInventoryCache` (namespace: `NightHunt.Inventory.Core`)
3. Component sẽ tự động là Singleton

### 1.3 Setup DontDestroyOnLoad (Optional)
- Nếu muốn cache persist qua scenes, thêm script:
```csharp
// Hoặc đơn giản check "Dont Destroy On Load" trong Inspector
```

---

## 👤 BƯỚC 2: Setup NetworkPlayer Prefab

### 2.1 Cấu trúc Hierarchy của Player

```
NetworkPlayer (Root)
├── Model (hoặc các child khác)
├── InventorySystem (Child GameObject - MỚI TẠO)
│   ├── InventoryManager
│   ├── InventoryNetworkSync
│   ├── InventoryNetworkClient
│   └── InventoryOperationValidator
├── EquipmentSystem (Child GameObject - MỚI TẠO)
│   └── EquipmentManager
├── WeaponSystem (Child GameObject - MỚI TẠO)
│   └── WeaponManager
├── QuickSlotSystem (Child GameObject - MỚI TẠO)
│   ├── QuickSlotManager
│   ├── QuickSlotInputHandler
│   └── ConsumableUsage
└── PlayerInventoryCacheInitializer (Component trên NetworkPlayer root)
```

### 2.2 Tạo Child Objects

#### 2.2.1 InventorySystem Child
1. Right-click trên NetworkPlayer → Create Empty
2. Đặt tên: **"InventorySystem"**
3. Attach các components:
   - `InventoryManager` (namespace: `NightHunt.Inventory.Domain`)
   - `InventoryNetworkSync` (namespace: `NightHunt.Inventory.Networking`)
   - `InventoryNetworkClient` (namespace: `NightHunt.Inventory.Networking`)
   - `InventoryOperationValidator` (namespace: `NightHunt.Inventory.Domain`)

**Config InventoryManager:**
- Max Slots: `20` (hoặc số bạn muốn)

**Config InventoryNetworkSync:**
- Network Sync Config: Tạo ScriptableObject mới (xem Bước 4)

**Config InventoryNetworkClient:**
- Không cần config

**Config InventoryOperationValidator:**
- Không cần config

#### 2.2.2 EquipmentSystem Child
1. Right-click trên NetworkPlayer → Create Empty
2. Đặt tên: **"EquipmentSystem"**
3. Attach component:
   - `EquipmentManager` (namespace: `NightHunt.Inventory.Domain`)

**Config EquipmentManager:**
- Equipment Slots Config: Tạo ScriptableObject mới (xem Bước 4)

#### 2.2.3 WeaponSystem Child
1. Right-click trên NetworkPlayer → Create Empty
2. Đặt tên: **"WeaponSystem"**
3. Attach component:
   - `WeaponManager` (namespace: `NightHunt.Inventory.Domain`)

**Config WeaponManager:**
- Không cần config

#### 2.2.4 QuickSlotSystem Child
1. Right-click trên NetworkPlayer → Create Empty
2. Đặt tên: **"QuickSlotSystem"**
3. Attach các components:
   - `QuickSlotManager` (namespace: `NightHunt.Inventory.QuickSlot`)
   - `QuickSlotInputHandler` (namespace: `NightHunt.Inventory.QuickSlot`)
   - `ConsumableUsage` (namespace: `NightHunt.Inventory.QuickSlot`)

**Config QuickSlotManager:**
- Quick Slot Config: Tạo ScriptableObject mới (xem Bước 4)

**Config QuickSlotInputHandler:**
- Quick Slot Config: Reference đến config ở trên

**Config ConsumableUsage:**
- Consume Duration: `2.0` (seconds)
- Progress Bar UI: Assign từ Canvas (xem Bước 3)

### 2.3 Attach PlayerInventoryCacheInitializer
1. Select NetworkPlayer (root)
2. Add Component → `PlayerInventoryCacheInitializer` (namespace: `NightHunt.Inventory.Core`)
3. Component sẽ tự động cache player khi spawn

---

## 🖼️ BƯỚC 3: Setup Canvas (UI riêng ngoài Player)

### 3.1 Tạo Canvas riêng
1. Trong Hierarchy, tạo Canvas mới (không phải child của player)
2. Đặt tên: **"InventoryCanvas"**
3. Setup Canvas:
   - Render Mode: `Screen Space - Overlay` (hoặc `Screen Space - Camera`)
   - Canvas Scaler: `Scale With Screen Size`
   - Reference Resolution: `1920 x 1080`

### 3.2 Tạo Inventory Panel Structure

```
InventoryCanvas
└── InventoryPanel (GameObject)
    ├── Background (Image - Optional)
    ├── Header (GameObject)
    │   ├── Title (TextMeshPro - "Inventory")
    │   ├── SortButton (Button)
    │   └── AutoStackButton (Button)
    ├── InventoryContent (GameObject với ScrollRect)
    │   ├── Viewport (GameObject)
    │   │   └── Content (GameObject với Vertical Layout Group)
    │   │       └── [InventoryCellUI sẽ spawn ở đây]
    │   └── Scrollbar (Optional)
    ├── EquipmentPanel (GameObject - Optional)
    ├── WeaponPanel (GameObject - Optional)
    ├── QuickSlotPanel (GameObject - Optional)
    ├── TrashSlot (GameObject với TrashSlotUI)
    ├── TooltipPanel (GameObject với TooltipController)
    ├── DragGhost (GameObject với DragGhostVisual)
    └── ProgressBar (GameObject với ProgressBarUI)
```

### 3.3 Setup InventoryUIController
1. Select GameObject "InventoryPanel"
2. Add Component → `InventoryUIController` (namespace: `NightHunt.Inventory.UI`)
3. Config:
   - **Inventory Panel**: Reference đến GameObject "InventoryPanel" (self)
   - **Inventory Content Parent**: Reference đến "Content" GameObject
   - **Inventory Cell Prefab**: Tạo prefab (xem 3.4)
   - **Sort Button**: Reference đến SortButton
   - **Auto Stack Button**: Reference đến AutoStackButton

### 3.4 Tạo InventoryCellUI Prefab
1. Tạo GameObject mới: **"InventoryCellPrefab"**
2. Setup:
   - Add Component: `Image` (background)
   - Add Component: `Button` (optional, for click)
   - Add Component: `InventoryCellUI` (namespace: `NightHunt.Inventory.UI`)
3. Tạo child objects:
   ```
   InventoryCellPrefab
   ├── IconImage (Image - child)
   ├── StackSizeText (TextMeshPro - child)
   └── DurabilityBar (Image - child, type: Filled)
   ```
4. Config InventoryCellUI:
   - **Icon Image**: Reference đến IconImage
   - **Background Image**: Reference đến root Image
   - **Stack Size Text**: Reference đến StackSizeText
   - **Durability Bar**: Reference đến DurabilityBar
5. Save as Prefab: `Assets/_Night_Hunt/Prefabs/UI/InventoryCellPrefab.prefab`

### 3.5 Setup TooltipController
1. Select GameObject "TooltipPanel"
2. Add Component → `TooltipController` (namespace: `NightHunt.Inventory.UI`)
3. Tạo child structure:
   ```
   TooltipPanel
   ├── Background (Image)
   ├── ItemNameText (TextMeshPro)
   └── StatsText (TextMeshPro)
   ```
4. Config TooltipController:
   - **Tooltip Panel**: Reference đến TooltipPanel (self)
   - **Item Name Text**: Reference đến ItemNameText
   - **Stats Text**: Reference đến StatsText
   - **Tooltip Rect**: Reference đến RectTransform của TooltipPanel
   - **Offset**: `(10, 0)` (adjust nếu cần)
5. Add Component → `TooltipHoverDetector` (namespace: `NightHunt.Inventory.UI`) vào TooltipPanel

### 3.6 Setup DragGhostVisual
1. Select GameObject "DragGhost"
2. Add Component → `Image` (transparent background)
3. Add Component → `CanvasGroup`
4. Add Component → `DragGhostVisual` (namespace: `NightHunt.Inventory.UI`)
5. Config:
   - **Ghost Image**: Reference đến Image component
   - **Canvas Group**: Reference đến CanvasGroup
   - **Ghost Alpha**: `0.6`

### 3.7 Setup ProgressBarUI
1. Select GameObject "ProgressBar"
2. Add Component → `Image` (background)
3. Tạo child: **"Fill"** (Image, type: Filled)
4. Add Component → `ProgressBarUI` (namespace: `NightHunt.Inventory.UI`)
5. Config:
   - **Progress Bar Root**: Reference đến ProgressBar (self)
   - **Progress Bar Fill**: Reference đến Fill Image

### 3.8 Setup TrashSlotUI
1. Select GameObject "TrashSlot"
2. Add Component → `Image` (trash icon)
3. Add Component → `TrashSlotUI` (namespace: `NightHunt.Inventory.UI`)
4. Config:
   - **Trash Icon**: Reference đến Image
   - **Normal Color**: White
   - **Hover Color**: Red

### 3.9 Setup DragDropController
1. Select GameObject "InventoryPanel" (hoặc Canvas root)
2. Add Component → `DragDropController` (namespace: `NightHunt.Inventory.UI`)
3. Không cần config

---

## ⚙️ BƯỚC 4: Tạo ScriptableObject Configs

### 4.1 NetworkSyncConfig
1. Right-click trong Project → Create → Inventory → NetworkSyncConfig
2. Đặt tên: `NetworkSyncConfig_Default`
3. Config:
   - Use Delta Sync: `true`
   - Full Sync Interval: `5`
   - Use Compression: `false` (để test, bật sau)

### 4.2 EquipmentSlotsConfig
1. Right-click trong Project → Create → Inventory → EquipmentConfig
2. Đặt tên: `EquipmentSlotsConfig_Default`
3. Config Slots array:
   - Size: `3`
   - Element 0:
     - Slot Type: `Helmet`
     - Slot Name: "Helmet"
     - Slot Description: "Protects your head"
   - Element 1:
     - Slot Type: `Armor`
     - Slot Name: "Armor"
     - Slot Description: "Protects your body"
   - Element 2:
     - Slot Type: `Backpack`
     - Slot Name: "Backpack"
     - Slot Description: "Increases inventory capacity"

### 4.3 QuickSlotConfig
1. Right-click trong Project → Create → Inventory → QuickSlotConfig
2. Đặt tên: `QuickSlotConfig_Default`
3. Config:
   - Slot Count: `4`
   - Bindings array:
     - Size: `4`
     - Element 0: Slot Index `0`, Display Key `"Ctrl+1"`
     - Element 1: Slot Index `1`, Display Key `"Ctrl+2"`
     - Element 2: Slot Index `2`, Display Key `"Ctrl+3"`
     - Element 3: Slot Index `3`, Display Key `"Ctrl+4"`

### 4.4 WeightPenaltyConfig (Optional)
1. Right-click trong Project → Create → Inventory → WeightPenaltyConfig
2. Đặt tên: `WeightPenaltyConfig_Default`
3. Config:
   - Normal Capacity Percent: `100`
   - Max Capacity Percent: `150`
   - Speed Curve: Linear từ (0,1) đến (150,0)
   - Stamina Drain Multiplier: `2.0`
   - Stamina Regen Multiplier: `0.5`
   - Can Sprint When Overweight: `false`

### 4.5 ModifierSystemConfig (Optional)
1. Right-click trong Project → Create → Inventory → ModifierSystemConfig
2. Đặt tên: `ModifierSystemConfig_Default`
3. Config:
   - Calculation Type: `FlatAddition` (hoặc `PercentMultiplier`)

---

## 🎮 BƯỚC 5: Tạo Test ItemDefinitions

### 5.1 Tạo ItemDefinition cho Weapon
1. Right-click trong Project → Create → Inventory → ItemDefinition
2. Đặt tên: `Item_AK47`
3. Config:
   - Item ID: `"weapon_ak47"`
   - Item Type: `Weapon`
   - Icon: Assign sprite
   - Weight: `4.5`
   - Is Stackable: `false`
   - Max Stack Size: `1`
   - Allowed Slot Locations: `[Inventory, Weapon]`
   - Equipment Slot: (không dùng cho weapon)
   - Attachment Slots: `[Scope, Grip, Muzzle, Magazine]`
   - Max Durability: `100`

### 5.2 Tạo ItemDefinition cho Consumable
1. Right-click trong Project → Create → Inventory → ItemDefinition
2. Đặt tên: `Item_Medkit`
3. Config:
   - Item ID: `"consumable_medkit"`
   - Item Type: `Consumable`
   - Icon: Assign sprite
   - Weight: `0.5`
   - Is Stackable: `true`
   - Max Stack Size: `5`
   - Allowed Slot Locations: `[Inventory, QuickSlot]`
   - Max Durability: `0` (không có durability)

### 5.3 Tạo ItemDefinition cho Equipment
1. Right-click trong Project → Create → Inventory → ItemDefinition
2. Đặt tên: `Item_Backpack`
3. Config:
   - Item ID: `"equipment_backpack"`
   - Item Type: `Armor`
   - Icon: Assign sprite
   - Weight: `2.0`
   - Is Stackable: `false`
   - Allowed Slot Locations: `[Inventory, Equipment]`
   - Equipment Slot: `Backpack`
   - Character Stat Modifiers: Add modifier cho `WeightCapacity` với value `20`

---

## 🔗 BƯỚC 6: Link References

### 6.1 Link Configs vào Managers
1. **InventoryNetworkSync**:
   - Network Sync Config → `NetworkSyncConfig_Default`

2. **EquipmentManager**:
   - Equipment Slots Config → `EquipmentSlotsConfig_Default`

3. **QuickSlotManager**:
   - Quick Slot Config → `QuickSlotConfig_Default`

4. **QuickSlotInputHandler**:
   - Quick Slot Config → `QuickSlotConfig_Default`

### 6.2 Link UI References
1. **InventoryUIController**:
   - Inventory Panel → InventoryPanel GameObject
   - Inventory Content Parent → Content GameObject
   - Inventory Cell Prefab → InventoryCellPrefab
   - Sort Button → SortButton
   - Auto Stack Button → AutoStackButton

2. **ConsumableUsage**:
   - Progress Bar UI → ProgressBar GameObject

---

## ✅ BƯỚC 7: Test Setup

### 7.1 Test PlayerInventoryCache
1. Play scene
2. Check Console:
   - Should see: `[PlayerInventoryCache] Cached player: [PlayerName]`
3. Verify cache:
   - `PlayerInventoryCache.Instance.IsCacheValid()` should return `true`

### 7.2 Test Inventory UI
1. Press Tab (hoặc key bạn setup) để mở inventory
2. Check:
   - Inventory panel opens
   - No errors in console
   - UI elements visible

### 7.3 Test Add Item (Code)
```csharp
// Trong test script hoặc console:
var cache = PlayerInventoryCache.Instance;
if (cache != null && cache.IsCacheValid())
{
    var itemDef = Resources.Load<ItemDefinition>("Items/Item_AK47");
    var itemInstance = new ItemInstance
    {
        InstanceId = System.Guid.NewGuid().ToString(),
        Definition = itemDef,
        StackSize = 1,
        CurrentDurability = 100f
    };
    
    cache.InventoryManager.TryAddItem(itemInstance);
}
```

### 7.4 Test QuickSlot
1. Add item vào quickslot (via drag & drop hoặc code)
2. Press `Ctrl+1` (hoặc Ctrl+2/3/4)
3. Check:
   - Consumable: Progress bar appears
   - Throwable: Event fires

---

## 🐛 Troubleshooting

### Lỗi: "PlayerInventoryCache.Instance is null"
- **Giải pháp**: Đảm bảo GameObject "PlayerInventoryCache" tồn tại trong scene và có component `PlayerInventoryCache`

### Lỗi: "InventoryManager not found"
- **Giải pháp**: 
  - Check InventorySystem child object tồn tại
  - Check InventoryManager component được attach
  - Check PlayerInventoryCacheInitializer được attach trên NetworkPlayer

### Lỗi: "UI không hiển thị"
- **Giải pháp**:
  - Check Canvas được setup đúng
  - Check InventoryUIController references đầy đủ
  - Check InventoryPanel GameObject active

### Lỗi: "Component not found in hierarchy"
- **Giải pháp**:
  - Đảm bảo components nằm trong hierarchy của NetworkPlayer
  - Check ComponentFinder.FindInHierarchy đang tìm đúng GameObject

---

## 📝 Checklist Setup

- [ ] PlayerInventoryCache GameObject created
- [ ] PlayerInventoryCache component attached
- [ ] NetworkPlayer prefab có child objects:
  - [ ] InventorySystem với 4 components
  - [ ] EquipmentSystem với EquipmentManager
  - [ ] WeaponSystem với WeaponManager
  - [ ] QuickSlotSystem với 3 components
- [ ] PlayerInventoryCacheInitializer trên NetworkPlayer root
- [ ] Canvas riêng được tạo
- [ ] InventoryPanel structure hoàn chỉnh
- [ ] InventoryCellUI prefab created
- [ ] TooltipController setup
- [ ] DragGhostVisual setup
- [ ] ProgressBarUI setup
- [ ] TrashSlotUI setup
- [ ] Tất cả ScriptableObject configs created
- [ ] Test ItemDefinitions created
- [ ] Tất cả references linked
- [ ] Test thành công

---

## 🎯 Quick Start Script

Tạo script này để test nhanh:

```csharp
using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Domain;

public class InventoryTestScript : MonoBehaviour
{
    [SerializeField] private ItemDefinition testItem;
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestAddItem();
        }
    }
    
    void TestAddItem()
    {
        var cache = PlayerInventoryCache.Instance;
        if (cache == null || !cache.IsCacheValid())
        {
            Debug.LogError("PlayerInventoryCache not valid!");
            return;
        }
        
        if (testItem == null)
        {
            Debug.LogError("Test item not assigned!");
            return;
        }
        
        var itemInstance = new ItemInstance
        {
            InstanceId = System.Guid.NewGuid().ToString(),
            Definition = testItem,
            StackSize = 1,
            CurrentDurability = testItem.MaxDurability
        };
        
        bool success = cache.InventoryManager.TryAddItem(itemInstance);
        Debug.Log($"Add item result: {success}");
    }
}
```

Attach script này vào bất kỳ GameObject nào trong scene để test!

---

**Chúc bạn setup thành công! 🎉**
