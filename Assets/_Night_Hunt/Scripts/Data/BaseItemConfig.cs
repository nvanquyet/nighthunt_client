using System;
using UnityEngine;

namespace NightHunt.Data
{
    /// <summary>
    /// Base class for all item configurations
    /// Contains common fields shared by all item types
    /// </summary>
    [Serializable]
    public abstract class BaseItemConfig
    {
        // Common fields
        public string ItemId;
        public string DisplayName;
        public string Category;
        public ItemType Type;
        public string[] Tags; // Filter tags: Scope, Suppressor, Medkit...

        // Stacking
        public bool IsStackable;
        public int MaxStack;

        // Visual (prefabs)
        public string WorldPrefabId; // Prefab cho loot ngoài world
        public string EquippedPrefabId; // Prefab khi equip lên player/item (có thể = WorldPrefabId)
        public string IconSpriteId; // UI icon sprite ID (load từ Resources/Sprites)

        // Equip rules
        public string[] EquipSlots; // ["PrimaryWeapon", "Head", "Body"...]
        public SocketDefinition[] Sockets; // Cho nested equipment

        // Use/Cast
        public UseType UseType; // Instant, Channel, PlaceOnGround...
        public float UseTime; // Thời gian cast/use (VD: 2.5s cho consumable)
        public string FxStartId; // FX khi bắt đầu use
        public string FxSuccessId; // FX khi use thành công
        public string AnimId; // Animation ID (VD: "DrinkPotion", "InjectBooster")

        // Security
        public bool ServerOnlySpawn; // Chỉ server spawn từ config này
        public bool CanDrop;
        public bool CanTrade;
        public int AllowedPhaseMask;

        // Weight
        public float Weight;

        // Rarity
        public string Rarity;

        /// <summary>
        /// Get icon sprite (load from Resources)
        /// </summary>
        public Sprite GetIcon()
        {
            if (string.IsNullOrEmpty(IconSpriteId)) return null;
            return Resources.Load<Sprite>($"Sprites/Items/{IconSpriteId}");
        }
    }
}

