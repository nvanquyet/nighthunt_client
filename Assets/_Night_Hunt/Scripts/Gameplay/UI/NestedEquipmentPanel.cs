using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Structs;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Nested Equipment Panel - Shows attachment slots for equipped items
    /// FIXED VERSION with proper slot generation and tooltip support
    /// </summary>
    public class NestedEquipmentPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform slotsContainer;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private GameObject slotPrefab;

        [Header("Settings")]
        [SerializeField] private string titleFormat = "{0} Attachments";

        private InventoryPanel inventoryPanel;
        private InventorySlot currentItemSlot;
        private List<ItemCell> nestedSlotUIs = new List<ItemCell>();
        private EquipmentDataBase currentEquipmentData;

        public void Initialize(InventoryPanel panel)
        {
            inventoryPanel = panel;
            Hide();
        }

        /// <summary>
        /// Show panel for item with attachments
        /// </summary>
        public void ShowForItem(InventorySlot itemSlot, bool isEquipped = false)
        {
            if (itemSlot == null || itemSlot.IsEmpty)
            {
                Hide();
                return;
            }

            currentItemSlot = itemSlot;
            var item = itemSlot.Item;
            if (item == null || item.ItemData == null)
            {
                Hide();
                return;
            }

            // Check if item is EquipmentDataBase (has attachment slots)
            if (item.ItemData is EquipmentDataBase equipmentData)
            {
                currentEquipmentData = equipmentData;
                
                if (equipmentData.AttachmentSlots == null || equipmentData.AttachmentSlots.Length == 0)
                {
                    // No attachment slots - hide panel
                    Hide();
                    return;
                }

                // Show panel and create slots
                if (panelRoot != null)
                {
                    panelRoot.SetActive(true);
                }
                
                CreateNestedSlots(equipmentData, itemSlot);
            }
            else
            {
                // Not an equipment item - hide
                Hide();
            }
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
            ClearSlots();
            currentItemSlot = null;
            currentEquipmentData = null;
        }

        public bool IsShowing()
        {
            return panelRoot != null && panelRoot.activeSelf;
        }

        /// <summary>
        /// Create attachment slots from EquipmentDataBase
        /// </summary>
        private void CreateNestedSlots(EquipmentDataBase equipmentData, InventorySlot parentItemSlot)
        {
            ClearSlots();
            
            if (equipmentData.AttachmentSlots == null) return;
            
            // Create slot for EACH attachment slot definition
            foreach (var slotDef in equipmentData.AttachmentSlots)
            {
                GameObject slotObj = Instantiate(slotPrefab, slotsContainer);
                slotObj.SetActive(true);
                
                ItemCell slotUI = slotObj.GetComponent<ItemCell>();
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<ItemCell>();
                }
                
                // Create empty slot
                InventorySlot nestedSlot = new InventorySlot();
                
                // TODO: Check if parent item has this slot filled
                // Load attached item if exists
                
                // Initialize as attachment slot
                slotUI.Initialize(
                    nestedSlot, 
                    inventoryPanel, 
                    ItemCellLocation.Attachment, 
                    nestedSlotUIs.Count
                );
                
                // Store attachment slot type for validation
                slotUI.SetAttachmentSlotType(slotDef.slotType);
                
                nestedSlotUIs.Add(slotUI);
            }
        }

        /// <summary>
        /// Set slot label text (shows attachment slot name)
        /// </summary>
        private void SetSlotLabel(GameObject slotObj, AttachmentSlotDefinition slotDef)
        {
            // Find label text in slot prefab
            var labels = slotObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (labels != null && labels.Length > 0)
            {
                // Use first text component as label
                var label = labels[0];
                label.text = slotDef.displayName ?? slotDef.slotType.ToString();
            }
        }

        private void ClearSlots()
        {
            foreach (var slotUI in nestedSlotUIs)
            {
                if (slotUI != null)
                {
                    Destroy(slotUI.gameObject);
                }
            }
            nestedSlotUIs.Clear();
        }

        /// <summary>
        /// Get all attachment slot UIs
        /// </summary>
        public List<ItemCell> GetAttachmentSlots()
        {
            return nestedSlotUIs;
        }

        /// <summary>
        /// Get attachment slot by index
        /// </summary>
        public ItemCell GetAttachmentSlot(int index)
        {
            if (index >= 0 && index < nestedSlotUIs.Count)
            {
                return nestedSlotUIs[index];
            }
            return null;
        }

        /// <summary>
        /// Get attachment slot by type
        /// </summary>
        public ItemCell GetAttachmentSlotByType(AttachmentSlotType slotType)
        {
            foreach (var slotUI in nestedSlotUIs)
            {
                if (slotUI != null && slotUI.GetAttachmentSlotType() == slotType)
                {
                    return slotUI;
                }
            }
            return null;
        }
    }
}