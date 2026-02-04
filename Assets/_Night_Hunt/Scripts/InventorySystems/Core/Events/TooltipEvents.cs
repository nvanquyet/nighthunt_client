using System;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using UnityEngine;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Events for tooltip system.
    /// </summary>
    public static class TooltipEvents
    {
        public static event Action<ItemInstance, Vector3> OnShowTooltip; // item, position
        public static event Action<SlotLocationType, int, Vector3> OnShowSlotInfo; // slotType, index, position
        public static event Action OnHideTooltip;
        public static event Action<bool> OnTooltipHovered; // isHovering
        
        public static void InvokeShowTooltip(ItemInstance item, Vector3 position) => OnShowTooltip?.Invoke(item, position);
        public static void InvokeShowSlotInfo(SlotLocationType slotType, int index, Vector3 position) => OnShowSlotInfo?.Invoke(slotType, index, position);
        public static void InvokeHideTooltip() => OnHideTooltip?.Invoke();
        public static void InvokeTooltipHovered(bool isHovering) => OnTooltipHovered?.Invoke(isHovering);
    }
}