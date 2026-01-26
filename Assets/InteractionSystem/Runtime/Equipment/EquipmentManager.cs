using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Items;
using UnityEngine;

namespace NightHunt.InteractionSystem.Equipment
{
    public class EquipmentManager : NetworkBehaviour
    {
        [Header("Equipment Slots")]
        private readonly SyncDictionary<EquipmentSlot, ItemInstance> equippedItems =
            new SyncDictionary<EquipmentSlot, ItemInstance>();

        [Header("Visual Controllers")] [SerializeField]
        private EquipmentVisualController visualController;

        [Header("Equipment Points")] [SerializeField]
        private Transform headPoint;

        [SerializeField] private Transform chestPoint;
        [SerializeField] private Transform backPoint;
        [SerializeField] private Transform primaryWeaponPoint;
        [SerializeField] private Transform secondaryWeaponPoint;
        [SerializeField] private Transform meleePoint;

        private Dictionary<EquipmentSlot, AttachmentManager> attachmentManagers =
            new Dictionary<EquipmentSlot, AttachmentManager>();

        public IReadOnlyDictionary<EquipmentSlot, ItemInstance> EquippedItems => equippedItems;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            equippedItems.OnChange += OnEquipmentChanged;
        }

        [Server]
        public bool TryEquip(ItemInstance item, EquipmentSlot slot)
        {
            // Validate item type
            ItemDataBase itemData = ItemDatabaseManager.Instance.GetItemData(item.itemDataId);
            if (itemData == null) return false;

            EquipmentDataBase equipData = itemData as EquipmentDataBase;
            if (equipData == null)
            {
                Debug.LogError($"Item {item.itemDataId} is not equipment");
                return false;
            }

            if (equipData.equipmentSlot != slot)
            {
                Debug.LogError($"Item {item.itemDataId} cannot be equipped in slot {slot}");
                return false;
            }

            // Unequip existing item if any
            if (equippedItems.ContainsKey(slot))
            {
                TryUnequip(slot);
            }

            // Equip new item
            equippedItems[slot] = item;

            OnItemEquipped(slot, item);
            return true;
        }

        [Server]
        public bool TryUnequip(EquipmentSlot slot)
        {
            if (!equippedItems.ContainsKey(slot))
            {
                return false;
            }

            ItemInstance item = equippedItems[slot];
            equippedItems.Remove(slot);

            OnItemUnequipped(slot, item);
            return true;
        }

        public ItemInstance? GetEquippedItem(EquipmentSlot slot)
        {
            if (equippedItems.TryGetValue(slot, out ItemInstance item))
            {
                return item;
            }

            return null;
        }

        public EquipmentDataBase GetEquippedItemData(EquipmentSlot slot)
        {
            ItemInstance? item = GetEquippedItem(slot);
            if (!item.HasValue) return null;

            return ItemDatabaseManager.Instance.GetItemData<EquipmentDataBase>(item.Value.itemDataId);
        }

        public AttachmentManager GetAttachmentManager(EquipmentSlot slot)
        {
            if (attachmentManagers.TryGetValue(slot, out AttachmentManager manager))
            {
                return manager;
            }

            return null;
        }

        [Server]
        private void OnItemEquipped(EquipmentSlot slot, ItemInstance item)
        {
            // Notify visual controller
            ObserversUpdateVisual(slot, item);

            // Initialize attachment manager
            InitializeAttachmentManager(slot, item);

            // Update player stats
            RecalculatePlayerStats();
        }

        [Server]
        private void OnItemUnequipped(EquipmentSlot slot, ItemInstance item)
        {
            // Clear visual
            ObserversClearVisual(slot);

            // Cleanup attachment manager
            CleanupAttachmentManager(slot);

            // Update player stats
            RecalculatePlayerStats();
        }

        private void InitializeAttachmentManager(EquipmentSlot slot, ItemInstance item)
        {
            EquipmentDataBase equipData = GetEquippedItemData(slot);
            if (equipData == null) return;

            Transform mountPoint = GetMountPoint(slot);
            if (mountPoint == null) return;

            // Create attachment manager
            GameObject managerObj = new GameObject($"AttachmentManager_{slot}");
            managerObj.transform.SetParent(mountPoint);
            managerObj.transform.localPosition = Vector3.zero;
            managerObj.transform.localRotation = Quaternion.identity;

            AttachmentManager manager = managerObj.AddComponent<AttachmentManager>();
            manager.Initialize(equipData, mountPoint);

            // Apply existing attachments from item instance
            if (item.attachments != null)
            {
                foreach (var attachmentInstance in item.attachments)
                {
                    AttachmentData attachmentData =
                        ItemDatabaseManager.Instance.GetItemData<AttachmentData>(attachmentInstance.attachmentDataId);
                    if (attachmentData != null)
                    {
                        manager.TryAttach(attachmentInstance.slotType, attachmentData);
                    }
                }
            }

            attachmentManagers[slot] = manager;
        }

        private void CleanupAttachmentManager(EquipmentSlot slot)
        {
            if (attachmentManagers.TryGetValue(slot, out AttachmentManager manager))
            {
                Destroy(manager.gameObject);
                attachmentManagers.Remove(slot);
            }
        }

        public Transform GetMountPoint(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Head => headPoint,
                EquipmentSlot.Chest => chestPoint,
                EquipmentSlot.Backpack => backPoint,
                EquipmentSlot.PrimaryWeapon => primaryWeaponPoint,
                EquipmentSlot.SecondaryWeapon => secondaryWeaponPoint,
                EquipmentSlot.Melee => meleePoint,
                _ => null
            };
        }

        [ObserversRpc]
        private void ObserversUpdateVisual(EquipmentSlot slot, ItemInstance item)
        {
            visualController?.UpdateEquipmentVisual(slot, item);
        }

        [ObserversRpc]
        private void ObserversClearVisual(EquipmentSlot slot)
        {
            visualController?.ClearEquipmentVisual(slot);
        }

        private void OnEquipmentChanged(SyncDictionaryOperation op, EquipmentSlot key, ItemInstance value,
            bool asServer)
        {
            // Client-side sync callback
            if (!asServer)
            {
                visualController?.UpdateEquipmentVisual(key, value);
            }
        }

        private void RecalculatePlayerStats()
        {
            // Aggregate stats from all equipped items + attachments
            List<StatModifier> allModifiers = new List<StatModifier>();

            foreach (var kvp in equippedItems)
            {
                EquipmentSlot slot = kvp.Key;

                // Get attachment manager for this slot
                if (attachmentManagers.TryGetValue(slot, out AttachmentManager manager))
                {
                    StatModifier[] finalStats = manager.GetFinalStats();
                    allModifiers.AddRange(finalStats);
                }
            }

            // Apply to player
            ApplyStatsToPlayer(allModifiers.ToArray());
        }

        private void ApplyStatsToPlayer(StatModifier[] modifiers)
        {
            // TODO: Apply to PlayerController, HealthComponent, etc.
            // Example:
            // PlayerController.SetMovementSpeed(modifiers.Where(m => m.statType == StatType.MovementSpeed).Sum(m => m.value));
        }
    }
}