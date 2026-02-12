using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Player;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Network;
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
        #region Serialized References
        [Header("Camera")]
        [SerializeField] private CinemachineCamera _playerCamera;
        [SerializeField] private PlayerInventoryNetwork inventorySystem;
        #endregion
        
        #region Public Accessors
        
        public PlayerInventoryNetwork Inventory => inventorySystem;
        public string DisplayName => PlayerData.DisplayName;
        public int TeamId => PlayerData.TeamId;
        public bool IsLocalPlayer => IsOwner;
        
        // Component accessors (for ServerGameManager and systems)
        public CinemachineCamera PlayerCamera => _playerCamera;
        
        
        private PlayerPublicData _playerData;
        private PlayerPublicData PlayerData => _playerData;
        public void SetPublicData(PlayerPublicData data)
        {
            _playerData = data;
        }
        #endregion
        
        #region Lifecycle
        
        private void Awake()
        {
            if (_playerCamera == null)
                _playerCamera = GetComponentInChildren<CinemachineCamera>();
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
            PlayerPublicRegistry.Instance?.Register((int) this.ObjectId, PlayerData, this);
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
            PlayerPublicRegistry.Instance.Unregister((int) this.ObjectId);
        }
        

        #endregion
        
        #region Client Setup
        
        private void SetupOwnerSide()
        {
            // Owner only: Enable camera and input
            SetupCamera();
            EnableInput();
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