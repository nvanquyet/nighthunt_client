using UnityEngine;
using GameplaySystems.Stat;
    
namespace GameplaySystems.Core.Data
{
/// <summary>
    /// Base class for all item definitions
    /// Abstract - must be inherited by specific item types
    /// 
    /// Usage:
    /// 1. Create derived class (WeaponDefinition, ArmorDefinition, etc.)
    /// 2. Create ScriptableObject asset
    /// 3. Configure in inspector
    /// 4. ItemDatabase auto-loads from Resources folder
    /// </summary>
    public abstract class ItemDefinition : ScriptableObject
    {
        #region Basic Info
        
        [Header("Basic Info")]
        [Tooltip("Unique ID cho item này (auto-generated from asset name)")]
        public string ItemID;
        
        [Tooltip("Tên hiển thị")]
        public string DisplayName;
        
        [TextArea(3, 5)]
        [Tooltip("Mô tả item")]
        public string Description;
        
        [Tooltip("Icon hiển thị trong inventory")]
        public Sprite Icon;
        
        /// <summary>
        /// Item type (override in derived classes)
        /// </summary>
        public abstract ItemType Type { get; }
        
        #endregion
        
        #region Stack & Weight
        
        [Header("Stack & Weight")]
        [Tooltip("Item này có thể stack không")]
        public bool IsStackable = false;
        
        [Tooltip("Số lượng tối đa trong 1 stack")]
        [Min(1)]
        public int MaxStackSize = 1;
        
        [Tooltip("Trọng lượng của 1 item (kg)")]
        [Min(0f)]
        public float Weight = 1f;
        
        [Tooltip("Item này có modify weight khi equipped không")]
        public bool ModifyWeightWhenEquipped = false;
        
        [Tooltip("Modifier weight khi equipped (âm = giảm weight, dương = tăng weight)")]
        public float EquippedWeightModifier = 0f;
        
        #endregion
        
        #region Resource System
        
        [Header("Resource System")]
        [Tooltip("Loại resource chính của item này")]
        public ItemResourceType ResourceType = ItemResourceType.None;
        
        [Tooltip("Giá trị resource tối đa")]
        [Min(0f)]
        public float MaxResource = 0f;
        
        [Tooltip("Giá trị resource mặc định khi spawn")]
        [Min(0f)]
        public float DefaultResource = 0f;
        
        #endregion
        
        #region Attachments
        
        [Header("Attachments")]
        [Tooltip("Các attachment slot mà item này có")]
        public AttachmentSlotType[] AttachmentSlots;
        
        [Tooltip("Item này có thể attach vào slot types nào")]
        public AttachmentSlotType[] CanAttachTo;
        
        #endregion
        
        #region Slot Placement
        
        [Header("Slot Placement")]
        [Tooltip("Item này có thể đặt ở slot locations nào")]
        public SlotLocationType[] ValidSlots;
        
        #endregion
        
        #region Visual
        
        [Header("Visual")]
        [Tooltip("Prefab model khi equipped")]
        public GameObject EquippedPrefab;
        
        [Tooltip("Prefab khi drop trên ground")]
        public GameObject DroppedPrefab;
        
        #endregion
        
        #region Usage System
        
        [Header("Usage System")]
        [Tooltip("Thời gian sử dụng item (0 = instant, >0 = có progress bar)")]
        [Min(0f)]
        public float UsageDuration = 0f;
        
        [Tooltip("Có thể cancel usage giữa chừng không")]
        public bool CanCancelUsage = true;
        
        [Tooltip("Có thể sử dụng khi đang di chuyển không")]
        public bool CanUseWhileMoving = false;
        
        #endregion
        
        #region Abstract Methods
        
        /// <summary>
        /// Get maximum resource value
        /// Override in derived classes if needed (e.g., weapon may use different resource)
        /// </summary>
        public virtual float GetMaxResource()
        {
            return MaxResource;
        }
        
        /// <summary>
        /// Get default resource value when item spawned
        /// Override in derived classes if needed
        /// </summary>
        public virtual float GetDefaultResource()
        {
            return DefaultResource;
        }
        
        #endregion
        
        #region Validation
        
        /// <summary>
        /// Validate item definition
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
            
            if (ResourceType != ItemResourceType.None && MaxResource <= 0)
            {
                error = $"MaxResource must be > 0 when ResourceType is {ResourceType}";
                return false;
            }
            
            error = null;
            return true;
        }
        
        #endregion
        
        #region Helpers
        
        /// <summary>
        /// Get total weight for a quantity
        /// </summary>
        public virtual float GetTotalWeight(int quantity)
        {
            return Weight * quantity;
        }
        
        /// <summary>
        /// Check if item can be placed in slot type
        /// </summary>
        public bool CanPlaceInSlot(SlotLocationType slotType)
        {
            if (ValidSlots == null || ValidSlots.Length == 0)
                return slotType == SlotLocationType.Inventory; // Default to inventory only
            
            foreach (var validSlot in ValidSlots)
            {
                if (validSlot == slotType)
                    return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if item can be attached to specific slot type
        /// </summary>
        public bool CanAttachToSlot(AttachmentSlotType slotType)
        {
            if (CanAttachTo == null || CanAttachTo.Length == 0)
                return false;
            
            foreach (var slot in CanAttachTo)
            {
                if (slot == slotType)
                    return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Get number of attachment slots
        /// </summary>
        public int GetAttachmentSlotCount()
        {
            return AttachmentSlots != null ? AttachmentSlots.Length : 0;
        }
        
        #endregion
        
        #region Editor
        
#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // Auto-generate ItemID from asset name if empty
            if (string.IsNullOrEmpty(ItemID))
            {
                ItemID = name;
            }
            
            // Ensure stackable items have valid stack size
            if (IsStackable && MaxStackSize < 1)
            {
                MaxStackSize = 1;
            }
            
            // Non-stackable items should have MaxStackSize = 1
            if (!IsStackable)
            {
                MaxStackSize = 1;
            }
            
            // DefaultResource cannot exceed MaxResource
            if (DefaultResource > MaxResource)
            {
                DefaultResource = MaxResource;
            }
            
            // UsageDuration cannot be negative
            if (UsageDuration < 0f)
            {
                UsageDuration = 0f;
            }
        }
#endif
        
        #endregion
    }
    #region Enums
   /// <summary>
    /// Defines item types for categorization
    /// Used for filtering and UI organization
    /// </summary>
    public enum ItemType
    {
        Equipment,      // Armor, clothing, backpack
        Weapon,         // Guns, melee weapons
        Consumable,     // Food, medkits, potions
        Attachment,     // Scopes, suppressors, lights
        Material,       // Crafting materials, resources
        Ammo,           // Ammunition (if separate from weapons)
        Throwable,      // Grenades, flashbangs
        Quest,          // Quest items
        Misc            // Other items
    }
    
    /// <summary>
    /// Defines where items can be placed
    /// Used for validation and UI layout
    /// </summary>
    public enum SlotLocationType
    {
        Inventory,      // Main inventory grid
        Equipment,      // Equipment slots (head, chest, etc.)
        Weapon,         // Weapon slots (primary, secondary, melee)
        QuickSlot,      // Quick access slots (1-4 hotkeys)
        Attachment      // Attachment slots on items
    }
    
    /// <summary>
    /// Defines equipment slot types
    /// Each type has specific icon and allowed item types
    /// </summary>
    public enum EquipmentSlotType
    {
        Head,       // Helmet, hat
        Face,       // Mask, goggles
        Chest,      // Body armor, vest
        Back,       // Backpack
        Belt,       // Belt with pouches
        Legs,       // Pants
        Feet,       // Boots
        Hands       // Gloves
    }
    
    /// <summary>
    /// Defines weapon slot types
    /// </summary>
    public enum WeaponSlotType
    {
        Primary,    // Main weapon (rifle, shotgun)
        Secondary,  // Sidearm (pistol)
        Melee,       // Melee weapon (knife, axe) - optional,
        None
    }
    
    /// <summary>
    /// Defines attachment slot types
    /// Items can have multiple attachment slots
    /// Attachments modify item stats
    /// </summary>
    public enum AttachmentSlotType
    {
        None,
        
        // Weapon Attachments
        Optic,          // Scopes, red dots, holographic sights
        Barrel,         // Suppressors, muzzle brakes, compensators
        Grip,           // Foregrips, vertical grips
        Magazine,       // Extended magazines
        Stock,          // Stocks, buttstocks
        UnderBarrel,    // Flashlights, lasers, grenade launchers
        
        // Equipment Attachments
        Light,          // Flashlights, headlamps (for helmet/vest)
        Pouch,          // Extra storage pouches (for vest/belt)
        Plate,          // Armor plates (for vest)
        
        // Generic Slots
        Accessory1,
        Accessory2,
        Accessory3
    }
    /// <summary>
    /// Defines primary resource type for items
    /// Determines what resource the item uses/tracks
    /// </summary>
    public enum ItemResourceType
    {
        /// <summary>
        /// No resource (e.g., clothing, backpack)
        /// Item doesn't degrade or consume anything
        /// </summary>
        None,
        
        /// <summary>
        /// Durability - reduces with use/damage
        /// When reaches 0, item becomes broken/unusable
        /// Used by: Armor, melee weapons, tools
        /// </summary>
        Durability,
        
        /// <summary>
        /// Ammo - consumed when shooting
        /// When reaches 0, weapon cannot fire
        /// Used by: Weapons
        /// Note: Weapon có separate magazine tracking
        /// </summary>
        Ammo,
        
        /// <summary>
        /// Energy/Battery - depletes over time when active
        /// When reaches 0, item stops functioning
        /// Used by: Flashlights, night vision goggles
        /// </summary>
        Energy,
        
        /// <summary>
        /// Fuel - consumed over time
        /// Used by: Vehicles, generators (future)
        /// </summary>
        Fuel,
        
        /// <summary>
        /// Charge - can be recharged
        /// Used by: Electronic devices (future)
        /// </summary>
        Charge
    }
    #endregion
}