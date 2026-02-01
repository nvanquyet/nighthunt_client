using UnityEngine;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Static registry for UI components
    /// UI components register themselves on Awake/Start
    /// Avoids expensive FindObject calls
    /// </summary>
    public static class UIRegistry
    {
        private static PlayerHUD _playerHUD;
        private static InventoryPanel _inventoryPanel;

        /// <summary>
        /// Register PlayerHUD (called by PlayerHUD on Awake)
        /// </summary>
        public static void RegisterPlayerHUD(PlayerHUD hud)
        {
            if (hud == null) return;
            
            if (_playerHUD != null && _playerHUD != hud)
            {
                Debug.LogWarning($"[UIRegistry] Multiple PlayerHUD found! Replacing {_playerHUD.name} with {hud.name}");
            }
            
            _playerHUD = hud;
            Debug.Log($"[UIRegistry] PlayerHUD registered: {hud.name}");
        }

        /// <summary>
        /// Unregister PlayerHUD (called by PlayerHUD on Destroy)
        /// </summary>
        public static void UnregisterPlayerHUD(PlayerHUD hud)
        {
            if (_playerHUD == hud)
            {
                _playerHUD = null;
                Debug.Log($"[UIRegistry] PlayerHUD unregistered: {hud.name}");
            }
        }

        /// <summary>
        /// Register InventoryPanel (called by InventoryPanel on Awake)
        /// </summary>
        public static void RegisterInventoryPanel(InventoryPanel panel)
        {
            if (panel == null) return;
            
            if (_inventoryPanel != null && _inventoryPanel != panel)
            {
                Debug.LogWarning($"[UIRegistry] Multiple InventoryPanel found! Replacing {_inventoryPanel.name} with {panel.name}");
            }
            
            _inventoryPanel = panel;
            Debug.Log($"[UIRegistry] InventoryPanel registered: {panel.name}");
        }

        /// <summary>
        /// Unregister InventoryPanel (called by InventoryPanel on Destroy)
        /// </summary>
        public static void UnregisterInventoryPanel(InventoryPanel panel)
        {
            if (_inventoryPanel == panel)
            {
                _inventoryPanel = null;
                Debug.Log($"[UIRegistry] InventoryPanel unregistered: {panel.name}");
            }
        }

        /// <summary>
        /// Get registered PlayerHUD (no FindObject, instant access)
        /// </summary>
        public static PlayerHUD GetPlayerHUD()
        {
            Debug.Log($"[UIRegistry] GetPlayerHUD() called - Result: {(_playerHUD != null ? _playerHUD.name : "NULL")}");
            return _playerHUD;
        }

        /// <summary>
        /// Get registered InventoryPanel (no FindObject, instant access)
        /// </summary>
        public static InventoryPanel GetInventoryPanel()
        {
            Debug.Log($"[UIRegistry] GetInventoryPanel() called - Result: {(_inventoryPanel != null ? _inventoryPanel.name : "NULL")}");
            return _inventoryPanel;
        }

        /// <summary>
        /// Check if UI components are registered
        /// </summary>
        public static bool IsUIReady()
        {
            bool ready = _playerHUD != null && _inventoryPanel != null;
            Debug.Log($"[UIRegistry] IsUIReady() called - PlayerHUD: {(_playerHUD != null ? _playerHUD.name : "NULL")}, InventoryPanel: {(_inventoryPanel != null ? _inventoryPanel.name : "NULL")}, Ready: {ready}");
            return ready;
        }

        /// <summary>
        /// Clear all registrations (for testing/cleanup)
        /// </summary>
        public static void Clear()
        {
            _playerHUD = null;
            _inventoryPanel = null;
            Debug.Log("[UIRegistry] All UI registrations cleared");
        }
    }
}
