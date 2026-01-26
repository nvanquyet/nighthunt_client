using System.Collections.Generic;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Equipment;
using NightHunt.InteractionSystem.Items;
using UnityEngine;

namespace NightHunt.InteractionSystem.Combat
{
    public class ArmorComponent : MonoBehaviour
    {
        [Header("Dependencies")] [SerializeField]
        private EquipmentManager equipmentManager;

        private Dictionary<DamageType, float> damageReduction = new Dictionary<DamageType, float>();

        public float CalculateDamageReduction(float rawDamage, DamageType damageType)
        {
            UpdateArmorStats();

            if (damageReduction.TryGetValue(damageType, out float reduction))
            {
                return rawDamage * (1f - reduction);
            }

            return rawDamage;
        }

        private void UpdateArmorStats()
        {
            damageReduction.Clear();

            // Get armor from helmet
            ProcessEquipmentSlot(EquipmentSlot.Head);

            // Get armor from chest
            ProcessEquipmentSlot(EquipmentSlot.Chest);
        }

        private void ProcessEquipmentSlot(EquipmentSlot slot)
        {
            ItemInstance? item = equipmentManager.GetEquippedItem(slot);
            if (!item.HasValue) return;

            // Get attachment manager for this slot
            AttachmentManager attachmentManager = equipmentManager.GetAttachmentManager(slot);
            if (attachmentManager == null) return;

            // Get final stats (base + attachments)
            StatModifier[] stats = attachmentManager.GetFinalStats();

            foreach (var stat in stats)
            {
                if (stat.statType == StatType.DamageReduction)
                {
                    // Aggregate damage reduction
                    if (!damageReduction.ContainsKey(DamageType.Bullet))
                    {
                        damageReduction[DamageType.Bullet] = 0f;
                    }

                    damageReduction[DamageType.Bullet] += stat.value / 100f; // Convert percentage
                }
            }
        }

        public float GetTotalDamageReduction(DamageType damageType)
        {
            UpdateArmorStats();

            if (damageReduction.TryGetValue(damageType, out float reduction))
            {
                return Mathf.Clamp01(reduction) * 100f; // Return as percentage
            }

            return 0f;
        }
    }
}