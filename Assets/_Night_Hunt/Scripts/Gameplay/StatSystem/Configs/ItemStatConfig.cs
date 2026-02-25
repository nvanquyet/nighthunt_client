using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;

namespace NightHunt.StatSystem.Configs
{
    /// <summary>
    /// Base config for item stats. Contains Stats + PlayerModifiers + ItemModifiers.
    /// Equipment/Weapon use PlayerModifiers. Attachment uses ItemModifiers.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemStatConfig", menuName = "StatSystem/Config/Item Stat Config")]
    public class ItemStatConfig : ScriptableObject
    {
        [Header("Base Stats")]
        [Tooltip("Item stat definitions (Damage, Accuracy, Weight, etc.)")]
        public ItemStatDefinition[] Stats;

        [Header("Player Modifiers (Equipment, Weapon)")]
        [Tooltip("Modifiers applied to player when equipped")]
        public PlayerStatModifier[] PlayerModifiers;

        [Header("Item Modifiers (Attachment)")]
        [Tooltip("Modifiers applied to parent item when attached")]
        public ItemStatModifier[] ItemModifiers;

        #region Helpers

        public float GetStatValue(ItemStatType type)
        {
            if (Stats == null) return 0f;
            foreach (var s in Stats)
            {
                if (s.Type == type) return s.DefaultValue;
            }
            return 0f;
        }

        public bool HasStat(ItemStatType type)
        {
            if (Stats == null) return false;
            foreach (var s in Stats)
            {
                if (s.Type == type) return true;
            }
            return false;
        }

        #endregion

        #region Stat Definition

        [System.Serializable]
        public struct ItemStatDefinition
        {
            public ItemStatType Type;
            public float DefaultValue;
            public float MinValue;
            public float MaxValue;
        }

        #endregion 
    }

    /// <summary>
    /// Equipment stat config - Stats + PlayerModifiers
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentStatConfig", menuName = "StatSystem/Config/Equipment Stat Config")]
    public class EquipmentStatConfig : ItemStatConfig { }

    /// <summary>
    /// Weapon stat config - Stats + PlayerModifiers
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponStatConfig", menuName = "StatSystem/Config/Weapon Stat Config")]
    public class WeaponStatConfig : ItemStatConfig { }

    /// <summary>
    /// Attachment stat config - Stats + ItemModifiers
    /// </summary>
    [CreateAssetMenu(fileName = "AttachmentStatConfig", menuName = "StatSystem/Config/Attachment Stat Config")]
    public class AttachmentStatConfig : ItemStatConfig { }

    /// <summary>
    /// Consumable stat config - Stats + ConsumableEffects
    /// </summary>
    [CreateAssetMenu(fileName = "ConsumableStatConfig", menuName = "StatSystem/Config/Consumable Stat Config")]
    public class ConsumableStatConfig : ItemStatConfig
    {
        [Header("Consumable Effects")]
        [Tooltip("All effects applied when this item finishes being consumed.")]
        public GameplaySystems.Core.Data.ConsumableEffect[] Effects;
    }

    /// <summary>
    /// Throwable stat config - Stats only (typically Weight).
    /// No PlayerModifiers / ItemModifiers / Effects.
    /// </summary>
    [CreateAssetMenu(fileName = "ThrowableStatConfig", menuName = "StatSystem/Config/Throwable Stat Config")]
    public class ThrowableStatConfig : ItemStatConfig { }
}
