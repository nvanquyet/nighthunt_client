# Setup Tools Documentation

## Tổng quan
Các Editor tools tự động hóa việc setup items, UI components và test scenes từ config data.

## Tools Available

### 1. Item & UI Setup Tool
**Menu:** `Night Hunt > Setup Tools > Item & UI Setup Tool`

**Chức năng:**
- Tự động tạo loot prefabs từ item configs
- Tạo UI component prefabs (InventoryUI, EquipmentUI, DropAmountSelector)
- Tạo test scene với items spawn sẵn

**Cách sử dụng:**
1. Mở tool từ menu
2. Chọn options:
   - `Generate Loot Prefabs`: Tạo prefabs cho các loot items
   - `Generate UI Components`: Tạo UI prefabs
   - `Create Test Scene`: Tạo test scene
3. Click `Generate All` hoặc từng button riêng lẻ

**Output:**
- Loot prefabs: `Assets/_Night_Hunt/Prefabs/Generated/Loot_[ItemId].prefab`
- UI prefabs: `Assets/_Night_Hunt/Prefabs/UI/[ComponentName].prefab`
- Test scene: `Assets/_Night_Hunt/Scenes/Test_ItemSetup.unity`

### 2. Loot Spawner Setup Tool
**Menu:** `Night Hunt > Setup Tools > Loot Spawner Setup`

**Chức năng:**
- Setup LootSpawner với spawn points từ scene
- Tạo grid spawn points tự động

**Cách sử dụng:**
1. Option 1: Chọn các GameObjects trong scene làm spawn points
   - Check `Use Selected Objects`
   - Select các objects trong Hierarchy
   - Click `Setup LootSpawner`

2. Option 2: Dùng parent object chứa spawn points
   - Assign parent object vào `Spawn Points Parent`
   - Click `Setup LootSpawner`

3. Tạo grid spawn points:
   - Click `Create Spawn Points Grid`
   - Tool sẽ tạo 5x5 grid với spacing 3m

### 3. Item Config Validator
**Menu:** `Night Hunt > Setup Tools > Validate Item Configs`

**Chức năng:**
- Validate tất cả item configs
- Check duplicate IDs, missing fields, invalid values
- Hiển thị errors và warnings

**Cách sử dụng:**
1. Mở tool từ menu
2. Click `Validate All Configs`
3. Xem errors/warnings trong window

**Checks:**
- Duplicate ItemId
- Empty DisplayName
- Negative weight
- Invalid MaxStack
- Invalid UseType/EffectType enums
- Conversion errors

### 4. Quick Setup Tool
**Menu:** `Night Hunt > Quick Setup > Setup Everything for Testing`

**Chức năng:**
- Tự động setup tất cả để test ngay
- Tạo test scene với đầy đủ components
- Setup UI sẵn sàng

**Cách sử dụng:**
1. Click `Setup Everything for Testing`
2. Tool sẽ tự động:
   - Tạo GameConfigLoader nếu chưa có
   - Tạo test scene với ground, lighting
   - Setup InventoryUI, EquipmentUI, DropAmountSelector
   - Tạo LootSpawner với spawn points
   - Spawn test loot items từ config
   - Save scene

**Output:**
- Test scene: `Assets/_Night_Hunt/Scenes/Test_QuickSetup.unity`

## Workflow Khuyến nghị

### Lần đầu setup:
1. **Quick Setup**: Dùng `Quick Setup > Setup Everything for Testing` để tạo test scene nhanh
2. **Validate**: Dùng `Item Config Validator` để check configs
3. **Generate Prefabs**: Dùng `Item & UI Setup Tool` để tạo prefabs từ configs

### Khi thêm item mới:
1. Thêm item vào config JSON
2. Dùng `Item Config Validator` để validate
3. Dùng `Item & UI Setup Tool > Generate Loot Prefabs Only` để tạo prefab mới

### Khi setup spawn points:
1. Tạo spawn points trong scene (hoặc dùng `Create Spawn Points Grid`)
2. Dùng `Loot Spawner Setup` để link với LootSpawner

## Requirements

### Layers cần có:
- `InteractableLoot` - Layer cho loot items (nếu không có sẽ warning)

### Tags cần có:
- `Loot` - Tag cho loot items

### Dependencies:
- GameConfigLoader phải được load trước khi generate prefabs
- TextMeshPro phải được import (cho UI components)

## Troubleshooting

### "GameConfigLoader.Instance is null"
- Đảm bảo GameConfigLoader đã được setup trong scene
- Hoặc dùng Quick Setup Tool để tự động tạo

### "Layer 'InteractableLoot' not found"
- Tạo layer mới trong Project Settings > Tags and Layers
- Hoặc ignore warning (không ảnh hưởng functionality)

### "TextMeshPro font not found"
- Import TextMeshPro package nếu chưa có
- Tool sẽ dùng default font nếu không tìm thấy

### Prefabs không có references
- Một số references cần setup manual sau khi generate
- Check Inspector của prefab và assign missing references

## Tips

1. **Test Scene**: Luôn dùng Quick Setup để tạo test scene mới, không edit scene có sẵn
2. **Prefabs**: Sau khi generate, check prefabs trong Inspector để đảm bảo references đúng
3. **Validation**: Luôn validate configs trước khi generate prefabs
4. **Spawn Points**: Dùng grid tool để tạo spawn points nhanh, sau đó adjust manual

