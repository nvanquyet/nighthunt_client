using System.Collections.Generic;
using UnityEngine;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Character.Stats;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Equipment;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Items.Data;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Game-specific bridge that maps InteractionSystem equipment stats
    /// (backpacks, armor, etc.) into CharacterStats modifiers using enum-based stat system.
    /// This keeps the InteractionSystem package generic and all concrete
    /// gameplay logic in the NightHunt.Gameplay layer.
    /// </summary>
    public class CharacterEquipmentStatsBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterStats characterStats;
        [SerializeField] private EquipmentManager equipmentManager;

        private const string EquipSourcePrefix = "Equip:";

        private void Awake()
        {
            if (characterStats == null)
                characterStats = GetComponent<CharacterStats>();

            if (equipmentManager == null)
                equipmentManager = GetComponent<EquipmentManager>();

            InventoryEvents.OnEquipmentChanged += HandleEquipmentChanged;

            // Initial sync in case equipment is already present (eg. default loadout).
            HandleEquipmentChanged();
        }

        private void OnDestroy()
        {
            InventoryEvents.OnEquipmentChanged -= HandleEquipmentChanged;
        }

        private void HandleEquipmentChanged()
        {
            if (characterStats == null || equipmentManager == null)
                return;

            // Clear all equipment-related modifiers, then re-apply from current equipment.
            characterStats.RemoveAllModifiersWithSourcePrefix(EquipSourcePrefix);

            Dictionary<EquipmentSlot, ItemInstance> allEquipped = equipmentManager.GetAllEquippedItems();
            foreach (var kvp in allEquipped)
            {
                EquipmentSlot slot = kvp.Key;
                EquipmentDataBase data = equipmentManager.GetEquippedData(slot);
                if (data == null)
                    continue;

                string sourceIdPrefix = $"{EquipSourcePrefix}{slot}";
                ApplyEquipmentDataToCharacter(data, sourceIdPrefix);
            }
        }

        private void ApplyEquipmentDataToCharacter(EquipmentDataBase equipmentData, string sourceId)
        {
            // 1) Apply generic StatModifier entries from equipment (armor, special gear, etc.)
            if (equipmentData.BaseStatModifiers != null)
            {
                foreach (InteractionSystem.Core.Structs.StatModifier mod in equipmentData.BaseStatModifiers)
                {
                    MapAndApplyStatModifier(mod, sourceId);
                }
            }

            // 2) Special handling for backpacks (extra capacity / slots).
            if (equipmentData is BackpackData backpack)
            {
                if (Mathf.Abs(backpack.AdditionalWeightCapacity) > 0.0001f)
                {
                    // Use enum-based API
                    characterStats.AddModifier(CharacterStatType.WeightCapacity, Stats.ModifierType.Add, backpack.AdditionalWeightCapacity, sourceId);
                }

                // NOTE: AdditionalSlots affects inventory grid/list, not character movement directly.
                // Slot capacity changes are handled at inventory layer (if/when implemented),
                // keeping this bridge focused purely on character stats.
            }
        }

        /// <summary>
        /// Map InteractionSystem StatType (package) to CharacterStatType (game) and apply it.
        /// This mapping is game-specific and can be extended as needed.
        /// </summary>
        private void MapAndApplyStatModifier(InteractionSystem.Core.Structs.StatModifier mod, string sourceId)
        {
            CharacterStatType? charStatType = null;
            Stats.ModifierType type;
            float value = mod.value;

            // Map package StatType to game CharacterStatType
            switch (mod.statType)
            {
                case InteractionSystem.Core.Structs.StatType.MovementSpeed:
                    charStatType = CharacterStatType.MoveSpeed;
                    type = Stats.ModifierType.Multiply;
                    break;

                case InteractionSystem.Core.Structs.StatType.Weight:
                    // Interpret equipment "Weight" as extra carry capacity in this game.
                    charStatType = CharacterStatType.WeightCapacity;
                    type = Stats.ModifierType.Add;
                    break;

                case InteractionSystem.Core.Structs.StatType.VisionRange:
                    charStatType = CharacterStatType.VisionRadius;
                    type = Stats.ModifierType.Add;
                    break;

                case InteractionSystem.Core.Structs.StatType.DamageReduction:
                    charStatType = CharacterStatType.DamageReduction;
                    type = Stats.ModifierType.Add;
                    break;

                case InteractionSystem.Core.Structs.StatType.ArmorValue:
                    // Could map to DamageReduction or a separate stat
                    charStatType = CharacterStatType.DamageReduction;
                    type = Stats.ModifierType.Add;
                    break;

                default:
                    // Other StatTypes are weapon-specific (Damage, Recoil, FireRate, etc.) and are
                    // handled by weapon stat calculators, not by character stats.
                    return;
            }

            if (charStatType.HasValue)
            {
                characterStats.AddModifier(charStatType.Value, type, value, sourceId);
            }
        }
    }
}
