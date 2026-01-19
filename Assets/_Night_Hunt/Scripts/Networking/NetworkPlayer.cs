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
    /// Game logic is handled by ServerGameManager and other systems
    /// 
    /// Responsibilities:
    /// - Sync player data (name, team)
    /// - Handle input transmission (client → server)
    /// - Setup camera for owner
    /// - Client-side prediction
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
        [SerializeField] private float sendRate = 20f;

        // Synchronized variables
        private readonly SyncVar<string> playerName = new SyncVar<string>();
        private readonly SyncVar<int> teamId = new SyncVar<int>();

        // Client prediction state
        private Vector3 lastServerPosition;
        private float reconciliationThreshold = 0.1f;

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
        /// Called by ServerGameManager or other server-side systems
        /// </summary>
        [Server]
        public void SetPlayerName(string name)
        {
            playerName.Value = name;
        }

        /// <summary>
        /// Server: Set team ID
        /// Called by TeamSystem on server
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

            // Client owner: Send input to server periodically
            if (IsOwner)
            {
                if (Time.frameCount % Mathf.RoundToInt(60f / sendRate) == 0)
                {
                    SendInputToServer();
                }
                
                ReconcilePosition();
            }
        }

        private void SendInputToServer()
        {
            if (inputHandler == null || !IsSpawned || !IsOwner)
                return;

            Vector2 moveInput = inputHandler.GetMoveInput();
            bool isSprinting = inputHandler.IsSprinting();
            bool isCrouching = inputHandler.IsCrouching();
            
            SendMovementInput(moveInput, isSprinting, isCrouching);
            
            if (combat != null)
            {
                Vector3 aimDirection = inputHandler.GetAimDirection();
                bool isAttacking = inputHandler.IsAttacking();
                bool isReloading = inputHandler.IsReloading();
                SendCombatInput(aimDirection, isAttacking, isReloading);
            }
        }

        [ServerRpc(RequireOwnership = true, RunLocally = true)]
        private void SendMovementInput(Vector2 moveInput, bool isSprinting, bool isCrouching)
        {
            if (!IsSpawned || movement == null) return;
            
            movement.SetMoveInput(moveInput);
            movement.SetSprinting(isSprinting);
            movement.SetCrouching(isCrouching);
        }

        [ServerRpc(RequireOwnership = true, RunLocally = true)]
        private void SendCombatInput(Vector3 aimDirection, bool isAttacking, bool isReloading)
        {
            if (!IsSpawned || combat == null) return;
            
            combat.SetAimDirection(aimDirection);
            combat.SetAttacking(isAttacking);
            combat.SetReloading(isReloading);
        }

        #endregion

        #region Client-Side Prediction

        private void ReconcilePosition()
        {
            if (movement == null) return;
            
            Vector3 serverPosition = transform.position;
            float distance = Vector3.Distance(lastServerPosition, serverPosition);
            
            if (distance > reconciliationThreshold)
            {
                lastServerPosition = serverPosition;
            }
            else
            {
                lastServerPosition = serverPosition;
            }
        }

        #endregion

        #region Callbacks (for logging/debugging)

        private void OnPlayerNameChanged(string oldName, string newName, bool asServer)
        {
            Debug.Log($"[NetworkPlayer] Name changed: {oldName} → {newName}");
        }

        private void OnPlayerTeamChanged(int oldTeam, int newTeam, bool asServer)
        {
            Debug.Log($"[NetworkPlayer] Team changed: {oldTeam} → {newTeam}");
        }

        #endregion

        #region Game Events (called by other systems)

        /// <summary>
        /// Called when player dies (by health system or combat system)
        /// </summary>
        public void OnDeath()
        {
            Debug.Log($"[NetworkPlayer] Player {playerName.Value} died");
            
            // Notify ServerGameManager to handle respawn
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
    }
}