# NIGHT HUNT — SCENE SETUP GUIDE
> Unity 6 + FishNet Pro V4 + Dedicated Server
> Reference for Inspector wiring + Build Settings for each scene.

---

## RANKED MATCHMAKING FLOW

```
Player presses "Find Match" (HomeView)
  → POST /api/matchmaking/queue          enqueue with gameMode, mapId
  → Backend tick (every 5s) forms lobby when ELO ranges overlap
  → Backend: createMatchedRoom() → allocateServerForMatch() → Docker container starts
  → DS boots → POST /api/ds/register → POST /api/ds/game-ready

⚠ NOTE: There is NO match_found / accept step. Backend sends match_ready directly
  once the DS is allocated. No MatchFoundOverlay, no accept endpoint.

WS: match_cancelled  { reason }          ← if any player's session drops
  → Toast shown, player returns to queue button

WS: match_ready  { gameMode, mapId, dsIp, dsPort, matchId, roomCode, sessionToken }
  → [GWS] GameWebSocketService.HandleMatchReady()
  → [MFC] MatchFlowCoordinator.HandleMatchReady()
      → RoomState populated (matchId, dsIp, dsPort, roomCode, mapId)
      → MatchLoadingOverlay.Show(sceneId)    [stage: DsBooting — "Starting server..."]
      → SceneLoader.LoadGame(sceneId)        ← async Unity scene load begins

WS: ds_ready  { dsIp, dsPort, matchId, mapId, serverId }
  → [GWS] GameWebSocketService.HandleDsReady()
      → RoomState.SetDedicatedServer(dsIp, dsPort, matchId, mapId)  ← REQUIRED for AutoConnectDS
  → [MFC] MatchFlowCoordinator.HandleDsReady()
      → MatchLoadingOverlay.MarkDsReady()    [stage: Connecting]
      → NetworkGameManager.NotifyDsReady()   → _dsReadyReceived = true
      → TryConnectIfReady()  (needs both _dsReadyReceived AND _gameSceneLoaded)

Both flags true → AutoConnectDS() → FishNet.StartClient(dsIp, dsPort)

FishNet ClientConnected:
  → MatchLoadingOverlay.MarkConnected() [stage: ServerReady]

GameplayEventBus: SpawningStartedEvent
  → MatchLoadingOverlay.MarkSpawning()  [stage: Spawning]

GameplayEventBus: PlayerSpawnedEvent × N
  → Progress cards update per player

GameplayEventBus: AllPlayersReadyEvent
  → MatchLoadingOverlay reaches 100%, waits minimumDisplayDuration
  → MatchLoadingOverlay.Hide()
  → Game begins
```

**Custom Lobby flow** — host presses "Start Match":
- No matchmaking queue, no DS allocation — backend sends `match_ready` directly with RelayIp/RelayPort
- `match_ready` arrives → same handler runs from `match_ready` onward
- Host runs `FishNet.StartHost()`, members run `FishNet.StartClient(relayIp, relayPort)` via `AutoConnectRelay()`

---

## SCENE 1 — `00_DS_Boot.unity` (Dedicated Server only)

Runs headless on the Linux Docker container.
**No Camera, no AudioListener, no GameManager, no UI.**

### Required Hierarchy

```
00_DS_Boot
├── [NetworkManager]          ← FishNet NetworkManager
│   └── Tugboat (Transport)
├── [ServerBootstrap]
└── [ServerUISuppressor]
```

### Inspector Wiring

| Component | Field | Value |
|---|---|---|
| `ServerBootstrap` | `networkManager` | drag `[NetworkManager]` GO |
| `ServerBootstrap` | `fallbackPort` | `7777` |
| `ServerBootstrap` | `fallbackBackendUrl` | your backend HTTPS URL |
| `ServerBootstrap` | `fallbackServerId` | `"localhost-production-test"` |
| `ServerBootstrap` | `fallbackServerSecret` | dev secret from allocate API |
| `ServerBootstrap` | `fallbackMapId` | `"map_01"` |
| `ServerBootstrap` | `fallbackMaxPlayers` | `16` |

### DS Boot Sequence (log tag `[DS-Boot]`)

```
Step 1  ParseCommandLineArgs  → --port, --backendUrl, --serverId, --secret,
                                --mapId, --matchId, --maxPlayers, --expectedPlayers
Step 2  StartServer(port)     → FishNet NetworkManager.StartServer()
Step 3  Wait FishNet active   → ServerManager.Started == true  (timeout 10s)
Step 4  POST /api/ds/register → auth with serverSecret, ds marked "ready" in DB
Step 5  LoadGameScene(mapId)  → ServerManager.LoadGlobalScenes(sceneName)
Step 6  Scene loaded          → game scene ready for player connections
Step 7  POST /api/ds/game-ready → backend broadcasts WS ds_ready to all players
        HeartbeatLoop()       → POST /api/ds/heartbeat every 30s
```

### Validation Checklist (CC-6)

- [ ] `ServerBootstrap._networkManager` → wired
- [ ] Transport is **Tugboat** (not Relay shim)
- [ ] **NO** `GameManager`, `UINavigator`, `AudioManager` in scene
- [ ] **NO** `Camera` or `AudioListener` (headless)
- [ ] `ServerUISuppressor` present and active

---

## SCENE 2 — `01_Home.unity` (Client — Login / Home / Lobby)

### Required Hierarchy

```
01_Home
├── [GameManager]                   ← SingletonPersistent, all services
├── [NetworkGameManager]            ← SingletonPersistent, client connect
├── [PlatformInputDetector]         ← SingletonPersistent, DontDestroyOnLoad
├── [AudioManager]                  ← SingletonPersistent, audio mixer
├── [PersistentUICanvas]            ← DontDestroyOnLoad UI layer
│   ├── [MatchFlowCoordinator]      ← WS match lifecycle authority
│   ├── MatchLoadingOverlay
│   ├── LoadingOverlay
│   ├── PingDisplay
│   └── ToastService
├── [UINavigator]                   ← panel routing
└── Canvas/Panels
    ├── LoginView
    ├── HomeView
    ├── LobbyView
    ├── PartyPanel
    ├── ResultsView
    └── SettingsPanel
        ├── AudioSettingsPanel
        └── ControlsSettingsPanel
```

### Inspector Wiring — `GameManager`

| Field | Assign |
|---|---|
| `backendHttpClient` | `BackendHttpClient` component |
| `authService` | `AuthService` component |
| `gameConfigService` | `GameConfigService` component |
| `friendService` | `FriendService` component |
| `partyService` | `PartyService` component |
| `roomService` | `RoomService` component |
| `gameWebSocketService` | `GameWebSocketService` component |
| `instanceConfig` | `InstanceConfig` ScriptableObject asset |
| `sessionState` | `SessionState` component |
| `roomState` | `RoomState` component |

### Inspector Wiring — `NetworkGameManager`

| Field | Assign |
|---|---|
| `networkManager` | `[NetworkManager]` GO in scene |
| `port` | `7777` (overridden at runtime from WS `ds_ready` event) |
| `_retryDelay` | `3` |
| `_maxRetries` | `2` |

### Inspector Wiring — `PersistentUICanvas`

| Field | Assign |
|---|---|
| `loadingManager` | `LoadingOverlay` child GO |
| `matchLoadingOverlay` | `MatchLoadingOverlay` child GO |
| `pingDisplay` | `PingDisplay` child GO |
| `toastService` | `ToastService` child GO |

> **Note:** `matchFoundOverlay` slot still exists in the Inspector but is unused — leave it empty.
> `MatchFoundOverlay` and `match_found` WS event are fully removed from the ranked flow.
> `MatchFlowCoordinator` only handles: `match_ready`, `ds_ready`, `match_cancelled`, `match_ended`.

### Inspector Wiring — `AudioSettingsPanel`

| Field | Assign |
|---|---|
| All slider references | Master, Music, SFX, Ambient `Slider` components |
| `AudioMixer` | `NightHunt_Mixer` asset |

> PlayerPrefs keys: `"NH_Audio_Master"`, `"NH_Audio_Music"`, `"NH_Audio_SFX"`, `"NH_Audio_Ambient"`

### Validation Checklist (CC-5)

- [ ] `GameManager` → all service + state slots filled
- [ ] `MatchFlowCoordinator` → placed on `PersistentUICanvas` root
- [ ] `PersistentUICanvas` → `matchLoadingOverlay`, `loadingManager`, `toastService`, `pingDisplay` wired
- [ ] `PersistentUICanvas.matchFoundOverlay` → leave empty (removed from flow)
- [ ] `ControlsSettingsPanel` → `InputActionAsset` wired
- [ ] `AudioSettingsPanel` → `AudioManager` wired
- [ ] Rename `CanvasDontDestroy/ModalWindown` → `CanvasDontDestroy/ModalWindow` (typo)

---

## SCENE 3 — `02_Map_01.unity` (Client + Server — Match)

### Required Hierarchy

```
02_Map_01
├── [NetworkManager]                ← FishNet (same prefab or in-scene)
├── [ServerGameManager]             ← NetworkBehaviour, server orchestration
├── [GameBootstrap]                 ← server-only phase orchestration
├── [MatchPhaseManager]             ← NetworkBehaviour, phase SyncVars
├── [MatchEndManager]               ← NetworkBehaviour, reads ScoringSystem
├── [ScoringSystem]                 ← NetworkBehaviour, SyncList scores
├── [SpawnSystem]                   ← NetworkBehaviour, spawn point registry
├── [RespawnSystem]                 ← NetworkBehaviour
├── [TeamService]                   ← Singleton registry
├── [RegistryService]               ← Singleton registry
├── [ZoneSystem]                    ← Singleton, zone registry
│   └── [LockdownZone]              ← self-registers on OnEnable
├── [BossSpawnManager]              ← NetworkBehaviour
├── [ObjectiveSystem]               ← NetworkBehaviour
│   ├── [CaptureZoneObjective]
│   ├── [EMPNodeObjective]
│   └── [RadarStationObjective]
├── [WorldSpawnManager]
├── [AntiCampingSystem]
├── [SpectateManager]               ← Singleton
├── [GameCameraController]
├── [CameraStateManager]
└── GameHUD (Canvas)
    ├── [CombatHUDPanel]
    ├── [PlayerHUDPanel]
    ├── [BossHUDPanel]              ← SetActive(false) by default
    ├── [SpectatorHUD]              ← SetActive(false) by default
    ├── [DeathScreen]               ← SetActive(false) by default
    ├── [ReconnectOverlay]          ← SetActive(false) by default
    ├── [MinimapUI]
    └── [KillFeedUI]
```

### Inspector Wiring — `GameBootstrap`

| Field | Assign |
|---|---|
| `_matchPhaseManager` | `[MatchPhaseManager]` GO |
| `_spawnSystem` | `[SpawnSystem]` GO |
| `_teamAssignmentSystem` | `[TeamAssignmentSystem]` GO |

### Inspector Wiring — `ServerGameManager`

| Field | Assign |
|---|---|
| `playerPrefab` | `Assets/_Night_Hunt/Prefabs/PlayerPrefab.prefab` |
| `_spawnSystem` | `[SpawnSystem]` GO |
| `_matchPhaseManager` | `[MatchPhaseManager]` GO |
| `clientNetworkHandlerPrefab` | `ClientNetworkHandler` prefab |

### Inspector Wiring — `MatchEndManager`

| Field | Assign |
|---|---|
| `_phaseManager` | `[MatchPhaseManager]` GO |
| `_scoringSystem` | `[ScoringSystem]` GO |

### Inspector Wiring — `BossSpawnManager`

| Field | Assign |
|---|---|
| `_phaseManager` | `[MatchPhaseManager]` GO |
| `_bossSpawns` | List of `BossSpawnEntry` (Boss prefab + spawn points + spawn phase) |

> Boss spawns only when `SpawnPhase == MatchPhaseState.Hunt` (Phase 2).

### Inspector Wiring — `ZoneSystem`

| Field | Assign |
|---|---|
| `_phaseManager` | `[MatchPhaseManager]` GO |

> Individual zones (LockdownZone etc.) use `ZoneSystem.Instance.PhaseManager`
> instead of calling `FindFirstObjectByType<MatchPhaseManager>()`.

### Inspector Wiring — `ObjectiveSystem`

| Field | Assign |
|---|---|
| `_phaseManager` | `[MatchPhaseManager]` GO |
| `captureZoneObjectives` | all `CaptureZoneObjective` GOs |
| `radarObjectives` | all `RadarStationObjective` GOs |
| `empObjectives` | all `EMPNodeObjective` GOs |

### Inspector Wiring — `GameCameraController`

| Field | Assign |
|---|---|
| `_spectatorInput` | `SpectatorInputHandler` component in scene |
| `_virtualCamera` | Cinemachine Virtual Camera GO |

### Inspector Wiring — `BossHUDPanel`

| Field | Assign |
|---|---|
| `_panel` | Root GO of the boss HUD panel |
| `_hpSlider` | `Slider` component |
| `_bossNameText` | `TextMeshProUGUI` boss name label |
| `_hpText` | `TextMeshProUGUI` HP fraction text (`"3500 / 5000"`) |
| `_hpLabelText` | `TextMeshProUGUI` static label (displays `"BOSS HP"`) |
| `_defeatedDisplayDuration` | `2` (seconds to show "Boss Defeated!" before hiding) |

### Validation Checklist (CC-4)

- [ ] `GameBootstrap` → all `[SerializeField]` slots filled
- [ ] `ServerGameManager.playerPrefab` → wired
- [ ] `GameCameraController._spectatorInput` → wired
- [ ] `GameCameraController._virtualCamera` → wired
- [ ] `MatchEndManager._phaseManager` → wired
- [ ] `MatchEndManager._scoringSystem` → wired
- [ ] `BossSpawnManager._phaseManager` → wired
- [ ] `ZoneSystem._phaseManager` → wired
- [ ] `ObjectiveSystem._phaseManager` → wired
- [ ] `ObjectiveSystem` → all objective lists populated
- [ ] `BossHUDPanel` → all UI refs wired, default `SetActive(false)`
- [ ] `SpectatorHUD` → default `SetActive(false)`
- [ ] `DeathScreen` → default `SetActive(false)`

---

## BUILD SETTINGS

### Dedicated Server Build (Linux headless)

```
Platform:          Linux (Dedicated Server)
Scripting Backend: IL2CPP
Architecture:      x86_64
Server Build:      ✓ checked

Scenes (BuildScript.DsScenes):
  Index 0 → 00_DS_Boot.unity
  Index 1 → 02_Map_01.unity
```

### Client Build (Windows / macOS / WebGL)

```
Platform:          Target platform
Scripting Backend: IL2CPP
Server Build:      ✗ unchecked

Scenes (BuildScript.ClientScenes):
  Index 0 → 01_Home.unity
  Index 1 → 02_Map_01.unity
```

> `00_DS_Boot.unity` must **never** appear in the client scene list.
> Use separate arrays in `BuildScript.cs` (`DsScenes` / `ClientScenes`).
> Delete duplicate `02_Map_01 1.unity` — confirm it is excluded from both build lists first.

---

## DATA FLOW SUMMARY

```
[Backend] Matchmaking
  POST /api/matchmaking/queue (per player)
  MatchmakingQueueService.formMatch() tick every 5s
  → [MM]  createMatchedRoom()         STEP 1: create ranked room
  → [MM]  allocateServerForMatch()    STEP 2: find existing DS or spin new
  → [DS-Alloc] spinUpNewServer()      → Docker container start (if new)
  → [MM]  delete queue entries        STEP 3
  → [MM]  WS match_ready to N players STEP 4

[01_Home] Client — match_ready received
  [GWS] GameWebSocketService.HandleMatchReady()
  [MFC] MatchFlowCoordinator.HandleMatchReady()
      → RoomState populated (matchId, dsIp, dsPort, roomCode, mapId)
      → MatchLoadingOverlay.Show(mapId)       [stage: DsBooting]
      → SceneLoader.LoadGame(mapId)           ← UnityEngine.SceneManager.LoadScene

  USceneManager.sceneLoaded fires (02_Map_01)
  [NGM] NetworkGameManager.OnUnitySceneLoaded()
      → _gameSceneLoaded = true
      → TryConnectIfReady()

[DS Container] DS boot sequence
  [DS-Boot] Step 1: ParseCommandLineArgs (--port, --backendUrl, --serverId,
                    --secret, --mapId, --matchId, --maxPlayers, --expectedPlayers)
  [DS-Boot] Step 2: FishNet.StartServer(port)
  [DS-Boot] Step 3: Wait FishNet active (timeout 10s)
  [DS-Boot] Step 4: POST /api/ds/register → ds marked ready in DB
  [DS-Boot] Step 5: LoadGameScene(mapId)
  [DS-Boot] Step 6: Scene loaded → accepting connections
  [DS-Boot] Step 7: POST /api/ds/game-ready

[Backend] game-ready handler
  [DS-Svc] notifyGameReady() validates secret, sets status=ready
  → finds room by matchId, gets all RoomPlayers
  → broadcasts WS ds_ready to each player

[01_Home] Client — ds_ready received
  [GWS] GameWebSocketService.HandleDsReady()
      → RoomState.SetDedicatedServer(dsIp, dsPort, matchId, mapId)  ← CRITICAL: must happen before AutoConnectDS
  [MFC] MatchFlowCoordinator.HandleDsReady()
      → MatchLoadingOverlay.MarkDsReady()     [stage: Connecting]
  [NGM] NetworkGameManager.NotifyDsReady()
      → _dsReadyReceived = true
      → TryConnectIfReady()

  Both flags true → AutoConnectDS() → FishNet.StartClient(RoomState.DsIp, RoomState.DsPort)
  [NGM] OnClientConnectionState: Started → Connected
  [MLO] MatchLoadingOverlay.MarkConnected()   [stage: ServerReady]

[02_Map_01] FishNet: client connects
  ServerGameManager.OnClientConnected(conn)
      → ClientNetworkHandler validates JWT
      → SpawnPlayer workflow
      → GameBootstrap.OnPlayerRegistered → phase activation

  GameplayEventBus: SpawningStartedEvent  → [MLO] MarkSpawning()  [stage: Spawning]
  GameplayEventBus: PlayerSpawnedEvent×N  → progress cards update
  GameplayEventBus: AllPlayersReadyEvent  → [MLO] 100% → waits minimumDisplayDuration → Hide()

[02_Map_01] Match end
  MatchEndManager.EndMatch()
  → GameplayEventBus.Publish(MatchEndedEvent)
  → [NGM] NetworkGameManager: _matchEnded = true
  → ServerBootstrap.ReportMatchEndAndShutdown()
      → POST /api/match/end/ranked
      → Application.Quit()

  Client: FishNet Stopped event
  → _matchEnded == true → skip TryRetry()
  → ResultsView shown with ELO data from WS match_ended event
  → SceneLoader.LoadHome()
```

---

## LOG FILTER TAGS

All log lines carry a bracketed prefix so you can filter in Unity Console or `docker logs`:

| Tag | Class | Tier | What it covers |
|---|---|---|---|
| `[GWS]` | `GameWebSocketService` | Client | WS event received + deserialized |
| `[MFC]` | `MatchFlowCoordinator` | Client | match_ready / ds_ready / cancelled / ended handlers |
| `[NGM]` | `NetworkGameManager` | Client | TryConnectIfReady, AutoConnectDS/Relay, connection state changes |
| `[MLO]` | `MatchLoadingOverlay` | Client | Stage transitions, timeout, hide |
| `[DS-Boot]` | `ServerBootstrap` | DS (Docker) | Boot steps 1–7, heartbeat player count |
| `[MM]` | `MatchmakingQueueService` | Backend | formMatch steps 1–4, re-queue on failure |
| `[DS-Alloc]` | `DedicatedServerService` | Backend | allocateServer, spinUpNewServer, container start |
| `[DS-Svc]` | `DedicatedServerService` | Backend | game-ready validation, ds_ready broadcast per player |

### Docker log filter examples
```bash
# Watch DS boot steps only
docker logs <container> 2>&1 | grep "\[DS-Boot\]"

# Watch backend match + DS allocation
docker logs nighthunt-backend 2>&1 | grep -E "\[MM\]|\[DS-Alloc\]|\[DS-Svc\]"
```

### Unity Console filter
Type the tag in the search box, e.g. `[NGM]` to see only NetworkGameManager lines.
