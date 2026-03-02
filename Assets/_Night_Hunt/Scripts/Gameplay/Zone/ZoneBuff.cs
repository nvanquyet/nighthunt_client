// using UnityEngine;
// using NightHunt.Inventory.Stats;
//
// namespace NightHunt.Gameplay.Zone
// {
//     /// <summary>
//     /// Zone-based buffs/nerfs
//     /// </summary>
//     public class ZoneBuff : MonoBehaviour
//     {
//         [Header("Zone Buff Settings")]
//         [SerializeField] private string statName;
//         [SerializeField] private float value;
//         //[SerializeField] private ModifierType modifierType = ModifierType.Multiply;
//         [SerializeField] private float radius = 10f;
//
//         private void OnTriggerEnter(Collider other)
//         {
//             var characterStats = other.GetComponent<PlayerStats>();
//             if (characterStats != null)
//             {
//                 ApplyBuff(characterStats);
//             }
//         }
//
//         private void OnTriggerExit(Collider other)
//         {
//             var characterStats = other.GetComponent<PlayerStats>();
//             if (characterStats != null)
//             {
//                 RemoveBuff(characterStats);
//             }
//         }
//
//         /// <summary>
//         /// Apply buff to character
//         /// </summary>
//         private void ApplyBuff(PlayerStats stats)
//         {
//             //var modifier = new StatModifier(statName, modifierType, value, $"Zone_{gameObject.GetInstanceID()}");
//             // Apply through modifier stack when ZoneSystem is enabled.
//         }
//
//         /// <summary>
//         /// Remove buff from character
//         /// </summary>
//         private void RemoveBuff(PlayerStats stats)
//         {
//             // Remove modifier by source ID when ZoneSystem is enabled.
//         }
//
//         private void OnDrawGizmosSelected()
//         {
//             Gizmos.color = Color.green;
//             Gizmos.DrawWireSphere(transform.position, radius);
//         }
//     }
// }
//
