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
        [Header("References")]
        [SerializeField] private WeaponSystem weaponSystem;
        [SerializeField] private InventorySystem inventorySystem;
        [SerializeField] private InventoryNetworkSync inventorySync;

        [Header("Network Settings")]
        [SerializeField] private bool enableClientPrediction = true;
        [SerializeField] private float reconciliationTimeout = 2f;
        
        [Header("Anti-Cheat")]
        [SerializeField] private bool enableValidation = true;
        [SerializeField] private int maxOperationsPerSecond = 20;
        [SerializeField] private bool logSuspiciousActivity = true;

        [Header("Visual Sync")]
        [SerializeField] private Transform weaponHolder;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

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
        
        // Client prediction state
        private List<PendingOperation> pendingOperations = new List<PendingOperation>();
        
        // Anti-cheat tracking
        private Dictionary<NetworkConnection, RateLimitData> rateLimitTracking = new Dictionary<NetworkConnection, RateLimitData>();
        
        // Data structures
        private class PendingOperation
        {
            public string OperationId;
            public float Timestamp;
            public System.Action Operation;
        }
        
        private class RateLimitData
        {
            public int OperationCount;
            public float WindowStart;
        }

        // === Lifecycle ===
        
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            inventorySystem = transform.parent.GetComponentInChildren<InventorySystem>();
            weaponSystem = transform.parent.GetComponentInChildren<WeaponSystem>();
            inventorySync = transform.parent.GetComponentInChildren<InventoryNetworkSync>();
        }
#endif

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
                    UpdateWeaponFromSync(key, value);
                    UpdateWeaponVisual(key, value);
                    break;

                case SyncDictionaryOperation.Remove:
                    // Update local system - unequip weapon
                    if (weaponSystem != null && weaponSystem.IsSlotEquipped(key))
                    {
                        weaponSystem.UnequipWeapon(key, out _);
                    }
                    RemoveWeaponVisual(key);
                    break;

                case SyncDictionaryOperation.Clear:
                    // Update local system - unequip all weapons
                    if (weaponSystem != null)
                    {
                        // Unequip primary
                        if (weaponSystem.IsSlotEquipped(WeaponSlotType.Primary))
                        {
                            weaponSystem.UnequipWeapon(WeaponSlotType.Primary, out _);
                        }
                        // Unequip secondary
                        if (weaponSystem.IsSlotEquipped(WeaponSlotType.Secondary))
                        {
                            weaponSystem.UnequipWeapon(WeaponSlotType.Secondary, out _);
                        }
                    }
                    ClearAllWeaponVisuals();
                    break;
            }
        }
        
        /// <summary>
        /// Update local weapon system from synced data.
        /// According to Flow 4: Client updates Local System state → Local System fires Events.
        /// </summary>
        private void UpdateWeaponFromSync(WeaponSlotType slotType, ItemInstanceData data)
        {
            if (weaponSystem == null)
                return;
            
            // Check if empty data
            if (string.IsNullOrEmpty(data.InstanceId))
            {
                // Unequip if currently equipped
                if (weaponSystem.IsSlotEquipped(slotType))
                {
                    weaponSystem.UnequipWeapon(slotType, out _);
                }
                return;
            }
            
            // Get item definition
            var definition = GetItemDefinition(data.ItemId);
            if (definition == null)
            {
                LogWarning($"[CLIENT] Unknown weapon definition: {data.ItemId}");
                return;
            }
            
            // Deserialize weapon
            var weapon = ItemInstance.Deserialize(data, definition);
            
            // Check if already equipped (to avoid duplicate events)
            var currentEquipped = weaponSystem.GetEquippedWeapon(slotType);
            if (currentEquipped != null && currentEquipped.InstanceId == weapon.InstanceId)
            {
                // Already equipped with same weapon - no update needed
                return;
            }
            
            // Find weapon in inventory or weapon slots
            ItemInstance actualWeapon = null;
            
            // Check inventory first
            if (inventorySystem != null)
            {
                actualWeapon = inventorySystem.FindItem(weapon.InstanceId);
            }
            
            // If not in inventory, check if it's already equipped in another slot
            if (actualWeapon == null && weaponSystem != null)
            {
                var primary = weaponSystem.GetEquippedWeapon(WeaponSlotType.Primary);
                if (primary != null && primary.InstanceId == weapon.InstanceId)
                    actualWeapon = primary;
                
                var secondary = weaponSystem.GetEquippedWeapon(WeaponSlotType.Secondary);
                if (secondary != null && secondary.InstanceId == weapon.InstanceId)
                    actualWeapon = secondary;
            }
            
            // If still not found, use deserialized weapon (for observers)
            if (actualWeapon == null)
            {
                actualWeapon = weapon;
            }
            
            // Update local weapon system
            // This will fire WeaponEvents automatically
            weaponSystem.EquipWeapon(actualWeapon, slotType);
            
            Log($"[CLIENT] Updated weapon from sync: {definition.DisplayName} in {slotType}");
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

        // === PUBLIC API ===
        
        /// <summary>
        /// Request to equip weapon from inventory to weapon slot.
        /// </summary>
        public void RequestEquipWeaponFromInventory(string instanceId, WeaponSlotType slotType)
        {
            if (!IsOwner)
                return;
            
            string operationId = $"equipWeapon_{instanceId}_{slotType}";
            
            if (enableClientPrediction)
            {
                PredictOperation(operationId, () => { });
            }
            
            RequestEquipWeapon_ServerRpc(instanceId, slotType);
        }
        
        /// <summary>
        /// Request to unequip weapon from weapon slot back to inventory.
        /// </summary>
        public void RequestUnequipWeaponToInventory(WeaponSlotType slotType)
        {
            if (!IsOwner)
                return;
            
            string operationId = $"unequipWeapon_{slotType}";
            
            if (enableClientPrediction)
            {
                PredictOperation(operationId, () => { });
            }
            
            RequestUnequipWeapon_ServerRpc(slotType);
        }
        
        /// <summary>
        /// Request to switch active weapon.
        /// </summary>
        public void RequestSwitchWeapon(WeaponSlotType slotType)
        {
            if (!IsOwner)
                return;
            
            RequestSwitchWeapon_ServerRpc(slotType);
        }
        
        /// <summary>
        /// Request to reload weapon.
        /// </summary>
        public void RequestReload(int ammoAmount)
        {
            if (!IsOwner)
                return;
            
            RequestReload_ServerRpc(ammoAmount);
        }
        
        /// <summary>
        /// Request to consume ammo.
        /// </summary>
        public void RequestConsumeAmmo(int amount)
        {
            if (!IsOwner)
                return;
            
            RequestConsumeAmmo_ServerRpc(amount);
        }
        
        // === SERVER RPCs ===

        /// <summary>
        /// Request to equip weapon from inventory to weapon slot.
        /// Execution order:
        /// Step 1: ValidateRequest → Rate limit, connection
        /// Step 2: ValidateOwnership → Check item ownership
        /// Step 3: ValidateItemExists → Check item exists and valid
        /// Step 4: ValidateTypeCompatibility → Check item type matches slot
        /// Step 5: ValidateLimits → (not needed for weapons)
        /// Step 6: Execute Local System → EquipWeapon, RemoveItem
        /// Step 7: ConfirmOperation_TargetRpc → Notify client
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestEquipWeapon_ServerRpc(string instanceId, WeaponSlotType slotType,
            NetworkConnection conn = null)
        {
            string operationId = $"equipWeapon_{instanceId}_{slotType}";
            
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "EquipWeapon"))
            {
                ConfirmWeaponOperation_TargetRpc(conn, OperationResult.UnknownError, slotType);
                return;
            }
            
            // Step 2: ValidateOwnership
            if (inventorySystem == null)
            {
                LogWarning("[SERVER] InventorySystem not assigned!");
                ConfirmWeaponOperation_TargetRpc(conn, OperationResult.UnknownError, slotType);
                return;
            }
            
            if (!inventorySystem.HasItem(instanceId))
            {
                LogWarning($"[SERVER] Client {conn.ClientId} tried to equip weapon they don't own: {instanceId}");
                ConfirmWeaponOperation_TargetRpc(conn, OperationResult.ItemNotFound, slotType);
                return;
            }
            
            // Step 3: ValidateItemExists
            var weapon = inventorySystem.FindItem(instanceId);
            if (weapon == null || weapon.Definition == null)
            {
                LogWarning($"[SERVER] Weapon not found or invalid: {instanceId}");
                ConfirmWeaponOperation_TargetRpc(conn, OperationResult.ItemNotFound, slotType);
                return;
            }
            
            // Step 4: ValidateTypeCompatibility
            if (!weaponSystem.CanEquip(weapon, slotType))
            {
                LogWarning($"[SERVER] Weapon type incompatible with slot: {weapon.Definition.ItemId} -> {slotType}");
                ConfirmWeaponOperation_TargetRpc(conn, OperationResult.IncompatibleSlot, slotType);
                return;
            }
            
            // Step 5: ValidateLimits (not needed for weapons)
            
            // Step 6: Execute Local System
            var result = weaponSystem.EquipWeapon(weapon, slotType);

            if (result == OperationResult.Success)
            {
                inventorySystem.RemoveItem(instanceId);
            }

            // Step 7: Confirm to client
            ConfirmWeaponOperation_TargetRpc(conn, result, slotType);
            
            Log($"[SERVER] Equip weapon from inventory: {weapon.Definition.DisplayName} to {slotType} - {result}");
        }

        /// <summary>
        /// Request to unequip weapon from weapon slot back to inventory.
        /// Execution order: Same as RequestEquipWeapon_ServerRpc
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestUnequipWeapon_ServerRpc(WeaponSlotType slotType, NetworkConnection conn = null)
        {
            string operationId = $"unequipWeapon_{slotType}";
            
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "UnequipWeapon"))
            {
                ConfirmWeaponOperation_TargetRpc(conn, OperationResult.UnknownError, slotType);
                return;
            }
            
            // Step 2: ValidateOwnership (check if weapon is equipped)
            var equippedWeapon = weaponSystem.GetEquippedWeapon(slotType);
            if (equippedWeapon == null)
            {
                LogWarning($"[SERVER] No weapon equipped in slot: {slotType}");
                ConfirmWeaponOperation_TargetRpc(conn, OperationResult.NotEquipped, slotType);
                return;
            }
            
            // Step 3: ValidateItemExists (already checked above)
            
            // Step 4: ValidateTypeCompatibility (not needed for unequip)
            
            // Step 5: ValidateLimits (check inventory space)
            if (inventorySystem == null)
            {
                LogWarning("[SERVER] InventorySystem not assigned!");
                ConfirmWeaponOperation_TargetRpc(conn, OperationResult.UnknownError, slotType);
                return;
            }
            
            if (inventorySystem.GetEmptySlotCount() == 0)
            {
                LogWarning($"[SERVER] Inventory full, cannot unequip weapon to inventory");
                ConfirmWeaponOperation_TargetRpc(conn, OperationResult.InventoryFull, slotType);
                return;
            }
            
            // Step 6: Execute Local System
            var result = weaponSystem.UnequipWeapon(slotType, out ItemInstance unequipped);

            if (result == OperationResult.Success && unequipped != null)
            {
                var addResult = inventorySystem.AddItem(unequipped, out _);
                if (addResult != OperationResult.Success)
                {
                    LogWarning($"[SERVER] Failed to add unequipped weapon to inventory: {addResult}");
                }
            }

            // Step 7: Confirm to client
            ConfirmWeaponOperation_TargetRpc(conn, result, slotType);
            
            Log($"[SERVER] Unequip weapon to inventory: {slotType} - {result}");
        }

        [ServerRpc(RequireOwnership = true)]
        private void RequestSwitchWeapon_ServerRpc(WeaponSlotType slotType, NetworkConnection conn = null)
        {
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "SwitchWeapon"))
                return;
            
            // Step 2-5: Validation (check if weapon is equipped in slot)
            if (!weaponSystem.IsSlotEquipped(slotType))
            {
                LogWarning($"[SERVER] No weapon equipped in slot: {slotType}");
                return;
            }
            
            // Step 6: Execute Local System
            var result = weaponSystem.SwitchToWeapon(slotType);
            
            // Step 7: Confirm to client
            ConfirmWeaponOperation_TargetRpc(conn, result, slotType);
        }

        [ServerRpc(RequireOwnership = true)]
        private void RequestReload_ServerRpc(int ammoAmount, NetworkConnection conn = null)
        {
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "Reload"))
                return;
            
            // Step 2-5: Validation (check if weapon is active and has ammo)
            var activeWeapon = weaponSystem.GetActiveWeapon();
            if (activeWeapon == null)
            {
                LogWarning("[SERVER] No active weapon to reload");
                return;
            }
            
            // Step 6: Execute Local System
            var result = weaponSystem.Reload(ammoAmount);
            // Ammo sync happens via OnAmmoChanged_Server event
        }

        [ServerRpc(RequireOwnership = true)]
        private void RequestConsumeAmmo_ServerRpc(int amount, NetworkConnection conn = null)
        {
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "ConsumeAmmo"))
                return;
            
            // Step 2-5: Validation (check if weapon is active)
            var activeWeapon = weaponSystem.GetActiveWeapon();
            if (activeWeapon == null)
            {
                LogWarning("[SERVER] No active weapon to consume ammo from");
                return;
            }
            
            // Step 6: Execute Local System
            weaponSystem.ConsumeAmmo(amount);
            // Ammo sync happens via OnAmmoChanged_Server event
        }

        [TargetRpc]
        private void ConfirmWeaponOperation_TargetRpc(NetworkConnection conn, OperationResult result,
            WeaponSlotType slotType)
        {
            string operationId = $"weapon_{slotType}";
            
            // Find pending operation
            var pending = pendingOperations.Find(op => op.OperationId == operationId);
            if (pending != null)
            {
                pendingOperations.Remove(pending);
                
                if (result == OperationResult.Success)
                {
                    Log($"[CLIENT] Weapon operation confirmed: {operationId}");
                }
                else
                {
                    LogWarning($"[CLIENT] Weapon operation rejected: {operationId} - {result}");
                    RollbackPrediction(pending);
                }
            }
        }
        
        // === CLIENT PREDICTION ===
        
        /// <summary>
        /// Predict operation locally for UI responsiveness.
        /// </summary>
        private void PredictOperation(string operationId, System.Action operation)
        {
            if (!enableClientPrediction || !IsOwner)
                return;
            
            var pending = new PendingOperation
            {
                OperationId = operationId,
                Timestamp = Time.time,
                Operation = operation
            };
            
            pendingOperations.Add(pending);
            operation?.Invoke();
            
            Log($"[CLIENT] Predicted operation: {operationId}");
        }
        
        /// <summary>
        /// Rollback prediction if server rejects.
        /// </summary>
        private void RollbackPrediction(PendingOperation pending)
        {
            LogWarning($"[CLIENT] Rolling back prediction: {pending.OperationId}");
        }
        
        void Update()
        {
            if (!IsOwner)
                return;
            
            float currentTime = Time.time;
            pendingOperations.RemoveAll(op =>
            {
                if (currentTime - op.Timestamp > reconciliationTimeout)
                {
                    LogWarning($"[CLIENT] Operation timed out: {op.OperationId}");
                    return true;
                }
                return false;
            });
        }
        
        // === VALIDATION & ANTI-CHEAT ===
        
        /// <summary>
        /// Validate server RPC request (anti-cheat).
        /// </summary>
        private bool ValidateRequest(NetworkConnection conn, string operationType)
        {
            if (!enableValidation)
                return true;
            
            if (conn == null)
            {
                LogWarning($"[SERVER] Null connection for {operationType}");
                return false;
            }
            
            if (!CheckRateLimit(conn))
            {
                LogWarning($"[SERVER] Rate limit exceeded for client {conn.ClientId}");
                if (logSuspiciousActivity)
                {
                    Debug.LogError($"[ANTI-CHEAT] Client {conn.ClientId} exceeded rate limit - possible hack attempt");
                }
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check rate limit for connection.
        /// </summary>
        private bool CheckRateLimit(NetworkConnection conn)
        {
            if (!rateLimitTracking.ContainsKey(conn))
            {
                rateLimitTracking[conn] = new RateLimitData();
            }
            
            var data = rateLimitTracking[conn];
            float currentTime = Time.time;
            
            if (currentTime - data.WindowStart >= 1f)
            {
                data.OperationCount = 0;
                data.WindowStart = currentTime;
            }
            
            data.OperationCount++;
            
            return data.OperationCount <= maxOperationsPerSecond;
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
        
        void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[WeaponNetworkSync] {message}");
        }
    }
}