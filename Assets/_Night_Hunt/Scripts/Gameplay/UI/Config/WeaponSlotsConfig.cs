using UnityEngine;

namespace NightHunt.Gameplay.UI.Config
{
    /// <summary>
    /// Config for weapon slots
    /// Defines number of weapon slots and their prefab
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponSlotsConfig", menuName = "NightHunt/UI/WeaponSlotsConfig")]
    public class WeaponSlotsConfig : ScriptableObject
    {
        [Header("Weapon Slots Settings")]
        [Tooltip("Number of weapon slots (default: 2)")]
        [Range(1, 4)]
        public int slotCount = 2;

        [Tooltip("Prefab for weapon slot UI (spawned dynamically)")]
        public GameObject slotPrefab;
    }
}
