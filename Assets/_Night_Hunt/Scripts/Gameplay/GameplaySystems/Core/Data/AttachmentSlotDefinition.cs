using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Attachment item definition
    /// Extends ItemDefinition with attachment-specific properties
    /// 
    /// Key Features:
    /// - Modifies parent item stats (weapon/armor)
    /// - Can be attached to specific slot types
    /// - Examples: Scopes, Grips, Suppressors, Flashlights, Pouches
    /// </summary>
    [CreateAssetMenu(fileName = "Attachment_", menuName = "GameplaySystems/Items/Attachment Definition")]
    public class AttachmentDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Attachment;
        
        #region Attachment Modifiers
        
        [Header("Item Stat Modifiers")]
        [Tooltip("Modifiers applied to parent item stats when attached")]
        public ItemStatModifier[] ItemModifiers;
        
        #endregion
        
        #region Helpers
        
        /// <summary>
        /// Get modifier for specific stat type
        /// </summary>
        public ItemStatModifier? GetModifier(ItemStatType statType)
        {
            if (ItemModifiers == null)
                return null;
            
            foreach (var mod in ItemModifiers)
            {
                if (mod.StatType == statType)
                    return mod;
            }
            
            return null;
        }
        
        /// <summary>
        /// Check if modifies specific stat
        /// </summary>
        public bool ModifiesStat(ItemStatType statType)
        {
            return GetModifier(statType).HasValue;
        }
        
        #endregion
        
        #region Validation
        
        public override bool IsValid(out string error)
        {
            if (!base.IsValid(out error))
                return false;
            
            // Attachments must be able to attach to something
            if (CanAttachTo == null || CanAttachTo.Length == 0)
            {
                error = "Attachment must specify CanAttachTo slot types";
                return false;
            }
            
            // Attachments should not be stackable
            if (IsStackable)
            {
                error = "Attachments should not be stackable";
                return false;
            }
            
            error = null;
            return true;
        }
        
        #endregion
        
        #region Editor Setup
        
#if UNITY_EDITOR
        [ContextMenu("Setup Red Dot Scope")]
        private void SetupRedDotScope()
        {
            DisplayName = "Red Dot Sight";
            Description = "Improves accuracy and aim speed at close-medium range";
            Weight = 0.2f;
            
            CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Optic };
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            
            ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreateFlat(ItemStatType.Accuracy, 10f, "Red dot improves accuracy"),
                ItemStatModifier.CreatePercentage(ItemStatType.AimSpeed, 20f, "Faster target acquisition"),
                ItemStatModifier.CreateFlat(ItemStatType.Recoil, -3f, "Better recoil control")
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[AttachmentDefinition] Setup Red Dot Scope complete!");
        }
        
        [ContextMenu("Setup Suppressor")]
        private void SetupSuppressor()
        {
            DisplayName = "Suppressor";
            Description = "Reduces weapon noise and recoil but decreases range";
            Weight = 0.4f;
            
            CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Barrel };
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            
            ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreatePercentage(ItemStatType.Recoil, -30f, "Suppressor reduces recoil"),
                ItemStatModifier.CreatePercentage(ItemStatType.Range, -10f, "Slightly reduced range"),
                ItemStatModifier.CreateFlat(ItemStatType.Damage, -2f, "Minor damage reduction")
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[AttachmentDefinition] Setup Suppressor complete!");
        }
        
        [ContextMenu("Setup Vertical Grip")]
        private void SetupVerticalGrip()
        {
            DisplayName = "Vertical Grip";
            Description = "Improves recoil control and stability";
            Weight = 0.15f;
            
            CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Grip };
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            
            ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreatePercentage(ItemStatType.Recoil, -25f, "Better recoil control"),
                ItemStatModifier.CreateFlat(ItemStatType.Accuracy, 5f, "Improved stability"),
                ItemStatModifier.CreatePercentage(ItemStatType.Spread, -15f, "Tighter bullet spread")
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[AttachmentDefinition] Setup Vertical Grip complete!");
        }
        
        [ContextMenu("Setup Extended Magazine")]
        private void SetupExtendedMagazine()
        {
            DisplayName = "Extended Magazine";
            Description = "Increases magazine capacity but adds weight";
            Weight = 0.3f;
            
            CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Magazine };
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            
            ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreatePercentage(ItemStatType.MagazineSize, 50f, "+50% magazine capacity"),
                ItemStatModifier.CreatePercentage(ItemStatType.ReloadSpeed, -10f, "Slower reload")
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[AttachmentDefinition] Setup Extended Magazine complete!");
        }
        
        [ContextMenu("Setup Tactical Flashlight")]
        private void SetupTacticalFlashlight()
        {
            DisplayName = "Tactical Flashlight";
            Description = "Provides illumination in dark areas";
            Weight = 0.1f;
            
            ResourceType = ItemResourceType.Energy;
            MaxResource = 3600f; // 1 hour
            DefaultResource = 3600f;
            
            CanAttachTo = new AttachmentSlotType[] 
            { 
                AttachmentSlotType.UnderBarrel,  // On weapons
                AttachmentSlotType.Light          // On helmets/vests
            };
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            
            ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreateFlat(ItemStatType.LightRange, 15f, "Illumination range"),
                ItemStatModifier.CreateFlat(ItemStatType.Brightness, 80f, "Light brightness")
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[AttachmentDefinition] Setup Tactical Flashlight complete!");
        }
        
        [ContextMenu("Setup Storage Pouch")]
        private void SetupStoragePouch()
        {
            DisplayName = "Storage Pouch";
            Description = "Increases weight capacity when attached to vest or belt";
            Weight = 0.2f;
            
            CanAttachTo = new AttachmentSlotType[] { AttachmentSlotType.Pouch };
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory };
            
            ItemModifiers = new ItemStatModifier[]
            {
                ItemStatModifier.CreateFlat(ItemStatType.WeightCapacityBonus, 5f, "Extra carrying capacity")
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[AttachmentDefinition] Setup Storage Pouch complete!");
        }
#endif
        
        #endregion
    }
}