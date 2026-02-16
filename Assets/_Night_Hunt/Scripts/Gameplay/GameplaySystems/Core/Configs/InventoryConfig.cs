using GameplaySystems.Core.Data;
using UnityEngine;
using GameplaySystems.Inventory;

namespace GameplaySystems.Core.Configs
{
    /// <summary>
    /// Configuration for inventory system
    /// Defines slot counts, icons, and behavior settings
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryConfig", menuName = "GameplaySystems/Config/Inventory Config")]
    public class InventoryConfig : ScriptableObject
    {
        #region Slot Counts
        
        [Header("Slot Counts")]
        [Tooltip("Số lượng QuickSlots (hotkey 1-4)")]
        [Range(1, 10)]
        public int QuickSlotCount = 4;
        
        [Tooltip("Số lượng Weapon slots")]
        [Range(1, 4)]
        public int WeaponSlotCount = 3; // Primary, Secondary, Melee
        
        #endregion
        
        #region Equipment Slots
        
        [Header("Equipment Slots")]
        [Tooltip("Định nghĩa các equipment slots")]
        public EquipmentSlotConfig[] EquipmentSlots;
        
        #endregion
        
        #region Weapon Slots
        
        [Header("Weapon Slots")]
        [Tooltip("Định nghĩa các weapon slots")]
        public WeaponSlotConfig[] WeaponSlots;
        
        #endregion
        
        #region UI Icons
        
        [Header("UI Icons - Inventory")]
        [Tooltip("Icon mặc định cho inventory slot trống")]
        public Sprite DefaultInventoryEmptyIcon;
        
        [Header("UI Icons - QuickSlot")]
        [Tooltip("Icon mặc định cho quickslot trống")]
        public Sprite DefaultQuickSlotEmptyIcon;
        
        #endregion
        
        #region Behavior Settings
        
        [Header("Behavior Settings")]
        [Tooltip("Tự động stack items khi add vào inventory")]
        public bool AutoStackOnAdd = true;
        
        [Tooltip("Tự động merge stacks khi move/drag")]
        public bool AutoMergeOnMove = true;
        
        [Tooltip("Số empty slots mặc định spawn thêm trong UI")]
        [Range(10, 50)]
        public int DefaultExtraEmptySlots = 20;
        
        [Tooltip("Số empty slots tối thiểu luôn hiển thị")]
        [Range(5, 30)]
        public int MinimumEmptySlots = 10;
        
        #endregion
        
        #region Drop Settings
        
        [Header("Drop Settings")]
        [Tooltip("Khoảng cách drop từ player (meters)")]
        [Min(0.5f)]
        public float DropDistance = 2f;
        
        [Tooltip("Drop force")]
        [Min(0f)]
        public float DropForce = 5f;
        
        #endregion
        
        #region QuickSlot Settings
        
        [Header("QuickSlot Settings")]
        [Tooltip("Item types được phép trong QuickSlot")]
        public ItemType[] AllowedQuickSlotTypes = new ItemType[]
        {
            ItemType.Consumable,
            ItemType.Throwable
        };
        
        #endregion
        
        #region Helpers
        
        /// <summary>
        /// Get equipment slot config by type
        /// </summary>
        public EquipmentSlotConfig GetEquipmentSlot(EquipmentSlotType type)
        {
            if (EquipmentSlots == null)
                return default;
            
            foreach (var slot in EquipmentSlots)
            {
                if (slot.SlotType == type)
                    return slot;
            }
            
            return default;
        }
        
        /// <summary>
        /// Get weapon slot config by type
        /// </summary>
        public WeaponSlotConfig GetWeaponSlot(WeaponSlotType type)
        {
            if (WeaponSlots == null)
                return default;
            
            foreach (var slot in WeaponSlots)
            {
                if (slot.SlotType == type)
                    return slot;
            }
            
            return default;
        }
        
        /// <summary>
        /// Check if item type allowed in QuickSlot
        /// </summary>
        public bool IsAllowedInQuickSlot(ItemType type)
        {
            if (AllowedQuickSlotTypes == null)
                return false;
            
            foreach (var allowed in AllowedQuickSlotTypes)
            {
                if (allowed == type)
                    return true;
            }
            
            return false;
        }
        
        #endregion
        
        #region Editor Setup
        
#if UNITY_EDITOR
        [ContextMenu("Setup Default Equipment Slots")]
        private void SetupDefaultEquipmentSlots()
        {
            EquipmentSlots = new EquipmentSlotConfig[]
            {
                new EquipmentSlotConfig
                {
                    SlotType = EquipmentSlotType.Head,
                    DisplayName = "Head",
                    EmptySlotIcon = null, // Assign manually
                    UIPosition = new Vector2(1, 0)
                },
                new EquipmentSlotConfig
                {
                    SlotType = EquipmentSlotType.Face,
                    DisplayName = "Face",
                    EmptySlotIcon = null,
                    UIPosition = new Vector2(1, 1)
                },
                new EquipmentSlotConfig
                {
                    SlotType = EquipmentSlotType.Chest,
                    DisplayName = "Chest",
                    EmptySlotIcon = null,
                    UIPosition = new Vector2(1, 2)
                },
                new EquipmentSlotConfig
                {
                    SlotType = EquipmentSlotType.Back,
                    DisplayName = "Back",
                    EmptySlotIcon = null,
                    UIPosition = new Vector2(0, 0)
                },
                new EquipmentSlotConfig
                {
                    SlotType = EquipmentSlotType.Belt,
                    DisplayName = "Belt",
                    EmptySlotIcon = null,
                    UIPosition = new Vector2(1, 3)
                },
                new EquipmentSlotConfig
                {
                    SlotType = EquipmentSlotType.Legs,
                    DisplayName = "Legs",
                    EmptySlotIcon = null,
                    UIPosition = new Vector2(1, 4)
                },
                new EquipmentSlotConfig
                {
                    SlotType = EquipmentSlotType.Feet,
                    DisplayName = "Feet",
                    EmptySlotIcon = null,
                    UIPosition = new Vector2(1, 5)
                },
                new EquipmentSlotConfig
                {
                    SlotType = EquipmentSlotType.Hands,
                    DisplayName = "Hands",
                    EmptySlotIcon = null,
                    UIPosition = new Vector2(2, 2)
                }
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[InventoryConfig] Setup default equipment slots complete!");
        }
        
        [ContextMenu("Setup Default Weapon Slots")]
        private void SetupDefaultWeaponSlots()
        {
            WeaponSlots = new WeaponSlotConfig[]
            {
                new WeaponSlotConfig
                {
                    SlotType = WeaponSlotType.Primary,
                    DisplayName = "Primary",
                    EmptySlotIcon = null, // Assign rifle icon
                    Hotkey = "1"
                },
                new WeaponSlotConfig
                {
                    SlotType = WeaponSlotType.Secondary,
                    DisplayName = "Secondary",
                    EmptySlotIcon = null, // Assign pistol icon
                    Hotkey = "2"
                },
                new WeaponSlotConfig
                {
                    SlotType = WeaponSlotType.Melee,
                    DisplayName = "Melee",
                    EmptySlotIcon = null, // Assign knife icon
                    Hotkey = "3"
                }
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[InventoryConfig] Setup default weapon slots complete!");
        }
#endif
        
        #endregion
    }
    
    #region Supporting Types
    
    [System.Serializable]
    public struct EquipmentSlotConfig
    {
        [Tooltip("Loại equipment slot")]
        public EquipmentSlotType SlotType;
        
        [Tooltip("Tên hiển thị")]
        public string DisplayName;
        
        [Tooltip("Icon cho slot trống")]
        public Sprite EmptySlotIcon;
        
        [Tooltip("Vị trí trong UI (để layout)")]
        public Vector2 UIPosition;
    }
    
    [System.Serializable]
    public struct WeaponSlotConfig
    {
        [Tooltip("Loại weapon slot")]
        public WeaponSlotType SlotType;
        
        [Tooltip("Tên hiển thị")]
        public string DisplayName;
        
        [Tooltip("Icon cho slot trống")]
        public Sprite EmptySlotIcon;
        
        [Tooltip("Hotkey (e.g., '1', '2', '3')")]
        public string Hotkey;
    }
    
    #endregion
}