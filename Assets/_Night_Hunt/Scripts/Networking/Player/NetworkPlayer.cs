using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using FishNet.Observing;
using NightHunt.Gameplay.Core.Events;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Spectator;
using NightHunt.Gameplay.FogOfWar;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using UnityEngine;
using Unity.Cinemachine;
using NightHunt.Utilities;
using NightHunt.UI;

namespace NightHunt.Networking.Player
{
    /// <summary>
    /// NETWORK PLAYER - Player object representation
    /// 
    /// Server Authority:
    /// - All data synchronized via SyncVars (server → clients)
    /// - Client can only REQUEST changes via ServerRpc
    /// - Component references set in Awake, accessible to ServerGameManager
    /// 
    /// Responsibilities:
    /// - Hold player data (name, team, stats)
    /// - Provide component references
    /// - Notify server when client is ready
    /// - Setup camera/input for owner
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour, IFogTeamOwned
    {
        // ── IFogTeamOwned (FOW team visibility) ───────────────────────────────
        /// <inheritdoc/>
        public int  FogOwnerTeamId  => TeamId;  // SyncVar-backed, always up-to-date on all clients
        /// <inheritdoc/>
        public bool FogAlwaysVisible => false;  // Players obey FOW; ally vs enemy decided by team compare
        /// <summary>
        /// Fired on the owning client when this NetworkPlayer has completed owner-side setup
        /// (camera + input). Used by gameplay systems (e.g. PlayerInteractionSystem) to
        /// safely subscribe to input after Network is ready.
        /// </summary>
        public static event System.Action<NetworkPlayer> OnOwnerReady;

        #region Public Accessors

        public string DisplayName => PlayerData.DisplayName;
        public int TeamId => PlayerData.TeamId;
        public bool IsLocalPlayer => IsOwner;

        public int CharacterModelIndex => PlayerData.CharacterModelIndex;

        public IGameplayBridge GamePlaySystemBridge { get; private set; }

        // Cached owner-side refs — set in EnableInput(), used 
        // to cleanly disable / re-enable when IsAlive changes.
        private NightHunt.Gameplay.Input.Handlers.Combat.CombatInputHandler _cachedCombatHandler;
        private NightHunt.GameplaySystems.Core.Interfaces.IAimSystem _cachedAimSystem;
        private NightHunt.Gameplay.Input.Handlers.Movement.MovementInputHandler _cachedMovementHandler;
        private NightHunt.Gameplay.Input.Handlers.Camera.CameraInputHandler _cachedCameraHandler;
        private PlayerModelLoader _modelLoader;
        private CharacterAnimationController _animationController;
        private bool _modelReady;
        private bool _clientRuntimeReadySent;


        // ── PUBLIC PLAYER DATA ────────────────────────────────────────────────
        // SyncVar ensures FishNet includes the current value in every spawn packet
        // (including late-joiners / reconnects) AND broadcasts changes to all
        // connected clients whenever SetPublicData() is called on the server.
        // Using a plain C# field here would mean every client receives an empty
        // struct — no name, no team — because FishNet never serialises it.
        private readonly SyncVar<PlayerPublicData> _playerData = new SyncVar<PlayerPublicData>();

        // Alive state — server-authoritative, replicated to all clients
        private readonly SyncVar<bool> _isAlive = new SyncVar<bool>(true, new SyncTypeSettings());

        /// <summary>True while the player is alive (false during death / waiting for respawn).</summary>
        public bool IsAlive => _isAlive.Value;

        /// <summary>
        /// Fired on every client (including server) when _isAlive changes.
        /// CharacterVisualController subscribes to trigger ragdoll / animator visuals.
        /// </summary>
        public event System.Action<bool> OnAliveChanged;

        private PlayerPublicData PlayerData => _playerData.Value;

        /// <summary>Server: Set / update the publicly-visible player data.</summary>
        public void SetPublicData(PlayerPublicData data)
        {
            _playerData.Value = data;
            // Propagate team change to server-side FoW visibility map.
            if (IsServerStarted)
                ServerFogVisibilityService.Instance?.UpdatePlayerData(
                    ObjectId, data.TeamId, ResolveServerVisionRange());
        }

        /// <summary>Server: Mark player as dead or alive.</summary>
        public void SetAlive(bool alive)
        {
            _isAlive.Value = alive;
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // Initialize GameplaySystemsBridge with Dependency Injection
            // Get all system components
            var inventory = ComponentResolver.Find<IInventorySystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[NetworkPlayer] IInventorySystem not found")
                .Resolve();
            var equipment = ComponentResolver.Find<IEquipmentSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[NetworkPlayer] IEquipmentSystem not found")
                .Resolve();
            var weapon = ComponentResolver.Find<IWeaponSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[NetworkPlayer] IWeaponSystem not found")
                .Resolve();
            var itemSelection = ComponentResolver.Find<IItemSelectionSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[NetworkPlayer] IItemSelectionSystem not found")
                .Resolve();
            var statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[NetworkPlayer] IPlayerStatSystem not found")
                .Resolve();
            var itemUse = ComponentResolver.Find<IItemUseSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[NetworkPlayer] IItemUseSystem not found")
                .Resolve();
            var attachment = ComponentResolver.Find<IAttachmentSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[NetworkPlayer] IAttachmentSystem not found")
                .Resolve();

            // Create bridge with DI
            if (inventory != null && equipment != null && weapon != null &&
                itemSelection != null && statSystem != null && itemUse != null)
            {
                GamePlaySystemBridge = new GameplaySystemsBridge(
                    inventory, equipment, weapon, itemSelection, statSystem, itemUse, attachment);
            }
            else
            {
                Debug.LogError(
                    "[NetworkPlayer] Failed to initialize GameplaySystemsBridge - missing required systems!");
            }

            _modelLoader = ComponentResolver.Find<PlayerModelLoader>(this)
                .OnSelf().OnRoot().InRootChildren()
                .Resolve();
            if (_modelLoader != null)
                _modelLoader.OnModelReady += HandleModelReady;
        }

        private void OnDestroy()
        {
            if (_modelLoader != null)
                _modelLoader.OnModelReady -= HandleModelReady;

            // Dispose bridge when player is destroyed
            if (GamePlaySystemBridge is System.IDisposable disposable)
            {
                disposable.Dispose();
            }
        }


        public override void OnStartServer()
        {
            base.OnStartServer();

            // On DEDICATED SERVER only: disable camera.
            // In HOST mode (IsClientInitialized == true) the client-side setup (SetupCamera)
            // will enable the camera for the owning player — do NOT disable it here.
            bool isDedicatedServer = !IsClientInitialized;

            // ── Server-side FoW Net-Culling ────────────────────────────────────
            // Register this player with the visibility service so ConditionMet() can
            // query whether any other player has LoS to this one.
            var svc = ServerFogVisibilityService.Instance;
            if (svc != null)
                svc.RegisterPlayer(ObjectId, transform, TeamId, ResolveServerVisionRange());

            // NOTE: FogOfWarObserverCondition must be added to the player prefab via the Inspector:
            // → NetworkObserver component → Observer Conditions → drag FogOfWarObserverCondition asset.
            // (NetworkObserver.ObserverConditionsInternal is internal and not accessible at runtime.)
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerFogVisibilityService.Instance?.UnregisterPlayer(ObjectId);
        }

        /// <summary>
        /// Reads VisionRange from this player's stat system (server-side access).
        /// Falls back to 20 f when stat system is not yet initialised.
        /// </summary>
        private float ResolveServerVisionRange()
        {
            float v = GamePlaySystemBridge?.Stat?.GetStat(PlayerStatType.VisionRange) ?? 0f;
            return v > 0.1f ? v : 20f;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Listen for SyncVar changes — must be registered before any check below
            // so we never miss the first change that carries the real player data.
            _playerData.OnChange += OnPlayerDataChanged;
            _isAlive.OnChange += OnAliveStateChanged;
            _clientRuntimeReadySent = false;

            if (_modelLoader != null && _modelLoader.CurrentModelInstance != null)
            {
                _modelReady = true;
                TryNotifyClientRuntimeReady();
            }

            Debug.Log($"[FLOW §10] NetworkPlayer.OnStartClient: ObjectId={ObjectId} IsOwner={IsOwner} Name='{PlayerData.DisplayName}' TeamId={PlayerData.TeamId} IsAlive={IsAlive}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");

            // ServerGameManager calls Spawn() first and SetPublicData() right after.
            // FishNet processes these as two separate messages; on the owning client the
            // spawn packet arrives with an empty SyncVar struct (Name='', TeamId=0) and
            // the dirty-sync for SetPublicData arrives one tick later.
            // We therefore defer owner-side setup until we have a non-empty DisplayName,
            // either from the initial spawn packet (late-join snapshot) or from the
            // first OnChange callback (fresh spawn path).
            if (IsOwner)
            {
                if (!string.IsNullOrEmpty(PlayerData.DisplayName))
                {
                    // Data already present in spawn packet (late-join / relay host case).
                    FinishOwnerSetup();
                }
                // else: data is empty — wait for OnPlayerDataChanged to call FinishOwnerSetup().
            }
            else
            {
                // Non-owner: register with current snapshot (may be empty, will be updated via OnChange).
                PlayerPublicRegistry.Instance?.Register((int)this.ObjectId, PlayerData, this);
            }
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            // Ownership transferred at runtime (rare). Reset guard so FinishOwnerSetup()
            // runs for the new owner with correct data.
            if (IsOwner)
            {
                _ownerSetupDone = false;
                _clientRuntimeReadySent = false;
                if (!string.IsNullOrEmpty(PlayerData.DisplayName))
                    FinishOwnerSetup();
                // else: wait for next OnPlayerDataChanged (data not yet synced)
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _playerData.OnChange -= OnPlayerDataChanged;
            _isAlive.OnChange -= OnAliveStateChanged;
            PlayerPublicRegistry.Instance.Unregister((int)this.ObjectId);
            _modelReady = false;
            _clientRuntimeReadySent = false;
        }

        /// <summary>
        /// Fired on all clients whenever the server changes _playerData (e.g. team
        /// reassignment, name update).  Keeps PlayerPublicRegistry in sync.
        /// External systems (e.g. FogTeamVisibilityBinder) can subscribe to react to team changes.
        /// </summary>
        public event System.Action<PlayerPublicData, PlayerPublicData> OnPublicDataChanged;

        // Guards FinishOwnerSetup() so it runs exactly once even if OnPlayerDataChanged
        // fires multiple times before and after the first valid data arrives.
        private bool _ownerSetupDone;

        private void OnPlayerDataChanged(PlayerPublicData prev, PlayerPublicData next, bool asServer)
        {
            // Always keep the cached PlayerPublicData in the registry in sync.
            // • Non-owners: networkPlayers entry was created in OnStartClient; this updates the players dict.
            // • Owner:      FinishOwnerSetup() will call Register() for both dicts; this pre-populates players dict only.
            PlayerPublicRegistry.Instance?.UpdatePublicData((int)this.ObjectId, next);

            OnPublicDataChanged?.Invoke(prev, next);

            // Deferred owner setup: OnStartClient ran while the SyncVar was still empty
            // (Spawn packet arrived before SetPublicData dirty-sync).  Now that we have
            // a non-empty DisplayName the real player data is ready.
            if (IsOwner && !_ownerSetupDone && !string.IsNullOrEmpty(next.DisplayName))
            {
                Debug.Log($"[FLOW §10b] NetworkPlayer: PlayerData arrived via OnChange — Name='{next.DisplayName}' TeamId={next.TeamId}. Completing owner setup.");
                FinishOwnerSetup();
            }
        }

        /// <summary>
        /// Completes owner-side initialisation once valid player data (DisplayName) is available.
        /// Called either directly from OnStartClient (late-join / data-in-spawn-packet path)
        /// or deferred to the first OnPlayerDataChanged callback (fresh-spawn path where
        /// SetPublicData arrives one FishNet tick after the spawn packet).
        /// </summary>
        private void FinishOwnerSetup()
        {
            if (_ownerSetupDone) return;
            _ownerSetupDone = true;

            // Register with up-to-date data now that DisplayName is populated.
            PlayerPublicRegistry.Instance?.Register((int)this.ObjectId, PlayerData, this);

            Debug.Log($"[FLOW §10c] NetworkPlayer.FinishOwnerSetup: Name='{PlayerData.DisplayName}' TeamId={PlayerData.TeamId}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");

            // FIX: Register with SpectateManager BEFORE firing SetupOwnerSide() / OnOwnerReady.
            // CameraStateManager.HandleOwnerReady() calls SpectateManager.GetCurrentPlayer() to
            // decide whether to activate the virtual camera.  If SetLocalPlayer() has not been
            // called yet, GetCurrentPlayer() returns null → HandleCurrentPlayerChanged(null) →
            // SetVirtualCameraActive(false) → camera is turned OFF right after SetOwnerCamera(true)
            // turned it ON.  Calling SetLocalPlayer first ensures GetCurrentPlayer() returns this
            // player and the camera stays active.
            try
            {
                SpectateManager.Instance?.SetLocalPlayer(this);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }

            SetupOwnerSide();
            TryNotifyClientRuntimeReady();
        }

        private void HandleModelReady(GameObject _)
        {
            _modelReady = true;
            TryNotifyClientRuntimeReady();
        }

        private void TryNotifyClientRuntimeReady()
        {
            if (!IsOwner || _clientRuntimeReadySent || !_ownerSetupDone)
                return;

            if (_modelLoader != null && !_modelReady && _modelLoader.CurrentModelInstance == null)
                return;

            _clientRuntimeReadySent = true;
            Debug.Log($"[FLOW §12] NetworkPlayer client runtime ready: ObjectId={ObjectId} modelReady={_modelReady || _modelLoader == null}");
            NotifyClientRuntimeReadyServerRpc();
        }

        [ServerRpc(RequireOwnership = true)]
        private void NotifyClientRuntimeReadyServerRpc(NetworkConnection conn = null)
        {
            ServerGameManager.Instance?.OnPlayerClientRuntimeReady(conn ?? Owner, this);
        }

        [TargetRpc]
        public void TargetMatchAllPlayersReady(NetworkConnection conn, string source)
        {
            Debug.Log($"[FLOW §13] NetworkPlayer.TargetMatchAllPlayersReady: source={source} ObjectId={ObjectId}");
            MatchLoadingOverlay.SignalAllPlayersReady($"network-player-target:{source}");
            GameplayEventBus.Instance?.Publish(new AllPlayersReadyEvent());
        }

        public void RequestInteractAnimation(int interactIndex)
        {
            if (!IsOwner)
                return;

            RequestInteractAnimationServerRpc((byte)Mathf.Clamp(interactIndex, 0, byte.MaxValue));
        }

        [ServerRpc(RequireOwnership = true)]
        private void RequestInteractAnimationServerRpc(byte interactIndex)
        {
            PlayInteractAnimationObserversRpc(interactIndex);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void PlayInteractAnimationObserversRpc(byte interactIndex)
        {
            _animationController ??= ComponentResolver.Find<CharacterAnimationController>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .OrDefault(null)
                .Resolve();

            _animationController?.TriggerInteract(interactIndex);
        }

        /// <summary>
        /// Called on all clients when _isAlive SyncVar changes.
        /// Forwards the new value through OnAliveChanged so CharacterVisualController
        /// can react without depending on FishNet internals.
        /// Also gates combat input for the owning client.
        /// </summary>
        private void OnAliveStateChanged(bool prev, bool next, bool asServer)
        {
            OnAliveChanged?.Invoke(next);

            // Owner-only, client-side: keep input + cursor in sync with alive state.
            if (!IsOwner || asServer) return;

            if (!next)
            {
                // Dying: force EndFire (hides RangeIndicator, stops weapon) then disable
                // all combat input so no further clicks register while ragdolled.
                _cachedCombatHandler?.DisableInput();
                _cachedMovementHandler?.DisableInput();
                _cachedCameraHandler?.DisableInput();
                _cachedAimSystem?.SetCursorVisible(false);
                // Disable AimSystem to stop rotation/aim processing while dead.
                if (_cachedAimSystem is MonoBehaviour aimMB) aimMB.enabled = false;
            }
            else
            {
                // Respawning: re-enable input and restore cursor.
                _cachedCombatHandler?.EnableInput();
                _cachedMovementHandler?.EnableInput();
                _cachedCameraHandler?.EnableInput();
                _cachedAimSystem?.SetCursorVisible(true);
                // Re-enable AimSystem after respawn.
                if (_cachedAimSystem is MonoBehaviour aimMB) aimMB.enabled = true;
            }
        }

        #endregion

        #region Client Setup

        private void SetupOwnerSide()
        {
            // Owner only: Enable camera and input.
            // In FishNet Host mode the player prefab can be spawned (OnStartClient) in the same
            // frame as Awake(), before InputManager.Start() has run. Check Instance first and
            // defer to a coroutine if not yet available to avoid silently losing input.
            if (InputManager.Instance != null)
            {
                EnableInput();
            }
            else
            {
                Debug.LogWarning("[FLOW §11] NetworkPlayer.SetupOwnerSide: InputManager not ready yet — deferring EnableInput().");
                StartCoroutine(RetryEnableInputWhenReady());
            }

            // Directly activate the local player's virtual camera on the same prefab.
            // CameraStateManager.HandleOwnerReady() cannot be used here because it subscribes
            // in OnEnable() which Unity calls AFTER FishNet's OnStartClient — meaning the
            // subscription may not yet exist when OnOwnerReady fires on a freshly spawned prefab.
            // Calling SetOwnerCamera() directly from the prefab's own CameraStateManager bypasses
            // the timing gap entirely.
            var csmOnPrefab = GetComponentInChildren<NightHunt.Gameplay.Camera.CameraStateManager>(includeInactive: true);
            csmOnPrefab?.SetOwnerCamera(true);

            // Broadcast to scene-level listeners (GameHUD, RaycastDetector, PlayerInteractionSystem,
            // LootContainerUI, etc.) that live OUTSIDE the player prefab and subscribe in OnEnable()
            // during scene load — they are guaranteed to be subscribed before any player spawns.
            Debug.Log($"[FLOW §11] NetworkPlayer.SetupOwnerSide: firing OnOwnerReady ObjectId={ObjectId} Name='{PlayerData.DisplayName}' TeamId={PlayerData.TeamId}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            RaiseOwnerReady();
        }

        private void RaiseOwnerReady()
        {
            var handlers = OnOwnerReady;
            if (handlers == null)
                return;

            foreach (System.Delegate handlerDelegate in handlers.GetInvocationList())
            {
                var handler = handlerDelegate as System.Action<NetworkPlayer>;
                if (handler == null)
                    continue;

                try
                {
                    handler(this);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }
        }

        /// <summary>
        /// Fallback: retry EnableInput() each frame until InputManager.Instance is available.
        /// Guards against FishNet Host race where OnStartClient fires before InputManager.Start().
        /// </summary>
        private System.Collections.IEnumerator RetryEnableInputWhenReady()
        {
            const float timeoutSeconds = 5f;
            float elapsed = 0f;
            while (InputManager.Instance == null && elapsed < timeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (InputManager.Instance != null)
            {
                Debug.Log($"[FLOW §11b] NetworkPlayer: InputManager ready after {elapsed:F2}s — calling EnableInput().");
                EnableInput();
            }
            else
            {
                Debug.LogError($"[NetworkPlayer] InputManager never became available after {timeoutSeconds}s! " +
                               "Player input will not work. Ensure InputManager is in the game scene.");
            }
        }


        private void EnableInput()
        {
            var inputManager = FindFirstObjectByType<InputManager>();
            if (inputManager == null)
            {
                Debug.LogError("[NetworkPlayer] EnableInput: InputManager NOT FOUND in scene! " +
                               "This will prevent all player input. Ensure InputManager GameObject " +
                               "is present in the game scene with correct execution order. " +
                               $"ObjectId={ObjectId} Name='{PlayerData.DisplayName}'");
                return;
            }

            Debug.Log($"[FLOW §11c] NetworkPlayer.EnableInput: ObjectId={ObjectId} Name='{PlayerData.DisplayName}'  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            inputManager.EnableAllInput();

            // Lấy các refs cần thiết cho BindCombatSystems
            var weaponSystem = ComponentResolver.Find<NightHunt.GameplaySystems.Core.Interfaces.IWeaponSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[NetworkPlayer] IWeaponSystem not found")
                .Resolve();

            // CameraStateManager nằm trên player prefab (hoặc child)
            var cameraStateManager = ComponentResolver.Find<NightHunt.Gameplay.Camera.CameraStateManager>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[NetworkPlayer] CameraStateManager not found")
                .Resolve();

            if (cameraStateManager == null)
                Debug.LogWarning("[NetworkPlayer] EnableInput: CameraStateManager not found on player prefab! " +
                                 "Camera will not freeze during fire.");

            // Bind tất cả refs vào CombatHandler
            inputManager.CombatHandler?.BindCombatSystems(
                inputManager.MovementHandler,
                weaponSystem,
                transform,
                cameraStateManager
            );
            _cachedCombatHandler = inputManager.CombatHandler; // cache for alive-state gating
            _cachedMovementHandler = inputManager.MovementHandler; // cache for alive-state gating
            _cachedCameraHandler   = inputManager.CameraHandler;   // cache for alive-state gating

            // Bind AimSystem — single source of aim direction (VisionRange-clamped cursor).
            // AimSystem.Initialize wires it to this player's transform + stat system.
            var aimSystem = UnityEngine.Object.FindFirstObjectByType<NightHunt.GameplaySystems.Aim.AimSystem>(FindObjectsInactive.Include);
            if (aimSystem != null)
            {
                if (!aimSystem.gameObject.activeSelf)
                {
                    aimSystem.gameObject.SetActive(true);
                    Debug.LogWarning("[NetworkPlayer] AimSystem GameObject was inactive during owner setup; activating it so the world cursor can render.", aimSystem);
                }

                if (!aimSystem.enabled)
                {
                    aimSystem.enabled = true;
                    Debug.LogWarning("[NetworkPlayer] AimSystem component was disabled during owner setup; enabling it for local player aim.", aimSystem);
                }

                var statSystem = ComponentResolver.Find<NightHunt.Gameplay.StatSystem.Core.Interfaces.IPlayerStatSystem>(this)
                    .OnSelf().InChildren().InParent()
                    .OrLogWarning("[NetworkPlayer] IPlayerStatSystem not found — AimSystem will use fallback VisionRange")
                    .Resolve();
                if (statSystem == null)
                    Debug.LogWarning(
                        "[NetworkPlayer] IPlayerStatSystem not found on player — AimSystem will use fallback VisionRange.");
                else
                    Debug.Log($"[NetworkPlayer] AimSystem bound with statSystem: {statSystem.GetType().Name}");
                aimSystem.Initialize(transform, statSystem);
                aimSystem.SetCursorVisible(true);
                inputManager.CombatHandler?.BindAimSystem(aimSystem);
                _cachedAimSystem = aimSystem; // cache for alive-state gating

                // Propagate AimSystem to WeaponVFXController so trail length = VisionRange.
                ComponentResolver.Find<NightHunt.GameplaySystems.Weapon.WeaponVFXController>(this)
                    .OnSelf().InChildren()
                    .OrLogWarning("[NetworkPlayer] WeaponVFXController not found")
                    .Resolve()
                    ?.Initialize(aimSystem);
            }
            else
            {
                Debug.LogWarning(
                    "[NetworkPlayer] AimSystem not found in scene — falling back to own ground-plane raycast.");
            }

            // Bind RangeIndicator so BeginFire/EndFire can Show/Hide the vision ring.
            var rangeIndicator =
                UnityEngine.Object.FindFirstObjectByType<NightHunt.GameplaySystems.UI.Combat.RangeIndicator>();
            if (rangeIndicator != null)
            {
                rangeIndicator.SetFollowTarget(transform);
                // Initial range — refreshed again every BeginFire via _aimSystem.GetVisionRange().
                // Use AimSystem.GetVisionRange() so the same fallback logic applies (handles cache-not-ready).
                float initRange = _cachedAimSystem != null ? _cachedAimSystem.GetVisionRange() : 15f;
                rangeIndicator.SetRange(initRange);
                inputManager.CombatHandler?.BindAttackIndicators(rangeIndicator);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayer] RangeIndicator not found in scene — vision ring will not appear.");
            }

            // Bind item systems directly from the player bridge. UI panels also bind
            // these later, but combat input must not depend on HUD initialization order.
            var cachedItemSelection = GamePlaySystemBridge?.ItemSelection
                ?? ComponentResolver.Find<IItemSelectionSystem>(this)
                    .OnSelf().InChildren().InParent()
                    .OrLogWarning("[NetworkPlayer] IItemSelectionSystem not found for CombatHandler binding")
                    .Resolve();
            var cachedItemUse = GamePlaySystemBridge?.ItemUse
                ?? ComponentResolver.Find<IItemUseSystem>(this)
                    .OnSelf().InChildren().InParent()
                    .OrLogWarning("[NetworkPlayer] IItemUseSystem not found for CombatHandler binding")
                    .Resolve();
            inputManager.CombatHandler?.BindItemSelectionSystem(cachedItemSelection);
            inputManager.CombatHandler?.BindItemUseSystem(cachedItemUse);
            Debug.Log($"[NH_FLOW][01][NetworkPlayer.BindCombatItems] selection={(cachedItemSelection != null ? "ok" : "null")} itemUse={(cachedItemUse != null ? "ok" : "null")}");
        }

        #endregion
    }
}
