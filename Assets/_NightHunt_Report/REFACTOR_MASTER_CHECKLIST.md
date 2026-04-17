# NIGHT HUNT — MASTER REFACTOR CHECKLIST
> Generated after full project audit. Language target: **English** (all logs, UI text, comments).
> Priority: 🔴 Critical → 🟠 High → 🟡 Medium → 🟢 Polish

---

## LEGEND
- `[FILE]` = script file to modify
- `[NEW]`  = new file to create
- `[SCENE]` = scene hierarchy / Inspector change
- `[ASSET]` = ScriptableObject / prefab / config change
- `[CI]`   = GitHub Actions / Docker / build pipeline

---

## ═══════════════════════════════════════════════
## SPRINT 1 — CRITICAL BLOCKERS (Week 1–2)
## ═══════════════════════════════════════════════

### S1-A │ LANGUAGE STANDARDIZATION — English Only
> All Debug.Log, UI strings, comments, variable names must be English.
> Mixed Vietnamese/English is a production blocker for public release.

- [ ] **[FILE]** `Scripts/Core/GameManager.cs`
  - Replace all Vietnamese comments and log strings with English
  - e.g. `"Không tìm thấy"` → `"Not found"`, `"Đã khởi tạo"` → `"Initialized"`

- [ ] **[FILE]** `Scripts/Networking/NetworkGameManager.cs`
  - All log messages → English

- [ ] **[FILE]** `Scripts/Server/ServerBootstrap.cs`
  - All log messages → English (already mostly English, verify fully)

- [ ] **[FILE]** `Scripts/Gameplay/Match/MatchPhaseManager.cs`
  - Comments + logs → English
  - `"Bắt Đầu!"` countdown text → `"GO!"`

- [ ] **[FILE]** `Scripts/Gameplay/Match/MatchEndManager.cs`
  - All Vietnamese comments → English

- [ ] **[FILE]** `Scripts/Networking/ServerGameManager.cs`
  - All Vietnamese comments → English

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Inventory/InventoryScreen.cs`
  - Comments: `"Màn hình Inventory chính"` → `"Main Inventory Screen"`
  - All Vietnamese tooltip/header strings → English

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Inventory/UIRootController.cs`
  - Comments + logs → English

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Inventory/UIDomainBridge.cs`
  - Comments + logs → English

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Inventory/PlayerHUDPanel.cs`
  - All Vietnamese Tooltip/Header attributes → English

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Combat/CombatHUDPanel.cs`
  - Comments + logs → English

- [ ] **[FILE]** `Scripts/Gameplay/FogOfWar/FogTeamVisibilityBinder.cs`
  - All Vietnamese comments → English

- [ ] **[FILE]** `Scripts/Gameplay/FogOfWar/FogVisionBinder.cs`
  - Comments → English

- [ ] **[FILE]** `Scripts/Gameplay/Input/Core/InputLayerManager.cs`
  - All Vietnamese comments in the context table → English

- [ ] **[FILE]** `Scripts/UI/UINavigator.cs`
  - All Vietnamese Tooltip/Header/comments → English

- [ ] **[FILE]** `Scripts/UI/MatchLoadingOverlay.cs`
  - All Vietnamese comments → English
  - Status strings: `"Đang kết nối..."` → `"Connecting..."` etc.

- [ ] **[FILE]** `Scripts/UI/ResultsView.cs`
  - Comments → English (already mostly English, verify)

- [ ] **[TASK]** Run global search for Vietnamese characters in all `.cs` files under `Assets/_Night_Hunt/Scripts/`
  - Search pattern: any string containing `[àáâãèéêìíòóôõùúýăđơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ]`
  - Replace all found strings with English equivalents

---

### S1-B │ GAME LOOP COMPLETION

- [ ] **[FILE]** `Scripts/UI/ResultsView.cs`
  - Subscribe `GameWebSocketService.Instance.OnMatchEnded` in `OnEnable()`
  - Unsubscribe in `OnDisable()`
  - On receive: update `_eloChangeText` per player from WS event data
  - On receive: update `SessionState.Instance.Coins` from WS event
  - Add 10s timeout fallback: if WS event not received → show `"Calculating..."`
  - Add `_matchEndHandled` bool flag to prevent double `LoadHome()` call
  - In `NavigatePostMatch()`: set `_matchEndHandled = true` before `SceneLoader.LoadHome()`
  - In `NetworkGameManager.OnClientConnectionState(Stopped)`: if `_matchEndHandled` → skip retry

- [ ] **[FILE]** `Scripts/Networking/NetworkGameManager.cs`
  - Add `private bool _matchEnded = false;`
  - Subscribe `GameplayEventBus.Instance.Subscribe<MatchEndedEvent>` in `Start()`
  - On `MatchEndedEvent`: set `_matchEnded = true`
  - In `OnClientConnectionState(Stopped)`: if `_matchEnded` → skip `TryRetry()`, log `"Match ended — skipping retry"`
  - In `LoadHome()`: reset `_matchEnded = false`

- [ ] **[FILE]** `Scripts/UI/MatchLoadingOverlay.cs`
  - Decouple `SceneLoader.LoadGame()` from `ShowInternal()`
  - New flow: caller calls `Show(mapId)` first, then separately calls `SceneLoader.LoadGame(mapId)`
  - In `ShowInternal()`: check if `s_dsReadyReceived` already true → call `MarkDsReady()` immediately
  - Add `RetryButton` reference + `OnRetryClicked()` handler for timeout case
  - Status strings must all be English: `"Starting game server..."`, `"Connecting to server..."`, etc.

- [ ] **[NEW]** `Scripts/UI/MatchFlowCoordinator.cs`
  - `SingletonPersistent<MatchFlowCoordinator>`
  - Subscribe: `OnMatchFound`, `OnMatchReady`, `OnDsReady`, `OnMatchEnded` from `GameWebSocketService`
  - `OnMatchFound` → `MatchFoundOverlay.Instance?.Show(evt)`
  - `OnMatchReady` → populate `RoomState` → `MatchLoadingOverlay.Instance?.Show(mapId)` → `SceneLoader.LoadGame(mapId)`
  - `OnDsReady` → `MatchLoadingOverlay.Instance?.MarkDsReady()`
  - `OnMatchEnded` → `ResultsView` ELO update via event
  - Remove scattered match-flow logic from `HomeView`, `PartyController`, `CustomLobbyView`

---

### S1-C │ SCORE SYSTEM UNIFICATION

- [ ] **[FILE]** `Scripts/Gameplay/Match/MatchEndManager.cs`
  - Remove `_teamKillScore` dictionary (duplicate of `ScoringSystem`)
  - Remove `_teamObjectiveScore` dictionary (duplicate of `ScoringSystem`)
  - Add `[SerializeField] private ScoringSystem _scoringSystem;` (already exists, verify wired)
  - `GetTotalScore(teamId)` → read from `_scoringSystem.GetTeamScore(teamId)` only
  - `GetFinalResults()` → use `_scoringSystem.GetPlayerScore((uint)np.ObjectId)` for all players
  - Keep `_playerKillCount` and `_playerDeathCount` (per-player tracking still needed for results)
  - `AddKill(killerTeamId, backendPlayerId)` → only update `_playerKillCount`, remove team score update
  - `AddObjectiveScore(teamId, delta)` → delegate to `_scoringSystem.AwardObjectiveCapture(teamId, delta)`

- [ ] **[FILE]** `Scripts/Gameplay/Scoring/ScoringSystem.cs`
  - Replace `SyncVar<string> scoreDataJson` with `SyncList<PlayerScore>` + `SyncList<TeamScore>`
  - Remove `JsonUtility.ToJson()` call in `SyncScores()`
  - Add `OnScoreChanged` event fired per-player delta (for real-time HUD update)
  - `GetTeamScore(teamId)` must be public and accurate (used by `MatchEndManager`)

---

### S1-D │ GAMEBOOTSTRAP PHASE ACTIVATION FIX

- [ ] **[FILE]** `Scripts/Gameplay/Core/GameBootstrap.cs`
  - `ActivatePhase1Systems()`:
    - Call `worldSpawn?.BeginSpawnCycle()` (not just log warning)
    - Call `antiCamp?.Enable()`
    - Use `[SerializeField]` refs instead of `FindFirstObjectByType`
  - `ActivatePhase2Systems()`:
    - Call `bossSpawn?.ScheduleBossSpawn()`
    - Call `objectiveSystem?.ActivateObjectives()`
  - `ActivatePhase3Systems()`:
    - Call `lockdownZone?.Activate()`
    - Call `respawnSystem?.SetPhase3Mode()`
  - Add `[SerializeField]` for: `WorldSpawnManager`, `BossSpawnManager`, `ObjectiveSystem`, `LockdownZone`, `RespawnSystem`, `AntiCampingSystem`
  - Add `OnValidate()` to warn if any required ref is null
  - All log messages → English

---

### S1-E │ SCENE LOAD + CONNECTION FLOW FIX

- [ ] **[FILE]** `Scripts/Networking/NetworkGameManager.cs`
  - Replace `networkManager.SceneManager.OnLoadEnd` listener with `UnityEngine.SceneManagement.SceneManager.sceneLoaded`
  - In `sceneLoaded` callback: check `scene.name.StartsWith("02_Map_")` → set `_gameSceneLoaded = true` → `TryConnectIfReady()`
  - Unsubscribe `sceneLoaded` in `OnDestroy()`
  - `LoadHome()`: add `s_dsReadyReceived = false` (already present, verify)
  - Add `public static void ResetConnectionFlags()` called by `RoomState.ClearRoom()`

- [ ] **[FILE]** `Scripts/State/RoomState.cs`
  - `ClearRoom()`: call `NetworkGameManager.ResetConnectionFlags()` if instance exists

---

## ═══════════════════════════════════════════════
## SPRINT 2 — HIGH PRIORITY (Week 3–4)
## ═══════════════════════════════════════════════

### S2-A │ SPECTATOR SYSTEM — FOW + CAMERA + HUD

- [ ] **[FILE]** `Scripts/Gameplay/FogOfWar/FogVisionBinder.cs`
  - Subscribe `SpectateManager.Instance.OnSpectateStarted` in `OnEnable()`
  - Subscribe `SpectateManager.Instance.OnSpectateStopped` in `OnEnable()`
  - `HandleSpectateStarted()`:
    - If this is local player's binder → disable `_revealer`
    - Find spectated player's `FogVisionBinder` → enable their `_revealer`
  - `HandleSpectateStopped()`:
    - Re-enable local player's `_revealer`
    - Disable spectated player's `_revealer` (restore their normal state)
  - All log messages → English

- [ ] **[FILE]** `Scripts/Gameplay/Camera/Spectator/GameCameraController.cs`
  - `SetCameraTarget(Transform target)`: search for child named `"CameraFollowTarget"` first, fallback to root
  - Subscribe `SpectateManager.Instance.OnSpectateStarted` → call `CameraStateManager.ForceState(CameraState.Free)`
  - Remove `FindFirstObjectByType<SpectatorInputHandler>()` in `Awake()` → use `[SerializeField]` or self-register pattern
  - All log messages → English

- [ ] **[FILE]** `Scripts/Gameplay/Camera/CameraStateManager.cs`
  - Subscribe `SpectateManager.Instance.OnSpectateStarted` in `OnEnable()`
  - `HandleSpectateStarted()` → `ForceState(CameraState.Free)` to unlock camera
  - All log messages → English

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Inventory/UIRootController.cs`
  - Already subscribes `SpectateManager.OnCurrentPlayerChanged` → verify `OnCurrentPlayerChanged` re-binds all panels
  - Ensure `_playerHudPanel.Initialize(bridge)` is called with spectated player's bridge
  - Ensure `_combatHudPanel.Initialize(...)` is called with spectated player's systems
  - When spectating: hide combat input buttons (fire, reload) — spectator cannot act
  - All log messages → English

- [ ] **[NEW]** `Scripts/UI/SpectatorHUD.cs`
  - Show/hide based on `SpectateManager.IsSpectating()`
  - Display: spectated player name, team, health bar, current weapon
  - Navigation hint: `"[Tab] Next Player  [Q] Previous  [E] Exit"`
  - Subscribe `SpectateManager.OnCurrentPlayerChanged` → refresh display
  - Subscribe `SpectateManager.OnSpectateStarted` → `gameObject.SetActive(true)`
  - Subscribe `SpectateManager.OnSpectateStopped` → `gameObject.SetActive(false)`

- [ ] **[SCENE]** `02_Map_01.unity` — HUD hierarchy
  - Add `SpectatorHUD` GameObject under `GameHUD` canvas
  - Default: `SetActive(false)`
  - Wire `SpectatorHUD` reference in `GameHUD` Inspector

---

### S2-B │ INPUT SYSTEM — KEY REBINDING

- [ ] **[FILE]** `Scripts/UI/Settings/ControlsSettingsPanel.cs`
  - Add `[SerializeField] private List<RebindActionUI> _rebindableActions;`
  - Add `SaveBindings()`: `InputActionAsset.SaveBindingOverridesAsJson()` → `PlayerPrefs.SetString("NH_InputBindings", json)`
  - Add `LoadBindings()`: `PlayerPrefs.GetString("NH_InputBindings")` → `InputActionAsset.LoadBindingOverridesFromJson(json)`
  - Add `ResetAllBindings()`: `InputActionAsset.RemoveAllBindingOverrides()` → `SaveBindings()`
  - Call `LoadBindings()` in `Start()`
  - Add conflict detection: warn if two actions share same key
  - All UI strings → English: `"Mouse Sensitivity"`, `"Invert Y"`, `"Reset to Defaults"`, `"Key Bindings"`

- [ ] **[NEW]** `Scripts/UI/Settings/RebindActionUI.cs`
  - `[SerializeField] private InputActionReference _actionRef`
  - `[SerializeField] private TextMeshProUGUI _bindingText` — shows current binding
  - `[SerializeField] private Button _rebindButton` — starts interactive rebind
  - `[SerializeField] private Button _resetButton` — resets this action to default
  - `StartRebinding()`: `InputSystem.PerformInteractiveRebinding(action)` with overlay
  - `UpdateBindingDisplay()`: reads current binding path → formats as human-readable key name
  - Fire `OnBindingChanged` event → `ControlsSettingsPanel.SaveBindings()`

- [ ] **[SCENE]** `01_Home.unity` — Settings/Controls panel
  - Add `RebindActionUI` entries for: Move, Sprint, Crouch, Jump, Fire, Aim, Reload, Interact, Inventory, ToggleCameraLock
  - Add `"Reset All Bindings"` button wired to `ControlsSettingsPanel.ResetAllBindings()`

---

### S2-C │ INPUT SYSTEM — PLATFORM AUTO-DETECT

- [ ] **[NEW]** `Scripts/Gameplay/Input/Core/PlatformInputDetector.cs`
  - `SingletonPersistent<PlatformInputDetector>`
  - `public enum InputPlatform { KeyboardMouse, Gamepad, Touch }`
  - `public InputPlatform CurrentPlatform { get; private set; }`
  - `public event Action<InputPlatform> OnPlatformChanged`
  - In `Awake()`: detect initial platform via `Application.isMobilePlatform`, `Input.touchSupported`
  - Subscribe `InputSystem.onDeviceChange` → re-detect on gamepad connect/disconnect
  - `ApplyPlatform(InputPlatform)`:
    - `Touch` → show mobile HUD (joystick, fire button, zoom slider), hide keyboard hints
    - `KeyboardMouse` → hide mobile HUD, show keyboard hints
    - `Gamepad` → hide mobile HUD, show gamepad button hints

- [ ] **[FILE]** `Scripts/UI/GameHUD.cs`
  - Subscribe `PlatformInputDetector.Instance.OnPlatformChanged` in `OnEnable()`
  - `HandlePlatformChanged(InputPlatform)`:
    - Toggle `_mobileMovementBridge.gameObject.SetActive(isMobile)`
    - Toggle `_mobilePinchZoomBridge.gameObject.SetActive(isMobile)`
    - Toggle fire button visibility
  - All log messages → English

- [ ] **[FILE]** `Scripts/Gameplay/Input/Handlers/Combat/CombatInputHandler.cs`
  - Add `public void SetFirePressed(bool pressed)` — called by `FireButton` on mobile
  - Ensure `SetFirePressed` respects `InputLayerManager.IsLayerActive(InputLayer.Combat)`

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Combat/FireButton.cs`
  - `OnPointerDown` → `CombatInputHandler.Instance?.SetFirePressed(true)`
  - `OnPointerUp` → `CombatInputHandler.Instance?.SetFirePressed(false)`
  - Remove any direct `WeaponSystem` calls
  - Guard: check `InputLayerManager.IsLayerActive(InputLayer.Combat)` before firing

---

### S2-D │ RECONNECT FLOW

- [ ] **[FILE]** `Scripts/Networking/ServerGameManager.cs`
  - Add `private Dictionary<string, PlayerRegistryData> _disconnectedPlayers` (backendId → data)
  - `OnPlayerDisconnected()`: save data to `_disconnectedPlayers` before despawn
  - Add `[Server] OnPlayerReconnected(NetworkConnection conn, string backendPlayerId)`:
    - Look up `_disconnectedPlayers[backendPlayerId]`
    - Re-spawn player at last known position or nearest spawn point
    - Re-assign ownership
    - Re-register with `RegistryService`
    - Remove from `_disconnectedPlayers`
  - Timeout: if player does not reconnect within 60s → remove from `_disconnectedPlayers`

- [ ] **[FILE]** `Scripts/Networking/Player/ClientNetworkHandler.cs`
  - Add `bool isReconnect` field to `PlayerRegistryData` sent to server
  - On reconnect: send `isReconnect = true` + `backendPlayerId` so server can match

- [ ] **[NEW]** `Scripts/UI/ReconnectOverlay.cs`
  - Show `"Reconnecting... (attempt N/3)"` overlay when FishNet connection drops mid-match
  - Subscribe `NetworkGameManager` connection state events
  - Hide when reconnected or when max retries reached
  - `"Return to Home"` button available after 10s

---

## ═══════════════════════════════════════════════
## SPRINT 3 — MEDIUM PRIORITY (Week 5–6)
## ═══════════════════════════════════════════════

### S3-A │ DS SCENE VALIDATOR TOOL

- [ ] **[NEW]** `Scripts/Editor/Tools/NightHuntSceneValidator.cs`
  - `[MenuItem("NightHunt/Validate/DS Boot Scene (00_DS_Boot)")]`
    - Required: `NetworkManager`, `ServerBootstrap`
    - Warn if `fallbackServerId` is still default value
  - `[MenuItem("NightHunt/Validate/Gameplay Scene (02_Map_01)")]`
    - Required: `ServerGameManager`, `MatchPhaseManager`, `MatchEndManager`, `ScoringSystem`
    - Required: `SpawnSystem`, `RespawnSystem`, `GameBootstrap`, `BossSpawnManager`
    - Required: `ZoneSystem`, `ObjectiveSystem`, `WorldSpawnManager`, `AntiCampingSystem`
    - Required: `TeamAssignmentSystem`, `BeaconManager`
    - Warn if any `[SerializeField]` reference on above components is null
  - `[MenuItem("NightHunt/Validate/All Build Scenes")]`
    - Validate all scenes in `BuildScript.DsScenes` + `BuildScript.ClientScenes`
  - Display results in `EditorWindow` with pass ✅ / fail ❌ per component
  - Implement `IPreprocessBuildWithReport` → run validation before every build, fail build on error

- [ ] **[FILE]** `Scripts/Editor/BuildScript.cs`
  - Add pre-build validation call: `NightHuntSceneValidator.ValidateAll()` → throw if errors found
  - Add `02_Map_01 1.unity` cleanup note (duplicate scene file detected in project)

---

### S3-B │ DS SCENE AUTO-SETUP TOOL

- [ ] **[FILE]** `Scripts/Editor/Tools/WorldSpawnSceneSetupTool.cs`
  - Extend to add all required gameplay components:
    - `MatchEndManager` with `_phaseManager` + `_scoringSystem` auto-wired
    - `ScoringSystem` with default score configs
    - `RespawnSystem` with `_matchEndManager` auto-wired
    - `AntiCampingSystem`
    - `GameBootstrap` with all `[SerializeField]` refs auto-assigned
  - Add spawn point placement wizard: grid-based, configurable count per team
  - Add `LockdownZone` setup: center = scene origin, radius = configurable
  - All tool UI strings → English

---

### S3-C │ GITHUB ACTIONS IMPROVEMENTS

- [ ] **[CI]** `.github/workflows/build-dedicated-server.yml`
  - Add `validate-secrets` job before `build-unity`:
    ```yaml
    validate-secrets:
      runs-on: ubuntu-latest
      steps:
        - name: Check required secrets
          run: |
            [ -n "${{ secrets.UNITY_LICENSE }}" ] || (echo "UNITY_LICENSE missing" && exit 1)
            [ -n "${{ secrets.UNITY_EMAIL }}" ]   || (echo "UNITY_EMAIL missing" && exit 1)
            [ -n "${{ secrets.UNITY_PASSWORD }}" ]|| (echo "UNITY_PASSWORD missing" && exit 1)
            [ -n "${{ secrets.GHCR_TOKEN }}" ]    || (echo "GHCR_TOKEN missing" && exit 1)
            [ -n "${{ secrets.VPS_HOST }}" ]      || (echo "VPS_HOST missing" && exit 1)
            [ -n "${{ secrets.VPS_SSH_KEY }}" ]   || (echo "VPS_SSH_KEY missing" && exit 1)
    ```
  - Add `needs: [validate-secrets]` to `build-unity` job
  - Add rollback step in `notify-backend`: if smoke test fails → revert `DS_IMAGE_REF` to previous version
  - Add Discord/Slack webhook notification on success and failure
  - Add build number tracking: increment `BUILD_NUMBER` env var per run

- [ ] **[CI]** `docker/dedicated-server/scripts/entrypoint.sh`
  - Add SIGTERM graceful shutdown handler:
    ```bash
    trap 'echo "[DS] SIGTERM received — waiting for Unity shutdown..."; wait $PID; exit 0' TERM INT
    exec "${DS_BINARY}" ... &
    PID=$!
    wait $PID
    ```
  - Remove `exec` prefix (exec replaces shell, preventing trap from working)
  - Add 30s timeout: if Unity does not exit after SIGTERM → send SIGKILL

---

### S3-D │ FOW DEPLOYABLE FIX

- [ ] **[FILE]** `Scripts/Gameplay/Deployables/BaseDeployable.cs`
  - Change `OwnerTeamId` to `SyncVar<int>`:
    ```csharp
    private readonly SyncVar<int> _ownerTeamId = new SyncVar<int>(-1);
    public int OwnerTeamId => _ownerTeamId.Value;
    ```
  - `_ownerTeamId.OnChange += OnOwnerTeamChanged`
  - `OnOwnerTeamChanged()` → find `FogTeamVisibilityBinder` on this GO → `RefreshVisibilityForLocalTeam()`
  - Ensure `OwnerTeamId` is set on server BEFORE `NetworkObject.Spawn()`

- [ ] **[FILE]** `Scripts/Gameplay/Deployables/VisionWard.cs`
  - Verify `FogTeamVisibilityBinder` component is present on prefab
  - Verify `FogOfWarRevealer3D` is present and configured

---

### S3-E │ NAMING & CODE ORGANIZATION

- [ ] **[FILE]** `Scripts/Networking/RegistryService.cs`
  - Rename internal log prefix: `"[RegistryService]"` (already correct, verify consistency)
  - All Vietnamese comments → English

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Core/Bridge/GameplaySystemsBridge.cs`
  - All Vietnamese comments → English
  - Verify `IsReady` is set to `true` only after ALL systems are non-null

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Systems/Inventory/InventorySystem.cs`
  - All Vietnamese comments → English
  - Rename `_enableDebugLogs` → `_debugLogs` for consistency with other systems

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Systems/Weapon/WeaponSystem.cs`
  - All Vietnamese comments → English
  - Verify partial class files all use consistent naming

- [ ] **[TASK]** Rename duplicate scene file
  - `Assets/_Night_Hunt/Scenes/02_Map_01 1.unity` → delete or rename to `02_Map_02.unity` if intentional
  - Update `BuildScript.DsScenes` and `BuildScript.ClientScenes` accordingly

- [ ] **[TASK]** Standardize `[Header]` attribute strings across all scripts
  - All `[Header("...")]` → English only
  - All `[Tooltip("...")]` → English only
  - Pattern: `[Header("References")]`, `[Header("Settings")]`, `[Header("Debug")]`

---

### S3-F │ AUDIO SYSTEM CONSOLIDATION

- [ ] **[FILE]** `Scripts/Audio/AudioManager.cs`
  - Verify `AudioManager` is the single audio system (not Shift UI's audio)
  - Add `PlayMatchStart()`, `PlayMatchEnd()`, `PlayPhaseTransition()` convenience methods
  - Add `PlaySpectatorAmbience()` / `StopSpectatorAmbience()` for spectator mode
  - All log messages → English

- [ ] **[FILE]** `Scripts/Audio/AudioSettingsPanel.cs`
  - Verify settings are saved to `PlayerPrefs` with prefix `"NH_Audio_"`
  - Verify settings are loaded on `Start()`
  - All UI strings → English: `"Master Volume"`, `"Music Volume"`, `"SFX Volume"`, etc.

- [ ] **[TASK]** Audit Shift UI audio components in scene
  - Check `Canvas/Background Music` and `Canvas/UI Audio` GameObjects
  - If they use Shift UI audio → bridge to `AudioManager` or replace
  - Ensure no duplicate audio sources playing same clip

---

## ═══════════════════════════════════════════════
## SPRINT 4 — POLISH (Week 7–8)
## ═══════════════════════════════════════════════

### S4-A │ SPECTATOR FREE-FLY CAMERA

- [ ] **[FILE]** `Scripts/Gameplay/Input/Core/InputLayerManager.cs`
  - Add `InputState.SpectatorFreeCamera` to `ContextPresets`:
    ```csharp
    { InputState.SpectatorFreeCamera, InputLayer.Camera | InputLayer.UI }
    ```

- [ ] **[NEW]** `Scripts/Gameplay/Camera/Spectator/SpectatorFreeCameraController.cs`
  - WASD/arrow keys → move camera position
  - Mouse drag → rotate camera
  - Scroll wheel → zoom (FOV or dolly)
  - Clamp position within map bounds (`Collider` or `Bounds` config)
  - `[Tab]` → toggle between free-fly and follow-player mode

- [ ] **[FILE]** `Scripts/Gameplay/Input/Handlers/Spectator/SpectatorInputHandler.cs`
  - Add `OnToggleFreeCam` event
  - Bind `Tab` key to `OnToggleFreeCam`

---

### S4-B │ UI/UX FLOW IMPROVEMENTS

- [ ] **[FILE]** `Scripts/UI/UINavigator.cs`
  - Add `PanelType.GameHUD`, `PanelType.Results`, `PanelType.Spectator` to enum
  - Add `GoGameHUD()`, `GoResults()`, `GoSpectator()` methods
  - Add `UnityEvent OnGoGameHUD`, `OnGoResults`, `OnGoSpectator`
  - All comments → English

- [ ] **[FILE]** `Scripts/UI/DeathScreen.cs`
  - Show killer name from `CharacterLifecycleController.LastKillerName`
  - Show countdown to spectate: `"Spectating in 3s..."`
  - `"Spectate"` button → `SpectateManager.Instance.SwitchSpectatedPlayer(true)`
  - All UI strings → English

- [ ] **[FILE]** `Scripts/UI/LoadingOverlay.cs`
  - All status strings → English
  - Verify fade-in/fade-out works correctly

- [ ] **[SCENE]** `01_Home.unity` — Settings panels
  - `Settings/Content/Panels/Controls`: add `RebindActionUI` list
  - `Settings/Content/Panels/Audio`: verify all labels are English
  - `Settings/Content/Panels/Visuals`: verify all labels are English
  - `Settings/Content/Panels/Gameplay`: verify all labels are English

- [ ] **[SCENE]** `01_Home.unity` — Toast/Modal
  - `CanvasDontDestroy/ModalWindown` → rename to `CanvasDontDestroy/ModalWindow` (typo fix)
  - `CanvasDontDestroy/ModalWindown/Content/Content List/Buttons/ThridButton` → rename to `ThirdButton`
  - Verify all modal text is English

---

### S4-C │ FISHNET PRO V4 — BEST PRACTICES AUDIT

- [ ] **[FILE]** `Scripts/Networking/NetworkGameManager.cs`
  - Verify `Tugboat` transport is configured correctly for both DS and Relay modes
  - Add `[ServerRpc]` / `[ObserversRpc]` attribute audit — ensure no missing `[Server]` guards
  - Verify `NetworkManager.IsServerStarted` checks before all server-only operations

- [ ] **[FILE]** `Scripts/Gameplay/Match/MatchPhaseManager.cs`
  - Verify `networkPhaseDuration.Value` is read by clients (not recalculated)
  - Add late-join sync: client joining mid-phase receives correct `PhaseRemainingTime`
  - `RpcMatchCountdown` → verify fires on all clients including late-joiners

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Systems/Inventory/InventorySystem.cs`
  - Verify `SyncList<ItemInstanceData>` callbacks fire correctly on client
  - Verify `OnItemAdded`/`OnItemRemoved` events fire after sync (not before)

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Systems/Weapon/WeaponSystem.cs`
  - Verify `SyncDictionary<WeaponSlotType, string>` is properly initialized
  - Verify `SyncVar<WeaponSlotType?> _activeSlot` nullable sync works in FishNet Pro V4
  - Audit all `[ServerRpc]` calls — ensure `RequireOwnership = true` wher




  .... Còn tiếp bổ sung thêm 