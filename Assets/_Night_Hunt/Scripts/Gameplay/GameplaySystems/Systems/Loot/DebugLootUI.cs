using UnityEngine;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Connection;
using FishNet.Object;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Debug UI for testing loot system
    /// Simple OnGUI display - shows container/corpse contents and allows taking items
    /// </summary>
    public class DebugLootUI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool showDebugUI = true;

        private WorldContainer currentContainer;
        private WorldCorpse currentCorpse;
        private Vector2 scrollPosition;

        private void OnEnable()
        {
            // Subscribe to events
            WorldContainer.OnContainerOpened += OnContainerOpened;
            WorldCorpse.OnCorpseOpened += OnCorpseOpened;
        }

        private void OnDisable()
        {
            WorldContainer.OnContainerOpened -= OnContainerOpened;
            WorldCorpse.OnCorpseOpened -= OnCorpseOpened;
        }

        private void OnContainerOpened(WorldContainer container, NetworkConnection viewer)
        {
            // Only show for local client
            var localConn = GetLocalConnection();
            if (viewer != null && localConn != null && localConn != viewer)
                return;

            currentContainer = container;
            currentCorpse = null;

            // Switch to InventoryOpen state (disable E/F, enable drag&drop)
            if (NightHunt.Gameplay.Input.Core.InputLayerManager.Instance != null)
            {
                NightHunt.Gameplay.Input.Core.InputLayerManager.Instance.TransitionToState(
                    NightHunt.Gameplay.Input.InputState.InventoryOpen
                );
            }
        }

        private void OnCorpseOpened(WorldCorpse corpse, NetworkConnection viewer)
        {
            // Only show for local client
            var localConn = GetLocalConnection();
            if (viewer != null && localConn != null && localConn != viewer)
                return;

            currentCorpse = corpse;
            currentContainer = null;

            // Switch to InventoryOpen state (disable E/F, enable drag&drop)
            if (NightHunt.Gameplay.Input.Core.InputLayerManager.Instance != null)
            {
                NightHunt.Gameplay.Input.Core.InputLayerManager.Instance.TransitionToState(
                    NightHunt.Gameplay.Input.InputState.InventoryOpen
                );
            }
        }

        private void OnGUI()
        {
            if (!showDebugUI) return;

            if (currentContainer != null)
            {
                DrawContainerDebug();
            }
            else if (currentCorpse != null)
            {
                DrawCorpseDebug();
            }
        }

        private void DrawContainerDebug()
        {
            float width = 400f;
            float height = 500f;
            float x = Screen.width - width - 10f;
            float y = 10f;

            GUILayout.BeginArea(new Rect(x, y, width, height));
            GUILayout.Box("=== CONTAINER LOOT ===");

            var storage = currentContainer.GetStorage();
            
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            if (storage.Count == 0)
            {
                GUILayout.Label("Container is empty");
            }
            else
            {
                for (int i = 0; i < storage.Count; i++)
                {
                    var item = storage[i];
                    var def = ItemDatabase.GetDefinition(item.DefinitionID);

                    GUILayout.BeginHorizontal(GUILayout.Height(30));
                    
                    string itemName = def != null ? def.DisplayName : item.DefinitionID;
                    GUILayout.Label($"[{i}] {itemName} x{item.Quantity}", GUILayout.Width(250));

                    if (GUILayout.Button("Take All", GUILayout.Width(80)))
                    {
                        var playerNob = GetLocalPlayerNob();
                        if (playerNob != null)
                            currentContainer.RequestTakeItem(playerNob, i, item.Quantity);
                    }

                    if (item.Quantity > 1 && GUILayout.Button("Take 1", GUILayout.Width(60)))
                    {
                        var playerNob = GetLocalPlayerNob();
                        if (playerNob != null)
                            currentContainer.RequestTakeItem(playerNob, i, 1);
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();

            if (GUILayout.Button("Close"))
            {
                currentContainer = null;

                // Switch back to PlayerAlive state (enable E/F)
                if (NightHunt.Gameplay.Input.Core.InputLayerManager.Instance != null)
                {
                    NightHunt.Gameplay.Input.Core.InputLayerManager.Instance.TransitionToState(
                        NightHunt.Gameplay.Input.InputState.PlayerAlive
                    );
                }
            }

            GUILayout.EndArea();
        }

        private void DrawCorpseDebug()
        {
            float width = 400f;
            float height = 500f;
            float x = Screen.width - width - 10f;
            float y = 10f;

            GUILayout.BeginArea(new Rect(x, y, width, height));
            GUILayout.Box("=== CORPSE LOOT ===");

            var storage = currentCorpse.GetStorage();
            
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            if (storage.Count == 0)
            {
                GUILayout.Label("Corpse is empty");
            }
            else
            {
                for (int i = 0; i < storage.Count; i++)
                {
                    var item = storage[i];
                    var def = ItemDatabase.GetDefinition(item.DefinitionID);

                    GUILayout.BeginHorizontal(GUILayout.Height(30));
                    
                    string itemName = def != null ? def.DisplayName : item.DefinitionID;
                    GUILayout.Label($"[{i}] {itemName} x{item.Quantity}", GUILayout.Width(250));

                    if (GUILayout.Button("Take All", GUILayout.Width(80)))
                    {
                        var playerNob = GetLocalPlayerNob();
                        if (playerNob != null)
                            currentCorpse.RequestTakeItem(playerNob, i, item.Quantity);
                    }

                    if (item.Quantity > 1 && GUILayout.Button("Take 1", GUILayout.Width(60)))
                    {
                        var playerNob = GetLocalPlayerNob();
                        if (playerNob != null)
                            currentCorpse.RequestTakeItem(playerNob, i, 1);
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();

            if (GUILayout.Button("Close"))
            {
                currentCorpse = null;

                // Switch back to PlayerAlive state (enable E/F)
                if (NightHunt.Gameplay.Input.Core.InputLayerManager.Instance != null)
                {
                    NightHunt.Gameplay.Input.Core.InputLayerManager.Instance.TransitionToState(
                        NightHunt.Gameplay.Input.InputState.PlayerAlive
                    );
                }
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Lấy NetworkObject của local player — dùng để thầy cho RPC thay vì conn.Objects loop.
        /// </summary>
        private NetworkObject GetLocalPlayerNob()
        {
            foreach (var np in Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (np.IsOwner) return np.GetComponent<NetworkObject>();
            }
            Debug.LogWarning("[DebugLootUI] Không tìm thấy local player NetworkObject!");
            return null;
        }

        /// <summary>
        /// Get local client's NetworkConnection
        /// </summary>
        private NetworkConnection GetLocalConnection()
        {
            // Try NetworkGameManager first (preferred)
            if (NightHunt.Networking.NetworkGameManager.Instance != null)
            {
                var networkManager = NightHunt.Networking.NetworkGameManager.Instance.NetworkManager;
                if (networkManager != null && networkManager.ClientManager != null)
                {
                    return networkManager.ClientManager.Connection;
                }
            }

            // Fallback: Find NetworkManager directly
            var nm = FindFirstObjectByType<NetworkManager>();
            if (nm != null && nm.ClientManager != null)
            {
                return nm.ClientManager.Connection;
            }

            Debug.LogWarning("[DebugLootUI] Could not find NetworkManager or ClientManager!");
            return null;
        }
    }
}
