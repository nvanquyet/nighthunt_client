using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Data;
using NightHunt.InteractionSystem.Shop;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// UI component for shop item in Buy tab
    /// Displays item icon, name, price, and purchase button
    /// </summary>
    public class ShopItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI stockText;
        [SerializeField] private Button purchaseButton;

        private ShopItemData shopItemData;
        private ItemConfigData itemConfig;
        private ShopPanel shopPanel;

        /// <summary>
        /// Initialize shop item UI
        /// </summary>
        public void Initialize(ShopItemData itemData, ItemConfigData config, ShopPanel panel)
        {
            shopItemData = itemData;
            itemConfig = config;
            shopPanel = panel;

            UpdateUI();
        }

        /// <summary>
        /// Update UI display
        /// </summary>
        private void UpdateUI()
        {
            if (itemConfig != null)
            {
                // Item name
                if (itemNameText != null)
                {
                    itemNameText.text = itemConfig.DisplayName ?? shopItemData.itemId;
                }

                // Price
                if (priceText != null)
                {
                    priceText.text = $"${shopItemData.price:F2}";
                }

                // Stock
                if (stockText != null)
                {
                    if (shopItemData.stock < 0)
                    {
                        stockText.text = "Unlimited";
                    }
                    else
                    {
                        stockText.text = $"Stock: {shopItemData.currentStock}/{shopItemData.stock}";
                    }
                }

                // TODO: Load item icon from ItemDataRegistry or Resources
                // if (itemIcon != null) { ... }
            }

            // Purchase button
            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveAllListeners();
                purchaseButton.onClick.AddListener(OnPurchaseClicked);

                // Disable if out of stock
                bool canPurchase = shopItemData.stock < 0 || shopItemData.currentStock > 0;
                purchaseButton.interactable = canPurchase;
            }
        }

        /// <summary>
        /// Handle purchase button click
        /// </summary>
        private void OnPurchaseClicked()
        {
            if (shopPanel != null && shopItemData != null)
            {
                shopPanel.PurchaseItem(shopItemData.itemId);
            }
        }
    }
}
