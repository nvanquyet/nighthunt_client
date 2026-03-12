using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.Gameplay.Core.State;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Loot;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Handles dropping corpse with all player items when player dies
    /// Server-only: Spawns corpse NetworkObject with all items
    /// </summary>
    public class CorpseDropOnDeath : NetworkBehaviour
    {
        [Header("References")] [SerializeField]
        private CharacterLifecycleController lifecycleController;

        [SerializeField] private MonoBehaviour inventorySystemComponent;
        [SerializeField] private MonoBehaviour equipmentSystemComponent;
        [SerializeField] private MonoBehaviour quickSlotSystemComponent;
        [SerializeField] private MonoBehaviour attachmentSystemComponent;

        [Header("Prefab")] [Tooltip("Corpse prefab with WorldCorpse component")] [SerializeField]
        private GameObject corpsePrefab;

        private IInventorySystem inventorySystem;
        private IEquipmentSystem equipmentSystem;
        private IQuickSlotSystem quickSlotSystem;
        private IAttachmentSystem attachmentSystem;

        private void Awake()
        {
            if (lifecycleController == null)
                lifecycleController = ComponentResolver.Find<CharacterLifecycleController>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] CharacterLifecycleController not found")
                    .Resolve();

            // Get systems
            if (inventorySystemComponent != null)
                inventorySystem = inventorySystemComponent as IInventorySystem;

            if (equipmentSystemComponent != null)
                equipmentSystem = equipmentSystemComponent as IEquipmentSystem;

            if (quickSlotSystemComponent != null)
                quickSlotSystem = quickSlotSystemComponent as IQuickSlotSystem;

            if (attachmentSystemComponent != null)
                attachmentSystem = attachmentSystemComponent as IAttachmentSystem;

#if UNITY_EDITOR
            // Auto-find if not assigned
            if (inventorySystem == null)
            {
                inventorySystem = ComponentResolver.Find<IInventorySystem>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] IInventorySystem not found")
                    .Resolve();
                if (inventorySystem != null)
                    inventorySystemComponent = inventorySystem as MonoBehaviour;
            }

            if (equipmentSystem == null)
            {
                equipmentSystem = ComponentResolver.Find<IEquipmentSystem>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] IEquipmentSystem not found")
                    .Resolve();
                if (equipmentSystem != null)
                    equipmentSystemComponent = equipmentSystem as MonoBehaviour;
            }

            if (quickSlotSystem == null)
            {
                quickSlotSystem = ComponentResolver.Find<IQuickSlotSystem>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] IQuickSlotSystem not found")
                    .Resolve();
                if (quickSlotSystem != null)
                    quickSlotSystemComponent = quickSlotSystem as MonoBehaviour;
            }

            if (attachmentSystem == null)
            {
                attachmentSystem = ComponentResolver.Find<IAttachmentSystem>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] IAttachmentSystem not found")
                    .Resolve();
                if (attachmentSystem != null)
                    attachmentSystemComponent = attachmentSystem as MonoBehaviour;
            }
#endif
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (lifecycleController != null)
                lifecycleController.OnDied += HandleDeath;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            if (lifecycleController != null)
                lifecycleController.OnDied -= HandleDeath;
        }

        /// <summary>
        /// SERVER: Khi player chết
        /// </summary>
        [Server]
        private void HandleDeath()
        {
            if (!IsServerInitialized)
                return;

            // 1. Collect all items (unequip + detach)
            var allItems = CollectAllItems();

            // 2. Clear player systems
            ClearPlayerSystems();

            // 3. Spawn corpse với loot storage
            SpawnCorpse(allItems);
        }

        /// <summary>
        /// SERVER: Collect all items from player (inventory + equipment + quickslot)
        /// </summary>
        [Server]
        private List<ItemInstanceData> CollectAllItems()
        {
            var items = new List<ItemInstanceData>();

            // 1. Equipment items (unequip trước)
            if (equipmentSystem != null)
            {
                var equipped = equipmentSystem.GetAllEquippedItems();
                foreach (var kvp in equipped)
                {
                    // Unequip (trả về inventory tạm)
                    equipmentSystem.UnequipItem(kvp.Key);

                    // Detach attachments
                    if (attachmentSystem != null && kvp.Value != null)
                    {
                        attachmentSystem.DetachAllFromItem(kvp.Value.InstanceID);
                    }

                    // Refresh item reference sau khi unequip
                    var refreshedItem = inventorySystem?.GetItemByInstanceID(kvp.Value?.InstanceID);
                    if (refreshedItem != null)
                    {
                        items.Add(refreshedItem.ToData());
                    }
                }
            }

            // 2. Inventory items (sau khi equipment đã unequip)
            if (inventorySystem != null)
            {
                foreach (var item in inventorySystem.GetAllItems())
                {
                    items.Add(item.ToData());
                }
            }

            // 3. QuickSlot items (clear trước)
            if (quickSlotSystem != null)
            {
                var slots = quickSlotSystem.GetAllQuickSlots();
                foreach (var item in slots)
                {
                    if (item != null)
                    {
                        items.Add(item.ToData());
                    }
                }

                quickSlotSystem.ClearAllQuickSlots();
            }

            return items;
        }

        /// <summary>
        /// SERVER: Clear player systems sau khi đã collect items
        /// </summary>
        [Server]
        private void ClearPlayerSystems()
        {
            // Clear inventory (sau khi đã collect)
            if (inventorySystem != null)
                inventorySystem.ClearInventory();
        }

        /// <summary>
        /// SERVER: Spawn corpse với items
        /// </summary>
        [Server]
        private void SpawnCorpse(List<ItemInstanceData> items)
        {
            if (corpsePrefab == null)
            {
                Debug.LogWarning("[CorpseDropOnDeath] Corpse prefab is null!");
                return;
            }

            Vector3 deathPos = transform.position;
            Quaternion rot = transform.rotation;

            var go = Instantiate(corpsePrefab, deathPos, rot);
            var netObj = ComponentResolver.Find<NetworkObject>(go)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkObject not found")
                .Resolve();
            if (netObj == null)
            {
                Debug.LogError("[CorpseDropOnDeath] Corpse prefab must have NetworkObject component!");
                Destroy(go);
                return;
            }

            var corpse = ComponentResolver.Find<WorldCorpse>(go)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] WorldCorpse not found")
                .Resolve();
            if (corpse == null)
            {
                Debug.LogError("[CorpseDropOnDeath] Corpse prefab must have WorldCorpse component!");
                Destroy(go);
                return;
            }

            // Network spawn (tất cả clients thấy)
            ServerManager.Spawn(netObj);

            // Initialize với items
            corpse.Initialize(items);
        }
    }
}