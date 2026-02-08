using UnityEngine;

namespace NightHunt.Inventory.Core.Config
{
    /// <summary>
    /// Global inventory system configuration.
    /// Create via: Assets > Create > NightHunt > Config > Inventory Config
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryConfig", menuName = "NightHunt/Config/Inventory Config")]
    public class InventoryConfig : ScriptableObject
    {
        [Header("Weight System")]
        [Tooltip("Enable weight-based inventory limits")]
        public bool EnableWeightSystem = true;
        
        [Tooltip("Default max weight capacity (kg) - can be modified by equipment")]
        public float DefaultWeightCapacity = 50f;
        
        [Tooltip("Movement speed penalty when overweight (0-1 multiplier)")]
        [Range(0f, 1f)]
        public float OverweightSpeedPenalty = 0.5f;
        
        [Tooltip("Can player pickup items when overweight?")]
        public bool AllowPickupWhenOverweight = false;
        
        [Header("Stacking")]
        [Tooltip("Auto-merge stackable items when picking up")]
        public bool AutoMergeStacks = true;
        
        [Tooltip("Max stack size override (0 = use item definition)")]
        public int GlobalMaxStackSize = 0;
        
        [Header("Durability")]
        [Tooltip("Items break when durability reaches 0")]
        public bool ItemsBreakAtZeroDurability = false;
        
        [Tooltip("Can broken items be repaired?")]
        public bool AllowRepair = true;
        
        [Header("Drop Behavior")]
        [Tooltip("Distance from player to spawn dropped items (meters)")]
        public float DropDistance = 1.5f;
        
        [Tooltip("Force applied to dropped items")]
        public float DropForce = 2f;
        
        [Tooltip("Dropped items despawn after X seconds (0 = never)")]
        public float DroppedItemDespawnTime = 300f; // 5 minutes
        
        [Header("Network Optimization")]
        [Tooltip("Batch inventory updates to reduce RPC calls")]
        public bool EnableBatchedUpdates = true;
        
        [Tooltip("Max updates per batch")]
        [Range(1, 20)]
        public int MaxBatchSize = 10;
        
        [Tooltip("Batch send interval (seconds)")]
        [Range(0.05f, 1f)]
        public float BatchInterval = 0.1f;
        
        [Header("Validation")]
        [Tooltip("Enable server-side validation for all operations")]
        public bool EnableServerValidation = true;
        
        [Tooltip("Log validation failures for debugging")]
        public bool LogValidationFailures = true;
        
        [Header("References")]
        [Tooltip("Slot layout configuration")]
        public SlotLayoutConfig SlotLayout;
    }
}