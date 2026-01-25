using System.Collections.Generic;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.InteractionSystem.Loot
{
    public class LootContainerUI : MonoBehaviour
    {
        public static LootContainerUI Instance { get; private set; }

        [Header("UI References")] [SerializeField]
        private GameObject uiPanel;

        [SerializeField] private Transform containerGrid;
        [SerializeField] private Transform playerInventoryGrid;
        [SerializeField] private LootItemSlotUI slotPrefab;
        [SerializeField] private Button closeButton;

        [Header("Settings")] [SerializeField] private int containerColumns = 5;
        [SerializeField] private int inventoryColumns = 6;

        private ContainerInteractable currentContainer;
        private List<LootItemSlotUI> containerSlots = new List<LootItemSlotUI>();
        private List<LootItemSlotUI> inventorySlots = new List<LootItemSlotUI>();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            closeButton.onClick.AddListener(Close);
            uiPanel.SetActive(false);
        }

        public void Open(ContainerInteractable container, ItemInstance[] items)
        {
            currentContainer = container;

            // Clear old slots
            ClearSlots();

            // Create container slots
            CreateContainerSlots(items);

            // Create inventory slots (get from player inventory)
            CreateInventorySlots();

            uiPanel.SetActive(true);

            // Lock cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            if (currentContainer != null)
            {
                // Notify server player closed UI
                // currentContainer.OnPlayerClosedUI(LocalConnection);
            }

            currentContainer = null;
            uiPanel.SetActive(false);

            // Unlock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void UpdateItems(ItemInstance[] items)
        {
            // Update container grid
            for (int i = 0; i < containerSlots.Count; i++)
            {
                if (i < items.Length)
                {
                    containerSlots[i].SetItem(items[i]);
                }
                else
                {
                    containerSlots[i].Clear();
                }
            }
        }

        private void CreateContainerSlots(ItemInstance[] items)
        {
            for (int i = 0; i < 20; i++) // Max container slots
            {
                LootItemSlotUI slot = Instantiate(slotPrefab, containerGrid);
                slot.Initialize(LootItemSlotUI.SlotType.Container, i);

                if (i < items.Length)
                {
                    slot.SetItem(items[i]);
                }

                containerSlots.Add(slot);
            }
        }

        private void CreateInventorySlots()
        {
            // TODO: Get player inventory items
            ItemInstance[] playerItems = new ItemInstance[0];

            for (int i = 0; i < 30; i++) // Player inventory size
            {
                LootItemSlotUI slot = Instantiate(slotPrefab, playerInventoryGrid);
                slot.Initialize(LootItemSlotUI.SlotType.PlayerInventory, i);

                if (i < playerItems.Length)
                {
                    slot.SetItem(playerItems[i]);
                }

                inventorySlots.Add(slot);
            }
        }

        private void ClearSlots()
        {
            foreach (var slot in containerSlots)
            {
                Destroy(slot.gameObject);
            }

            containerSlots.Clear();

            foreach (var slot in inventorySlots)
            {
                Destroy(slot.gameObject);
            }

            inventorySlots.Clear();
        }
    }
}