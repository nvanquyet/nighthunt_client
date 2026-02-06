using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Networking;
using NightHunt.Inventory.Core;

namespace NightHunt.Networking.ClientOnly
{
    /// <summary>
    /// Manages UI visibility based on local player ownership.
    /// Automatically finds all Canvas components and shows/hides them based on whether
    /// the local player is spawned and owns the NetworkPlayer object.
    /// </summary>
    public class ClientOnlyUIManager : MonoBehaviour
    {
        public static ClientOnlyUIManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private bool autoFindCanvases = true;
        [SerializeField] private float playerSearchInterval = 0.5f; // Search for player every 0.5 seconds if not found

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private List<Canvas> managedCanvases = new List<Canvas>();
        private NetworkPlayer currentLocalPlayer;
        private float lastPlayerSearchTime = 0f;
        private bool hasLocalPlayer = false;

        #region Lifecycle

        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (autoFindCanvases)
            {
                FindAllCanvases();
            }
        }

        private void Update()
        {
            // Periodically search for local player if not found
            if (!hasLocalPlayer && Time.time - lastPlayerSearchTime >= playerSearchInterval)
            {
                FindLocalPlayer();
                lastPlayerSearchTime = Time.time;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Canvas Management

        /// <summary>
        /// Automatically finds all Canvas components in the scene
        /// </summary>
        public void FindAllCanvases()
        {
            managedCanvases.Clear();
            Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
            managedCanvases.AddRange(allCanvases);

            if (enableDebugLogs)
                Debug.Log($"[ClientOnlyUIManager] Found {managedCanvases.Count} Canvas components");

            // Update UI visibility based on current local player state
            UpdateUIVisibility();
        }

        /// <summary>
        /// Register a Canvas to be managed by this system
        /// </summary>
        public void RegisterCanvas(Canvas canvas)
        {
            if (canvas == null) return;

            if (!managedCanvases.Contains(canvas))
            {
                managedCanvases.Add(canvas);
                if (enableDebugLogs)
                    Debug.Log($"[ClientOnlyUIManager] Registered Canvas: {canvas.name}");
            }

            // Update visibility immediately
            UpdateCanvasVisibility(canvas);
        }

        /// <summary>
        /// Unregister a Canvas from management
        /// </summary>
        public void UnregisterCanvas(Canvas canvas)
        {
            if (canvas != null && managedCanvases.Remove(canvas))
            {
                if (enableDebugLogs)
                    Debug.Log($"[ClientOnlyUIManager] Unregistered Canvas: {canvas.name}");
            }
        }

        #endregion

        #region Player Management

        /// <summary>
        /// Find the local player in the scene
        /// </summary>
        private void FindLocalPlayer()
        {
            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
            NetworkPlayer localPlayer = players.FirstOrDefault(p => p.IsLocalPlayer);

            if (localPlayer != null && localPlayer != currentLocalPlayer)
            {
                SetLocalPlayer(localPlayer);
            }
            else if (localPlayer == null && currentLocalPlayer != null)
            {
                // Local player disconnected
                ClearLocalPlayer();
            }
        }

        /// <summary>
        /// Set the current local player and update UI visibility
        /// Called by NetworkPlayer when it spawns
        /// </summary>
        public void SetLocalPlayer(NetworkPlayer player)
        {
            if (player == null || !player.IsLocalPlayer)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[ClientOnlyUIManager] Attempted to set non-local player as local player");
                return;
            }

            currentLocalPlayer = player;
            hasLocalPlayer = true;

            if (enableDebugLogs)
                Debug.Log($"[ClientOnlyUIManager] Local player set: {player.PlayerName}");

            // Notify SpectateManager
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.SetLocalPlayer(player);
            }

            UpdateUIVisibility();
        }

        /// <summary>
        /// Clear the current local player (e.g., when disconnected)
        /// </summary>
        public void ClearLocalPlayer()
        {
            if (currentLocalPlayer != null)
            {
                if (enableDebugLogs)
                    Debug.Log("[ClientOnlyUIManager] Local player cleared");
            }

            currentLocalPlayer = null;
            hasLocalPlayer = false;
            UpdateUIVisibility();
        }

        /// <summary>
        /// Notify that local player ownership has changed
        /// Called by NetworkPlayer when ownership changes
        /// </summary>
        public void OnLocalPlayerOwnershipChanged(NetworkPlayer player)
        {
            if (player != null && player.IsLocalPlayer)
            {
                SetLocalPlayer(player);
            }
            else
            {
                ClearLocalPlayer();
            }
        }

        #endregion

        #region UI Visibility

        /// <summary>
        /// Update visibility of all managed UI canvases
        /// </summary>
        private void UpdateUIVisibility()
        {
            bool shouldShow = hasLocalPlayer && currentLocalPlayer != null && currentLocalPlayer.IsLocalPlayer;

            foreach (var canvas in managedCanvases)
            {
                if (canvas != null)
                {
                    UpdateCanvasVisibility(canvas, shouldShow);
                }
            }

            if (enableDebugLogs)
                Debug.Log($"[ClientOnlyUIManager] Updated UI visibility: {(shouldShow ? "SHOW" : "HIDE")}");
        }

        /// <summary>
        /// Update visibility of a specific canvas
        /// </summary>
        private void UpdateCanvasVisibility(Canvas canvas, bool? forceState = null)
        {
            if (canvas == null) return;

            bool shouldShow = forceState ?? (hasLocalPlayer && currentLocalPlayer != null && currentLocalPlayer.IsLocalPlayer);
            canvas.gameObject.SetActive(shouldShow);
        }

        /// <summary>
        /// Force show all UI (for debugging or special cases)
        /// </summary>
        public void ForceShowAllUI()
        {
            foreach (var canvas in managedCanvases)
            {
                if (canvas != null)
                {
                    canvas.gameObject.SetActive(true);
                }
            }

            if (enableDebugLogs)
                Debug.Log("[ClientOnlyUIManager] Force showing all UI");
        }

        /// <summary>
        /// Force hide all UI (for debugging or special cases)
        /// </summary>
        public void ForceHideAllUI()
        {
            foreach (var canvas in managedCanvases)
            {
                if (canvas != null)
                {
                    canvas.gameObject.SetActive(false);
                }
            }

            if (enableDebugLogs)
                Debug.Log("[ClientOnlyUIManager] Force hiding all UI");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the current local player
        /// </summary>
        public NetworkPlayer GetLocalPlayer()
        {
            return currentLocalPlayer;
        }

        /// <summary>
        /// Check if there is a local player
        /// </summary>
        public bool HasLocalPlayer()
        {
            return hasLocalPlayer && currentLocalPlayer != null && currentLocalPlayer.IsLocalPlayer;
        }

        /// <summary>
        /// Get count of managed canvases
        /// </summary>
        public int GetManagedCanvasCount()
        {
            return managedCanvases.Count;
        }

        #endregion
    }
}
