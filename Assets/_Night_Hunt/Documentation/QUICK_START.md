# Night Hunt - Quick Start Guide

## Mục đích

Hướng dẫn nhanh để bắt đầu test game locally trong 5 phút.

## Bước 1: Kiểm tra Dependencies

### Unity Packages
- [ ] FishNet Networking đã được install
- [ ] Cinemachine đã được install  
- [ ] Unity Input System đã được install
- [ ] FogOfWar Plugin có trong Plugins folder

**Kiểm tra:**
- Window > Package Manager > Kiểm tra các packages đã install

### Project Settings
- [ ] Edit > Project Settings > Player > Active Input Handling: "Input System Package (New)"

## Bước 2: Tạo Test Scene (Tự động)

### Option 1: Sử dụng Editor Tool (Khuyến nghị)
1. Menu: **Night Hunt > Setup > Create Gameplay Test Scene**
2. Scene sẽ được tạo tự động với tất cả GameObjects cần thiết
3. Lưu scene: `Assets/Scenes/TestGameplayScene.unity`

### Option 2: Tạo Manual
Xem `LOCAL_TESTING_SETUP.md` để hướng dẫn chi tiết.

## Bước 3: Setup NetworkManager

1. Chọn **NetworkManager** GameObject trong scene
2. Add component **NetworkManager** (FishNet)
3. Add component **NetworkGameManager** (script)
4. Assign NetworkManager reference
5. Configure:
   - Port: 7777
   - Max Connections: 16

## Bước 4: Setup Player Prefab

1. Tạo Player prefab với các components (xem `SETUP.md`)
2. Register prefab trong NetworkManager:
   - NetworkManager > Spawnable Prefabs > Add Player prefab

## Bước 5: Setup Input Actions

1. Tạo hoặc mở `InputSystem_Actions.inputactions`
2. Tạo các Action Maps (xem `SETUP.md`)
3. Assign vào InputLayerManager:
   - Chọn InputLayerManager GameObject
   - Assign Input Action Asset

## Bước 6: Test Local (Single Instance)

1. Mở TestGameplayScene
2. Chọn NetworkManager
3. NetworkGameManager Settings:
   - Start On Server: ✓
   - Start On Client: ✓
4. **Press Play**
5. Game sẽ chạy như local host

### Kiểm tra:
- [ ] Console không có errors
- [ ] Player spawn thành công
- [ ] WASD movement hoạt động
- [ ] Mouse look hoạt động
- [ ] Camera follow player
- [ ] Q/E rotate camera hoạt động
- [ ] Mouse wheel zoom hoạt động

## Bước 7: Test Multi-Instance (Optional)

### Instance 1 - Server
1. Mở TestGameplayScene
2. NetworkGameManager Settings:
   - Start On Server: ✓
   - Start On Client: ✗
3. Press Play
4. Server start và listen trên port 7777

### Instance 2 - Client
1. Mở Unity Editor instance thứ 2 (hoặc Build)
2. Mở TestGameplayScene
3. NetworkGameManager Settings:
   - Start On Server: ✗
   - Start On Client: ✓
   - Server Address: `127.0.0.1`
   - Port: `7777`
4. Press Play
5. Client connect đến server

### Kiểm tra:
- [ ] Client connect thành công
- [ ] 2 players spawn trong scene
- [ ] Network sync hoạt động
- [ ] Input chỉ hoạt động cho local player

## Troubleshooting Nhanh

### Input không hoạt động
1. Check InputLayerManager có Input Action Asset assigned
2. Check PlayerInputHandler enabled
3. Check InputState = Player

### Player không spawn
1. Check NetworkManager có player prefab registered
2. Check spawn points trong scene
3. Check NetworkObject component

### Camera không follow
1. Check CinemachineBrain trên MainCamera
2. Check CinemachineVirtualCamera assigned
3. Check follow target set

### Systems không initialize
1. Check GameplayBootstrap trong scene
2. Check console for errors
3. Check tất cả GameObjects tồn tại

## Next Steps

Sau khi test local thành công:
1. Đọc `LOCAL_TESTING_SETUP.md` để test chi tiết từng system
2. Đọc `GAMEPLAY_IMPLEMENTATION_SUMMARY.md` để hiểu architecture
3. Test với dedicated server build
4. Performance testing và optimization

## Files Quan Trọng

- **SETUP.md**: Setup guide đầy đủ
- **LOCAL_TESTING_SETUP.md**: Hướng dẫn test local chi tiết
- **GAMEPLAY_IMPLEMENTATION_SUMMARY.md**: Tổng quan implementation
- **QUICK_START.md**: File này

## Support

Nếu gặp vấn đề:
1. Check console logs
2. Đọc troubleshooting section trong SETUP.md
3. Kiểm tra tất cả dependencies đã install
4. Verify config files tồn tại

