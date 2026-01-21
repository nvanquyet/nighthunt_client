using System;
using System.Collections.Generic;
using NightHunt.Data;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Runtime item instance
    /// Represents an item in inventory or equipment with unique instance ID
    /// Supports nested equipment through sockets
    /// </summary>
    [Serializable]
    public class ItemInstance
    {
        public string ItemId;
        public int Quantity;
        public Guid InstanceId; // Unique ID cho instance này
        public Dictionary<string, ItemInstance> Sockets; // key = socketId, value = attached item instance
        public BaseItemConfig Config; // Reference tới config (load từ GameConfigLoader)

        public ItemInstance()
        {
            InstanceId = Guid.NewGuid();
            Sockets = new Dictionary<string, ItemInstance>();
            Quantity = 1;
        }

        public ItemInstance(string itemId, int quantity = 1) : this()
        {
            ItemId = itemId;
            Quantity = quantity;
            LoadConfig();
        }

        /// <summary>
        /// Load config from GameConfigLoader
        /// </summary>
        public void LoadConfig()
        {
            if (GameConfigLoader.Instance != null)
            {
                // Use new BaseItemConfig pipeline (converted from legacy if needed)
                Config = GameConfigLoader.Instance.GetItemConfigBase(ItemId);
            }
        }

        /// <summary>
        /// Attach item to socket
        /// </summary>
        public bool AttachToSocket(string socketId, ItemInstance item)
        {
            if (Sockets.ContainsKey(socketId))
            {
                return false; // Socket already occupied
            }

            // Validate socket compatibility
            if (Config?.Sockets != null)
            {
                foreach (var socket in Config.Sockets)
                {
                    if (socket.SocketId == socketId)
                    {
                        // Check if item is compatible: any tag of item matches any allowed category
                        if (item?.Config != null
                            && socket.AllowedCategories != null
                            && item.Config.Tags != null)
                        {
                            bool compatible = false;
                            foreach (var allowed in socket.AllowedCategories)
                            {
                                if (string.IsNullOrEmpty(allowed)) continue;
                                foreach (var tag in item.Config.Tags)
                                {
                                    if (string.Equals(tag, allowed, StringComparison.OrdinalIgnoreCase))
                                    {
                                        compatible = true;
                                        break;
                                    }
                                }
                                if (compatible) break;
                            }

                            if (compatible)
                            {
                                Sockets[socketId] = item;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Detach item from socket
        /// </summary>
        public ItemInstance DetachFromSocket(string socketId)
        {
            if (Sockets.ContainsKey(socketId))
            {
                var item = Sockets[socketId];
                Sockets.Remove(socketId);
                return item;
            }
            return null;
        }

        /// <summary>
        /// Check if socket is available
        /// </summary>
        public bool IsSocketAvailable(string socketId)
        {
            return !Sockets.ContainsKey(socketId);
        }
    }
}

