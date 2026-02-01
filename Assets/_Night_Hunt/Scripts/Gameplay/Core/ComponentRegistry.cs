using System.Collections.Generic;
using UnityEngine;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Items;
using NightHunt.Gameplay.Weapons;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Core
{
    /// <summary>
    /// Central registry for gameplay components
    /// Components register themselves on Awake, avoiding expensive FindObject calls
    /// Similar to UIRegistry but for gameplay components
    /// 
    /// MULTIPLAYER SUPPORT (FishNet Pro v4):
    /// - Uses Dictionary<NetworkPlayer, Component> to support multiple players
    /// - Each player has their own component instances
    /// - Thread-safe for FishNet's main thread (Unity main thread only)
    /// - Components automatically unregister on Destroy/Despawn
    /// </summary>
    public static class ComponentRegistry
    {
        // Registry storage: Dictionary<NetworkPlayer, Component>
        // Each NetworkPlayer has their own component instances (multiplayer support)
        private static Dictionary<NetworkPlayer, InventoryService> _inventoryServices = new Dictionary<NetworkPlayer, InventoryService>();
        private static Dictionary<NetworkPlayer, CharacterStats> _characterStats = new Dictionary<NetworkPlayer, CharacterStats>();
        private static Dictionary<NetworkPlayer, CharacterCombat> _characterCombat = new Dictionary<NetworkPlayer, CharacterCombat>();
        private static Dictionary<NetworkPlayer, IMovementController> _movementControllers = new Dictionary<NetworkPlayer, IMovementController>();
        private static Dictionary<NetworkPlayer, InputRouter> _inputRouters = new Dictionary<NetworkPlayer, InputRouter>();
        private static Dictionary<NetworkPlayer, ItemUsageSystem> _itemUsageSystems = new Dictionary<NetworkPlayer, ItemUsageSystem>();
        private static Dictionary<NetworkPlayer, WeaponSwitchingSystem> _weaponSwitchingSystems = new Dictionary<NetworkPlayer, WeaponSwitchingSystem>();
        
        /// <summary>
        /// Get all registered players (for debugging/monitoring)
        /// </summary>
        public static IReadOnlyCollection<NetworkPlayer> GetAllRegisteredPlayers()
        {
            HashSet<NetworkPlayer> players = new HashSet<NetworkPlayer>();
            players.UnionWith(_inventoryServices.Keys);
            players.UnionWith(_characterStats.Keys);
            players.UnionWith(_characterCombat.Keys);
            players.UnionWith(_movementControllers.Keys);
            players.UnionWith(_inputRouters.Keys);
            players.UnionWith(_itemUsageSystems.Keys);
            players.UnionWith(_weaponSwitchingSystems.Keys);
            return players;
        }
        
        /// <summary>
        /// Clean up registrations for a specific player (called when player despawns)
        /// </summary>
        public static void CleanupPlayer(NetworkPlayer player)
        {
            if (player == null) return;
            
            _inventoryServices.Remove(player);
            _characterStats.Remove(player);
            _characterCombat.Remove(player);
            _movementControllers.Remove(player);
            _inputRouters.Remove(player);
            _itemUsageSystems.Remove(player);
            _weaponSwitchingSystems.Remove(player);
            
            Debug.Log($"[ComponentRegistry] Cleaned up all registrations for player: {player.name}");
        }

        #region InventoryService Registration

        /// <summary>
        /// Register InventoryService for a NetworkPlayer (called by InventoryService on Awake)
        /// </summary>
        public static void RegisterInventoryService(NetworkPlayer player, InventoryService service)
        {
            if (player == null || service == null)
            {
                Debug.LogWarning("[ComponentRegistry] Cannot register: player or service is null");
                return;
            }

            if (_inventoryServices.ContainsKey(player))
            {
                Debug.LogWarning($"[ComponentRegistry] InventoryService already registered for {player.name}. Replacing...");
            }

            _inventoryServices[player] = service;
            Debug.Log($"[ComponentRegistry] InventoryService registered for player: {player.name}, service: {service.gameObject.name}");
        }

        /// <summary>
        /// Unregister InventoryService (called by InventoryService on Destroy)
        /// </summary>
        public static void UnregisterInventoryService(NetworkPlayer player, InventoryService service)
        {
            if (player == null) return;

            if (_inventoryServices.ContainsKey(player) && _inventoryServices[player] == service)
            {
                _inventoryServices.Remove(player);
                Debug.Log($"[ComponentRegistry] InventoryService unregistered for player: {player.name}");
            }
        }

        /// <summary>
        /// Get InventoryService for a NetworkPlayer (no FindObject, instant access)
        /// </summary>
        public static InventoryService GetInventoryService(NetworkPlayer player)
        {
            if (player == null)
            {
                Debug.LogWarning("[ComponentRegistry] GetInventoryService called with null player");
                return null;
            }

            if (_inventoryServices.TryGetValue(player, out InventoryService service))
            {
                return service;
            }

            Debug.LogWarning($"[ComponentRegistry] InventoryService not found for player: {player.name}");
            return null;
        }

        /// <summary>
        /// Check if InventoryService is registered for a player
        /// </summary>
        public static bool HasInventoryService(NetworkPlayer player)
        {
            return player != null && _inventoryServices.ContainsKey(player);
        }

        #endregion

        #region CharacterStats Registration

        public static void RegisterCharacterStats(NetworkPlayer player, CharacterStats stats)
        {
            if (player == null || stats == null) return;
            _characterStats[player] = stats;
            Debug.Log($"[ComponentRegistry] CharacterStats registered for player: {player.name}");
        }

        public static void UnregisterCharacterStats(NetworkPlayer player, CharacterStats stats)
        {
            if (player == null) return;
            if (_characterStats.ContainsKey(player) && _characterStats[player] == stats)
            {
                _characterStats.Remove(player);
                Debug.Log($"[ComponentRegistry] CharacterStats unregistered for player: {player.name}");
            }
        }

        public static CharacterStats GetCharacterStats(NetworkPlayer player)
        {
            if (player == null) return null;
            return _characterStats.TryGetValue(player, out CharacterStats stats) ? stats : null;
        }

        #endregion

        #region CharacterCombat Registration

        public static void RegisterCharacterCombat(NetworkPlayer player, CharacterCombat combat)
        {
            if (player == null || combat == null) return;
            _characterCombat[player] = combat;
            Debug.Log($"[ComponentRegistry] CharacterCombat registered for player: {player.name}");
        }

        public static void UnregisterCharacterCombat(NetworkPlayer player, CharacterCombat combat)
        {
            if (player == null) return;
            if (_characterCombat.ContainsKey(player) && _characterCombat[player] == combat)
            {
                _characterCombat.Remove(player);
                Debug.Log($"[ComponentRegistry] CharacterCombat unregistered for player: {player.name}");
            }
        }

        public static CharacterCombat GetCharacterCombat(NetworkPlayer player)
        {
            if (player == null) return null;
            return _characterCombat.TryGetValue(player, out CharacterCombat combat) ? combat : null;
        }

        #endregion

        #region MovementController Registration

        public static void RegisterMovementController(NetworkPlayer player, IMovementController movement)
        {
            if (player == null || movement == null) return;
            _movementControllers[player] = movement;
            Debug.Log($"[ComponentRegistry] MovementController registered for player: {player.name}");
        }

        public static void UnregisterMovementController(NetworkPlayer player, IMovementController movement)
        {
            if (player == null) return;
            if (_movementControllers.ContainsKey(player) && _movementControllers[player] == movement)
            {
                _movementControllers.Remove(player);
                Debug.Log($"[ComponentRegistry] MovementController unregistered for player: {player.name}");
            }
        }

        public static IMovementController GetMovementController(NetworkPlayer player)
        {
            if (player == null) return null;
            return _movementControllers.TryGetValue(player, out IMovementController movement) ? movement : null;
        }

        #endregion

        #region InputRouter Registration

        public static void RegisterInputRouter(NetworkPlayer player, InputRouter router)
        {
            if (player == null || router == null) return;
            _inputRouters[player] = router;
            Debug.Log($"[ComponentRegistry] InputRouter registered for player: {player.name}");
        }

        public static void UnregisterInputRouter(NetworkPlayer player, InputRouter router)
        {
            if (player == null) return;
            if (_inputRouters.ContainsKey(player) && _inputRouters[player] == router)
            {
                _inputRouters.Remove(player);
                Debug.Log($"[ComponentRegistry] InputRouter unregistered for player: {player.name}");
            }
        }

        public static InputRouter GetInputRouter(NetworkPlayer player)
        {
            if (player == null) return null;
            return _inputRouters.TryGetValue(player, out InputRouter router) ? router : null;
        }

        #endregion

        #region ItemUsageSystem Registration

        public static void RegisterItemUsageSystem(NetworkPlayer player, ItemUsageSystem system)
        {
            if (player == null || system == null) return;
            _itemUsageSystems[player] = system;
            Debug.Log($"[ComponentRegistry] ItemUsageSystem registered for player: {player.name}");
        }

        public static void UnregisterItemUsageSystem(NetworkPlayer player, ItemUsageSystem system)
        {
            if (player == null) return;
            if (_itemUsageSystems.ContainsKey(player) && _itemUsageSystems[player] == system)
            {
                _itemUsageSystems.Remove(player);
                Debug.Log($"[ComponentRegistry] ItemUsageSystem unregistered for player: {player.name}");
            }
        }

        public static ItemUsageSystem GetItemUsageSystem(NetworkPlayer player)
        {
            if (player == null) return null;
            return _itemUsageSystems.TryGetValue(player, out ItemUsageSystem system) ? system : null;
        }

        #endregion

        #region WeaponSwitchingSystem Registration

        public static void RegisterWeaponSwitchingSystem(NetworkPlayer player, WeaponSwitchingSystem system)
        {
            if (player == null || system == null) return;
            _weaponSwitchingSystems[player] = system;
            Debug.Log($"[ComponentRegistry] WeaponSwitchingSystem registered for player: {player.name}");
        }

        public static void UnregisterWeaponSwitchingSystem(NetworkPlayer player, WeaponSwitchingSystem system)
        {
            if (player == null) return;
            if (_weaponSwitchingSystems.ContainsKey(player) && _weaponSwitchingSystems[player] == system)
            {
                _weaponSwitchingSystems.Remove(player);
                Debug.Log($"[ComponentRegistry] WeaponSwitchingSystem unregistered for player: {player.name}");
            }
        }

        public static WeaponSwitchingSystem GetWeaponSwitchingSystem(NetworkPlayer player)
        {
            if (player == null) return null;
            return _weaponSwitchingSystems.TryGetValue(player, out WeaponSwitchingSystem system) ? system : null;
        }

        #endregion

        /// <summary>
        /// Clear all registrations (for testing/cleanup)
        /// </summary>
        public static void Clear()
        {
            _inventoryServices.Clear();
            _characterStats.Clear();
            _characterCombat.Clear();
            _movementControllers.Clear();
            _inputRouters.Clear();
            _itemUsageSystems.Clear();
            _weaponSwitchingSystems.Clear();
            Debug.Log("[ComponentRegistry] All component registrations cleared");
        }
    }
}
