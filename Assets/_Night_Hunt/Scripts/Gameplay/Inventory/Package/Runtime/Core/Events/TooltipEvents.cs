using System;
using UnityEngine;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Events
{
    /// <summary>
    /// Events for tooltip display.
    /// </summary>
    public static class TooltipEvents
    {
        public static event Action<ItemInstance, Vector3> OnShowTooltip;
        public static event Action<SlotLocationType, int, Vector3> OnShowSlotInfo;
        public static event Action OnHideTooltip;
        public static event Action<bool> OnTooltipHovered;
        
        public static void FireShowTooltip(ItemInstance item, Vector3 position) => OnShowTooltip?.Invoke(item, position);
        public static void FireShowSlotInfo(SlotLocationType slotType, int slotIndex, Vector3 position) => OnShowSlotInfo?.Invoke(slotType, slotIndex, position);
        public static void FireHideTooltip() => OnHideTooltip?.Invoke();
        public static void FireTooltipHovered(bool isHovering) => OnTooltipHovered?.Invoke(isHovering);
    }
}
