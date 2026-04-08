using UnityEngine;
using UnityEngine.Serialization;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Abstract base for items that have a physical presence in the world —
    /// they can be held in a character's hands and dropped on the ground.
    ///
    /// Adds: world visuals (HeldPrefab, GroundPrefab) and inventory placement rules (ValidSlots).
    /// Every field here is meaningful to ALL subclasses (Weapon, Equipment, Attachment,
    /// Consumable, Throwable).
    /// </summary>
    public abstract class PhysicalItemDefinition : ItemDefinition
    {
        [Header("World Visuals")]
        [Tooltip("Prefab shown while the item is held in the character's hands / equipped. " +
                 "Client-side only — no NetworkObject required. " +
                 "For weapons: assign Weapon_Hitscan_Template or Weapon_Projectile_Template " +
                 "(generated via NightHunt/Tools/Build Template Prefabs) then add your mesh under [Model] " +
                 "and position [FirePoint] + [LeftHandIK] in prefab edit mode.")]
        [FormerlySerializedAs("HeldPrefab")]
        [SerializeField] private GameObject _heldPrefab;

        [Tooltip("Prefab shown when the item lies on the ground waiting to be picked up. " +
                 "Attached to the WorldItem spawner.")]
        [FormerlySerializedAs("GroundPrefab")]
        [SerializeField] private GameObject _groundPrefab;

        public override GameObject HeldPrefab  { get => _heldPrefab;  set => _heldPrefab  = value; }
        public override GameObject GroundPrefab { get => _groundPrefab; set => _groundPrefab = value; }

        [Header("Placement")]
        [Tooltip("Inventory regions this item is allowed to occupy. " +
                 "Leave empty to restrict to the main Inventory grid only.")]
        public SlotLocationType[] ValidSlots;

        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when this item is allowed to sit in the specified slot region.
        /// If ValidSlots is empty the item defaults to Inventory only.
        /// </summary>
        public bool CanPlaceInSlot(SlotLocationType slotType)
        {
            if (ValidSlots == null || ValidSlots.Length == 0)
                return slotType == SlotLocationType.Inventory;

            foreach (var s in ValidSlots)
                if (s == slotType) return true;

            return false;
        }
    }
}
