using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Networking;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Input;
using System.Collections.Generic;
using System.Linq;
using Camera = UnityEngine.Camera;
using NightHunt.Gameplay.Camera.Spectator;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Spectator;

namespace NightHunt.Gameplay.Camera
{
    /// <summary>
    /// Spectator Camera System (Refactored with Cinemachine)
    /// Allows players to spectate other players when dead or waiting for revive
    /// View-only mode, no controls
    /// </summary>
    public class SpectatorCameraSystem : MonoBehaviour
    {
        [Header("Spectator Settings")]
        [SerializeField] private bool enableSpectatorMode = true;
        [SerializeField] private Key toggleSpectatorKey = Key.Tab; // Toggle spectator mode on/off
        [SerializeField] private Key nextPlayerKey = Key.RightBracket; // Switch to next player
        [SerializeField] private Key previousPlayerKey = Key.LeftBracket; // Switch to previous player
        [SerializeField] private float switchCooldown = 0.5f;
        [SerializeField] private bool autoEnterOnDeath = false; // Auto enter spectator when dead (for testing, set to false to unlock)

        [Header("Camera Settings")]
        [SerializeField] private Vector3 spectatorOffset = new Vector3(0, 20, 0);
        [SerializeField] private float followSpeed = 10f;
        [SerializeField] private float rotationSpeed = 90f;

        private NetworkPlayer localPlayer;
        private NetworkPlayer currentSpectatedPlayer;
        private List<NetworkPlayer> availablePlayers = new List<NetworkPlayer>();
        private int currentSpectatedIndex = -1;
        private float lastSwitchTime = 0f;
        private bool isSpectating = false;

        // Spectator camera controller
        private SpectatorCameraController spectatorCameraController;
        private SpectatorInputHandler spectatorInputHandler;
        private InputLayerManager inputLayerManager;

        private void Awake()
        {
            // Get or create spectator camera controller
            spectatorCameraController = GetComponent<SpectatorCameraController>();
            if (spectatorCameraController == null)
            {
                spectatorCameraController = gameObject.AddComponent<SpectatorCameraController>();
            }

            // Get or create spectator input handler
            spectatorInputHandler = GetComponent<SpectatorInputHandler>();
            if (spectatorInputHandler == null)
            {
                spectatorInputHandler = gameObject.AddComponent<SpectatorInputHandler>();
            }

            inputLayerManager = InputLayerManager.Instance;
        }

        private void Start()
        {
            // Find local player
            FindLocalPlayer();
            
            // Find all players
            RefreshPlayerList();
        }

        private void Update()
        {
            if (!enableSpectatorMode) return;

            // Handle toggle spectator mode input (Tab)
            HandleToggleSpectatorInput();

            // Check if should auto-enter spectator mode (player is dead, etc.)
            if (autoEnterOnDeath)
            {
                CheckSpectatorMode();
            }

            // Handle spectator input (switch players)
            // Input is handled by SpectatorInputHandler component
            if (isSpectating)
            {
                UpdateSpectatorCamera();
            }
        }

        /// <summary>
        /// Handle toggle spectator mode input (Tab key)
        /// </summary>
        private void HandleToggleSpectatorInput()
        {
            if (Keyboard.current == null) return;

            // Toggle spectator mode with Tab key
            if (Keyboard.current[toggleSpectatorKey].wasPressedThisFrame)
            {
                if (isSpectating)
                {
                    // Exit spectator mode, return to own camera
                    ExitSpectatorMode();
                }
                else
                {
                    // Enter spectator mode
                    EnterSpectatorMode();
                }
            }
        }

        /// <summary>
        /// Check if should auto-enter spectator mode (when player dies)
        /// Only called if autoEnterOnDeath is true
        /// </summary>
        private void CheckSpectatorMode()
        {
            if (localPlayer == null)
            {
                FindLocalPlayer();
                return;
            }

            // Check if player is dead or downed (waiting for revive)
            CharacterDeathSystem deathSystem = localPlayer.GetComponent<CharacterDeathSystem>();
            bool shouldSpectate = false;

            if (deathSystem != null)
            {
                // Enter spectator mode if dead or downed
                shouldSpectate = deathSystem.IsDead || deathSystem.IsDowned;
            }

            // Enter spectator mode if needed
            if (shouldSpectate && !isSpectating)
            {
                EnterSpectatorMode();
            }
            // Exit spectator mode if player is alive again
            else if (!shouldSpectate && isSpectating)
            {
                ExitSpectatorMode();
            }
        }

        /// <summary>
        /// Enter spectator mode
        /// </summary>
        public void EnterSpectatorMode()
        {
            if (!enableSpectatorMode) return;

            isSpectating = true;
            RefreshPlayerList();

            // Disable local player camera
            if (localPlayer != null && localPlayer.PlayerCamera != null)
            {
                localPlayer.PlayerCamera.enabled = false;
            }

            // Enable spectator camera
            if (spectatorCameraController != null)
            {
                // Start spectating first available player
                if (availablePlayers.Count > 0)
                {
                    currentSpectatedIndex = 0;
                    currentSpectatedPlayer = availablePlayers[0];
                    spectatorCameraController.StartSpectating(currentSpectatedPlayer);
                }
            }

            // Update input state
            if (inputLayerManager != null)
            {
                inputLayerManager.TransitionToState(InputState.Spectating);
            }

            // Enable spectator input
            if (spectatorInputHandler != null)
            {
                spectatorInputHandler.OnNextPlayer += OnNextPlayer;
                spectatorInputHandler.OnPreviousPlayer += OnPreviousPlayer;
                spectatorInputHandler.OnExitSpectator += ExitSpectatorMode;
                spectatorInputHandler.EnableInput();
            }

            Debug.Log("[SpectatorCameraSystem] Entered spectator mode");
        }

        /// <summary>
        /// Exit spectator mode
        /// </summary>
        public void ExitSpectatorMode()
        {
            isSpectating = false;

            // Disable spectator camera
            if (spectatorCameraController != null)
            {
                spectatorCameraController.StopSpectating();
            }

            // Enable local player camera
            if (localPlayer != null && localPlayer.PlayerCamera != null)
            {
                localPlayer.PlayerCamera.enabled = true;
            }

            // Update input state
            if (inputLayerManager != null)
            {
                // Determine appropriate state based on player status
                // TODO: Check if player is alive or dead
                inputLayerManager.TransitionToState(InputState.PlayerAlive);
            }

            // Disable spectator input
            if (spectatorInputHandler != null)
            {
                spectatorInputHandler.OnNextPlayer -= OnNextPlayer;
                spectatorInputHandler.OnPreviousPlayer -= OnPreviousPlayer;
                spectatorInputHandler.OnExitSpectator -= ExitSpectatorMode;
                spectatorInputHandler.DisableInput();
            }

            currentSpectatedPlayer = null;
            currentSpectatedIndex = -1;

            Debug.Log("[SpectatorCameraSystem] Exited spectator mode");
        }

        /// <summary>
        /// Handle next player input
        /// </summary>
        private void OnNextPlayer()
        {
            if (spectatorCameraController != null)
            {
                spectatorCameraController.SwitchToNextPlayer();
                currentSpectatedPlayer = spectatorCameraController.CurrentSpectatedPlayer;
            }
        }

        /// <summary>
        /// Handle previous player input
        /// </summary>
        private void OnPreviousPlayer()
        {
            if (spectatorCameraController != null)
            {
                spectatorCameraController.SwitchToPreviousPlayer();
                currentSpectatedPlayer = spectatorCameraController.CurrentSpectatedPlayer;
            }
        }

        /// <summary>
        /// Update spectator camera (handled by Cinemachine)
        /// </summary>
        private void UpdateSpectatorCamera()
        {
            if (currentSpectatedPlayer == null)
            {
                RefreshPlayerList();
                if (availablePlayers.Count == 0)
                {
                    ExitSpectatorMode();
                    return;
                }
                
                // Reassign if lost reference
                if (currentSpectatedIndex >= 0 && currentSpectatedIndex < availablePlayers.Count)
                {
                    currentSpectatedPlayer = availablePlayers[currentSpectatedIndex];
                    if (spectatorCameraController != null)
                    {
                        spectatorCameraController.StartSpectating(currentSpectatedPlayer);
                    }
                }
                else
                {
                    currentSpectatedIndex = 0;
                    currentSpectatedPlayer = availablePlayers[0];
                    if (spectatorCameraController != null)
                    {
                        spectatorCameraController.StartSpectating(currentSpectatedPlayer);
                    }
                }
            }
        }

        /// <summary>
        /// Refresh list of available players to spectate
        /// </summary>
        public void RefreshPlayerList()
        {
            availablePlayers.Clear();
            
            // Use TeamSpectatorFilter to get teammates
            if (localPlayer != null)
            {
                availablePlayers = TeamSpectatorFilter.GetTeammates(localPlayer);
            }
            else
            {
                // Fallback: get all players
                var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
                foreach (var player in allPlayers)
                {
                    if (player != null && player.gameObject.activeInHierarchy)
                    {
                        // Check if player is alive (not dead)
                        CharacterDeathSystem deathSystem = player.GetComponent<CharacterDeathSystem>();
                        bool isAlive = deathSystem == null || deathSystem.IsAlive;
                        
                        if (isAlive)
                        {
                            availablePlayers.Add(player);
                        }
                    }
                }
            }

            // Update spectator camera controller
            if (spectatorCameraController != null)
            {
                spectatorCameraController.RefreshPlayerList();
            }

            // Remove current spectated player if it's no longer available
            if (currentSpectatedPlayer != null && !availablePlayers.Contains(currentSpectatedPlayer))
            {
                currentSpectatedPlayer = null;
                currentSpectatedIndex = -1;
            }

            // Update index if current player is still in list
            if (currentSpectatedPlayer != null && availablePlayers.Contains(currentSpectatedPlayer))
            {
                currentSpectatedIndex = availablePlayers.IndexOf(currentSpectatedPlayer);
            }
        }

        /// <summary>
        /// Find local player
        /// </summary>
        private void FindLocalPlayer()
        {
            NetworkPlayer[] allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player.IsLocalPlayer)
                {
                    localPlayer = player;
                    break;
                }
            }
        }

        /// <summary>
        /// Get current spectated player
        /// </summary>
        public NetworkPlayer CurrentSpectatedPlayer => currentSpectatedPlayer;

        /// <summary>
        /// Check if currently spectating
        /// </summary>
        public bool IsSpectating => isSpectating;
    }
}

