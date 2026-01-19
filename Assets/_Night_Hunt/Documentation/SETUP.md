# Night Hunt Game Setup Guide

## Project Setup

### Dependencies Installation

1. **Unity Version**: Unity 2022.3 LTS or later
2. **Required Packages**:
   - FishNet Networking (v3.x)
   - Cinemachine (v2.9+)
   - Unity Input System (v1.7+)
   - FogOfWar Plugin (included in Plugins folder)

### Package Installation Steps

1. **FishNet Networking**:
   - Window > Package Manager > Add package from git URL
   - URL: `https://github.com/FirstGearGames/FishNet.git?path=/FishNet/Runtime`
   - Hoặc import từ Asset Store

2. **Cinemachine**:
   - Window > Package Manager > Unity Registry
   - Search "Cinemachine" và Install

3. **Unity Input System**:
   - Window > Package Manager > Unity Registry
   - Search "Input System" và Install
   - **Important**: Edit > Project Settings > Player > Active Input Handling: "Input System Package (New)"

4. **FogOfWar Plugin**:
   - Đã có trong `Assets/Plugins/FogOfWar/`
   - Import nếu chưa có

### Scene Setup

1. **Main Scene Structure**:
   - NetworkManager GameObject (with NetworkGameManager component)
   - GameplayBootstrap GameObject (with GameplayBootstrap component)
   - MatchPhaseManager GameObject (with MatchPhaseManager component)
   - ScoringSystem GameObject (with ScoringSystem component)
   - PredatorPreySystem GameObject (with PredatorPreySystem component)
   - ZoneSystem GameObject (with ZoneSystem component)
   - LootSpawner GameObject (with LootSpawner component)
   - AntiCampingSystem GameObject (with AntiCampingSystem component)
   - VisionSystem GameObject (with VisionSystem component)
   - InputLayerManager GameObject (with InputLayerManager component)
   - GameplayEventBus GameObject (with GameplayEventBus component)
   - ClientEffectManager GameObject (with ClientEffectManager component)
   - FogOfWarWorld GameObject (with FogOfWarWorld component)
   - Main Camera với CinemachineBrain
   - Player Spawn Points
   - Loot Spawn Points

2. **Player Prefab Setup**:
   - NetworkObject (FishNet)
   - NetworkPlayer component
   - CharacterController
   - CharacterStats component
   - CharacterStatsSync component
   - CharacterMovement component
   - CharacterCombat component
   - PlayerInputHandler component
   - InventorySystem component
   - VisionRevealer component
   - FogOfWarRevealer3D component
   - CinemachineCameraController component (trong CameraRig child)
   - CameraRotationInput component
   - CameraZoomInput component

### Network Setup

1. **NetworkManager Configuration**:
   - Set server port (default: 7770)
   - Configure max connections
   - Set spawn points

2. **Network Prefabs**:
   - Register player prefab in NetworkManager
   - Register weapon prefabs
   - Register projectile prefabs

### Config Setup

1. **Game Config File**:
   - Location: `StreamingAssets/NightHunt_Full_GameDesign_Config_v3.json`
   - Contains: Weapons, Items, Characters, Status Effects, Phases, Zones, Inventory, Stamina/Weight configs

2. **Input Actions**:
   - Location: `Assets/_Night_Hunt/InputSystem_Actions.inputactions`
   - Action Maps: Player, UI, Camera, Spectator, Gameplay

### Build Configuration

1. **Server Build**:
   - Headless mode enabled
   - Dedicated server build

2. **Client Build**:
   - Full graphics
   - Input system enabled

### Testing Setup

1. **Local Testing (Single Instance - Host)**:
   - Mở TestGameplayScene
   - NetworkManager Settings:
     - Start On Server: ✓
     - Start On Client: ✓
   - Press Play
   - Game chạy như local host

2. **Local Testing (Multi Instance)**:
   - Instance 1: Start server (Start On Server: ✓, Start On Client: ✗)
   - Instance 2: Start client (Start On Server: ✗, Start On Client: ✓)
   - Client connect đến `127.0.0.1:7770`

3. **Multiplayer Testing**:
   - Build server executable (headless)
   - Build client executable
   - Run server executable
   - Connect clients to server IP

**Chi tiết**: Xem `LOCAL_TESTING_SETUP.md` để hướng dẫn đầy đủ

## Component Setup

### Player Prefab Components

Required components on player prefab:
- NetworkPlayer
- CharacterController
- CharacterStats
- CharacterStatsSync
- CharacterMovement
- CharacterCombat
- PlayerInputHandler
- VisionRevealer
- FogOfWarRevealer3D
- CinemachineCameraController
- InputLayerManager (singleton, can be in scene)

### Weapon Prefab Components

Required components on weapon prefab:
- WeaponBase (or HitscanWeapon/ProjectileWeapon)
- ProjectileSpawner (for projectile weapons)
- ProjectileSync (for network sync)
- Collider (for projectile collision)

### Camera Setup

1. **Main Camera**:
   - CinemachineBrain component
   - CinemachineVirtualCamera (third-person with 45 degree angle)

2. **Spectator Camera**:
   - SpectatorCameraController component
   - SpectatorInputHandler component

## Quick Start Checklist

### Before Testing
- [ ] Tất cả packages đã được install
- [ ] Config file tồn tại trong StreamingAssets
- [ ] Input Actions asset đã được tạo và assigned
- [ ] Player prefab đã được setup đầy đủ components
- [ ] Scene có đầy đủ GameObjects (xem Scene Setup)
- [ ] NetworkManager đã được configure
- [ ] Spawn points đã được đặt trong scene

### Testing Steps
1. [ ] Mở TestGameplayScene
2. [ ] Press Play
3. [ ] Kiểm tra GameplayBootstrap initialize thành công
4. [ ] Kiểm tra player spawn
5. [ ] Kiểm tra input hoạt động
6. [ ] Kiểm tra camera follow
7. [ ] Kiểm tra network sync (nếu có 2 instances)

## Troubleshooting

### Common Issues

1. **Input not working**:
   - Check InputLayerManager is initialized
   - Verify InputActionAsset is assigned
   - Check PlayerInputHandler is enabled
   - Verify InputState is set correctly (should be Player)
   - Check Input System Package is set as Active Input Handling

2. **Network sync issues**:
   - Verify NetworkManager is started
   - Check NetworkPlayer ownership
   - Verify SyncVars are properly initialized
   - Check server is running (nếu test multi-instance)
   - Verify NetworkObject is spawned correctly

3. **Camera not following**:
   - Check CinemachineBrain is on main camera
   - Verify CinemachineVirtualCamera is assigned
   - Check follow target is set
   - Verify camera is enabled
   - Check camera priority settings

4. **Vision not working**:
   - Verify FogOfWarWorld is in scene
   - Check VisionRevealer component is on player
   - Verify FogOfWarRevealer3D is configured
   - Check vision radius settings

5. **Systems not initializing**:
   - Check GameplayBootstrap is in scene
   - Verify all required GameObjects exist
   - Check console for errors
   - Verify config files are loaded correctly
   - Check GameplayInitializer is on server

6. **Player not spawning**:
   - Verify NetworkManager has player prefab registered
   - Check spawn points exist in scene
   - Verify NetworkObject is properly configured
   - Check server is running

## Additional Resources

- **Local Testing Guide**: `LOCAL_TESTING_SETUP.md`
- **Implementation Summary**: `GAMEPLAY_IMPLEMENTATION_SUMMARY.md`
- **API Documentation**: (Coming soon)

