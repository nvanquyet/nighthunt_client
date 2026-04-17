# NIGHT HUNT — MASTER REFACTOR CHECKLIST V2
> Full audit: Unity 6 + FishNet Pro V4 + Dedicated Server + WebSocket backend.
> Language: **English only** (all logs, comments, UI text, variable names).
> All todos are actionable — each maps to a specific file, scene, or asset.

---

## LEGEND
- `[FILE]`  = Modify existing script
- `[NEW]`   = Create new script
- `[SCENE]` = Modify scene hierarchy / Inspector wiring
- `[ASSET]` = ScriptableObject / prefab / config change
- `[CI]`    = GitHub Actions / Docker / build pipeline
- `[DEL]`   = Delete / remove dead code or asset
- `[RENAME]`= Rename file or symbol for clarity

Priority: 🔴 Critical (blocking) → 🟠 High (gameplay-breaking) → 🟡 Medium → 🟢 Polish

---

## ═══════════════════════════════════════
## SPRINT 1 — CRITICAL BLOCKERS
## Target: 1–2 weeks
## ═══════════════════════════════════════

---

### S1-A │ LANGUAGE STANDARDIZATION — English Only

Every `.cs` file under `Assets/_Night_Hunt/Scripts/` must have:
- All `Debug.Log` / `LogWarning` / `LogError` strings in English
- All `[Header]`, `[Tooltip]` attributes in English  
- All `[SerializeField]` field names following `camelCase` / `_camelCase` convention
- All comments in English
- All UI-facing strings defined via localization keys or English literals

**Automated scan first:**
- [ ] **[TASK]** Run global search for Vietnamese characters across all `.cs` files
  - Pattern: `[àáâãèéêìíòóôõùúýăđơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ]`
  - Build a list of every file with a hit before editing

**Per-file cleanup (after scan):**
- [ ] **[FILE]** `Scripts/Core/GameManager.cs` — comments contain garbled Vietnamese due to encoding; translate all to English
- [ ] **[FILE]** `Scripts/Server/ServerBootstrap.cs` — header comment, all inline comments → English
- [ ] **[FILE]** `Scripts/Gameplay/Match/MatchPhaseManager.cs`
  - `[Tooltip]` on `_delayBeforeFirstPhase` → English
  - `[Tooltip]` on `phaseConfigs` → English
  - Countdown broadcast string `"Bắt Đầu!"` → `"GO!"`
- [ ] **[FILE]** `Scripts/Gameplay/Match/MatchEndManager.cs` — all inline comments → English
- [ ] **[FILE]** `Scripts/Networking/ServerGameManager.cs` — all inline comments → English
- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Inventory/InventoryScreen.cs` — all comments + any hardcoded strings → English
- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Inventory/UIRootController.cs` → English
- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Inventory/UIDomainBridge.cs` → English
- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Inventory/PlayerHUDPanel.cs` — all `[Header]`/`[Tooltip]` → English
- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Combat/CombatHUDPanel.cs` → English
- [ ] **[FILE]** `Scripts/Gameplay/FogOfWar/FogTeamVisibilityBinder.cs` → English
- [ ] **[FILE]** `Scripts/Gameplay/FogOfWar/FogVisionBinder.cs` — `[Header]` / `[Tooltip]` → English
- [ ] **[FILE]** `Scripts/Gameplay/Input/Core/InputLayerManager.cs` — context table comment → English
- [ ] **[FILE]** `Scripts/UI/UINavigator.cs` — all `[Tooltip]` / `[Header]` / comments → English
- [ ] **[FILE]** `Scripts/UI/MatchLoadingOverlay.cs` — all comments + status string literals → English
- [ ] **[FILE]** `Scripts/UI/ResultsView.cs` — verify fully English (mostly OK, confirm)
- [ ] **[FILE]** `Scripts/Gameplay/Scoring/ScoringSystem.cs` — `[Tooltip]` on `_scoreConfigList` → English
- [ ] **[FILE]** `Scripts/Gameplay/Core/GameBootstrap.cs` — all comments → English
- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Core/Bridge/GameplaySystemsBridge.cs` → English
- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Systems/Inventory/InventorySystem.cs` → English
- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Systems/Weapon/WeaponSystem.cs` → English

---

### S1-B │ SCENE LOAD + CONNECTION FLOW FIX

**Root Cause:** `SceneLoader.LoadGame()` uses `UnityEngine.SceneManager.LoadScene()` but
`NetworkGameManager` was previously listening to FishNet's `OnLoadEnd` (already fixed per current code).
Current fix uses `SceneManager.sceneLoaded` — verify and lock this down.

- [ ] **[FILE]** `Scripts/Networking/NetworkGameManager.cs`
  - Confirm `USceneManager.sceneLoaded` subscription is in `OnEnable()` / `Start()` — not `Awake()`
  - Confirm `USceneManager.sceneLoaded` is unsubscribed in `OnDestroy()` / `OnDisable()`
  - Confirm callback checks `scene.name.StartsWith("02_Map_")` before setting `_gameSceneLoaded = true`
  - Add `public static void ResetConnectionFlags()` if not already present:
    ```csharp
    public static void ResetConnectionFlags()
    {
        s_dsReadyReceived = false;
    }
    ```
  - In `LoadHome()`: ensure `s_dsReadyReceived = false` is called (verify already present)
  - Add `_matchEnded` flag check in `OnClientConnectionState(Stopped)` to skip `TryRetry()` after a clean match end
  - All log strings → English

- [ ] **[FILE]** `Scripts/State/RoomState.cs`
  - In `ClearRoom()`: call `NetworkGameManager.ResetConnectionFlags()` if instance is non-null
  - Ensures `s_dsReadyReceived` is cleared on every fresh matchmaking cycle

- [ ] **[FILE]** `Scripts/UI/MatchLoadingOverlay.cs`
  - **Decouple** `SceneLoader.LoadGame()` from `ShowInternal()`:
    - `ShowInternal()` should NOT call `SceneLoader.LoadGame()`
    - Caller (`MatchFlowCoordinator`) calls `Show(mapId)` first, then `SceneLoader.LoadGame(mapId)` separately
  - In `ShowInternal()`: check `NetworkGameManager.s_dsReadyReceived` (make it internal readable)
    → if already true, immediately call `MarkDsReady()` without waiting for event
  - Add `RetryButton` GameObject reference in Inspector
  - Add `OnRetryClicked()` handler that resets and reloads
  - All status strings must be English: `"Starting game server..."`, `"Connecting to server..."`, `"Spawning players..."`, `"All players ready!"`
  - All `[Header]` / `[Tooltip]` → English

---

### S1-C │ MATCH FLOW COORDINATOR (NEW)

**Problem:** Match-flow logic (OnMatchFound, OnMatchReady, OnDsReady, OnMatchEnded WS events)
is scattered across `HomeView`, `PartyController`, `CustomLobbyView`, and `NetworkGameManager`.
This makes reconnect, retry, and ELO update flows fragile.

- [ ] **[NEW]** `Scripts/UI/MatchFlowCoordinator.cs`
  ```csharp
  // SingletonPersistent<MatchFlowCoordinator>
  // Subscribes ALL WS match events in OnEnable()
  // Unsubscribes ALL in OnDisable()
  ```
  Responsibilities:
  - `OnMatchFound`  → `MatchFoundOverlay.Instance?.Show(evt)`
  - `OnMatchReady`  → populate `RoomState` → `MatchLoadingOverlay.Instance?.Show(mapId)` → `SceneLoader.LoadGame(mapId)` (sequential, not inside Show)
  - `OnDsReady`     → `MatchLoadingOverlay.Instance?.MarkDsReady()`
  - `OnMatchEnded`  → forward ELO/coins update to `ResultsView` via event
  - Owns `_matchHandled` bool to prevent double LoadHome
  - On `NetworkGameManager.OnClientConnectionState(Stopped)` if `_matchHandled` → skip `TryRetry()`

- [ ] **[FILE]** `Scripts/UI/HomeView.cs` — remove match-flow WS subscription logic; delegate to `MatchFlowCoordinator`
- [ ] **[FILE]** `Scripts/UI/PartyController.cs` — same
- [ ] **[FILE]** `Scripts/UI/CustomLobbyView.cs` — same

---

### S1-D │ GAME LOOP COMPLETION — ResultsView + ELO

- [ ] **[FILE]** `Scripts/UI/ResultsView.cs`
  - Verify `OnEnable()` subscribes `GameWebSocketService.Instance.OnMatchEnded += OnMatchEndedWsReceived`
  - Verify `OnDisable()` unsubscribes
  - `OnMatchEndedWsReceived(MatchEndedWsEvent evt)`:
    - Update `_eloChangeText` per player from `evt.eloChanges`
    - Update `SessionState.Instance.Coins` from `evt.coinsAwarded`
    - Show `_eloPanel` (hide by default, show only for Ranked_DS)
  - Add 10s fallback: if WS event not received → show `"Calculating..."` in `_eloChangeText`
  - Add `_matchEndHandled` bool to prevent double `LoadHome()` call
  - `NavigatePostMatch()`: set `_matchEndHandled = true` before `SceneLoader.LoadHome()`

- [ ] **[FILE]** `Scripts/Networking/NetworkGameManager.cs`
  - Subscribe `GameplayEventBus.Instance.Subscribe<MatchEndedEvent>` in `Start()`
  - On `MatchEndedEvent`: set `_matchEnded = true`
  - In `OnClientConnectionState(Stopped)`: if `_matchEnded` → skip retry, log `"[NetworkGameManager] Match ended — skipping reconnect retry"`
  - Reset `_matchEnded = false` in `LoadHome()`

---

### S1-E │ SCORE SYSTEM UNIFICATION

**Problem:** `ScoringSystem` (per-player score) and `MatchEndManager` (was tracking team scores separately).
`MatchEndManager` has already been refactored to read from `ScoringSystem` for team scores — verify fully.

- [ ] **[FILE]** `Scripts/Gameplay/Match/MatchEndManager.cs`
  - Confirm NO `_teamKillScore` or `_teamObjectiveScore` dictionaries remain
  - Confirm `GetTotalScore(teamId)` reads from `_scoringSystem.GetTeamScore(teamId)` only
  - Confirm `GetFinalResults()` uses `_scoringSystem.GetPlayerScore(objectId)` for all players
  - `_playerKillCount` and `_playerDeathCount` dictionaries: keep (for scoreboard display per-player)
  - All comments → English

- [ ] **[FILE]** `Scripts/Gameplay/Scoring/ScoringSystem.cs`
  - Replace `SyncVar<string> scoreDataJson` with `SyncList<PlayerScore>` + separate `SyncList<TeamScore>`
    - Remove `JsonUtility.ToJson()` in `SyncScores()` — eliminates per-frame GC pressure
  - Add `public event Action<uint, int> OnPlayerScoreChanged` (networkObjectId, newScore) for real-time HUD
  - Add `public event Action<int, int> OnTeamScoreChanged` (teamId, newScore) for match HUD
  - `GetTeamScore(teamId)` must be `public` (already used by `MatchEndManager`)
  - All `[Tooltip]` → English

---

### S1-F │ GAMEBOOTSTRAP PHASE ACTIVATION FIX

- [ ] **[FILE]** `Scripts/Gameplay/Core/GameBootstrap.cs`
  - Verify `ActivatePhase1Systems()` actually calls:
    - `_worldSpawnManager?.BeginSpawnCycle()`
    - `_antiCampingSystem?.Activate()`
  - Verify `ActivatePhase2Systems()` actually calls:
    - `_bossSpawnManager?.ScheduleBossSpawn()`
    - `_objectiveSystem?.ActivateObjectives()`
  - Verify `ActivatePhase3Systems()` actually calls:
    - `_lockdownZone?.Activate()`
    - `_respawnSystem?.SetPhase3Mode()`
  - Add `OnValidate()` warning if any `[SerializeField]` reference is null
  - Remove any remaining `FindFirstObjectByType` calls in `Awake()` — replace with `[SerializeField]`
  - All comments / logs → English

---

### S1-G │ DEDICATED SERVER SCENE VALIDATOR (EDITOR TOOL)

- [ ] **[NEW]** `Scripts/Editor/Tools/NightHuntSceneValidator.cs`
  - `[MenuItem("NightHunt/Validate/DS Boot Scene")]`
  - Checks `00_DS_Boot.unity` contains:
    - `NetworkManager` with `Tugboat` transport
    - `ServerBootstrap` component with `NetworkManager` wired
    - NO `GameManager` (client-only), NO `AudioListener`, NO `Camera`
  - Checks `02_Map_01.unity` contains:
    - `GameBootstrap` with all `[SerializeField]` refs populated
    - `ServerGameManager` NetworkBehaviour
    - `MatchPhaseManager`, `MatchEndManager`, `ScoringSystem`
    - `SpawnSystem`, `RespawnSystem`
  - Checks `01_Home.unity` contains:
    - `GameManager` (SingletonPersistent) with all services wired
    - `MatchFlowCoordinator`
    - `PersistentUICanvas`
  - Implement `IPreprocessBuildWithReport` → fail build on error
  - All tool strings → English

- [ ] **[DEL]** `Assets/_Night_Hunt/Scenes/02_Map_01 1.unity` — duplicate scene file
  - Confirm it is NOT used in `BuildScript.DsScenes` or `BuildScript.ClientScenes` first
  - Then delete via `AssetDatabase.DeleteAsset()`

---

## ═══════════════════════════════════════
## SPRINT 2 — HIGH PRIORITY
## Target: week 3–4
## ═══════════════════════════════════════

---

### S2-A │ RECONNECT FLOW

**Current:** Disconnect → `TryRetry()` 2× → `LoadHome()`. No "rejoin" capability.

- [ ] **[FILE]** `Scripts/Networking/ServerGameManager.cs`
  - Add `private Dictionary<string, PlayerRegistryData> _disconnectedPlayers`
  - In `OnPlayerDisconnected(conn)`:
    - Check if match is still running (`_matchEnded == false`)
    - If yes: stash player data → `_disconnectedPlayers[backendId] = data`
    - Start 60s coroutine; if not reconnected in time → remove from dictionary, check elimination
  - Add `TryRejoinPlayer(conn, backendId)`:
    - Look up `_disconnectedPlayers[backendId]`
    - Re-spawn at last recorded position or nearest respawn point
    - Re-assign FishNet ownership
    - Remove from `_disconnectedPlayers`

- [ ] **[FILE]** `Scripts/Networking/Player/ClientNetworkHandler.cs`
  - Add `bool IsReconnect` field to `PlayerRegistryData` sent on connect
  - Server reads `IsReconnect = true` → route to `TryRejoinPlayer()` instead of fresh spawn

- [ ] **[NEW]** `Scripts/UI/ReconnectOverlay.cs`
  - Shows when FishNet `LocalConnectionState` drops to `Stopping`/`Stopped` mid-match
  - Text: `"Reconnecting... (attempt {N}/{max})"` in English
  - Subscribe `NetworkGameManager.OnRetryAttempt` event (add this event to NetworkGameManager)
  - `"Return to Home"` button visible after attempt 2 or after 15s
  - Hide overlay when reconnect succeeds

- [ ] **[FILE]** `Scripts/Networking/NetworkGameManager.cs`
  - Add `public event Action<int, int> OnRetryAttempt` (currentAttempt, maxAttempts)
  - Fire event in `TryRetry()` coroutine before each attempt
  - After `_maxRetries` exceeded: fire event with `(maxRetries, maxRetries)` → overlay shows home button

---

### S2-B │ SPECTATOR SYSTEM — FOW + CAMERA + HUD

#### FOW during Spectate

- [ ] **[FILE]** `Scripts/Gameplay/FogOfWar/FogVisionBinder.cs`
  - Subscribe `SpectateManager.Instance.OnSpectateStarted` in `OnEnable()` → `HandleSpectateStarted(NetworkPlayer target)`
  - Subscribe `SpectateManager.Instance.OnSpectateStopped` in `OnEnable()` → `HandleSpectateStopped()`
  - `HandleSpectateStarted()`:
    - If this binder belongs to local player → disable `_revealer`
    - Find target player's `FogVisionBinder` via `target.GetComponentInChildren<FogVisionBinder>()` → enable their `_revealer`
  - `HandleSpectateStopped()`:
    - Re-enable local `_revealer`
    - Disable target `_revealer` (restore their normal state based on IsAlive)
  - Unsubscribe in `OnDisable()`

#### Camera during Spectate

- [ ] **[FILE]** `Scripts/Gameplay/Camera/Spectator/GameCameraController.cs`
  - `SetCameraTarget(Transform root)`:
    - Search for child named `"CameraFollowTarget"` first
    - Fallback to `root` if not found
    - Log: `"[GameCameraController] Follow target set to {name}"`
  - On `SpectateManager.OnSpectateStarted`: call `CameraStateManager.ForceState(CameraState.Free)`
    - Prevents camera freeze when player was in WeaponAim state when they died
  - `FindFirstObjectByType<SpectatorInputHandler>()` in `Awake()`:
    - Already fixed via `[SerializeField]` — verify Inspector is wired in `02_Map_01.unity`
  - All log strings → English

- [ ] **[FILE]** `Scripts/Gameplay/Camera/CameraStateManager.cs`
  - Subscribe `SpectateManager.Instance.OnSpectateStarted` in `OnEnable()`
  - `HandleSpectateStarted()` → `ForceState(CameraState.Free)` to unlock rotation
  - Unsubscribe in `OnDisable()`
  - All comments → English

#### HUD during Spectate

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Inventory/UIRootController.cs`
  - On `SpectateManager.OnCurrentPlayerChanged(target)`:
    - Call `_playerHudPanel.Initialize(targetBridge)` with spectated player's bridge
    - Call `_combatHudPanel.Initialize(...)` with spectated player's weapon/combat system
    - Hide fire/reload buttons: spectator cannot act
  - On `SpectateManager.OnSpectateStopped()`:
    - Re-bind all panels to local player's bridge
    - Re-show combat buttons

- [ ] **[NEW]** `Scripts/UI/SpectatorHUD.cs`
  - Show/hide via `SpectateManager.IsSpectating`
  - Display: spectated player's name, team color badge, health bar, active weapon name
  - Navigation hint: `"[Tab] Next  [Q] Prev  [E] Exit Spectate"`
  - Subscribe `SpectateManager.OnCurrentPlayerChanged` → refresh display
  - Subscribe `SpectateManager.OnSpectateStarted` → `gameObject.SetActive(true)`
  - Subscribe `SpectateManager.OnSpectateStopped` → `gameObject.SetActive(false)`
  - All strings → English

- [ ] **[SCENE]** `02_Map_01.unity` — HUD hierarchy
  - Add `SpectatorHUD` GameObject under `GameHUD` canvas root
  - Default: `SetActive(false)`
  - Wire `SpectatorHUD` reference in `GameHUD.cs` Inspector

---

### S2-C │ FOW DEPLOYABLE FIX

- [ ] **[FILE]** `Scripts/Gameplay/Deployables/BaseDeployable.cs`
  - Change `OwnerTeamId` to `SyncVar<int>`:
    ```csharp
    private readonly SyncVar<int> _ownerTeamId = new SyncVar<int>(-1);
    public int OwnerTeamId => _ownerTeamId.Value;
    ```
  - On `SyncVar` change: fire `OnOwnerTeamChanged` → `FogTeamVisibilityBinder.RefreshVisibility()`
  - Ensure `_ownerTeamId` is set on server BEFORE `NetworkObject.Spawn()`
  - All comments → English

- [ ] **[FILE]** `Scripts/Gameplay/Deployables/VisionWard.cs`
  - Confirm `FogTeamVisibilityBinder` component is on prefab (add in prefab if missing)
  - Confirm `FogOfWarRevealer3D` is configured on prefab

---

### S2-D │ INPUT SYSTEM — KEY REBINDING

- [ ] **[FILE]** `Scripts/UI/Settings/ControlsSettingsPanel.cs`
  - Add `[SerializeField] private List<RebindActionUI> _rebindableActions;`
  - Add `SaveBindings()`:
    ```csharp
    string json = _inputActionAsset.SaveBindingOverridesAsJson();
    PlayerPrefs.SetString("NH_InputBindings", json);
    PlayerPrefs.Save();
    ```
  - Add `LoadBindings()`: load from PlayerPrefs `"NH_InputBindings"` on `Start()`
  - Add `ResetAllBindings()`: `_inputActionAsset.RemoveAllBindingOverrides()` → `SaveBindings()`
  - Add conflict detection: warn user if two actions share same key after rebind
  - All UI label strings → English: `"Mouse Sensitivity"`, `"Invert Y"`, `"Reset to Defaults"`, `"Key Bindings"`

- [ ] **[NEW]** `Scripts/UI/Settings/RebindActionUI.cs`
  - `[SerializeField] InputActionReference _actionRef`
  - `[SerializeField] TextMeshProUGUI _bindingText` — shows current binding (e.g. `"LMB"`, `"Space"`)
  - `[SerializeField] Button _rebindButton` — starts interactive rebind
  - `[SerializeField] Button _resetButton` — resets this action to default
  - `StartRebinding()`:
    - Show overlay: `"Press any key to bind {actionName}..."`
    - Call `InputActionRebindingExtensions.PerformInteractiveRebinding(action)`
    - On complete: `UpdateBindingDisplay()` → fire `OnBindingChanged`
  - `UpdateBindingDisplay()`: format binding path as human-readable (e.g. `"<Keyboard>/space"` → `"Space"`)
  - Fire `OnBindingChanged` → `ControlsSettingsPanel.SaveBindings()`

- [ ] **[SCENE]** `01_Home.unity` — Settings/Controls panel
  - Add `RebindActionUI` prefab entries for: Move (WASD), Sprint, Crouch, Jump, Roll, Fire, Aim, Reload, Interact (tap), Hold Interact, Inventory toggle, Camera lock
  - Wire `InputActionAsset` reference in `ControlsSettingsPanel` Inspector
  - Add `"Reset All Bindings"` button → `ControlsSettingsPanel.ResetAllBindings()`

---

### S2-E │ INPUT SYSTEM — PLATFORM AUTO-DETECT

- [ ] **[NEW]** `Scripts/Gameplay/Input/Core/PlatformInputDetector.cs`
  - `SingletonPersistent<PlatformInputDetector>`
  - `public enum InputPlatform { KeyboardMouse, Gamepad, Touch }`
  - `public InputPlatform Current { get; private set; }`
  - `public event Action<InputPlatform> OnPlatformChanged`
  - Detect on `Awake()`: `Application.isMobilePlatform || Touchscreen.current != null` → Touch
  - Subscribe `InputSystem.onDeviceChange` → re-detect on gamepad connect/disconnect
  - `ApplyPlatform()`:
    - Touch → show mobile HUD (joystick, fire button, zoom slider), hide keyboard hints
    - Gamepad → hide mobile HUD, show gamepad button hints
    - KeyboardMouse → hide mobile HUD, hide gamepad hints

- [ ] **[FILE]** `Scripts/UI/GameHUD.cs`
  - Subscribe `PlatformInputDetector.Instance.OnPlatformChanged` in `OnEnable()`
  - Toggle `_mobileMovementBridge.gameObject.SetActive(isMobile)`
  - Toggle `_mobileCameraDragArea.gameObject.SetActive(isMobile)`
  - Toggle fire button (mobile) visibility
  - All log messages → English

- [ ] **[FILE]** `Scripts/Gameplay/Input/Handlers/Combat/CombatInputHandler.cs`
  - Add `public void SetFirePressed(bool pressed)` — called by `FireButton` on mobile
  - Guard: check `InputLayerManager.IsLayerActive(InputLayer.Combat)` before setting

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/Combat/FireButton.cs`
  - `OnPointerDown` → `CombatInputHandler.Instance?.SetFirePressed(true)`
  - `OnPointerUp` → `CombatInputHandler.Instance?.SetFirePressed(false)`
  - Guard: if `InputLayerManager` is not active for Combat → silently skip

---

### S2-F │ PHASE TIMING — LATE JOIN FIX

- [ ] **[FILE]** `Scripts/Gameplay/Match/MatchPhaseManager.cs`
  - Confirm clients read `networkPhaseDuration.Value` for display (not recalculate locally)
  - Add `[ClientRpc]` `RpcSyncPhaseOnJoin(MatchPhaseState phase, float startTime, float duration)` called on `OnStartClient()`
    - Fires for late-joiners so they get current phase + remaining time
  - `PhaseWarning`: on late-join, check if `PhaseRemainingTime < warningThreshold` → immediately fire warning
  - All comments → English, all `[Tooltip]` → English

---

## ═══════════════════════════════════════
## SPRINT 3 — MEDIUM PRIORITY
## Target: week 5–6
## ═══════════════════════════════════════

---

### S3-A │ PREDICTION SYSTEM — INPUT BUFFER UNIFICATION

- [ ] **[FILE]** `Scripts/Gameplay/Character/Movement/RigidbodyPredictedMovement.cs`
  - Confirm `drag = 0` is enforced in `Awake()` (prevents divergence with `MovePosition`)
  - All comments → English

- [ ] **[TASK]** Audit `PredictedAttack` and `PredictedInteraction`:
  - Each has a separate `InputBuffer` → verify they tick on the same FishNet tick
  - If not: merge all predicted inputs into a single `InputData` struct passed to `CreateReplicateData()`
  - Goal: 1 `Replicate` call per tick carrying movement + attack + interaction flags together

- [ ] **[TASK]** Document which reconciliation strategy is active:
  - `HybridReconciliation`, `SmoothReconciliation`, or `SnapReconciliation`
  - Add `[Header("Reconciliation Strategy")]` + comment in `RigidbodyPredictedMovement.cs`

---

### S3-B │ WORLD ITEM / LOOT PERFORMANCE

- [ ] **[TASK]** Audit `WorldDropManager`, `WorldSpawnManager`, `WorldContainer`, `WorldItem`:
  - All are `NetworkBehaviour` — count max spawned NetworkObjects per match
  - If > 50 active at once → consider pool-based approach (spawn pool, recycle on pick-up)
  
- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Loot/WorldSpawnManager.cs`
  - Add max-active-items limit via Inspector `[SerializeField] private int _maxActiveItems = 100`
  - When limit reached: despawn oldest unclaimed item before spawning new one
  - All comments → English

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Systems/Inventory/ItemDatabase.cs`
  - Replace `Resources.Load()` with async load (Addressables or `Resources.LoadAsync`)
  - Load during `MatchLoadingOverlay` stage (before player spawn) to avoid frame spike
  - Log: `"[ItemDatabase] Load complete — {count} items registered"` in English

---

### S3-C │ IK + ANIMATION SYSTEM AUDIT

- [ ] **[TASK]** Open `Character 01.prefab` in Inspector and verify:
  - Which IK package is being used: Unity Animation Rigging or FinalIK
  - Constraints present: AimIK, FootIK, HandIK
  - Rig builder order is correct (Rig Builder before Animator)

- [ ] **[FILE]** `Scripts/Gameplay/Character/CharacterRig.cs`
  - Expose `RigBuilder` reference
  - Add `SetAimTarget(Transform target)` used by `CameraStateManager.WeaponAim` state
  - Add `Enable()` / `Disable()` to toggle rig (save CPU when dead or spectating)
  - All comments → English

- [ ] **[FILE]** `Scripts/Gameplay/Character/CharacterAnimationController.cs`
  - Confirm animation state IS synced via movement state (not duplicated via NetworkAnimator)
  - If `NetworkAnimator` is present on prefab: remove it — movement-state-driven animation is sufficient
  - All comments → English

---

### S3-D │ AUDIO SYSTEM CONSOLIDATION

**Problem:** Project has both custom `AudioManager` and Shift UI package audio system — potential conflict.

- [ ] **[TASK]** Open `01_Home.unity` scene:
  - Find any `Canvas/Background Music` or `Canvas/UI Audio` GameObjects from Shift UI package
  - Determine if they play audio independently of `AudioManager`
  - Decision: bridge or remove Shift UI audio components (document decision in `AudioManager.cs`)

- [ ] **[FILE]** `Scripts/Audio/AudioManager.cs`
  - Add `[Header("Shift UI Audio Bridge")]` section
  - If bridging: route Shift UI audio events through `AudioManager.PlayUI(clip, volume)`
  - If removing: add `[MenuItem("NightHunt/Audio/Remove Shift UI Audio Sources")]` tool
  - All log strings → English

- [ ] **[FILE]** `Scripts/Audio/AudioSettingsPanel.cs`
  - Confirm Master / Music / SFX volumes saved to `PlayerPrefs` with prefix `"NH_Audio_"`
  - `LoadSettings()` called in `Start()` / `OnEnable()`
  - `SaveSettings()` called on slider `OnValueChanged`
  - All UI label strings → English: `"Master Volume"`, `"Music Volume"`, `"SFX Volume"`, `"Ambient Volume"`

- [ ] **[TASK]** Audit `CharacterAudioController`, `CombatAudioController`, `WeaponAudioController`:
  - All three are separate `MonoBehaviour` → cache all component lookups in `Awake()`
  - Verify no cross-calls between them use `GetComponent<>` at runtime

---

### S3-E │ GITHUB ACTIONS + BUILD PIPELINE

- [ ] **[CI]** `.github/workflows/build-dedicated-server.yml`
  - Add `validate-secrets` job before `build-unity`:
    ```yaml
    validate-secrets:
      runs-on: ubuntu-latest
      steps:
        - name: Check required secrets
          run: |
            if [ -z "${{ secrets.UNITY_LICENSE }}" ]; then echo "UNITY_LICENSE missing"; exit 1; fi
            if [ -z "${{ secrets.GHCR_TOKEN }}" ]; then echo "GHCR_TOKEN missing"; exit 1; fi
            if [ -z "${{ secrets.DS_DEPLOY_HOST }}" ]; then echo "DS_DEPLOY_HOST missing"; exit 1; fi
    ```
  - Add build version tracking: generate version string `YYYY.MM.DD-{run_number}` and embed in output name
  - Add comment: "Trigger: push with [build-ds] in commit message OR workflow_dispatch" in English

- [ ] **[CI]** `docker/dedicated-server/scripts/entrypoint.sh` (if exists, else create it)
  - Add SIGTERM graceful shutdown:
    ```bash
    trap 'echo "[DS] SIGTERM received — initiating graceful shutdown"; kill -TERM $UNITY_PID' TERM
    wait $UNITY_PID
    ```
  - Add 30s timeout: if Unity doesn't exit after SIGTERM → send SIGKILL
  - All echo strings → English

- [ ] **[FILE]** `Scripts/Editor/BuildScript.cs`
  - Add pre-build validation call: `NightHuntSceneValidator.ValidateAll()` before `BuildPipeline.BuildPlayer()`
  - Add note about duplicate `02_Map_01 1.unity` scene (must be excluded from builds)
  - All comments → English (verify fully)

---

### S3-F │ NAMING, CODE ORGANIZATION, DEAD CODE

- [ ] **[RENAME]** `InputState.DroneControl` — no `DroneInputHandler` exists; either:
  - Remove from `InputLayerManager.ContextPresets` dict, or
  - Implement `DroneInputHandler` if planned
  - Leave a comment explaining the decision

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Systems/Inventory/InventorySystem.cs`
  - Rename `_enableDebugLogs` → `_debugLogs` for consistency with other systems

- [ ] **[TASK]** Audit all `[Header]` attribute strings across all `.cs` files for consistency:
  - Standard pattern: `[Header("References")]`, `[Header("Settings")]`, `[Header("Debug")]`, `[Header("Events")]`
  - Replace any non-English or inconsistent header strings

- [ ] **[FILE]** `Scripts/Networking/RegistryService.cs`
  - All log prefix strings → `"[RegistryService]"` (English, consistent)

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Core/Bridge/GameplaySystemsBridge.cs`
  - Verify `IsReady` is set to `true` ONLY after ALL system references are non-null
  - Add explicit null-check log: `"[GameplaySystemsBridge] {systemName} is null — bridge not ready"` in English

---

## ═══════════════════════════════════════
## SPRINT 4 — POLISH
## Target: week 7–8
## ═══════════════════════════════════════

---

### S4-A │ SPECTATOR FREE-FLY CAMERA (OPTIONAL)

- [ ] **[FILE]** `Scripts/Gameplay/Input/Core/InputLayerManager.cs`
  - Add `InputState.SpectatorFreeCamera` context:
    - Camera ON, Spectator ON, UI ON — everything else OFF

- [ ] **[NEW]** `Scripts/Gameplay/Camera/Spectator/SpectatorFreeCameraController.cs`
  - WASD / arrow keys → translate camera position (speed adjustable via scroll)
  - Mouse look → rotate camera
  - `[Tab]` → toggle between free-fly and follow-player mode
  - Deactivate when `SpectateManager.OnSpectateStopped` fires

- [ ] **[FILE]** `Scripts/Gameplay/Input/Handlers/Spectator/SpectatorInputHandler.cs`
  - Add `OnToggleFreeCam` event (Action)
  - Bind `Tab` key to `OnToggleFreeCam`

---

### S4-B │ UI/UX FLOW IMPROVEMENTS

- [ ] **[FILE]** `Scripts/UI/UINavigator.cs`
  - Add `PanelType.Spectator` to enum if not already present
  - All `[Tooltip]` / `[Header]` / comments → English

- [ ] **[FILE]** `Scripts/UI/DeathScreen.cs`
  - Show killer name from `CharacterLifecycleController.LastKillerName`
  - Show respawn countdown timer (read from `RespawnSystem.GetRespawnTime(localPlayer)`)
  - All UI label strings → English: `"You were eliminated by {name}"`, `"Respawning in {N}s..."`

- [ ] **[SCENE]** `01_Home.unity` — Settings panels
  - `Settings/Content/Panels/Audio` → verify all labels are English, wire `AudioSettingsPanel`
  - `Settings/Content/Panels/Controls` → add `RebindActionUI` list (from S2-D)
  - `Settings/Content/Panels/Graphics` → verify all labels are English
  - `CanvasDontDestroy/ModalWindown` → **rename** to `CanvasDontDestroy/ModalWindow` (typo fix)

- [ ] **[FILE]** `Scripts/UI/LoadingOverlay.cs`
  - All status strings → English
  - Verify fade-in / fade-out timing matches `MatchLoadingOverlay` fade

- [ ] **[FILE]** `Scripts/UI/PingDisplay.cs`
  - Label: `"Ping: {N} ms"` in English (not Vietnamese `"Độ trễ"`)

- [ ] **[FILE]** `Scripts/UI/ToastService.cs`
  - Verify all default toast messages are English templates

---

### S4-C │ FISHNET PRO V4 BEST PRACTICES AUDIT

- [ ] **[FILE]** `Scripts/Networking/NetworkGameManager.cs`
  - Verify `Tugboat` transport is configured for DS mode (not Relay shim)
  - Verify all server-only operations guard with `if (!IsServer) return;`
  - Verify all `[ServerRpc]` calls originate from owner-only context (`[ServerRpc(RequireOwnership = true)]`)

- [ ] **[FILE]** `Scripts/Gameplay/Match/MatchPhaseManager.cs`
  - Confirm `networkPhaseDuration.Value` is the canonical source on client (not local recalc)
  - Confirm `RpcMatchCountdown` is called via `ObserversRpc` (all clients including late-joiners)
  - All `[ObserversRpc]` should pass `Channel.Reliable` for phase transitions

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Systems/Inventory/InventorySystem.cs`
  - Verify `SyncList<ItemInstanceData>` callbacks fire correctly on clients
  - Verify `OnItemAdded` / `OnItemRemoved` events fire AFTER `SyncList` callback (not before)

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/Systems/Weapon/WeaponSystem.cs`
  - Verify `SyncDictionary<WeaponSlotType, string>` initialized before `OnStartNetwork`
  - Verify `WeaponSystem.Fire.cs` checks `IsOwner` before calling `FireServerRpc()`
  - Verify hitscan raycast runs on server only (not replicated client-side for authority)

---

### S4-D │ ZONE + BOSS + OBJECTIVE POLISH

- [ ] **[FILE]** `Scripts/Gameplay/Zone/ZoneSystem.cs`
  - Add `[SerializeField] private MatchPhaseManager _phaseManager` (avoid `FindFirstObjectByType`)
  - Zones only activate in Phase 3 (Lockdown) — validate subscription to `OnPhaseStarted`
  - All comments → English

- [ ] **[FILE]** `Scripts/Gameplay/Boss/BossSpawnManager.cs`
  - Add `[SerializeField] private MatchPhaseManager _phaseManager`
  - Boss spawns only in Phase 2 (Hunt) — validate subscription to `OnPhaseStarted`
  - Verify boss loot drop references `WorldDropManager.SpawnDrop()` correctly
  - All comments → English

- [ ] **[FILE]** `Scripts/Gameplay/Objective/ObjectiveSystem.cs`
  - Confirm `CaptureZoneObjective`, `EMPNodeObjective`, `RadarStationObjective` all implement `IObjective`
  - Confirm capture/EMP/radar awarding delegates to `ScoringSystem.AwardObjectiveCapture()` (not direct score add)
  - All comments → English

- [ ] **[FILE]** `Scripts/UI/BossHUDPanel.cs`
  - Subscribe `BossController.OnHealthChanged` for live health bar update
  - Subscribe `GameplayEventBus.BossKilledEvent` → hide panel
  - Show on `GameplayEventBus.BossSpawnedEvent`
  - All strings → English: `"Boss Defeated!"`, `"BOSS HP"`

---

### S4-E │ MINIMAP + NAMEPLATE

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/MinimapUI.cs`
  - Verify enemy players are hidden on minimap (FOW-awareness)
  - Ally players: show on minimap always
  - Boss / objectives: show as distinct icons
  - All log strings → English

- [ ] **[FILE]** `Scripts/Gameplay/GameplaySystems/UI/PlayerNameplateUI.cs`
  - Hide nameplate for enemy players (FOW-aware: visible only if enemy is in FOW reveal zone)
  - Show for allies always
  - All log strings → English

---

## ═══════════════════════════════════════
## CROSS-CUTTING CONCERNS (anytime)
## ═══════════════════════════════════════

### CC-1 │ ComponentResolver Usage Audit
- [ ] **[TASK]** Run grep for remaining `FindFirstObjectByType<>` calls in `Scripts/`:
  - List all occurrences
  - Replace each with `[SerializeField]` + Inspector wiring **or** cached singleton reference
  - Exception: editor tools only (`NightHuntSceneValidator.cs`, etc.)

### CC-2 │ Null-Safety in Singleton Access
- [ ] **[TASK]** Run grep for `Instance?.` patterns on singletons
  - Singletons must exist in scene — if `Instance` is null, it is a scene setup error
  - Replace `Instance?.DoThing()` with:
    ```csharp
    if (Instance == null) { Debug.LogError("[ClassName] Instance not found"); return; }
    Instance.DoThing();
    ```

### CC-3 │ Event Subscription / Unsubscription Audit
- [ ] **[TASK]** For every `event Action` subscription:
  - Ensure subscribe is in `OnEnable()` (not `Start()`)
  - Ensure unsubscribe is in `OnDisable()` (not `OnDestroy()`)
  - Exception: static events (subscribe in `Awake()`, unsubscribe in `OnDestroy()`)

### CC-4 │ Inspector Wiring Verification (02_Map_01.unity)
- [ ] **[SCENE]** `02_Map_01.unity`
  - `GameBootstrap` → all `[SerializeField]` slots filled
  - `GameCameraController._spectatorInput` → wired
  - `GameCameraController._virtualCamera` → wired
  - `MatchEndManager._phaseManager` → wired
  - `MatchEndManager._scoringSystem` → wired
  - `BossSpawnManager` → `[SerializeField] _phaseManager` wired
  - `ZoneSystem` → `[SerializeField] _phaseManager` wired
  - `ObjectiveSystem` → `[SerializeField] _scoringSystem` wired

### CC-5 │ Inspector Wiring Verification (01_Home.unity)
- [ ] **[SCENE]** `01_Home.unity`
  - `GameManager` → all services and state wired
  - `MatchFlowCoordinator` → `MatchFoundOverlay`, `MatchLoadingOverlay`, `ResultsView` refs wired
  - `PersistentUICanvas` → `MatchLoadingOverlay`, `LoadingOverlay`, `GameModalWindow` wired
  - `ControlsSettingsPanel` → `InputActionAsset` wired, `_rebindableActions` list populated
  - `AudioSettingsPanel` → `AudioManager` wired

### CC-6 │ Inspector Wiring Verification (00_DS_Boot.unity)
- [ ] **[SCENE]** `00_DS_Boot.unity`
  - `ServerBootstrap._networkManager` → wired
  - NO `GameManager`, NO `UINavigator`, NO `AudioManager` present
  - NO `Camera` or `AudioListener` (headless server)
  - `ServerUISuppressor` present and active

---

## ═══════════════════════════════════════
## ARCHITECTURE DIAGRAM — SYSTEM MAP
## ═══════════════════════════════════════

```
═══════════════════════════════════════════════════════
  NIGHT HUNT — SYSTEM ARCHITECTURE (Post-Refactor)
═══════════════════════════════════════════════════════

[01_Home Scene] ─────────────────────────────────────
  GameManager (SingletonPersistent)
  ├── AuthService         → JWT login
  ├── RoomService         → matchmaking API
  ├── FriendService       → friend list
  ├── PartyService        → party management
  ├── GameWebSocketService → WS events from backend
  ├── SessionState        → local user data
  └── RoomState           → current match room data

  MatchFlowCoordinator (SingletonPersistent) [NEW]
  ├── OnMatchFound  → MatchFoundOverlay.Show()
  ├── OnMatchReady  → MatchLoadingOverlay.Show() + SceneLoader.LoadGame()
  ├── OnDsReady     → MatchLoadingOverlay.MarkDsReady()
  └── OnMatchEnded  → ResultsView ELO/coins update

  UINavigator → PanelType (Login / Home / Lobby / Party / Settings)
  MatchLoadingOverlay (decoupled from SceneLoader)
  ResultsView (subscribes WS OnMatchEnded for ELO)

[02_Map_01 Scene] ────────────────────────────────────
  NETWORK (FishNet Pro V4):
    NetworkGameManager (Singleton, client-side)
    ├── USceneManager.sceneLoaded → _gameSceneLoaded
    ├── WS ds_ready → s_dsReadyReceived
    └── TryConnectIfReady() → FishNet.StartClient(DsIp, DsPort)

    ServerGameManager (NetworkBehaviour, server-side)
    ├── OnPlayerConnected → SpawnPlayerWorkflow
    ├── OnPlayerDisconnected → _disconnectedPlayers cache [NEW]
    └── TryRejoinPlayer() [NEW]

  MATCH CONTROL:
    GameBootstrap (server-only orchestrator)
    ├── Phase 1 → WorldSpawnManager.BeginSpawnCycle() + AntiCampingSystem
    ├── Phase 2 → BossSpawnManager.Schedule() + ObjectiveSystem.Activate()
    └── Phase 3 → LockdownZone.Activate() + RespawnSystem.SetPhase3Mode()

    MatchPhaseManager (NetworkBehaviour, SyncVar phase/duration)
    MatchEndManager (NetworkBehaviour, reads ScoringSystem)
    ScoringSystem (NetworkBehaviour, SyncList<PlayerScore> + SyncList<TeamScore>)

  PLAYER:
    NetworkPlayer (NetworkBehaviour, SyncVar TeamId, IsAlive)
    ClientNetworkHandler → sends PlayerData on connect (IsReconnect flag)
    RigidbodyPredictedMovement (FishNet V2 Prediction)
    GameplaySystemsBridge → connects all gameplay systems

    GameplaySystems per player:
    ├── InventorySystem (SyncList<ItemInstanceData>)
    ├── EquipmentSystem → StatApplyOrchestrator → PlayerStatSystem
    ├── WeaponSystem (partial: Fire / Reload / UnEquip / NetworkSync)
    ├── QuickSlotSystem → ItemUseSystem → ConsumableHandler / ThrowableHandler
    └── PlayerHealthSystem

  WORLD:
    WorldSpawnManager → WorldItem (NetworkObject, pooled)
    WorldDropManager → CorpseDropOnDeath
    WorldContainer → ContainerLootSource

  FOW:
    FogVisionBinder → FogOfWarRevealer3D (local player + spectate-aware)
    FogTeamVisibilityBinder → FogOfWarHider (enemy objects)

  CAMERA:
    GameCameraController → CinemachineCamera (Follow + LookAt)
    CameraStateManager → Free / Locked / WeaponAim
    SpectateManager → OnCurrentPlayerChanged
    SpectatorInputHandler → Next / Prev / Exit / FreeCamera [NEW]

  INPUT:
    InputLayerManager → Single Source of Truth (ActionMap enable/disable)
    InputManager (facade) → MovementInputHandler, CombatInputHandler, etc.
    PlatformInputDetector [NEW] → auto-switch Desktop / Mobile / Gamepad

  UI (in-game):
    GameHUD → CombatHUDPanel, PlayerHUDPanel
    SpectatorHUD [NEW] → shows spectated player info
    ReconnectOverlay [NEW] → shows retry progress
    KillFeedUI, BossHUDPanel, MinimapUI

[00_DS_Boot Scene] ───────────────────────────────────
  ServerBootstrap
  ├── ParseCommandLineArgs() (ENV vars from Docker)
  ├── FishNet.StartServer(port)
  ├── POST /api/ds/register
  ├── POST /api/ds/heartbeat (loop)
  └── ReportMatchEndAndShutdown() → POST /api/match/end/ranked → Quit()
  ServerUISuppressor → disables all UI/camera/audio
```

---

## PRIORITY SUMMARY

| Sprint | Focus | Estimated Items |
|--------|-------|----------------|
| S1 | Language fix, connection flow, score unification, game loop, DS validator | ~35 tasks |
| S2 | Reconnect, spectator FOW+cam+HUD, key rebinding, platform detect, phase late-join | ~30 tasks |
| S3 | Prediction, world items, IK audit, audio consolidation, CI improvements | ~25 tasks |
| S4 | Free-fly cam, UI polish, FishNet audit, zone/boss/objective polish | ~20 tasks |
| CC | Cross-cutting: FindFirstObjectByType, event lifecycle, Inspector wiring | ~15 tasks |
| **Total** | | **~125 tasks** |

---

## IMPLEMENTATION RULES

1. **Never skip `[SerializeField]` wiring** — if a reference is null at runtime, it is a scene setup bug.
2. **Single Source of Truth for Score** — `ScoringSystem` only; `MatchEndManager` reads from it.
3. **One InputLayerManager** — no handler calls `map.Enable()` / `map.Disable()` directly.
4. **FishNet authority** — server-only logic uses `[Server]` attribute; client logic uses `IsOwner` guard.
5. **English everywhere** — every string visible in logs, Inspector, or UI is English.
6. **Events: subscribe in OnEnable, unsubscribe in OnDisable** — no exceptions for MonoBehaviour.
7. **No `FindFirstObjectByType` in gameplay** — use `[SerializeField]` or singleton registry.
