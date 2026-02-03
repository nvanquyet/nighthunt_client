using UnityEngine;
using FishNet.Object;
using NightHunt.Networking;
using NightHunt.Gameplay.Core;
using NightHunt.Gameplay.Inventory;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.Gameplay.Weapons
{
    /// <summary>
    /// Network sync component for weapon operations
    /// Handles ServerRpc (client -> server) and ObserversRpc (server -> all clients)
    /// Ensures all weapon changes are validated on server and synced to all remote clients
    /// </summary>
    [RequireComponent(typeof(WeaponSwitchingSystem))]
    public class WeaponNetworkSync : NetworkBehaviour
    {
        private WeaponSwitchingSystem weaponSystem;
        private InventoryService inventoryService;
        private NetworkPlayer networkPlayer;

        private void Awake()
        {
            // Find WeaponSwitchingSystem
            weaponSystem = gameObject.FindInHierarchy<WeaponSwitchingSystem>();
            if (weaponSystem == null)
            {
                weaponSystem = GetComponent<WeaponSwitchingSystem>();
            }

            // Find NetworkPlayer
            networkPlayer = gameObject.FindInHierarchy<NetworkPlayer>();
            if (networkPlayer != null)
            {
                inventoryService = ComponentRegistry.GetInventoryService(networkPlayer);
            }

            if (weaponSystem == null)
            {
                Debug.LogError("[WeaponNetworkSync] WeaponSwitchingSystem not found! Please attach to GameObject with WeaponSwitchingSystem.");
            }
        }

        #region Equip Weapon (Network Sync)

        /// <summary>
        /// Client requests to equip weapon - validated on server
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void EquipWeaponServerRpc(string weaponId, int weaponSlotIndex, NetworkPlayer player)
        {
            // Validate on server
            if (!ValidateEquipWeapon(weaponId, weaponSlotIndex, player))
            {
                Debug.LogWarning($"[WeaponNetworkSync] EquipWeapon validation failed: {weaponId} to slot {weaponSlotIndex}");
                return;
            }

            // Process equip on server
            bool success = false;
            if (weaponSystem != null)
            {
                success = weaponSystem.EquipWeapon(weaponId, weaponSlotIndex);
            }

            if (success)
            {
                // Sync to all clients
                EquipWeaponObserversRpc(weaponId, weaponSlotIndex);
            }
        }

        /// <summary>
        /// Sync weapon equip to all clients
        /// </summary>
        [ObserversRpc]
        private void EquipWeaponObserversRpc(string weaponId, int weaponSlotIndex)
        {
            // Only process on clients (not on server again)
            if (IsServer)
                return;

            // Process equip on client
            if (weaponSystem != null)
            {
                weaponSystem.EquipWeapon(weaponId, weaponSlotIndex);
            }
        }

        /// <summary>
        /// Validate equip weapon operation (server-side)
        /// </summary>
        private bool ValidateEquipWeapon(string weaponId, int weaponSlotIndex, NetworkPlayer player)
        {
            if (weaponSystem == null || inventoryService == null || player == null)
                return false;

            // Validate slot index
            if (weaponSlotIndex < 0 || weaponSlotIndex >= 2) // Max 2 weapon slots
            {
                Debug.LogWarning($"[WeaponNetworkSync] ValidateEquipWeapon: Invalid slot index {weaponSlotIndex}");
                return false;
            }

            // Check if weapon exists in inventory (only for this player)
            if (!inventoryService.HasItem(weaponId))
            {
                Debug.LogWarning($"[WeaponNetworkSync] ValidateEquipWeapon: Weapon {weaponId} not in inventory");
                return false;
            }

            // TODO: Add more validation (weapon type, etc.)

            return true;
        }

        #endregion

        #region Switch Weapon (Network Sync)

        /// <summary>
        /// Client requests to switch weapon - validated on server
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void SwitchWeaponServerRpc(int slotIndex, NetworkPlayer player)
        {
            // Validate on server
            if (!ValidateSwitchWeapon(slotIndex))
            {
                Debug.LogWarning($"[WeaponNetworkSync] SwitchWeapon validation failed: slot {slotIndex}");
                return;
            }

            // Process switch on server
            if (weaponSystem != null)
            {
                weaponSystem.SwitchToWeapon(slotIndex);
            }

            // Sync to all clients
            SwitchWeaponObserversRpc(slotIndex);
        }

        /// <summary>
        /// Sync weapon switch to all clients
        /// </summary>
        [ObserversRpc]
        private void SwitchWeaponObserversRpc(int slotIndex)
        {
            // Only process on clients (not on server again)
            if (IsServer)
                return;

            // Process switch on client
            if (weaponSystem != null)
            {
                weaponSystem.SwitchToWeapon(slotIndex);
            }
        }

        /// <summary>
        /// Validate switch weapon operation (server-side)
        /// </summary>
        private bool ValidateSwitchWeapon(int slotIndex)
        {
            if (weaponSystem == null)
                return false;

            // Validate slot index
            if (slotIndex < 0 || slotIndex >= 2) // Max 2 weapon slots
            {
                Debug.LogWarning($"[WeaponNetworkSync] ValidateSwitchWeapon: Invalid slot index {slotIndex}");
                return false;
            }

            // Check if weapon exists in slot
            var weapon = weaponSystem.GetWeaponInSlot(slotIndex);
            if (weapon == null)
            {
                Debug.LogWarning($"[WeaponNetworkSync] ValidateSwitchWeapon: No weapon in slot {slotIndex}");
                return false;
            }

            return true;
        }

        #endregion
    }
}
