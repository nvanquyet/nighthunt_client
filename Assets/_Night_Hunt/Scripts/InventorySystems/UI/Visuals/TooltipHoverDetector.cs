using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Inventory.Core.Events;

namespace NightHunt.Inventory.UI.Visuals
{
    /// <summary>
    /// Detects when mouse hovers over tooltip.
    /// Prevents tooltip from hiding when cursor moves from cell to tooltip.
    /// </summary>
    public class TooltipHoverDetector : MonoBehaviour, 
        IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            TooltipEvents.InvokeTooltipHovered(true);
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipEvents.InvokeTooltipHovered(false);
        }
    }
}