using UnityEngine;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Base class for items that can exist as a client-side visual.
    /// Runtime or networked prefabs are not stored here; this class only owns the single pure visual prefab.
    /// </summary>
    public abstract class PhysicalItemDefinition : ItemDefinition
    {
        [Header("Visual")]
        [Tooltip("Single pure client-side visual prefab used for world loot and in-hand preview. Do not assign prefabs with NetworkObject here.")]
        [SerializeField] private GameObject _visualPrefab;

        public GameObject VisualPrefab { get => _visualPrefab; set => _visualPrefab = value; }

        [Header("Placement")]
        [Tooltip("Inventory regions this item is allowed to occupy. Leave empty to restrict to the main Inventory grid only.")]
        public SlotLocationType[] ValidSlots;

        public bool CanPlaceInSlot(SlotLocationType slotType)
        {
            if (ValidSlots == null || ValidSlots.Length == 0)
                return slotType == SlotLocationType.Inventory;

            foreach (var s in ValidSlots)
                if (s == slotType)
                    return true;

            return false;
        }
    }
}
