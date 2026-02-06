using UnityEngine;

namespace NightHunt.Inventory.Core.Data
{
    /// <summary>
    /// ScriptableObject configuration for UI slot state visuals.
    /// Reusable across all inventory panels for consistent visual feedback.
    /// </summary>
    [CreateAssetMenu(fileName = "UISlotStateConfig", menuName = "NightHunt/Inventory/UISlotStateConfig")]
    public class UISlotStateConfig : ScriptableObject
    {
        [Header("Empty State")]
        [Tooltip("Color for empty slots")]
        public Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        
        [Header("Occupied State")]
        [Tooltip("Color for slots with items")]
        public Color occupiedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        
        [Header("Hover State")]
        [Tooltip("Color when mouse hovers over slot")]
        public Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        
        [Header("Selected State")]
        [Tooltip("Color when slot is selected")]
        public Color selectedColor = new Color(0.5f, 0.7f, 0.5f, 1f);
        
        [Header("Unselected State")]
        [Tooltip("Color when slot transitions from selected to unselected")]
        public Color unselectedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        
        /// <summary>
        /// Gets the color for a specific state.
        /// </summary>
        /// <param name="state">The UI slot state</param>
        /// <returns>The color for the state</returns>
        public Color GetColorForState(Core.Enums.UISlotState state)
        {
            return state switch
            {
                Core.Enums.UISlotState.Empty => emptyColor,
                Core.Enums.UISlotState.Occupied => occupiedColor,
                Core.Enums.UISlotState.Hover => hoverColor,
                Core.Enums.UISlotState.Selected => selectedColor,
                Core.Enums.UISlotState.Unselected => unselectedColor,
                _ => occupiedColor
            };
        }
    }
}
