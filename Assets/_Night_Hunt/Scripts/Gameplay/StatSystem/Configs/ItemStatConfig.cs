using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;

namespace NightHunt.StatSystem.Configs
{
    /// <summary>
    /// Base config for item stats. Contains Stats + PlayerModifiers + ItemModifiers.
    /// Equipment/Weapon use PlayerModifiers. Attachment uses ItemModifiers.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemStatConfig", menuName = "NightHunt/StatSystem/Item Stat Config")]
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
}
