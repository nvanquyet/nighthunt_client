using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Detects when mouse hovers over tooltip to prevent hiding.
    /// </summary>
    public class TooltipHoverDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            TooltipEvents.FireTooltipHovered(true);
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipEvents.FireTooltipHovered(false);
        }
    }
}
