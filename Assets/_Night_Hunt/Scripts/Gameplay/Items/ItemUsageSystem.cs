using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Inventory;
using System.Collections;

namespace NightHunt.Gameplay.Items
{
    /// <summary>
    /// Handles item usage: heal, buff, consumables, etc.
    /// Integrates with Inventory and Character systems
    /// </summary>
    public class ItemUsageSystem : MonoBehaviour
    {
        [Header("Usage Settings")]
        [SerializeField] private float useCooldown = 0.5f;

        private CharacterStats characterStats;
        private CharacterMovement characterMovement;
        private InventorySystem inventorySystem;
        private float lastUseTime;

        private void Awake()
        {
            characterStats = GetComponent<CharacterStats>();
            characterMovement = GetComponent<CharacterMovement>();
            inventorySystem = GetComponent<InventorySystem>();
        }

        /// <summary>
        /// Use item by ID
        /// </summary>
        public bool UseItem(string itemId)
        {
            if (Time.time - lastUseTime < useCooldown)
                return false;

            var itemConfig = GameConfigLoader.Instance?.GetItemConfig(itemId);
            if (itemConfig == null)
                return false;

            // Check if item exists in inventory
            if (inventorySystem == null || !inventorySystem.GetItems().Exists(i => i.ItemId == itemId))
                return false;

            // Use based on item type
            bool used = false;
            switch (itemConfig.UseType)
            {
                case "Instant":
                    used = UseInstantItem(itemConfig);
                    break;
                case "Channel":
                    used = StartCoroutine(UseChannelItem(itemConfig)) != null;
                    break;
                case "PlaceOnGround":
                    used = PlaceItemOnGround(itemConfig);
                    break;
                case "Throw":
                    used = ThrowItem(itemConfig);
                    break;
            }

            if (used)
            {
                lastUseTime = Time.time;
            }

            return used;
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
                    if (characterMovement != null)
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
        /// Use channel item (over time)
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
            if (characterMovement != null && characterMovement.GetCurrentMoveSpeed() > 0.1f)
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

