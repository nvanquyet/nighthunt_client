using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Loot
{
    public class LootContainer : NetworkBehaviour
    {
        [Header("Container Settings")] [SerializeField]
        private int maxSlots = 20;

        [SerializeField] private ContainerType containerType = ContainerType.Chest;
        [SerializeField] private bool isLocked;

        private readonly SyncList<ItemInstance> containerItems = new SyncList<ItemInstance>();

        public IReadOnlyList<ItemInstance> ContainerItems => containerItems;
        public int MaxSlots => maxSlots;
        public bool IsLocked => isLocked;
        public ContainerType Type => containerType;

        public enum ContainerType
        {
            Chest,
            Crate,
            Barrel,
            PlayerCorpse,
            Vehicle
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Generate loot on spawn (if not player corpse)
            if (containerType != ContainerType.PlayerCorpse)
            {
                GenerateRandomLoot();
            }
        }

        [Server]
        public void GenerateRandomLoot()
        {
            // TODO: Implement loot table system
            // For now, add some test items

            containerItems.Clear();

            int itemCount = UnityEngine.Random.Range(3, 8);
            for (int i = 0; i < itemCount; i++)
            {
                ItemInstance item = new ItemInstance
                {
                    instanceId = Guid.NewGuid().ToString(),
                    itemDataId = "test_item_" + i,
                    quantity = UnityEngine.Random.Range(1, 5),
                    durability = 100f
                };

                containerItems.Add(item);
            }
        }

        public bool CanLoot(NetworkConnection player)
        {
            if (isLocked) return false;
            // TODO: Add more validation (distance, etc)
            return true;
        }

        [Server]
        public bool TryAddItem(ItemInstance item)
        {
            if (containerItems.Count >= maxSlots) return false;

            containerItems.Add(item);
            return true;
        }

        [Server]
        public bool TryRemoveItem(string instanceId)
        {
            int index = containerItems.FindIndex(i => i.instanceId == instanceId);
            if (index < 0) return false;

            containerItems.RemoveAt(index);
            return true;
        }

        [Server]
        public ItemInstance? GetItem(string instanceId)
        {
            return containerItems.FirstOrDefault(i => i.instanceId == instanceId);
        }

        [Server]
        public void SetLocked(bool locked)
        {
            isLocked = locked;
        }

        [Server]
        public void Clear()
        {
            containerItems.Clear();
        }
    }
}