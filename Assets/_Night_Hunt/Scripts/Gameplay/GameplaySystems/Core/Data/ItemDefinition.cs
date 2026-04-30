using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Abstract base for every item definition. Contains ONLY the identity data that is
    /// meaningful for ALL item categories without exception.
    ///
    /// Hierarchy:
    ///   ItemDefinition  (this class)
    ///   └─ PhysicalItemDefinition   — items that exist in the world (held / dropped)
    ///      ├─ EquippableItemDefinition  — items that go into slots and accept attachments
    ///      │  ├─ WeaponDefinition
    ///      │  └─ EquipmentDefinition
    ///      ├─ AttachmentDefinition     — modifies a host item's stats
    ///      └─ UsableItemDefinition     — items consumed / thrown with a usage duration
    ///         ├─ ConsumableDefinition
    ///         └─ ThrowableDefinition
    ///
    /// RULES:
    ///   • Every public field on this class must be relevant to ALL derived types.
    ///   • Category-specific data belongs on an intermediate abstract class or the leaf class.
    ///   • Never throw exceptions from validation; always return false + populate the error string.
    /// </summary>
    public abstract class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique definition ID. Auto-generated from the asset name on first validate.")]
        public string ItemID;

        [Tooltip("Name shown in the UI.")]
        public string DisplayName;

        [TextArea(2, 4)]
        [Tooltip("Short description displayed in the tooltip / inspection panel.")]
        public string Description;

        [Tooltip("Icon used in inventory slots, HUD, and tooltips.")]
        public Sprite Icon;

        [Tooltip("Rarity tier — controls UI border colour and loot-table weights.")]
        public ItemRarity Rarity = ItemRarity.Common;

        [Header("Stack & Weight")]
        [Tooltip("When true, multiple units share one inventory slot.")]
        public bool IsStackable;

        [Tooltip("Maximum units per stack.")]
        [Min(1)] public int MaxStackSize = 1;

        [Tooltip("Mass of one unit in kilograms. Contributes to the carry-weight budget.")]
        [Min(0f)] public float Weight = 1f;

        /// <summary>Item category. Implemented by every concrete definition class.</summary>
        public abstract ItemType Type { get; }

        // ── Polymorphic accessors for subclass-specific data ─────────────────────
        // These allow callers that hold an ItemDefinition reference to access common
        // subclass properties without casting. Subclasses override the ones they support.

        /// <summary>Attachment socket types this item exposes. Null for non-equippable items.</summary>
        public virtual AttachmentSlotType[] AttachmentSlots { get => null; set { } }

        /// <summary>Starting resource value (ammo / durability / battery) for a new ItemInstance. 0 by default.</summary>
        public virtual float GetDefaultCurrentValue() => 0f;

        /// <summary>Whether an in-progress use action can be cancelled. True by default.</summary>
        public virtual bool CanCancelUsage { get => true; set { } }

        /// <summary>True if this attachment fits the given slot type. Always false for non-attachment items.</summary>
        public virtual bool CanAttachToSlot(AttachmentSlotType slotType) => false;

        // ── Validation ──────────────────────────────────────────────────────────

        /// <summary>
        /// Validates the definition. Derived classes should call base.IsValid() first,
        /// then append their own checks.
        /// Returns false and populates <paramref name="error"/> on failure; never throws.
        /// </summary>
        public virtual bool IsValid(out string error)
        {
            if (string.IsNullOrEmpty(ItemID))
            {
                error = "ItemID cannot be empty";
                return false;
            }

            if (string.IsNullOrEmpty(DisplayName))
            {
                error = "DisplayName cannot be empty";
                return false;
            }

            if (Icon == null)
            {
                error = "Icon is required";
                return false;
            }

            if (IsStackable && MaxStackSize < 1)
            {
                error = "MaxStackSize must be >= 1 for stackable items";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Total mass for a stack of the given quantity.</summary>
        public virtual float GetTotalWeight(int quantity) => Weight * quantity;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(ItemID)) ItemID = name.ToLower();
            if (IsStackable && MaxStackSize < 1) MaxStackSize = 1;
            if (!IsStackable) MaxStackSize = 1;
        }
#endif
    }

    // ── Shared Enums ────────────────────────────────────────────────────────────

    /// <summary>Top-level item category. Never reorder — values are serialised.</summary>
    public enum ItemType
    {
        Equipment  = 0,  // Armour, clothing, backpack
        Weapon     = 1,  // Guns, melee weapons
        Consumable = 2,  // Food, medkits, potions
        Attachment = 3,  // Scopes, suppressors, grips
        Material   = 4,  // Crafting materials
        Throwable  = 5,  // Grenades, flashbangs
        Quest      = 6,  // Non-droppable quest items
        Deployable = 7,  // Placeable objects (beacons, traps)
        Misc       = 8,  // Everything else
    }

    /// <summary>Broad slot category. Used for drag-and-drop validation.</summary>
    public enum SlotLocationType
    {
        Inventory  = 0,  // Main inventory grid
        Equipment  = 1,  // Equipment panel (head, chest …)
        Weapon     = 2,  // Weapon slots (primary, secondary, melee)
        Attachment = 3,  // Attachment sockets on a parent item
    }

    /// <summary>Specific equipment body slot.</summary>
    public enum EquipmentSlotType
    {
        Head  = 0,
        Face  = 1,
        Chest = 2,
        Back  = 3,
        Belt  = 4,
        Legs  = 5,
        Feet  = 6,
        Hands = 7,
    }

    /// <summary>
    /// Weapon holster slot index.
    /// Values 0–4 are slot indices used as SyncDictionary keys — never reorder.
    /// None = 99 is intentionally outside the index range so iteration over
    /// InventoryConfig.WeaponConfig never accidentally includes it.
    /// To add a new game-mode slot, add a new value before None (e.g. Slot3 = 3).
    /// </summary>
    public enum WeaponSlotType
    {
        Primary   = 0,  // Main long-arm (rifle, shotgun, SMG)
        Secondary = 1,  // Sidearm (pistol)
        Melee     = 2,  // Knife, axe, close-combat
        Slot3     = 3,  // Reserved — 4-slot configs (e.g. dual setup)
        Slot4     = 4,  // Reserved — 5-slot configs
        None      = 99, // Unholstered / not in any slot (moved out of index range)
    }

    /// <summary>
    /// Attachment socket type. Weapons and equipment declare which sockets they expose;
    /// attachment definitions declare which socket types they fit into.
    /// Never reorder — values are serialised.
    /// </summary>
    public enum AttachmentSlotType
    {
        None        = 0,
        // Weapon sockets
        Optic       = 1,   // Scopes, red dots, holographic sights
        Barrel      = 2,   // Suppressors, muzzle brakes, compensators
        Grip        = 3,   // Foregrips, vertical grips
        Magazine    = 4,   // Extended magazines
        Stock       = 5,   // Buttstocks, folding stocks
        UnderBarrel = 6,   // Flashlights, lasers, grenade launchers
        // Equipment sockets
        Light       = 7,   // Headlamps, flashlights on armour
        Pouch       = 8,   // Storage pouches on vest / belt
        Plate       = 9,   // Armour plates on vest
        // Generic slots
        Accessory1  = 10,
        Accessory2  = 11,
        Accessory3  = 12,
    }
}
