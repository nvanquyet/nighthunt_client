using System;
using UnityEngine;

namespace NightHunt.Inventory.Core.Data
{
    /// <summary>
    /// Factory for creating ItemInstance objects.
    /// Server-authoritative: Only server generates instance IDs.
    /// Updated for unified Resource system.
    /// </summary>
    public static class ItemInstanceFactory
    {
        private static readonly System.Random random = new System.Random();

        // ================================
        // BASIC CREATION
        // ================================

        public static ItemInstance CreateInstance(ItemDefinition definition)
        {
            if (definition == null)
            {
                UnityEngine.Debug.LogError("[ItemInstanceFactory] Cannot create instance from null definition");
                return null;
            }

            string instanceId = GenerateInstanceId();
            return new ItemInstance(definition, instanceId);
        }

        // ================================
        // WITH STACK SIZE
        // ================================

        public static ItemInstance CreateInstance(ItemDefinition definition, int stackSize)
        {
            var instance = CreateInstance(definition);
            if (instance == null)
                return null;

            if (!definition.IsStackable)
                stackSize = 1;

            instance.StackSize = Mathf.Clamp(stackSize, 1, definition.MaxStackSize);
            return instance;
        }

        // ================================
        // WITH RESOURCE VALUE
        // ================================

        public static ItemInstance CreateInstanceWithResource(ItemDefinition definition, float resourceValue)
        {
            var instance = CreateInstance(definition);
            if (instance == null)
                return null;

            if (definition.ResourceType != ItemResourceType.None)
            {
                instance.CurrentResource = Mathf.Clamp(
                    resourceValue,
                    0f,
                    definition.MaxResource
                );
            }

            return instance;
        }

        // ================================
        // FULL CUSTOMIZATION
        // ================================

        public static ItemInstance CreateInstance(
            ItemDefinition definition,
            int stackSize,
            float resourceValue)
        {
            var instance = CreateInstance(definition);
            if (instance == null)
                return null;

            if (!definition.IsStackable)
                stackSize = 1;

            instance.StackSize = Mathf.Clamp(stackSize, 1, definition.MaxStackSize);

            if (definition.ResourceType != ItemResourceType.None)
            {
                instance.CurrentResource = Mathf.Clamp(
                    resourceValue,
                    0f,
                    definition.MaxResource
                );
            }

            return instance;
        }

        // ================================
        // CREATE AT INVENTORY INDEX
        // ================================

        public static ItemInstance CreateInstanceAtIndex(
            ItemDefinition definition,
            int inventoryIndex,
            int stackSize = 1)
        {
            var instance = CreateInstance(definition, stackSize);
            if (instance != null)
            {
                instance.InventoryIndex = inventoryIndex;
            }

            return instance;
        }

        // ================================
        // ID GENERATION
        // ================================

        private static string GenerateInstanceId()
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string randomPart = GenerateRandomString(4);
            string guidPart = Guid.NewGuid().ToString("N").Substring(0, 8);

            return $"{timestamp}_{randomPart}_{guidPart}";
        }

        private static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            char[] result = new char[length];

            for (int i = 0; i < length; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }

            return new string(result);
        }

        // ================================
        // VALIDATION
        // ================================

        public static bool ValidateInstance(ItemInstance instance)
        {
            if (instance == null)
                return false;

            if (instance.Definition == null)
            {
                UnityEngine.Debug.LogError("[ItemInstanceFactory] Instance has null definition");
                return false;
            }

            if (string.IsNullOrEmpty(instance.InstanceId))
            {
                UnityEngine.Debug.LogError("[ItemInstanceFactory] Instance has empty InstanceId");
                return false;
            }

            if (instance.StackSize <= 0)
            {
                UnityEngine.Debug.LogError("[ItemInstanceFactory] Instance has invalid StackSize");
                return false;
            }

            if (!instance.Definition.IsStackable && instance.StackSize != 1)
            {
                UnityEngine.Debug.LogError("[ItemInstanceFactory] Non-stackable item has StackSize != 1");
                return false;
            }

            if (instance.Definition.IsStackable &&
                instance.StackSize > instance.Definition.MaxStackSize)
            {
                UnityEngine.Debug.LogWarning(
                    $"[ItemInstanceFactory] StackSize ({instance.StackSize}) exceeds MaxStackSize ({instance.Definition.MaxStackSize})"
                );
            }

            if (instance.Definition.ResourceType != ItemResourceType.None)
            {
                if (instance.CurrentResource < 0f ||
                    instance.CurrentResource > instance.Definition.MaxResource)
                {
                    UnityEngine.Debug.LogWarning("[ItemInstanceFactory] Resource value out of bounds");
                }
            }

            return true;
        }
    }
}
