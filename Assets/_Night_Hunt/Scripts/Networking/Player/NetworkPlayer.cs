using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking.Player;
using UnityEngine;
using Unity.Cinemachine;
using NightHunt.Utilities;

namespace NightHunt.Networking
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
    public class NetworkPlayer : NetworkBehaviour
    {
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
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IInventorySystem not found")
                .Resolve();
            var equipment = ComponentResolver.Find<IEquipmentSystem>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IEquipmentSystem not found")
                .Resolve();
            var weapon = ComponentResolver.Find<IWeaponSystem>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IWeaponSystem not found")
                .Resolve();
            var itemSelection = ComponentResolver.Find<IItemSelectionSystem>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IItemSelectionSystem not found")
                .Resolve();
            var statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IPlayerStatSystem not found")
                .Resolve();
            var itemUse = ComponentResolver.Find<IItemUseSystem>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IItemUseSystem not found")
                .Resolve();
            var attachment = ComponentResolver.Find<IAttachmentSystem>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IAttachmentSystem not found")
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
        }

        private void OnDestroy()
        {
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
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Register with the local registry using the SyncVar value.
            // For the owning client this fires after the spawn packet is processed,
            // so _playerData.Value already holds the server-assigned data.
            // For late-joining clients all existing NetworkObjects are spawned with
            // their current SyncVar snapshot, so this also sees the correct data.
            PlayerPublicRegistry.Instance?.Register((int)this.ObjectId, PlayerData, this);

            // Listen for future data changes (name / team updates mid-game).
            _playerData.OnChange += OnPlayerDataChanged;
            _isAlive.OnChange += OnAliveStateChanged;

            // Owner-specific setup
            if (IsOwner)
            {
                SetupOwnerSide();
                SpectateManager.Instance.SetLocalPlayer(this);
            }
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            // Handle ownership transfer
            if (IsOwner)
            {
                SetupOwnerSide();
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _playerData.OnChange -= OnPlayerDataChanged;
            _isAlive.OnChange -= OnAliveStateChanged;
            PlayerPublicRegistry.Instance.Unregister((int)this.ObjectId);
        }

        /// <summary>
        /// Fired on all clients whenever the server changes _playerData (e.g. team
        /// reassignment, name update).  Keeps PlayerPublicRegistry in sync.
        /// External systems (e.g. FogTeamVisibilityBinder) can subscribe to react to team changes.
        /// </summary>
        public event System.Action<PlayerPublicData, PlayerPublicData> OnPublicDataChanged;

        private void OnPlayerDataChanged(PlayerPublicData prev, PlayerPublicData next, bool asServer)
        {
            PlayerPublicRegistry.Instance?.UpdatePublicData((int)this.ObjectId, next);
            OnPublicDataChanged?.Invoke(prev, next);
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
                if (!Application.isMobilePlatform)
                    _cachedAimSystem?.SetCursorVisible(true);
                // Re-enable AimSystem after respawn.
                if (_cachedAimSystem is MonoBehaviour aimMB) aimMB.enabled = true;
            }
        }

        #endregion

        #region Client Setup

        private void SetupOwnerSide()
        {
            // Owner only: Enable camera and input
            EnableInput();

            // Notify listeners that this NetworkPlayer is fully ready on the owning client
            OnOwnerReady?.Invoke(this);
        }


        private void EnableInput()
        {
            var inputManager = FindFirstObjectByType<InputManager>();
            if (inputManager == null)
            {
                Debug.LogWarning("[NetworkPlayer] EnableInput: InputManager not found!");
                return;
            }

            inputManager.EnableAllInput();

            // Lấy các refs cần thiết cho BindCombatSystems
            var weaponSystem = ComponentResolver.Find<NightHunt.GameplaySystems.Core.Interfaces.IWeaponSystem>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NightHunt.GameplaySystems.Core.Interfaces.IWeaponSystem not found")
                .Resolve();

            // CameraStateManager nằm trên player prefab (hoặc child)
            var cameraStateManager = ComponentResolver.Find<NightHunt.Gameplay.Camera.CameraStateManager>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .OrLogWarning("[Auto] NightHunt.Gameplay.Camera.CameraStateManager not found")
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
            var aimSystem = UnityEngine.Object.FindFirstObjectByType<NightHunt.GameplaySystems.Aim.AimSystem>();
            if (aimSystem != null)
            {
                var statSystem = ComponentResolver.Find<NightHunt.Gameplay.StatSystem.Core.Interfaces.IPlayerStatSystem>(this)
                                     .OnSelf()
                                     .InChildren()
                                     .OrLogWarning(
                                         "[Auto] NightHunt.Gameplay.StatSystem.Core.Interfaces.IPlayerStatSystem not found")
                                     .Resolve()
                                 ?? ComponentResolver.Find<NightHunt.Gameplay.StatSystem.Core.Interfaces.IPlayerStatSystem>(this)
                                     .OnSelf()
                                     .InChildren()
                                     .InParent()
                                     .OrLogWarning(
                                         "[Auto] NightHunt.Gameplay.StatSystem.Core.Interfaces.IPlayerStatSystem not found")
                                     .Resolve();
                if (statSystem == null)
                    Debug.LogWarning(
                        "[NetworkPlayer] IPlayerStatSystem not found on player — AimSystem will use fallback VisionRange.");
                else
                    Debug.Log($"[NetworkPlayer] AimSystem bound with statSystem: {statSystem.GetType().Name}");
                aimSystem.Initialize(transform, statSystem);
                inputManager.CombatHandler?.BindAimSystem(aimSystem);
                _cachedAimSystem = aimSystem; // cache for alive-state gating

                // Propagate AimSystem to WeaponVFXController so trail length = VisionRange.
                ComponentResolver.Find<NightHunt.GameplaySystems.Weapon.WeaponVFXController>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] NightHunt.GameplaySystems.Weapon.WeaponVFXController not found")
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

            // Bind ItemUseSystem so fire button calls ExecuteThrow during throwable mode.
            var cachedItemUse = ComponentResolver.Find<IItemUseSystem>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IItemUseSystem not found for CombatHandler binding")
                .Resolve();
            inputManager.CombatHandler?.BindItemUseSystem(cachedItemUse);
        }

        #endregion
    }
}