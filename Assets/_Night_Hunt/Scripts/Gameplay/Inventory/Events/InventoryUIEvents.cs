using System;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.UI;

namespace NightHunt.Gameplay.Inventory.Events
{
    /// <summary>
    /// Events fired by UI Layer to request actions from Logic Layer
    /// Logic Layer subscribes to these events and processes requests
    /// </summary>
    public static class InventoryUIEvents
    {
        // #region agent log helpers
        private static void AgentLog(string location, string message, string runId, string hypothesisId, string dataJson)
        {
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_uievt\",\"timestamp\":{ts},\"location\":\"{location}\",\"message\":\"{message}\",\"data\":{dataJson},\"sessionId\":\"debug-session\",\"runId\":\"{runId}\",\"hypothesisId\":\"{hypothesisId}\"}}\n"
                );
            }
            catch { /* ignore */ }
        }
        // #endregion

        // Item movement requests
        public static event Action<string, int, int, int, int> OnMoveItemRequested; // itemId, fromX, fromY, toX, toY
        public static event Action<string, int, int> OnMoveItemToSlotRequested; // itemId, toX, toY
        public static event Action<string, int, int, string, int, int> OnSwapItemsRequested; // itemId1, x1, y1, itemId2, x2, y2

        // Item removal requests
        public static event Action<string, int> OnRemoveItemRequested; // itemId, quantity

        // Equipment requests
        public static event Action<string, EquipmentSlotType> OnEquipItemRequested; // itemId, slotType
        public static event Action<string, EquipmentSlotType> OnUnequipItemRequested; // itemId, slotType
        
        // Weapon requests
        public static event Action<string, int> OnEquipWeaponRequested; // weaponId, weaponSlotIndex

        // Attachment requests (attach/detach to/from equipment)
        public static event Action<string, EquipmentSlotType, string> OnAttachItemRequested; // attachmentItemId, equipmentSlotType, attachmentSlotType
        public static event Action<EquipmentSlotType, string> OnDetachItemRequested; // equipmentSlotType, attachmentSlotType

        // Quick slot requests
        public static event Action<string, int> OnAssignQuickSlotRequested; // itemId, slotIndex
        public static event Action<int> OnClearQuickSlotRequested; // slotIndex

        // Usage requests
        public static event Action<string> OnUseItemRequested; // itemId
        public static event Action<string> OnCancelUseItemRequested; // itemId

        // Container/Shop requests
        public static event Action<string> OnOpenContainerRequested; // containerId
        public static event Action<string> OnCloseContainerRequested; // containerId
        public static event Action<string, string, int, int> OnMoveItemToContainerRequested; // containerId, itemId, fromX, fromY
        public static event Action<string, string, int, int> OnMoveItemFromContainerRequested; // containerId, itemId, toX, toY

        // Shop requests
        public static event Action<string, string> OnPurchaseItemRequested; // shopId, itemId
        public static event Action<string, string> OnSellItemRequested; // shopId, itemId

        // Fire methods (called by UI Layer)
        public static void RequestMoveItem(string itemId, int fromX, int fromY, int toX, int toY)
        {
            // #region agent log
            AgentLog(
                "InventoryUIEvents.cs:RequestMoveItem",
                "UI fired RequestMoveItem",
                "run2",
                "H1",
                $"{{\"itemId\":\"{itemId}\",\"fromX\":{fromX},\"fromY\":{fromY},\"toX\":{toX},\"toY\":{toY}}}"
            );
            // #endregion
            OnMoveItemRequested?.Invoke(itemId, fromX, fromY, toX, toY);
        }

        public static void RequestMoveItemToSlot(string itemId, int toX, int toY)
        {
            // #region agent log
            AgentLog(
                "InventoryUIEvents.cs:RequestMoveItemToSlot",
                "UI fired RequestMoveItemToSlot",
                "run2",
                "H2",
                $"{{\"itemId\":\"{itemId}\",\"toX\":{toX},\"toY\":{toY}}}"
            );
            // #endregion
            OnMoveItemToSlotRequested?.Invoke(itemId, toX, toY);
        }

        public static void RequestSwapItems(string itemId1, int x1, int y1, string itemId2, int x2, int y2)
        {
            // #region agent log
            AgentLog(
                "InventoryUIEvents.cs:RequestSwapItems",
                "UI fired RequestSwapItems",
                "run2",
                "H2",
                $"{{\"itemId1\":\"{itemId1}\",\"x1\":{x1},\"y1\":{y1},\"itemId2\":\"{itemId2}\",\"x2\":{x2},\"y2\":{y2}}}"
            );
            // #endregion
            OnSwapItemsRequested?.Invoke(itemId1, x1, y1, itemId2, x2, y2);
        }

        public static void RequestRemoveItem(string itemId, int quantity)
        {
            // #region agent log
            AgentLog(
                "InventoryUIEvents.cs:RequestRemoveItem",
                "UI fired RequestRemoveItem",
                "run2",
                "H3",
                $"{{\"itemId\":\"{itemId}\",\"quantity\":{quantity}}}"
            );
            // #endregion
            OnRemoveItemRequested?.Invoke(itemId, quantity);
        }

        public static void RequestEquipItem(string itemId, EquipmentSlotType slotType)
        {
            // #region agent log
            AgentLog(
                "InventoryUIEvents.cs:RequestEquipItem",
                "UI fired RequestEquipItem",
                "run2",
                "H4",
                $"{{\"itemId\":\"{itemId}\",\"slotType\":\"{slotType}\"}}"
            );
            // #endregion
            OnEquipItemRequested?.Invoke(itemId, slotType);
        }

        public static void RequestUnequipItem(string itemId, EquipmentSlotType slotType)
        {
            // #region agent log
            AgentLog(
                "InventoryUIEvents.cs:RequestUnequipItem",
                "UI fired RequestUnequipItem",
                "run2",
                "H4",
                $"{{\"itemId\":\"{itemId}\",\"slotType\":\"{slotType}\"}}"
            );
            // #endregion
            OnUnequipItemRequested?.Invoke(itemId, slotType);
        }

        public static void RequestEquipWeapon(string weaponId, int weaponSlotIndex)
        {
            // #region agent log
            AgentLog(
                "InventoryUIEvents.cs:RequestEquipWeapon",
                "UI fired RequestEquipWeapon",
                "run2",
                "H4",
                $"{{\"weaponId\":\"{weaponId}\",\"weaponSlotIndex\":{weaponSlotIndex}}}"
            );
            // #endregion
            OnEquipWeaponRequested?.Invoke(weaponId, weaponSlotIndex);
        }
        public static void RequestAttachItem(string attachmentItemId, EquipmentSlotType equipmentSlotType, string attachmentSlotType) => OnAttachItemRequested?.Invoke(attachmentItemId, equipmentSlotType, attachmentSlotType);
        public static void RequestDetachItem(EquipmentSlotType equipmentSlotType, string attachmentSlotType) => OnDetachItemRequested?.Invoke(equipmentSlotType, attachmentSlotType);
        public static void RequestAssignQuickSlot(string itemId, int slotIndex)
        {
            // #region agent log
            AgentLog(
                "InventoryUIEvents.cs:RequestAssignQuickSlot",
                "UI fired RequestAssignQuickSlot",
                "run2",
                "H4",
                $"{{\"itemId\":\"{itemId}\",\"slotIndex\":{slotIndex}}}"
            );
            // #endregion
            OnAssignQuickSlotRequested?.Invoke(itemId, slotIndex);
        }
        public static void RequestClearQuickSlot(int slotIndex) => OnClearQuickSlotRequested?.Invoke(slotIndex);
        public static void RequestUseItem(string itemId) => OnUseItemRequested?.Invoke(itemId);
        public static void RequestCancelUseItem(string itemId) => OnCancelUseItemRequested?.Invoke(itemId);
        public static void RequestOpenContainer(string containerId) => OnOpenContainerRequested?.Invoke(containerId);
        public static void RequestCloseContainer(string containerId) => OnCloseContainerRequested?.Invoke(containerId);
        public static void RequestMoveItemToContainer(string containerId, string itemId, int fromX, int fromY)
        {
            // #region agent log
            AgentLog(
                "InventoryUIEvents.cs:RequestMoveItemToContainer",
                "UI fired RequestMoveItemToContainer",
                "run2",
                "H1",
                $"{{\"containerId\":\"{containerId}\",\"itemId\":\"{itemId}\",\"fromX\":{fromX},\"fromY\":{fromY}}}"
            );
            // #endregion
            OnMoveItemToContainerRequested?.Invoke(containerId, itemId, fromX, fromY);
        }

        public static void RequestMoveItemFromContainer(string containerId, string itemId, int toX, int toY)
        {
            // #region agent log
            AgentLog(
                "InventoryUIEvents.cs:RequestMoveItemFromContainer",
                "UI fired RequestMoveItemFromContainer",
                "run2",
                "H1",
                $"{{\"containerId\":\"{containerId}\",\"itemId\":\"{itemId}\",\"toX\":{toX},\"toY\":{toY}}}"
            );
            // #endregion
            OnMoveItemFromContainerRequested?.Invoke(containerId, itemId, toX, toY);
        }
        public static void RequestPurchaseItem(string shopId, string itemId) => OnPurchaseItemRequested?.Invoke(shopId, itemId);
        public static void RequestSellItem(string shopId, string itemId) => OnSellItemRequested?.Invoke(shopId, itemId);
    }
}
