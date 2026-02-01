using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Inventory.Events;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Shop;
using NightHunt.Data;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Shop panel with 2 tabs: Buy (list of items with prices) and Items (purchased items container)
    /// Similar to LootContainerPanel but with purchase/sell functionality
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        [Header("Shop Info")]
        [SerializeField] private TextMeshProUGUI shopTitleText;
        [SerializeField] private TextMeshProUGUI playerMoneyText;

        [Header("Tabs")]
        [SerializeField] private Button buyTabButton;
        [SerializeField] private Button itemsTabButton;
        [SerializeField] private GameObject buyTabPanel;
        [SerializeField] private GameObject itemsTabPanel;

        [Header("Buy Tab")]
        [SerializeField] private Transform buyItemsParent;
        [SerializeField] private GameObject buyItemPrefab;

        [Header("Items Tab")]
        [SerializeField] private Transform itemsGridParent;
        [SerializeField] private GameObject itemSlotPrefab;
        [SerializeField] private GridLayoutGroup itemsGridLayout;

        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot; // Panel UI GameObject (separate from script GameObject)

        private InventoryPanel inventoryPanel;
        private List<ShopItemUI> buyItemUIs = new List<ShopItemUI>();
        private List<ItemCell> purchasedItemSlots = new List<ItemCell>();
        private string currentShopId;
        private ShopContainer currentShop;
        private bool isInitialized = false;
        private ShopTab currentTab = ShopTab.Buy;

        private enum ShopTab
        {
            Buy,
            Items
        }

        /// <summary>
        /// Initialize shop panel
        /// </summary>
        public void Initialize(InventoryPanel panel)
        {
            inventoryPanel = panel;
            isInitialized = true;
            Hide();
        }

        /// <summary>
        /// Load shop and display
        /// </summary>
        public void LoadShop(string shopId, ShopContainer shop)
        {
            if (string.IsNullOrEmpty(shopId) || shop == null)
            {
                Debug.LogWarning("[ShopPanel] LoadShop: shopId or shop is null");
                return;
            }

            currentShopId = shopId;
            currentShop = shop;
            SetShopTitle(shop.GetDisplayName());

            // Setup tabs
            if (buyTabButton != null)
            {
                buyTabButton.onClick.RemoveAllListeners();
                buyTabButton.onClick.AddListener(() => SwitchTab(ShopTab.Buy));
            }

            if (itemsTabButton != null)
            {
                itemsTabButton.onClick.RemoveAllListeners();
                itemsTabButton.onClick.AddListener(() => SwitchTab(ShopTab.Items));
            }

            // Load buy tab items
            RefreshBuyTab();

            // Load items tab (purchased items container)
            RefreshItemsTab();

            // Show buy tab by default
            SwitchTab(ShopTab.Buy);
        }

        /// <summary>
        /// Switch between Buy and Items tabs
        /// </summary>
        private void SwitchTab(ShopTab tab)
        {
            currentTab = tab;

            if (buyTabPanel != null)
            {
                buyTabPanel.SetActive(tab == ShopTab.Buy);
            }

            if (itemsTabPanel != null)
            {
                itemsTabPanel.SetActive(tab == ShopTab.Items);
            }

            // Update tab button visuals
            if (buyTabButton != null)
            {
                var colors = buyTabButton.colors;
                colors.normalColor = tab == ShopTab.Buy ? Color.white : Color.gray;
                buyTabButton.colors = colors;
            }

            if (itemsTabButton != null)
            {
                var colors = itemsTabButton.colors;
                colors.normalColor = tab == ShopTab.Items ? Color.white : Color.gray;
                itemsTabButton.colors = colors;
            }
        }

        /// <summary>
        /// Refresh buy tab - display items available for purchase
        /// </summary>
        private void RefreshBuyTab()
        {
            ClearBuyItems();

            if (currentShop == null || buyItemsParent == null || buyItemPrefab == null)
                return;

            var availableItems = currentShop.GetAvailableItems();
            foreach (var shopItem in availableItems)
            {
                if (shopItem == null || string.IsNullOrEmpty(shopItem.itemId))
                    continue;

                // Get item config for price and display name
                var itemConfig = GetItemConfig(shopItem.itemId);
                if (itemConfig == null)
                    continue;

                GameObject itemObj = Instantiate(buyItemPrefab, buyItemsParent);
                itemObj.SetActive(true);

                ShopItemUI shopItemUI = itemObj.GetComponent<ShopItemUI>();
                if (shopItemUI == null)
                {
                    shopItemUI = itemObj.AddComponent<ShopItemUI>();
                }

                shopItemUI.Initialize(shopItem, itemConfig, this);
                buyItemUIs.Add(shopItemUI);
            }
        }

        /// <summary>
        /// Refresh items tab - display purchased items (similar to LootContainerPanel)
        /// </summary>
        private void RefreshItemsTab()
        {
            ClearItemsTab();

            // TODO: Get purchased items from shop container
            // For now, create empty grid
            if (itemsGridParent == null || itemSlotPrefab == null)
                return;

            // Create empty slots for purchased items container
            int slotCount = 12; // TODO: Get from shop
            for (int i = 0; i < slotCount; i++)
            {
                GameObject slotObj = Instantiate(itemSlotPrefab, itemsGridParent);
                slotObj.SetActive(true);
                ItemCell slotUI = slotObj.GetComponent<ItemCell>();
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<ItemCell>();
                }

                // Use negative coordinates to distinguish from inventory slots
                var emptySlot = new InventorySlot(); // Empty slot (IsEmpty is read-only, defaults to true)
                slotUI.Initialize(emptySlot, inventoryPanel, ItemCellLocation.Inventory, i);
                purchasedItemSlots.Add(slotUI);
            }
        }

        /// <summary>
        /// Clear buy items
        /// </summary>
        private void ClearBuyItems()
        {
            foreach (var itemUI in buyItemUIs)
            {
                if (itemUI != null)
                {
                    Destroy(itemUI.gameObject);
                }
            }
            buyItemUIs.Clear();
        }

        /// <summary>
        /// Clear items tab
        /// </summary>
        private void ClearItemsTab()
        {
            foreach (var slot in purchasedItemSlots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            purchasedItemSlots.Clear();
        }

        /// <summary>
        /// Set shop title
        /// </summary>
        public void SetShopTitle(string title)
        {
            if (shopTitleText != null)
            {
                shopTitleText.text = title ?? "Shop";
            }
        }

        /// <summary>
        /// Update player money display
        /// </summary>
        public void UpdatePlayerMoney(float money)
        {
            if (playerMoneyText != null)
            {
                playerMoneyText.text = $"Money: ${money:F2}";
            }
        }

        /// <summary>
        /// Show shop panel
        /// </summary>
        public void Show()
        {
            Debug.Log($"[ShopPanel] ===== Show() called =====");
            
            // Show panel UI GameObject (script GameObject remains active)
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                Debug.Log($"[ShopPanel] panelRoot.SetActive(true) called");
            }
            else
            {
                Debug.LogWarning($"[ShopPanel] panelRoot is null! Cannot show panel UI.");
            }
        }

        /// <summary>
        /// Hide shop panel
        /// </summary>
        public void Hide()
        {
            Debug.Log($"[ShopPanel] ===== Hide() called =====");
            
            // Hide panel UI GameObject (script GameObject remains active)
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
                Debug.Log($"[ShopPanel] panelRoot.SetActive(false) called");
            }
            else
            {
                Debug.LogWarning($"[ShopPanel] panelRoot is null! Cannot hide panel UI.");
            }
        }

        /// <summary>
        /// Clear shop data
        /// </summary>
        public void ClearShop()
        {
            currentShopId = null;
            currentShop = null;
            ClearBuyItems();
            ClearItemsTab();

            if (shopTitleText != null)
            {
                shopTitleText.text = "";
            }
        }

        /// <summary>
        /// Purchase item from shop
        /// </summary>
        public void PurchaseItem(string itemId)
        {
            if (string.IsNullOrEmpty(currentShopId) || string.IsNullOrEmpty(itemId))
                return;

            // Fire UI event - Logic layer will handle it
            InventoryUIEvents.RequestPurchaseItem(currentShopId, itemId);
        }

        /// <summary>
        /// Sell item to shop
        /// </summary>
        public void SellItem(string itemId)
        {
            if (string.IsNullOrEmpty(currentShopId) || string.IsNullOrEmpty(itemId))
                return;

            // Fire UI event - Logic layer will handle it
            InventoryUIEvents.RequestSellItem(currentShopId, itemId);
        }

        /// <summary>
        /// Get item config from registry
        /// </summary>
        private ItemConfigData GetItemConfig(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            // TODO: Implement ItemConfigData system using ScriptableObject if needed
            // For now, return null - ItemDataBase should be used directly instead
            return null;
        }

        /// <summary>
        /// Check if shop is loaded
        /// </summary>
        public bool IsShopLoaded()
        {
            return !string.IsNullOrEmpty(currentShopId) && currentShop != null;
        }

        /// <summary>
        /// Get current shop ID
        /// </summary>
        public string GetShopId()
        {
            return currentShopId;
        }
    }

}
