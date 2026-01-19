using UnityEngine;
using Unity.Cinemachine;
using NightHunt.Networking;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Gameplay.Camera.Spectator
{
    /// <summary>
    /// Cinemachine spectator camera controller
    /// </summary>
    public class SpectatorCameraController : MonoBehaviour
    {
        [Header("Spectator Camera Settings")]
        [SerializeField] private CinemachineVirtualCamera spectatorCamera;
        [SerializeField] private float followSpeed = 10f;
        [SerializeField] private float rotationSpeed = 90f;

        private NetworkPlayer currentSpectatedPlayer;
        private List<NetworkPlayer> availablePlayers = new List<NetworkPlayer>();
        private int currentIndex = -1;
        private bool isSpectating = false;

        private void Awake()
        {
            // Create spectator camera if not assigned
            if (spectatorCamera == null)
            {
                GameObject camObj = new GameObject("SpectatorCamera");
                camObj.transform.SetParent(transform);
                spectatorCamera = camObj.AddComponent<CinemachineVirtualCamera>();
                spectatorCamera.Priority = 100; // Higher than player camera
            }
        }

        /// <summary>
        /// Start spectating a player
        /// </summary>
        public void StartSpectating(NetworkPlayer player)
        {
            if (player == null) return;

            currentSpectatedPlayer = player;
            isSpectating = true;

            if (spectatorCamera != null)
            {
                spectatorCamera.Follow = player.transform;
                spectatorCamera.LookAt = player.transform;
                spectatorCamera.enabled = true;
            }

            Debug.Log($"[SpectatorCameraController] Started spectating: {player.PlayerName}");
        }

        /// <summary>
        /// Stop spectating
        /// </summary>
        public void StopSpectating()
        {
            isSpectating = false;
            currentSpectatedPlayer = null;

            if (spectatorCamera != null)
            {
                spectatorCamera.enabled = false;
                spectatorCamera.Follow = null;
                spectatorCamera.LookAt = null;
            }
        }

        /// <summary>
        /// Switch to next player
        /// </summary>
        public void SwitchToNextPlayer()
        {
            RefreshPlayerList();
            if (availablePlayers.Count == 0) return;

            currentIndex = (currentIndex + 1) % availablePlayers.Count;
            StartSpectating(availablePlayers[currentIndex]);
        }

        /// <summary>
        /// Switch to previous player
        /// </summary>
        public void SwitchToPreviousPlayer()
        {
            RefreshPlayerList();
            if (availablePlayers.Count == 0) return;

            currentIndex = (currentIndex - 1 + availablePlayers.Count) % availablePlayers.Count;
            StartSpectating(availablePlayers[currentIndex]);
        }

        /// <summary>
        /// Refresh list of available players to spectate
        /// </summary>
        public void RefreshPlayerList()
        {
            availablePlayers.Clear();

            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            var localPlayer = allPlayers.FirstOrDefault(p => p.IsLocalPlayer);

            foreach (var player in allPlayers)
            {
                // Only add players that are not the local player and are alive
                if (player != localPlayer && player != null && player.gameObject.activeInHierarchy)
                {
                    // TODO: Check if player is alive (not dead)
                    availablePlayers.Add(player);
                }
            }

            // Update index if current player is still in list
            if (currentSpectatedPlayer != null && availablePlayers.Contains(currentSpectatedPlayer))
            {
                currentIndex = availablePlayers.IndexOf(currentSpectatedPlayer);
            }
            else if (availablePlayers.Count > 0)
            {
                currentIndex = 0;
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

