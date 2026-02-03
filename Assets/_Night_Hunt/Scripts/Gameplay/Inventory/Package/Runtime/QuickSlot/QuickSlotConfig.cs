using System;
using UnityEngine;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.QuickSlot
{
    /// <summary>
    /// Configuration for quickslot system.
    /// </summary>
    [CreateAssetMenu(fileName = "QuickSlotConfig", menuName = "Inventory/QuickSlotConfig")]
    public class QuickSlotConfig : ScriptableObject
    {
        public int SlotCount = 4; // Default: 4 slots
        
        public QuickSlotBinding[] Bindings;
        
        [Serializable]
        public class QuickSlotBinding
        {
            public int SlotIndex;           // 0-3
            public string HotkeyAction;     // "QuickSlot1", "QuickSlot2"... (from InputActions)
            public string DisplayKey = "Ctrl+1"; // For UI display
        }
    }
}
