using System;
using UnityEngine;

namespace NightHunt.Inventory.Domain.QuickSlot
{
    /// <summary>
    /// Configuration for quick slots.
    /// </summary>
    [CreateAssetMenu(fileName = "QuickSlotConfig", menuName = "NightHunt/Inventory/QuickSlot Config")]
    public class QuickSlotConfig : ScriptableObject
    {
        [Header("Slot Settings")]
        [Tooltip("Number of quick slots available")]
        public int SlotCount = 4;
        
        [Tooltip("Key bindings for each slot")]
        public QuickSlotBinding[] Bindings;
        
        [Serializable]
        public class QuickSlotBinding
        {
            [Tooltip("Slot index (0-3)")]
            public int SlotIndex;
            
            [Tooltip("Input action name from InputActions asset")]
            public string HotkeyAction;
            
            [Tooltip("Display text for UI (e.g., 'Ctrl+1')")]
            public string DisplayKey = "Ctrl+1";
        }
    }
}