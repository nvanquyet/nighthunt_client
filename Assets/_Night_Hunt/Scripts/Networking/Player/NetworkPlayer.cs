using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking.Player;
using UnityEngine;
using Unity.Cinemachine;

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
        #region Serialized References
        [Header("Camera")]
        [SerializeField] private CinemachineCamera _playerCamera;
        //[SerializeField] private PlayerInventoryNetwork inventorySystem;
        #endregion
        
        
        
        #region Public Accessors
        
        //public PlayerInventoryNetwork Inventory => inventorySystem;
        public string DisplayName => PlayerData.DisplayName;
        public int TeamId => PlayerData.TeamId;
        public bool IsLocalPlayer => IsOwner;
        
        // Component accessors (for ServerGameManager and systems)
        public CinemachineCamera PlayerCamera => _playerCamera;
        public IGameplayBridge GamePlaySystemBridge { get; private set; } 
        
        
        // ── PUBLIC PLAYER DATA ────────────────────────────────────────────────
        // SyncVar ensures FishNet includes the current value in every spawn packet
        // (including late-joiners / reconnects) AND broadcasts changes to all
        // connected clients whenever SetPublicData() is called on the server.
        // Using a plain C# field here would mean every client receives an empty
        // struct — no name, no team — because FishNet never serialises it.
        private readonly SyncVar<PlayerPublicData> _playerData = new SyncVar<PlayerPublicData>();

        private PlayerPublicData PlayerData => _playerData.Value;

        /// <summary>
        /// Server: Set / update the publicly-visible player data.
        /// The SyncVar broadcasts the new value to all observers automatically.
        /// </summary>
        public void SetPublicData(PlayerPublicData data)
        {
            _playerData.Value = data;
        }
        #endregion
        
        #region Lifecycle
        
        private void Awake()
        {
            if (_playerCamera == null)
                _playerCamera = GetComponentInChildren<CinemachineCamera>();
            
            // Initialize GameplaySystemsBridge with Dependency Injection
            // Get all system components
            var inventory = GetComponent<IInventorySystem>();
            var equipment = GetComponent<IEquipmentSystem>();
            var weapon = GetComponent<IWeaponSystem>();
            var quickSlot = GetComponent<IQuickSlotSystem>();
            var statSystem = GetComponent<IPlayerStatSystem>();
            var itemUse = GetComponent<IItemUseSystem>();
            
            // Create bridge with DI
            if (inventory != null && equipment != null && weapon != null && 
                quickSlot != null && statSystem != null && itemUse != null)
            {
                GamePlaySystemBridge = new GameplaySystemsBridge(
                    inventory, equipment, weapon, quickSlot, statSystem, itemUse);
            }
            else
            {
                Debug.LogError("[NetworkPlayer] Failed to initialize GameplaySystemsBridge - missing required systems!");
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
            
            // Server: Disable camera (dedicated server or host)
            if (_playerCamera != null)
            {
                _playerCamera.gameObject.SetActive(false);
            }
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
            PlayerPublicRegistry.Instance.Unregister((int)this.ObjectId);
        }

        /// <summary>
        /// Called on all clients whenever the server changes _playerData (e.g. team
        /// reassignment, name update).  Keeps PlayerPublicRegistry in sync.
        /// </summary>
        private void OnPlayerDataChanged(PlayerPublicData prev, PlayerPublicData next, bool asServer)
        {
            PlayerPublicRegistry.Instance?.UpdatePublicData((int)this.ObjectId, next);
        }
        

        #endregion
        
        #region Client Setup
        
        private void SetupOwnerSide()
        {
            // Owner only: Enable camera and input
            SetupCamera();
            EnableInput();

            // Notify listeners that this NetworkPlayer is fully ready on the owning client
            OnOwnerReady?.Invoke(this);
        }
        
        private void SetupCamera()
        {
            if (_playerCamera == null) return;
            
            // Only enable for owner and not on dedicated server
            bool shouldEnableCamera = IsOwner && !IsServerInitialized;
            
            _playerCamera.gameObject.SetActive(shouldEnableCamera);
            
            if (shouldEnableCamera)
            {
                // Set as main camera if none exists
                Camera cam = _playerCamera.GetComponent<Camera>();
                if (cam != null && Camera.main == null)
                {
                    cam.tag = "MainCamera";
                }
            }
        }
        
        private void EnableInput()
        {
            // Enable input for owner
            var inputManager = FindFirstObjectByType<InputManager>();
            if (inputManager != null)
            {
                inputManager.EnableAllInput();
            }
        }
        
        #endregion
        
        
        #region Validation (Editor Only)
        
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (_playerCamera == null)
                _playerCamera = GetComponentInChildren<CinemachineCamera>();
        }
#endif
        
        #endregion
    }
}