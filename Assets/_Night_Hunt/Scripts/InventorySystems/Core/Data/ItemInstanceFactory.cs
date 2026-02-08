using System;
using UnityEngine;

namespace NightHunt.Inventory.Core.Data
{
    /// <summary>
    /// Factory for creating ItemInstance objects.
    /// Server-authoritative: Only server generates instance IDs.
    /// </summary>
    public static class ItemInstanceFactory
    {
        private static System.Random random = new System.Random();
        
        /// <summary>
        /// Create a new item instance with server-generated unique ID.
        /// SERVER ONLY - Clients should never call this directly.
        /// </summary>
        public static ItemInstance CreateInstance(ItemDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("[ItemInstanceFactory] Cannot create instance from null definition");
                return null;
            }
            
            string instanceId = GenerateInstanceId();
            return new ItemInstance(definition, instanceId);
        }
        
        /// <summary>
        /// Create instance with custom stack size.
        /// </summary>
        public static ItemInstance CreateInstance(ItemDefinition definition, int stackSize)
        {
            var instance = CreateInstance(definition);
            if (instance != null)
            {
                instance.StackSize = Mathf.Clamp(stackSize, 1, definition.MaxStackSize);
            }
            return instance;
        }
        
        /// <summary>
        /// Create instance with custom durability.
        /// </summary>
        public static ItemInstance CreateInstanceWithDurability(ItemDefinition definition, float durability)
        {
            var instance = CreateInstance(definition);
            if (instance != null)
            {
                instance.CurrentDurability = Mathf.Clamp(durability, 0f, definition.MaxDurability);
            }
            return instance;
        }
        
        /// <summary>
        /// Generate unique instance ID.
        /// Format: {timestamp}_{random}_{guid}
        /// Example: 1738934400_A3F9_4d3e2f1a
        /// </summary>
        private static string GenerateInstanceId()
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string randomPart = GenerateRandomString(4);
            string guidPart = Guid.NewGuid().ToString("N").Substring(0, 8);
            
            return $"{timestamp}_{randomPart}_{guidPart}";
        }
        
        /// <summary>
        /// Generate random alphanumeric string.
        /// </summary>
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
    }
}