using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Character;
using Unity.Cinemachine;
using UnityEngine.Serialization;

namespace NightHunt.Networking
{
    /// <summary>
    /// Network Player Object - Simplified for FishNet Prediction
    /// 
    /// With PredictedObject + NetworkTransform:
    /// - NO manual ServerRpc for movement input
    /// - PredictedObject handles Replicate/Reconcile automatically
    /// - NetworkTransform syncs to remote clients
    /// - We only handle camera, input enabling, and game events
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {

        [SerializeField] private CharacterNormalMovement movement;
        [SerializeField] private CharacterCombat combat;
        [SerializeField] private CinemachineCamera playerCamera;

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
            if (movement == null)
                movement = GetComponent<CharacterNormalMovement>();

            if (combat == null)
                combat = GetComponent<CharacterCombat>();


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
            
            // Server instance: Disable camera
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
            
            // Unsubscribe
            if (playerName != null)
                playerName.OnChange -= OnPlayerNameChanged;
            if (teamId != null)
                teamId.OnChange -= OnPlayerTeamChanged;
            
            if (IsOwner && InputManager.Instance != null)
            {
                InputManager.Instance.DisableAllInput();
            }
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            
            SetupCameraForOwnership();
            SetupInputForOwnership();
        }

        #endregion

        #region Camera Setup

        private void SetupCameraForOwnership()
        {
            if (playerCamera == null) return;

            // Only enable camera for client owner
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
                }
            }
        }

        #endregion

        #region Input Setup

        private void SetupInputForOwnership()
        {
            if (IsOwner && InputManager.Instance != null)
            {
                InputManager.Instance.EnableAllInput();
            }
        }

        #endregion

        #region Data Sync

        [Server]
        public void SetPlayerName(string name)
        {
            playerName.Value = name;
        }

        [Server]
        public void SetTeamId(int team)
        {
            teamId.Value = team;
            Debug.Log($"[NetworkPlayer] Team set: {playerName.Value} → Team {team}");
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