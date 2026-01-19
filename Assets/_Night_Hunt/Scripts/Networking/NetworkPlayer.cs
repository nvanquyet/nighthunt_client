using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Character;
using Unity.Cinemachine;

namespace NightHunt.Networking
{
    /// <summary>
    /// Network Player Object - Handles ONLY networking concerns
    /// 
    /// FLOW WITH NETWORKTRANSFORM (Server Auth, Send To Owner OFF):
    /// 1. Client Owner: Predict movement → Send input to server
    /// 2. Server: Execute movement (authoritative) → NetworkTransform syncs to ALL clients
    /// 3. Client Owner: Reconcile with server state from NetworkTransform
    /// 4. Remote Clients: Apply server state from NetworkTransform
    /// 
    /// NetworkTransform Config:
    /// - Synchronize: To Observers
    /// - Server Authoritative: ON
    /// - Send To Owner: OFF (important for prediction!)
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Player References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerInputHandler inputHandler;
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterCombat combat;
        [SerializeField] private CinemachineCamera playerCamera;

        [Header("Network Settings")]
        [SerializeField] private float sendRate = 20f; // 20 inputs per second

        // Synchronized variables
        private readonly SyncVar<string> playerName = new SyncVar<string>();
        private readonly SyncVar<int> teamId = new SyncVar<int>();

        // Public accessors
        public string PlayerName => playerName.Value;
        public int TeamId => teamId.Value;
        public bool IsLocalPlayer => IsOwner;
        public CinemachineCamera PlayerCamera => playerCamera;

        #region Initialization

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Initialize components
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (movement == null)
                movement = GetComponent<CharacterMovement>();

            if (combat == null)
                combat = GetComponent<CharacterCombat>();

            if (inputHandler == null)
                inputHandler = GetComponent<PlayerInputHandler>();

            // Subscribe to sync var changes
            playerName.OnChange += OnPlayerNameChanged;
            teamId.OnChange += OnPlayerTeamChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Initialize camera
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<CinemachineCamera>();
                
                if (playerCamera == null)
                {
                    Debug.LogError($"[NetworkPlayer] No CinemachineCamera found for player: {playerName.Value}");
                }
            }

            // Setup camera and input based on ownership
            SetupCameraForOwnership();
            SetupInputForOwnership();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Server instance: Disable camera (only client owner uses camera)
            if (playerCamera != null && playerCamera.gameObject != null)
            {
                playerCamera.gameObject.SetActive(false);
                playerCamera.enabled = false;
            }

            Debug.Log($"[NetworkPlayer] Player spawned on server: {playerName.Value}");
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            // Unsubscribe from sync vars
            if (playerName != null)
                playerName.OnChange -= OnPlayerNameChanged;
            if (teamId != null)
                teamId.OnChange -= OnPlayerTeamChanged;
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            
            // Update camera and input when ownership changes
            SetupCameraForOwnership();
            SetupInputForOwnership();
        }

        #endregion

        #region Camera Setup

        private void SetupCameraForOwnership()
        {
            if (playerCamera == null) return;

            // Only enable camera for client owner (not server instance)
            if (IsOwner && !IsServerInitialized)
            {
                if (playerCamera.gameObject != null)
                {
                    playerCamera.gameObject.SetActive(true);
                    playerCamera.enabled = true;
                    
                    if (Camera.main == null || Camera.main == playerCamera)
                    {
                        playerCamera.tag = "MainCamera";
                    }
                    
                    Debug.Log($"[NetworkPlayer] Camera enabled for owner: {playerName.Value}");
                }
            }
            else
            {
                if (playerCamera.gameObject != null)
                {
                    playerCamera.gameObject.SetActive(false);
                    playerCamera.enabled = false;
                    
                    string reason = IsServerInitialized ? "server instance" : "remote client";
                    Debug.Log($"[NetworkPlayer] Camera disabled for {reason}: {playerName.Value}");
                }
            }
        }

        #endregion

        #region Input Setup

        private void SetupInputForOwnership()
        {
            if (inputHandler == null) return;

            if (IsOwner)
            {
                inputHandler.Initialize(this);
                inputHandler.EnableInput();
                Debug.Log($"[NetworkPlayer] Input enabled for owner: {playerName.Value}");
            }
            else
            {
                inputHandler.DisableInput();
                Debug.Log($"[NetworkPlayer] Input disabled for non-owner: {playerName.Value}");
            }
        }

        #endregion

        #region Data Sync (Server-side setters)

        /// <summary>
        /// Server: Set player name
        /// </summary>
        [Server]
        public void SetPlayerName(string name)
        {
            playerName.Value = name;
        }

        /// <summary>
        /// Server: Set team ID
        /// </summary>
        [Server]
        public void SetTeamId(int team)
        {
            teamId.Value = team;
            Debug.Log($"[NetworkPlayer] Team set: {playerName.Value} → Team {team}");
        }

        #endregion

        #region Input Transmission (Client → Server)

        private void Update()
        {
            if (!IsSpawned) return;

            // ✅ CHỈ CLIENT OWNER gửi input đến server
            if (IsOwner && !IsServerInitialized)
            {
                // Send input at fixed rate (20Hz)
                if (Time.frameCount % Mathf.RoundToInt(60f / sendRate) == 0)
                {
                    SendInputToServer();
                }
            }
        }

        private void SendInputToServer()
        {
            if (inputHandler == null || !IsSpawned || !IsOwner)
                return;

            Vector2 moveInput = inputHandler.GetMoveInput();
            bool isSprinting = inputHandler.IsSprinting();
            bool isCrouching = inputHandler.IsCrouching();
            
            // ✅ Gửi input lên server (RunLocally = false)
            ServerReceiveMovementInput(moveInput, isSprinting, isCrouching);
            
            if (combat != null)
            {
                Vector3 aimDirection = inputHandler.GetAimDirection();
                bool isAttacking = inputHandler.IsAttacking();
                bool isReloading = inputHandler.IsReloading();
                ServerReceiveCombatInput(aimDirection, isAttacking, isReloading);
            }
        }

        /// <summary>
        /// ✅ SERVER: Nhận input từ client
        /// RunLocally = false → CHỈ chạy trên server
        /// Server sẽ execute movement trong CharacterMovement.Update()
        /// </summary>
        [ServerRpc(RequireOwnership = true, RunLocally = false)]
        private void ServerReceiveMovementInput(Vector2 moveInput, bool isSprinting, bool isCrouching)
        {
            if (!IsSpawned || movement == null) return;
            
            // ✅ Chỉ SET input, movement logic sẽ execute trong Update()
            movement.SetMoveInput(moveInput);
            movement.SetSprinting(isSprinting);
            movement.SetCrouching(isCrouching);
        }

        [ServerRpc(RequireOwnership = true, RunLocally = false)]
        private void ServerReceiveCombatInput(Vector3 aimDirection, bool isAttacking, bool isReloading)
        {
            if (!IsSpawned || combat == null) return;
            
            combat.SetAimDirection(aimDirection);
            combat.SetAttacking(isAttacking);
            combat.SetReloading(isReloading);
        }

        #endregion

        #region Game Events

        /// <summary>
        /// Called when player dies
        /// </summary>
        public void OnDeath()
        {
            Debug.Log($"[NetworkPlayer] Player {playerName.Value} died");
            
            if (IsServerInitialized)
            {
                var serverGameManager = FindFirstObjectByType<ServerGameManager>();
                if (serverGameManager != null)
                {
                    serverGameManager.RespawnPlayer(this, delay: 3f);
                }
            }
        }

        /// <summary>
        /// Server: Respawn player at position
        /// </summary>
        [Server]
        public void RespawnAtPosition(Vector3 spawnPosition)
        {
            transform.position = spawnPosition;
            Debug.Log($"[NetworkPlayer] Player {playerName.Value} respawned at {spawnPosition}");
        }

        #endregion

        private void OnPlayerNameChanged(string oldName, string newName, bool asServer)
        {
            Debug.Log($"[NetworkPlayer] Name changed: {oldName} → {newName}");
        }

        private void OnPlayerTeamChanged(int oldTeam, int newTeam, bool asServer)
        {
            Debug.Log($"[NetworkPlayer] Team changed: {oldTeam} → {newTeam}");
        }
    }
}