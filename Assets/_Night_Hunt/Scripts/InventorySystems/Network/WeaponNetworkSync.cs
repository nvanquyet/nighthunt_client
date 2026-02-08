using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Systems;
using System.Collections.Generic;
using NightHunt.Inventory.Database;

namespace NightHunt.Inventory.Network
{
    /// <summary>
    /// Network synchronization for WeaponSystem.
    /// Syncs equipped weapons, active weapon, ammo, and weapon stats.
    /// </summary>
    public class WeaponNetworkSync : NetworkBehaviour
    {
        [Header("References")] [SerializeField]
        private WeaponSystem weaponSystem;

        [SerializeField] private InventoryNetworkSync inventorySync;

        [Header("Visual Sync")] [SerializeField]
        private Transform weaponHolder;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;

        // Synced weapon state
        private readonly SyncDictionary<WeaponSlotType, ItemInstanceData> syncedWeapons =
            new SyncDictionary<WeaponSlotType, ItemInstanceData>();

        // Synced active weapon
        private readonly SyncVar<WeaponSlotType> syncedActiveSlot = new SyncVar<WeaponSlotType>();

        // Synced ammo
        private readonly SyncDictionary<WeaponSlotType, int> syncedAmmo = new SyncDictionary<WeaponSlotType, int>();

        // Visual models
        private Dictionary<WeaponSlotType, GameObject> spawnedWeaponModels =
            new Dictionary<WeaponSlotType, GameObject>();

        // === Lifecycle ===

        public override void OnStartServer()
        {
            base.OnStartServer();

            WeaponEvents.OnWeaponEquipped += OnWeaponEquipped_Server;
            WeaponEvents.OnWeaponUnequipped += OnWeaponUnequipped_Server;
            WeaponEvents.OnActiveWeaponChanged += OnActiveWeaponChanged_Server;
            WeaponEvents.OnAmmoChanged += OnAmmoChanged_Server;

            Log("Server started - weapon sync enabled");
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            WeaponEvents.OnWeaponEquipped -= OnWeaponEquipped_Server;
            WeaponEvents.OnWeaponUnequipped -= OnWeaponUnequipped_Server;
            WeaponEvents.OnActiveWeaponChanged -= OnActiveWeaponChanged_Server;
            WeaponEvents.OnAmmoChanged -= OnAmmoChanged_Server;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            syncedWeapons.OnChange += OnSyncedWeaponsChanged_Client;
            syncedActiveSlot.OnChange += OnActiveSlotChanged_Client;
            syncedAmmo.OnChange += OnAmmoChanged_Client;

            Log("Client started - listening for weapon updates");
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            syncedWeapons.OnChange -= OnSyncedWeaponsChanged_Client;
            syncedActiveSlot.OnChange -= OnActiveSlotChanged_Client;
            syncedAmmo.OnChange -= OnAmmoChanged_Client;
        }

        // === SERVER EVENT HANDLERS ===

        private void OnWeaponEquipped_Server(ItemInstance weapon, WeaponSlotType slotType)
        {
            if (!IsServer)
                return;

            syncedWeapons[slotType] = weapon.Serialize();
            syncedAmmo[slotType] = weapon.CurrentAmmo;

            Log($"[SERVER] Weapon synced: {weapon.Definition.DisplayName} in {slotType}");
        }

        private void OnWeaponUnequipped_Server(ItemInstance weapon, WeaponSlotType slotType)
        {
            if (!IsServer)
                return;

            syncedWeapons.Remove(slotType);
            syncedAmmo.Remove(slotType);

            Log($"[SERVER] Weapon removed: {slotType}");
        }

        private void OnActiveWeaponChanged_Server(ItemInstance previous, ItemInstance newWeapon,
            WeaponSlotType slotType)
        {
            if (!IsServer)
                return;

            syncedActiveSlot.Value = slotType;

            Log($"[SERVER] Active weapon changed: {slotType}");
        }

        private void OnAmmoChanged_Server(ItemInstance weapon, int currentAmmo, int maxAmmo)
        {
            if (!IsServer)
                return;

            // Find which slot this weapon is in
            WeaponSlotType slotType = weaponSystem.GetActiveWeaponSlot();
            syncedAmmo[slotType] = currentAmmo;

            Log($"[SERVER] Ammo synced: {currentAmmo}/{maxAmmo} for {slotType}");
        }

        // === CLIENT SYNC ===

        private void OnSyncedWeaponsChanged_Client(SyncDictionaryOperation op, WeaponSlotType key,
            ItemInstanceData value, bool asServer)
        {
            if (asServer)
                return;

            Log($"[CLIENT] Weapon sync: {op} for {key}");

            switch (op)
            {
                case SyncDictionaryOperation.Add:
                case SyncDictionaryOperation.Set:
                    UpdateWeaponVisual(key, value);
                    break;

                case SyncDictionaryOperation.Remove:
                    RemoveWeaponVisual(key);
                    break;

                case SyncDictionaryOperation.Clear:
                    ClearAllWeaponVisuals();
                    break;
            }
        }

        private void OnActiveSlotChanged_Client(WeaponSlotType oldValue, WeaponSlotType newValue, bool asServer)
        {
            if (asServer)
                return;

            Log($"[CLIENT] Active weapon changed: {newValue}");

            // Hide all weapons except active
            foreach (var kvp in spawnedWeaponModels)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.SetActive(kvp.Key == newValue);
                }
            }
        }

        private void OnAmmoChanged_Client(SyncDictionaryOperation op, WeaponSlotType key, int value, bool asServer)
        {
            if (asServer)
                return;

            Log($"[CLIENT] Ammo updated: {key} = {value}");
        }

        // === VISUAL SYNC ===

        private void UpdateWeaponVisual(WeaponSlotType slotType, ItemInstanceData data)
        {
            RemoveWeaponVisual(slotType);

            var definition = GetItemDefinition(data.ItemId);
            if (definition == null || definition.EquippedModelPrefab == null)
                return;

            GameObject model = Instantiate(definition.EquippedModelPrefab, weaponHolder);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // Only show if active weapon
            model.SetActive(slotType == syncedActiveSlot.Value);

            spawnedWeaponModels[slotType] = model;

            Log($"Spawned weapon visual: {definition.DisplayName} in {slotType}");
        }

        private void RemoveWeaponVisual(WeaponSlotType slotType)
        {
            if (spawnedWeaponModels.ContainsKey(slotType))
            {
                Destroy(spawnedWeaponModels[slotType]);
                spawnedWeaponModels.Remove(slotType);
            }
        }

        private void ClearAllWeaponVisuals()
        {
            foreach (var model in spawnedWeaponModels.Values)
            {
                if (model != null)
                    Destroy(model);
            }

            spawnedWeaponModels.Clear();
        }

        // === SERVER RPCs ===

        [ServerRpc(RequireOwnership = true)]
        public void RequestEquipWeapon_ServerRpc(string instanceId, WeaponSlotType slotType,
            NetworkConnection conn = null)
        {
            var weapon = inventorySync.Inventory.FindItem(instanceId);
            if (weapon == null)
                return;

            var result = weaponSystem.EquipWeapon(weapon, slotType);

            if (result == OperationResult.Success)
            {
                inventorySync.Inventory.RemoveItem(instanceId);
            }

            ConfirmWeaponOperation_TargetRpc(conn, result, slotType);
        }

        [ServerRpc(RequireOwnership = true)]
        public void RequestUnequipWeapon_ServerRpc(WeaponSlotType slotType, NetworkConnection conn = null)
        {
            var result = weaponSystem.UnequipWeapon(slotType, out ItemInstance unequipped);

            if (result == OperationResult.Success && unequipped != null)
            {
                inventorySync.Inventory.AddItem(unequipped, out _);
            }

            ConfirmWeaponOperation_TargetRpc(conn, result, slotType);
        }

        [ServerRpc(RequireOwnership = true)]
        public void RequestSwitchWeapon_ServerRpc(WeaponSlotType slotType, NetworkConnection conn = null)
        {
            var result = weaponSystem.SwitchToWeapon(slotType);
            ConfirmWeaponOperation_TargetRpc(conn, result, slotType);
        }

        [ServerRpc(RequireOwnership = true)]
        public void RequestReload_ServerRpc(int ammoAmount, NetworkConnection conn = null)
        {
            var result = weaponSystem.Reload(ammoAmount);
            // Ammo sync happens via OnAmmoChanged_Server event
        }

        [ServerRpc(RequireOwnership = true)]
        public void RequestConsumeAmmo_ServerRpc(int amount, NetworkConnection conn = null)
        {
            weaponSystem.ConsumeAmmo(amount);
        }

        [TargetRpc]
        private void ConfirmWeaponOperation_TargetRpc(NetworkConnection conn, OperationResult result,
            WeaponSlotType slotType)
        {
            Log($"[CLIENT] Weapon operation confirmed: {result} for {slotType}");
        }

        // === HELPERS ===

        private ItemDefinition GetItemDefinition(string itemId)
        {
            return ItemDefinitionDatabase.Instance.GetDefinition(itemId);
        }

        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[WeaponNetworkSync] {message}");
        }
    }
}