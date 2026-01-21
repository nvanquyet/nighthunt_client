using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System;
using System.Collections;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Character;
using NightHunt.Data;

namespace NightHunt.Gameplay.Items
{
    /// <summary>
    /// Network controller for item usage
    /// Server-authoritative consumable flow with animation/FX
    /// </summary>
    public class NetworkItemUsageController : NetworkBehaviour
    {
        private InventorySystem inventorySystem;
        private CharacterStats characterStats;
        private Guid currentUseItemId = Guid.Empty;
        private bool isUsingItem = false;

        private void Awake()
        {
            inventorySystem = GetComponent<InventorySystem>();
            characterStats = GetComponent<CharacterStats>();
        }

        /// <summary>
        /// Client: Request to use item
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void ServerRpc_RequestUse(Guid itemInstanceId)
        {
            if (!IsServerStarted) return;

            // Validate
            if (!ValidateUseRequest(itemInstanceId))
            {
                return;
            }

            // Find item instance in inventory
            ItemInstance itemInstance = FindItemInstance(itemInstanceId);
            if (itemInstance == null || itemInstance.Config == null)
            {
                Debug.LogWarning($"[NetworkItemUsageController] Item instance not found: {itemInstanceId}");
                return;
            }

            // Start use process
            StartCoroutine(UseItemCoroutine(itemInstance));
        }

        /// <summary>
        /// Server: Validate use request
        /// </summary>
        [Server]
        private bool ValidateUseRequest(Guid itemInstanceId)
        {
            // Check if already using item
            if (isUsingItem)
            {
                Debug.LogWarning($"[NetworkItemUsageController] Already using item");
                return false;
            }

            // Check if player is alive
            if (characterStats != null && characterStats.GetHP() <= 0)
            {
                return false;
            }

            // Check if item exists in inventory
            ItemInstance itemInstance = FindItemInstance(itemInstanceId);
            if (itemInstance == null || itemInstance.Quantity <= 0)
            {
                return false;
            }

            // Check phase (would need match phase manager reference)
            // For now, skip phase check

            return true;
        }

        /// <summary>
        /// Server: Use item coroutine
        /// </summary>
        [Server]
        private IEnumerator UseItemCoroutine(ItemInstance itemInstance)
        {
            isUsingItem = true;
            currentUseItemId = itemInstance.InstanceId;

            var config = itemInstance.Config;
            float useTime = config.UseTime;

            // Send start animation/FX to client
            if (config is ConsumableItemConfig consumableConfig)
            {
                TargetRpc_PlayUseStart(Owner, consumableConfig.AnimId, consumableConfig.FxStartId);
            }

            // Wait for use time
            yield return new WaitForSeconds(useTime);

            // Apply effect
            ApplyItemEffect(config);

            // Consume item
            if (inventorySystem != null)
            {
                inventorySystem.RemoveItem(itemInstance.ItemId, 1);
            }

            // Send success FX
            if (config is ConsumableItemConfig consumableConfig2)
            {
                TargetRpc_PlayUseSuccess(Owner, consumableConfig2.FxSuccessId);
            }

            // Sync inventory
            var inventorySync = GetComponent<InventorySync>();
            if (inventorySync != null)
            {
                var slots = inventorySystem.GetItems();
                inventorySync.SyncInventory(slots);
            }

            isUsingItem = false;
            currentUseItemId = Guid.Empty;
        }

        /// <summary>
        /// Server: Apply item effect
        /// </summary>
        [Server]
        private void ApplyItemEffect(BaseItemConfig config)
        {
            if (config is ConsumableItemConfig consumable)
            {
                switch (consumable.EffectType)
                {
                    case EffectType.HealHP:
                        if (characterStats != null)
                        {
                            characterStats.Heal(consumable.EffectValue);
                        }
                        break;

                    case EffectType.HealStaminaOverTime:
                        // Would need stamina system
                        break;

                    case EffectType.SpeedBuff:
                        if (characterStats != null)
                        {
                            characterStats.ApplyStatusEffect("STATUS_SPEED", consumable.EffectDuration);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Client: Play use start animation/FX
        /// </summary>
        [TargetRpc]
        private void TargetRpc_PlayUseStart(FishNet.Connection.NetworkConnection conn, string animId, string fxStartId)
        {
            // Play animation (would need animator reference)
            // Play FX (would need FX manager reference)
            Debug.Log($"[NetworkItemUsageController] Playing use start: anim={animId}, fx={fxStartId}");
        }

        /// <summary>
        /// Client: Play use success FX
        /// </summary>
        [TargetRpc]
        private void TargetRpc_PlayUseSuccess(FishNet.Connection.NetworkConnection conn, string fxSuccessId)
        {
            // Play success FX
            Debug.Log($"[NetworkItemUsageController] Playing use success: fx={fxSuccessId}");
        }

        /// <summary>
        /// Find item instance in inventory
        /// </summary>
        private ItemInstance FindItemInstance(Guid instanceId)
        {
            // TODO: Implement proper ItemInstance lookup in inventory
            // For now, simplified - would need to store ItemInstance in InventorySlot
            return null;
        }
    }
}

