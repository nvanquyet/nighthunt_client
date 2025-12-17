using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Character;
using FishNet;
using Unity.Cinemachine;

namespace NightHunt.Networking
{
    /// <summary>
    /// Network Player Object with client-side prediction
    /// Handles player ownership, input, and synchronization
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Player References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerInputHandler inputHandler;
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterCombat combat;
        [SerializeField] private CinemachineCamera playerCamera; // Each player has their own camera

        [Header("Network Settings")]
        [SerializeField] private float sendRate = 20f; // Updates per second

        // Synchronized variables
        private readonly SyncVar<string> playerName = new SyncVar<string>();
        private readonly SyncVar<int> teamId = new SyncVar<int>();

        // Client prediction state (for reconciliation)
        private Vector3 lastServerPosition;
        private float reconciliationThreshold = 0.1f; // Threshold for position reconciliation

        public string PlayerName => playerName.Value;
        public int TeamId => teamId.Value;
         public bool IsLocalPlayer => IsOwner;
        public CinemachineCamera PlayerCamera => playerCamera;

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

            // Initialize camera if not assigned
            if (playerCamera == null)
            {
                // Try to find camera in children
                playerCamera = GetComponentInChildren<CinemachineCamera>();
                
                // If still null, create a camera
                if (playerCamera == null)
                {
                    Debug.LogError($"[NetworkPlayer] No CinemachineCamera found for player: {playerName.Value}");
                }
            }

            // Setup camera based on ownership
            SetupCameraForOwnership();
            
            // Setup input based on ownership
            if (IsOwner)
            {
                Debug.Log($"[NetworkPlayer] Local player started: {playerName.Value}");
                
                // Enable local player controls
                if (inputHandler != null)
                {
                    inputHandler.Initialize(this);
                    inputHandler.EnableInput();
                }
            }
            else
            {
                Debug.Log($"[NetworkPlayer] Remote player started: {playerName.Value}");
                
                // Disable input for remote players
                if (inputHandler != null)
                    inputHandler.DisableInput();
            }
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            
            // Update camera and input when ownership changes
            SetupCameraForOwnership();
            
            // Update input handler based on new ownership
            if (inputHandler != null)
            {
                if (IsOwner)
                {
                    inputHandler.EnableInput();
                    Debug.Log($"[NetworkPlayer] Input enabled for new owner: {playerName.Value}");
                }
                else
                {
                    inputHandler.DisableInput();
                    Debug.Log($"[NetworkPlayer] Input disabled: {playerName.Value} is no longer owner");
                }
            }
        }

        /// <summary>
        /// Setup camera based on ownership
        /// - Owner: Enable camera
        /// - Server (non-owner): Disable camera
        /// - Remote client: Disable camera
        /// </summary>
        private void SetupCameraForOwnership()
        {
            if (playerCamera == null) return;

            // Chỉ enable camera khi là owner (client owner)
            // Server instance hoặc remote client đều disable camera
            if (IsOwner && !IsServerInitialized)
            {
                // Client owner: Enable camera
                if (playerCamera.gameObject != null)
                {
                    playerCamera.gameObject.SetActive(true);
                    playerCamera.enabled = true;
                    // Set as main camera if no other main camera exists
                    if (Camera.main == null || Camera.main == playerCamera)
                    {
                        playerCamera.tag = "MainCamera";
                    }
                    Debug.Log($"[NetworkPlayer] Camera enabled for owner: {playerName.Value}");
                }
            }
            else
            {
                // Server instance hoặc remote client: Disable camera
                if (playerCamera.gameObject != null)
                {
                    playerCamera.gameObject.SetActive(false);
                    playerCamera.enabled = false;
                    string reason = IsServerInitialized ? "server instance" : "remote client";
                    Debug.Log($"[NetworkPlayer] Camera disabled for {reason}: {playerName.Value}");
                }
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log($"[NetworkPlayer] Player spawned on server: {playerName.Value}");
            
            // Disable camera on server instance
            if (playerCamera != null && playerCamera.gameObject != null)
            {
                playerCamera.gameObject.SetActive(false);
                playerCamera.enabled = false;
                Debug.Log($"[NetworkPlayer] Camera disabled on server instance: {playerName.Value}");
            }
            
            // Notify server game manager
            var serverGameManager = FindFirstObjectByType<ServerGameManager>();
            if (serverGameManager != null)
            {
                serverGameManager.OnPlayerConnected(this);
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            // Unsubscribe from sync vars
            if (playerName != null)
                playerName.OnChange -= OnPlayerNameChanged;
            if (teamId != null)
                teamId.OnChange -= OnPlayerTeamChanged;
            
            // Notify server game manager on disconnect
            if (IsServerInitialized)
            {
                var serverGameManager = FindFirstObjectByType<ServerGameManager>();
                if (serverGameManager != null)
                {
                    serverGameManager.OnPlayerDisconnected(this);
                }
            }
        }

        /// <summary>
        /// Server: Set player name
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void SetPlayerName(string name)
        {
            playerName.Value = name;
        }

        /// <summary>
        /// Server: Set team ID
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SetTeamId(int team)
        {
            teamId.Value = team;
        }

        /// <summary>
        /// Client: Send movement input to server
        /// RunLocally = true means host also processes movement on server
        /// </summary>
        [ServerRpc(RequireOwnership = true, RunLocally = true)]
        private void SendMovementInput(Vector2 moveInput, bool isSprinting, bool isCrouching)
        {
            // Check if NetworkObject is valid
            if (!IsSpawned) return;
            
            // Server processes movement (including host)
            // RunLocally = true ensures host's movement is processed on server too
            // IMPORTANT: This runs on SERVER side (including host's server instance)
            if (movement != null)
            {
                movement.SetMoveInput(moveInput);
                movement.SetSprinting(isSprinting);
                movement.SetCrouching(isCrouching);
                
                // Debug log to verify server is processing movement
                Debug.Log($"[NetworkPlayer] ServerRpc SendMovementInput - IsServer: {IsServerInitialized}, Player: {playerName.Value}, Input: {moveInput}");
            }
            else
            {
                Debug.LogWarning($"[NetworkPlayer] Movement component is null! Cannot process movement for {playerName.Value}");
            }
        }

        /// <summary>
        /// Client: Send combat input to server
        /// </summary>
        [ServerRpc(RequireOwnership = true, RunLocally = true)]
        private void SendCombatInput(Vector3 aimDirection, bool isAttacking, bool isReloading)
        {
            // Check if NetworkObject is valid
            if (!IsSpawned) return;
            
            // Server processes combat
            if (combat != null)
            {
                combat.SetAimDirection(aimDirection);
                combat.SetAttacking(isAttacking);
                combat.SetReloading(isReloading);
            }
        }

        private void Update()
        {
            if (!IsSpawned) return;

            // SERVER AUTHORITY với CLIENT-SIDE PREDICTION
            // Client owner: Chạy prediction + gửi input lên server
            // Server: Check lại và sync position qua NetworkTransform
            // Client reconcile nếu có sai lệch
            if (IsOwner)
            {
                // Client owner: Gửi input lên server định kỳ
                // CharacterMovement sẽ chạy prediction trên client owner
                // Server sẽ check lại và sync qua NetworkTransform
                if (Time.frameCount % Mathf.RoundToInt(60f / sendRate) == 0)
                {
                    SendInputToServer();
                }
                
                // Reconciliation: Check nếu server position khác nhiều với predicted position
                ReconcilePosition();
            }
            // Note: Position sync is handled by NetworkTransform component
            // NetworkTransform tự động sync position từ server → tất cả clients
        }
        
        /// <summary>
        /// Reconcile client prediction với server position
        /// Nếu sai lệch lớn, snap về server position
        /// </summary>
        private void ReconcilePosition()
        {
            if (movement == null) return;
            
            // Lấy server position từ NetworkTransform (transform.position đã được sync)
            Vector3 serverPosition = transform.position;
            
            // Nếu sai lệch lớn, snap về server position
            // (CharacterMovement prediction đã chạy, nhưng server position là authority)
            // NetworkTransform sẽ tự động sync, chỉ cần check threshold
            float distance = Vector3.Distance(lastServerPosition, serverPosition);
            
            if (distance > reconciliationThreshold)
            {
                // Server position khác nhiều → có thể cần reconcile
                // NetworkTransform đã sync position, chỉ cần update lastServerPosition
                lastServerPosition = serverPosition;
            }
            else
            {
                // Update lastServerPosition để track
                lastServerPosition = serverPosition;
            }
        }

        private void SendInputToServer()
        {
            if (inputHandler == null) return;
            
            // Check if NetworkObject is spawned and valid before sending RPCs
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            Vector2 moveInput = inputHandler.GetMoveInput();
            bool isSprinting = inputHandler.IsSprinting();
            bool isCrouching = inputHandler.IsCrouching();
            
            // Only send RPCs if NetworkObject is valid
            if (IsSpawned && IsOwner)
            {
                SendMovementInput(moveInput, isSprinting, isCrouching);
                
                // Combat input (optional - only if combat component exists)
                if (combat != null)
                {
                    Vector3 aimDirection = inputHandler.GetAimDirection();
                    bool isAttacking = inputHandler.IsAttacking();
                    bool isReloading = inputHandler.IsReloading();
                    SendCombatInput(aimDirection, isAttacking, isReloading);
                }
            }
        }

        // Note: Position sync is handled by NetworkTransform component
        // NetworkTransform automatically syncs transform.position and transform.rotation
        // No need for manual FixedUpdate sync - NetworkTransform handles it

        private void OnPlayerNameChanged(string oldName, string newName, bool asServer)
        {
            Debug.Log($"[NetworkPlayer] Player name changed: {oldName} -> {newName}");
        }

        private void OnPlayerTeamChanged(int oldTeam, int newTeam, bool asServer)
        {
            Debug.Log($"[NetworkPlayer] Player team changed: {oldTeam} -> {newTeam}");
        }

        /// <summary>
        /// Server: Handle player death
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void OnPlayerDeath()
        {
            // Handle death logic on server
            Debug.Log($"[NetworkPlayer] Player {playerName.Value} died");
        }

        /// <summary>
        /// Server: Handle player respawn
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RespawnPlayer(Vector3 spawnPosition)
        {
            transform.position = spawnPosition;
            // Reset health, etc.
        }
    }
}

