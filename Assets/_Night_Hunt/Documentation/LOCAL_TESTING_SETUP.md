# Night Hunt - Local Testing Setup Guide

## Mục đích

Hướng dẫn chi tiết để setup và test game locally với tất cả gameplay systems.

## Yêu cầu

### Unity Setup
- Unity 2022.3 LTS hoặc mới hơn
- Packages đã cài đặt:
  - FishNet Networking (v3.x)
  - Cinemachine (v2.9+)
  - Unity Input System (v1.7+)
  - FogOfWar Plugin (trong Plugins folder)

### Project Structure
```
NightHuntClient/
├── Assets/
│   ├── _Night_Hunt/
│   │   ├── Scripts/
│   │   │   └── Gameplay/        # Tất cả gameplay systems
│   │   ├── Documentation/       # Documentation files
│   │   └── StreamingAssets/     # Config files
│   └── Plugins/                 # FogOfWar plugin
```

## Scene Setup

### 1. Tạo Test Scene

1. Tạo scene mới: `Scenes/TestGameplayScene`
2. Thêm các GameObjects sau:

#### NetworkManager GameObject
```
NetworkManager
├── NetworkManager (FishNet component)
├── NetworkGameManager (script)
└── ServerGameManager (script)
```

**NetworkManager Settings:**
- Transport: Tugboat hoặc KCP
- Server Port: 7770
- Max Connections: 16
- Spawnable Prefabs: Add Player prefab

#### GameplayBootstrap GameObject
```
GameplayBootstrap
└── GameplayBootstrap (script)
```

**GameplayBootstrap Settings:**
- Auto Initialize: ✓
- Initialize On Start: ✓
- Assign references (hoặc để null để auto-find)

#### MatchPhaseManager GameObject
```
MatchPhaseManager
└── MatchPhaseManager (script)
```

**MatchPhaseManager Settings:**
- Phase Duration: 300s (5 minutes)
- Auto Start: ✓

#### ScoringSystem GameObject
```
ScoringSystem
└── ScoringSystem (script)
```

#### PredatorPreySystem GameObject
```
PredatorPreySystem
├── PredatorPreySystem (script)
├── RevealSystem (script)
└── RadarPingSystem (script)
```

#### ZoneSystem GameObject
```
ZoneSystem
└── ZoneSystem (script)
```

#### LootSpawner GameObject
```
LootSpawner
└── LootSpawner (script)
```

**LootSpawner Settings:**
- Spawn Interval: 30s
- Max Loot Per Spawn: 3
- Add LootSpawnPoint GameObjects trong scene

#### AntiCampingSystem GameObject
```
AntiCampingSystem
└── AntiCampingSystem (script)
```

#### VisionSystem GameObject
```
VisionSystem
└── VisionSystem (script)
```

#### InputLayerManager GameObject
```
InputLayerManager
└── InputLayerManager (script)
```

**InputLayerManager Settings:**
- Input Action Asset: Assign `InputSystem_Actions.inputactions`

#### GameplayEventBus GameObject
```
GameplayEventBus
└── GameplayEventBus (script)
```

#### ClientEffectManager GameObject
```
ClientEffectManager
└── ClientEffectManager (script)
```

### 2. Player Prefab Setup

Tạo Player prefab với các components:

```
PlayerPrefab
├── NetworkObject (FishNet)
├── NetworkPlayer (script)
├── CharacterController
├── CharacterStats (script)
├── CharacterStatsSync (script)
├── CharacterMovement (script)
├── CharacterCombat (script)
├── PlayerInputHandler (script)
├── InventorySystem (script)
├── VisionRevealer (script)
├── FogOfWarRevealer3D (FogOfWar plugin)
└── CameraRig (child)
    ├── CinemachineCameraController (script)
    ├── CameraRotationInput (script)
    └── CameraZoomInput (script)
```

**Player Prefab Settings:**

**NetworkPlayer:**
- Character Controller: Assign CharacterController
- Input Handler: Assign PlayerInputHandler
- Movement: Assign CharacterMovement
- Combat: Assign CharacterCombat

**CharacterStats:**
- Base HP: 100
- Base Stamina: 100
- Base Move Speed: 5
- Base Weight Capacity: 20

**PlayerInputHandler:**
- Input Action Asset: Assign `InputSystem_Actions.inputactions`
- Mouse Sensitivity: 1.0

**CinemachineCameraController:**
- Default Distance: 15
- Default Height: 10
- Rotation Speed: 90
- Zoom Speed: 2

### 3. Camera Setup

#### Main Camera
```
MainCamera
├── Camera
├── CinemachineBrain (Cinemachine component)
└── Audio Listener
```

**CinemachineBrain Settings:**
- Default Blend: 0.5s
- Update Method: Smart Update

### 4. Spawn Points

Tạo SpawnPoint GameObjects:
```
SpawnPoint_1
SpawnPoint_2
SpawnPoint_3
...
```

**SpawnPoint Settings:**
- Position: Đặt ở các vị trí khác nhau trên map
- Tag: "SpawnPoint" (hoặc custom tag)
- Add SpawnPoint component nếu có

### 5. Loot Spawn Points

Tạo LootSpawnPoint GameObjects:
```
LootSpawnPoint_1
LootSpawnPoint_2
...
```

**LootSpawnPoint Settings:**
- Spawn Radius: 2m
- Can Spawn: ✓

### 6. Zone Areas (Optional)

Tạo ZoneArea GameObjects:
```
ZoneArea_Safe
ZoneArea_Toxic
...
```

**ZoneArea Settings:**
- Zone ID: "ZONE_SAFE_01"
- Radius: 20m
- Duration: 60s
- Is Active: ✓

## Input System Setup

### Input Actions Asset

1. Tạo hoặc mở `InputSystem_Actions.inputactions`
2. Tạo các Action Maps:

**Player Map:**
- Move (Vector2)
- Look (Vector2)
- Attack (Button)
- Interact (Button)
- Crouch (Button)
- Sprint (Button)
- Reload (Button)
- Inventory (Button)

**Camera Map:**
- RotateLeft (Button) - Q key
- RotateRight (Button) - E key
- Zoom (Vector2) - Mouse Scroll

**UI Map:**
- Navigate (Vector2)
- Submit (Button)
- Cancel (Button)

**Spectator Map:**
- NextPlayer (Button)
- PreviousPlayer (Button)
- ExitSpectator (Button)

**ScoutMode Map:**
- ToggleScoutMode (Button) - M key

## Config Files Setup

### Game Config File

1. Đảm bảo file config tồn tại:
   `StreamingAssets/NightHunt_Full_GameDesign_Config_v3.json`

2. Config phải chứa:
   - Weapons config
   - Items config
   - Characters config
   - Status Effects config
   - Phases config
   - Zones config
   - Inventory config
   - Stamina/Weight config

## Testing Workflow

### 1. Single Player Test (Local Host)

1. Mở `TestGameplayScene`
2. Chọn NetworkManager GameObject
3. Trong NetworkManager Inspector:
   - Start On Headless: ✗
   - Start On Client: ✓
   - Start On Server: ✓
4. Press Play
5. Game sẽ start như local host (server + client cùng lúc)

**Kiểm tra:**
- Player spawn thành công
- Input hoạt động (WASD movement, mouse look)
- Camera follow player
- Camera rotation (Q/E)
- Camera zoom (mouse wheel)
- Character stats hiển thị đúng
- Match phase chuyển đổi

### 2. Multi-Player Test (2 Instances)

#### Instance 1 - Server
1. Mở `TestGameplayScene`
2. NetworkManager Settings:
   - Start On Headless: ✗
   - Start On Server: ✓
   - Start On Client: ✗
3. Press Play
4. Server sẽ start và listen trên port 7770

#### Instance 2 - Client
1. Mở Unity Editor instance thứ 2 (hoặc Build)
2. Mở `TestGameplayScene`
3. NetworkManager Settings:
   - Start On Headless: ✗
   - Start On Server: ✗
   - Start On Client: ✓
   - Server IP: `127.0.0.1`
   - Server Port: `7770`
4. Press Play
5. Client sẽ connect đến server

**Kiểm tra:**
- Client connect thành công
- 2 players spawn trong scene
- Network sync hoạt động (position, rotation)
- Input chỉ hoạt động cho local player
- Camera chỉ follow local player
- Prediction hoạt động (smooth movement)

### 3. Testing Individual Systems

#### Test Character Movement
1. Spawn player
2. Press WASD để move
3. Press Shift để sprint
4. Press Ctrl để crouch
5. Kiểm tra stamina drain/regen

#### Test Character Combat
1. Equip weapon
2. Press Left Click để attack
3. Kiểm tra visual bullets spawn
4. Kiểm tra damage application

#### Test Match Phases
1. Wait for phase transitions
2. Kiểm tra phase UI updates
3. Kiểm tra phase-based rules (respawn, scoring)

#### Test Predator/Prey System
1. Play match và accumulate score
2. Kiểm tra role switching khi score thay đổi
3. Kiểm tra reveal/radar ping effects

#### Test Zone System
1. Walk vào zone areas
2. Kiểm tra zone effects apply
3. Kiểm tra zone buffs/nerfs

#### Test Loot System
1. Wait for loot spawn
2. Walk đến loot items
3. Kiểm tra auto-pickup hoặc manual pickup

#### Test Anti-Camping
1. Stand tại một vị trí >90s
2. Kiểm tra reveal effect
3. Move và kiểm tra reveal remove

#### Test Vision System
1. Walk around map
2. Kiểm tra fog of war reveal
3. Kiểm tra line of sight

## Debugging

### Common Issues

#### Input Not Working
- Check InputLayerManager is initialized
- Verify InputActionAsset is assigned
- Check PlayerInputHandler is enabled
- Verify InputState is set correctly

#### Network Sync Issues
- Check NetworkManager is started
- Verify NetworkPlayer ownership
- Check SyncVars are properly initialized
- Verify server is running

#### Camera Not Following
- Check CinemachineBrain is on main camera
- Verify CinemachineVirtualCamera is assigned
- Check follow target is set
- Verify camera is enabled

#### Systems Not Initializing
- Check GameplayBootstrap is in scene
- Verify all required GameObjects exist
- Check console for errors
- Verify config files are loaded

### Debug Commands

Thêm vào console hoặc debug menu:

```csharp
// Force phase transition
MatchPhaseManager.Instance?.TransitionToPhase(MatchPhaseState.Hunt);

// Spawn loot manually
LootSpawner spawner = FindFirstObjectByType<LootSpawner>();
spawner?.SpawnLoot();

// Toggle predator/prey roles
PredatorPreySystem system = FindFirstObjectByType<PredatorPreySystem>();
system?.UpdateRoles();
```

## Performance Testing

### Profiler Setup
1. Window > Analysis > Profiler
2. Enable:
   - CPU Usage
   - Memory
   - Network Messages
   - Rendering

### Metrics to Monitor
- FPS (target: 60+)
- Network messages per second
- Memory usage
- Draw calls
- Physics updates

## Next Steps

Sau khi test local thành công:
1. Test với dedicated server build
2. Test với multiple clients
3. Test với different network conditions
4. Performance optimization
5. Bug fixes và polish

## Notes

- Tất cả systems đã được implement với SOLID principles
- Code sẵn sàng cho production
- Systems có thể hoạt động độc lập
- Dễ dàng mở rộng và maintain

