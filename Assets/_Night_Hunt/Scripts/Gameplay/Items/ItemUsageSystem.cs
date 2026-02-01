using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Inventory.Events;
using NightHunt.Gameplay.Inventory.Logic.Services;
using NightHunt.Gameplay.Core;
using NightHunt.Networking;
using NightHunt.InteractionSystem.Utilities;
using System.Collections;

namespace NightHunt.Gameplay.Items
{
    /// <summary>
    /// Handles item usage: heal, buff, consumables, etc.
    /// Integrates with Inventory and Character systems
    /// Implements IItemUsageService and fires InventoryLogicEvents for UI layer
    /// </summary>
    public class ItemUsageSystem : MonoBehaviour, IItemUsageService
    {
        [Header("Usage Settings")]
        [SerializeField] private float useCooldown = 0.5f;

        private CharacterStats characterStats;
        private CharacterPredictedMovement _characterPredictedMovement;
        private InventoryService inventorySystem;
        private float lastUseTime;

        // Current usage state
        private string currentUsingItemId = null;
        private Coroutine currentUsageCoroutine = null;
        private float usageStartTime = 0f;
        private float usageDuration = 0f;

        private void Awake()
        {
            NetworkPlayer networkPlayer = gameObject.FindInHierarchy<NetworkPlayer>();
            if (networkPlayer != null)
            {
                characterStats = ComponentRegistry.GetCharacterStats(networkPlayer);
                var movement = ComponentRegistry.GetMovementController(networkPlayer);
                if (movement is CharacterPredictedMovement predictedMovement)
                {
                    _characterPredictedMovement = predictedMovement;
                }
                inventorySystem = ComponentRegistry.GetInventoryService(networkPlayer);
                ComponentRegistry.RegisterItemUsageSystem(networkPlayer, this);
            }
            else
            {
                characterStats = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<CharacterStats>(gameObject, includeInactive: false);
                _characterPredictedMovement = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<CharacterPredictedMovement>(gameObject, includeInactive: false);
                inventorySystem = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<InventoryService>(gameObject, includeInactive: false);
            }
        }

        private void OnDestroy()
        {
            NetworkPlayer networkPlayer = gameObject.FindInHierarchy<NetworkPlayer>();
            
            if (networkPlayer != null)
            {
                ComponentRegistry.UnregisterItemUsageSystem(networkPlayer, this);
            }
        }

        /// <summary>
        /// Check if item can be used
        /// </summary>
        public bool CanUseItem(string itemId)
        {
            if (Time.time - lastUseTime < useCooldown)
                return false;

            if (IsUsingItem())
                return false;

            if (inventorySystem == null)
                return false;

            return inventorySystem.HasItem(itemId);
        }

        /// <summary>
        /// Start using item (with progress bar support)
        /// </summary>
        public bool StartUseItem(string itemId)
        {
            if (!CanUseItem(itemId))
                return false;

            // TODO: Load item data from ItemDataRegistry
            // For now, item usage is disabled until ItemDataBase system is fully implemented
            var registry = NightHunt.InteractionSystem.Core.Abstractions.ItemDataRegistry.Load();
            if (registry == null)
            {
                Debug.LogWarning($"[ItemUsageSystem] ItemDataRegistry is null - cannot use item: {itemId}");
                return false;
            }
            
            var itemData = registry.GetById(itemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[ItemUsageSystem] Item not found in ItemDataRegistry: {itemId}");
                return false;
            }
            
            // Use itemData instead of itemConfig

            // Check if item exists in inventory
            if (!inventorySystem.HasItem(itemId))
                return false;

            // TODO: Check item type when ItemDataBase is extended with useDuration property
            // Check item type to determine usage method
            if (itemData.IsConsumable)
            {
                // For now, treat all consumables as instant use
                // TODO: Check useDuration when ItemDataBase is extended
                // if (itemData.useDuration > 0f)
                // {
                //     // Consumable with duration - use with progress bar
                //     return StartUseConsumable(itemId, itemData);
                // }
            }
            
                // Instant use or other types
                return UseItem(itemId);
        }

        /// <summary>
        /// Start using consumable item with progress bar
        /// </summary>
        private bool StartUseConsumable(string itemId, ItemConfigData itemConfig)
        {
            if (IsUsingItem())
            {
                // Cancel current usage if different item
                if (currentUsingItemId != itemId)
                {
                    CancelUseItem(currentUsingItemId);
                }
                else
                {
                    // Same item - cancel current usage
                    CancelUseItem(itemId);
                    return false;
                }
            }

            currentUsingItemId = itemId;
            usageStartTime = Time.time;
            usageDuration = itemConfig.useDuration;

            // Fire event for UI
            InventoryLogicEvents.FireItemUseStarted(itemId);

            // Start usage coroutine
            currentUsageCoroutine = StartCoroutine(UseConsumableWithProgress(itemId, itemConfig));

            return true;
        }

        /// <summary>
        /// Use consumable item with progress tracking
        /// </summary>
        private IEnumerator UseConsumableWithProgress(string itemId, ItemConfigData itemConfig)
        {
            float elapsed = 0f;
            float updateInterval = 0.1f; // Update progress every 0.1 seconds

            while (elapsed < usageDuration)
            {
                // Check if should cancel
                if (ShouldCancelUsage())
                {
                    CancelUseItem(itemId);
                    yield break;
                }

                yield return new WaitForSeconds(updateInterval);
                elapsed += updateInterval;

                // Fire progress event
                float progress = Mathf.Clamp01(elapsed / usageDuration);
                InventoryLogicEvents.FireItemUseProgress(itemId, progress);
            }

            // Usage completed
            CompleteUseItem(itemId, itemConfig);
        }

        /// <summary>
        /// Check if usage should be cancelled
        /// </summary>
        private bool ShouldCancelUsage()
        {
            // Cancel if moving
            if (_characterPredictedMovement != null && _characterPredictedMovement.GetCurrentMoveSpeed() > 0.1f)
            {
                return true;
            }

            // TODO: Cancel if taking damage
            // TODO: Cancel if inventory/menu opened

            return false;
        }

        /// <summary>
        /// Complete item usage
        /// </summary>
        private void CompleteUseItem(string itemId, ItemConfigData itemConfig)
        {
            // Apply effect
            UseInstantItem(itemConfig);

            // Remove from inventory if consumable
            if (itemConfig.IsConsumable && inventorySystem != null)
            {
                inventorySystem.RemoveItem(itemId, 1);
            }

            // Fire completion event
            InventoryLogicEvents.FireItemUseCompleted(itemId);

            // Reset state
            currentUsingItemId = null;
            currentUsageCoroutine = null;
            lastUseTime = Time.time;
        }

        /// <summary>
        /// Cancel item usage
        /// </summary>
        public bool CancelUseItem(string itemId)
        {
            if (currentUsingItemId != itemId)
                return false;

            if (currentUsageCoroutine != null)
            {
                StopCoroutine(currentUsageCoroutine);
                currentUsageCoroutine = null;
            }

            // Fire cancel event
            InventoryLogicEvents.FireItemUseCancelled(itemId);

            // Reset state
            currentUsingItemId = null;
            usageStartTime = 0f;
            usageDuration = 0f;

            return true;
        }

        /// <summary>
        /// Check if currently using item
        /// </summary>
        public bool IsUsingItem()
        {
            return !string.IsNullOrEmpty(currentUsingItemId);
        }

        /// <summary>
        /// Get current using item ID
        /// </summary>
        public string GetCurrentUsingItemId()
        {
            return currentUsingItemId;
        }

        /// <summary>
        /// Get usage progress (0-1)
        /// </summary>
        public float GetUseProgress()
        {
            if (!IsUsingItem() || usageDuration <= 0f)
                return 0f;

            float elapsed = Time.time - usageStartTime;
            return Mathf.Clamp01(elapsed / usageDuration);
        }

        /// <summary>
        /// Use item by ID (legacy method, now calls StartUseItem)
        /// </summary>
        public bool UseItem(string itemId)
        {
            return StartUseItem(itemId);
        }

        /// <summary>
        /// Use instant item
        /// </summary>
        private bool UseInstantItem(ItemConfigData item)
        {
            switch (item.EffectType)
            {
                case "HealHP":
                    if (characterStats != null)
                    {
                        characterStats.Heal(item.EffectValue);
                        return true;
                    }
                    break;

                case "HealStamina":
                    if (_characterPredictedMovement != null)
                    {
                        // Would need to add heal stamina method
                        return true;
                    }
                    break;

                case "SpeedBuff":
                    if (characterStats != null)
                    {
                        characterStats.ApplyStatusEffect("STATUS_SPEED", item.EffectDuration);
                        return true;
                    }
                    break;

                case "NoiseReduce":
                    if (characterStats != null)
                    {
                        characterStats.ApplyStatusEffect("STATUS_SILENT", item.EffectDuration);
                        return true;
                    }
                    break;

                case "VisionIncrease":
                    if (characterStats != null)
                    {
                        // Would need vision modifier system
                        return true;
                    }
                    break;

                case "Cleanse":
                    if (characterStats != null)
                    {
                        // Remove all debuffs
                        // Would need to track active debuffs
                        return true;
                    }
                    break;
            }

            return false;
        }

        /// <summary>
        /// Use consumable item
        /// </summary>
        public bool UseConsumable(string itemId)
        {
            return StartUseItem(itemId);
        }

        /// <summary>
        /// Equip item
        /// </summary>
        public bool EquipItem(string itemId)
        {
            // TODO: Implement equipment logic
            // For now, delegate to inventory service
            if (inventorySystem == null)
                return false;

            // TODO: Determine equipment slot type from ItemDataBase.Category
            // For now, equipment usage is disabled until ItemDataBase system is fully implemented
            var registry = NightHunt.InteractionSystem.Core.Abstractions.ItemDataRegistry.Load();
            if (registry == null)
            {
                Debug.LogWarning($"[ItemUsageSystem] ItemDataRegistry is null - cannot use item: {itemId}");
                return false;
            }
            
            var itemData = registry.GetById(itemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[ItemUsageSystem] Item not found in ItemDataRegistry: {itemId}");
                return false;
            }
            
            // Use itemData.Category to determine equipment slot type

            // TODO: Map item category to EquipmentSlotType
            // For now, just return false
            return false;
        }

        /// <summary>
        /// Prepare to throw item
        /// </summary>
        public bool PrepareThrowItem(string itemId)
        {
            // TODO: Implement throw preparation logic
            return false;
        }

        /// <summary>
        /// Trigger event item
        /// </summary>
        public bool TriggerEventItem(string itemId)
        {
            // TODO: Implement event trigger logic
            return false;
        }

        /// <summary>
        /// Trigger quest item
        /// </summary>
        public bool TriggerQuestItem(string itemId)
        {
            // TODO: Implement quest trigger logic
            return false;
        }

        /// <summary>
        /// Use channel item (over time) - legacy method
        /// </summary>
        private IEnumerator UseChannelItem(ItemConfigData item)
        {
            float elapsed = 0f;
            float interval = 0.5f; // Update every 0.5 seconds

            while (elapsed < item.CastTime)
            {
                // Check if interrupted (movement, damage, etc.)
                if (ShouldInterruptChannel())
                {
                    yield break;
                }

                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }

            // Apply effect after channel
            if (item.EffectType == "HealHP" && characterStats != null)
            {
                characterStats.Heal(item.EffectValue);
            }

            // Remove from inventory
            if (inventorySystem != null && item.IsConsumable)
            {
                inventorySystem.RemoveItem(item.ItemId, 1);
            }
        }

        /// <summary>
        /// Check if channel should be interrupted
        /// </summary>
        private bool ShouldInterruptChannel()
        {
            // Interrupt if moving
            if (_characterPredictedMovement != null && _characterPredictedMovement.GetCurrentMoveSpeed() > 0.1f)
            {
                return true;
            }

            // Interrupt if taking damage
            // Would need to track recent damage

            return false;
        }

        /// <summary>
        /// Place item on ground
        /// </summary>
        private bool PlaceItemOnGround(ItemConfigData item)
        {
            // Raycast to find ground position
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, LayerMask.GetMask("Ground")))
            {
                Vector3 placePosition = hit.point;

                // Spawn item object based on type
                switch (item.EffectType)
                {
                    case "LightArea":
                        SpawnLightArea(placePosition, item);
                        break;
                    case "PlaceVisionNode":
                        SpawnVisionNode(placePosition, item);
                        break;
                    case "ExplosiveTrap":
                        SpawnTrap(placePosition, item);
                        break;
                    case "SlowField":
                        SpawnSlowField(placePosition, item);
                        break;
                }

                // Remove from inventory
                if (inventorySystem != null && item.IsConsumable)
                {
                    inventorySystem.RemoveItem(item.ItemId, 1);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Throw item
        /// </summary>
        private bool ThrowItem(ItemConfigData item)
        {
            // Calculate throw direction
            Vector3 throwDirection = transform.forward;
            Vector3 throwPosition = transform.position + Vector3.up * 1.5f;

            // Spawn thrown item
            switch (item.EffectType)
            {
                case "SmokeScreen":
                    ThrowSmokeGrenade(throwPosition, throwDirection, item);
                    break;
                case "DisableBeacon":
                    ThrowEMPGrenade(throwPosition, throwDirection, item);
                    break;
            }

            // Remove from inventory
            if (inventorySystem != null && item.IsConsumable)
            {
                inventorySystem.RemoveItem(item.ItemId, 1);
            }

            return true;
        }

        // Place item methods
        private void SpawnLightArea(Vector3 position, ItemConfigData item)
        {
            // Spawn light area prefab
            // Would need prefab reference
        }

        private void SpawnVisionNode(Vector3 position, ItemConfigData item)
        {
            // Spawn vision node prefab
        }

        private void SpawnTrap(Vector3 position, ItemConfigData item)
        {
            // Spawn trap prefab
        }

        private void SpawnSlowField(Vector3 position, ItemConfigData item)
        {
            // Spawn slow field prefab
        }

        // Throw item methods
        private void ThrowSmokeGrenade(Vector3 position, Vector3 direction, ItemConfigData item)
        {
            // Spawn smoke grenade with physics
        }

        private void ThrowEMPGrenade(Vector3 position, Vector3 direction, ItemConfigData item)
        {
            // Spawn EMP grenade with physics
        }
    }
}

