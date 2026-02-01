using UnityEngine;

namespace NightHunt.Gameplay.UI.Config
{
    /// <summary>
    /// Config for quick slots
    /// Defines number of quick slots and their prefab
    /// Supports runtime changes
    /// </summary>
    [CreateAssetMenu(fileName = "QuickSlotsConfig", menuName = "NightHunt/UI/QuickSlotsConfig")]
    public class QuickSlotsConfig : ScriptableObject
    {
        [Header("Quick Slots Settings")]
        [Tooltip("Number of quick slots (default: 4)")]
        [Range(1, 10)]
        public int slotCount = 4;

        [Tooltip("Prefab for quick slot UI (spawned dynamically)")]
        public GameObject slotPrefab;

        [Header("Runtime Settings")]
        [Tooltip("Can change number of slots in runtime")]
        public bool canChangeInRuntime = true;

        [Tooltip("Minimum number of slots (if canChangeInRuntime is true)")]
        [Range(1, 10)]
        public int minSlotCount = 1;

        [Tooltip("Maximum number of slots (if canChangeInRuntime is true)")]
        [Range(1, 20)]
        public int maxSlotCount = 10;
    }
}
