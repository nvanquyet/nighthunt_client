using UnityEngine;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Character.Stats;

namespace NightHunt.Gameplay.Zone
{
    /// <summary>
    /// Zone-based buffs/nerfs
    /// </summary>
    public class ZoneBuff : MonoBehaviour
    {
        [Header("Zone Buff Settings")]
        [SerializeField] private string statName;
        [SerializeField] private float value;
        [SerializeField] private ModifierType modifierType = ModifierType.Multiply;
        [SerializeField] private float radius = 10f;

        private void OnTriggerEnter(Collider other)
        {
            // Use ComponentFinder to search in hierarchy (including children)
            var characterStats = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<CharacterStats>(other.gameObject, includeInactive: false);
            if (characterStats != null)
            {
                ApplyBuff(characterStats);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Use ComponentFinder to search in hierarchy (including children)
            var characterStats = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<CharacterStats>(other.gameObject, includeInactive: false);
            if (characterStats != null)
            {
                RemoveBuff(characterStats);
            }
        }

        /// <summary>
        /// Apply buff to character
        /// </summary>
        private void ApplyBuff(CharacterStats stats)
        {
            var modifier = new StatModifier(statName, modifierType, value, $"Zone_{gameObject.GetInstanceID()}");
            // TODO: Apply through modifier stack
        }

        /// <summary>
        /// Remove buff from character
        /// </summary>
        private void RemoveBuff(CharacterStats stats)
        {
            // TODO: Remove modifier by source ID
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

