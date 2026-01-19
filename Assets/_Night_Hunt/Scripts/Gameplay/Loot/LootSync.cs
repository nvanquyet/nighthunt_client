using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace NightHunt.Gameplay.Loot
{
    /// <summary>
    /// Network sync for loot items
    /// </summary>
    public class LootSync : NetworkBehaviour
    {
        private readonly SyncVar<bool> networkIsLooted = new SyncVar<bool>();

        private LootItem lootItem;

        private void Awake()
        {
            lootItem = GetComponent<LootItem>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            networkIsLooted.OnChange += OnLootedChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (networkIsLooted != null)
                networkIsLooted.OnChange -= OnLootedChanged;
        }

        /// <summary>
        /// Server: Mark loot as looted
        /// </summary>
        [Server]
        public void SetLooted(bool looted)
        {
            networkIsLooted.Value = looted;
        }

        private void OnLootedChanged(bool oldLooted, bool newLooted, bool asServer)
        {
            if (!asServer && lootItem != null)
            {
                lootItem.SetLooted(newLooted);
            }
        }
    }
}

