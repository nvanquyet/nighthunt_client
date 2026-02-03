# 🔧 FIXES IMPLEMENTATION PLAN

## Priority Fixes (Critical Bugs)

### Fix 1: Container UI tự đóng sau khi move item
**File:** `LootContainerPanel.cs`
**Issue:** `HandleContainerClosed` được trigger khi item thay đổi
**Solution:** 
- Đảm bảo `isRaycastingContainer` được maintain đúng
- Không hide UI khi chỉ có item change, chỉ hide khi mất raycast
- Add check trong `HandleContainerItemsChanged` để không trigger close

### Fix 2: Item về vị trí đầu khi mở lại inventory
**File:** `InventoryPanel.cs`
**Issue:** `localItemPositions` không được persist đúng
**Solution:**
- Đảm bảo `UpdateLocalItemPosition()` được gọi khi move trong inventory
- Fix `RefreshInventoryGrid()` để restore đúng local positions
- Clear local positions chỉ khi item thực sự bị remove (không phải move to container)

### Fix 3: Move inventory → container: mất item UI
**File:** `ContainerNetworkSync.cs`, `LootContainerPanel.cs`
**Issue:** Server sync chưa đúng hoặc UI không refresh
**Solution:**
- Verify `MoveItemToContainerServerRpc` flow
- Đảm bảo `RefreshLootGrid()` được gọi sau khi add item
- Fix UI update timing

### Fix 4: Drop item không spawn
**File:** `ItemDropHandler.cs`, `LootSpawnManager.cs`
**Issue:** Spawn flow có vấn đề
**Solution:**
- Verify `ServerDropItem` → `LootSpawnManager.SpawnItemAtPosition()`
- Check logs để debug
- Ensure NetworkObject spawn correctly

### Fix 5: Hover weapon không hiển thị attachment UI
**File:** `ItemCell.cs`, `InventoryPanel.cs`, `NestedEquipmentPanel.cs`
**Issue:** `ShowNestedEquipmentPanelOnHover()` không được gọi hoặc check sai
**Solution:**
- Verify `OnPointerEnter()` logic
- Fix `HasEquipmentSlots()` check
- Ensure `CreateNestedSlots()` spawns all slots

### Fix 6: Attachment UI không spawn đủ slots
**File:** `NestedEquipmentPanel.cs`
**Issue:** `CreateNestedSlots()` không loop đủ
**Solution:**
- Verify loop qua tất cả `AttachmentSlots[]`
- Load attached items từ `ItemInstance.customData` hoặc `EquipmentHandler`

### Fix 7: Hover empty slot không show tooltip
**File:** `ItemCell.cs`, `ItemTooltip.cs`
**Issue:** Tooltip logic chưa handle empty slots
**Solution:**
- Add tooltip cho empty equipment/weapon/attachment slots
- Show slot type và requirements

### Fix 8: Clean logs
**Files:** All inventory files
**Solution:**
- Remove verbose debug logs
- Keep error/warning logs
- Keep critical flow logs
