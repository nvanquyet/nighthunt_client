using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using TMPro;
using UnityEngine;

namespace NightHunt.InteractionSystem.Loot
{
    public class PlayerCorpseLoot : NetworkBehaviour
    {
        [Header("Corpse Settings")] [SerializeField]
        private float despawnTime = 300f; // 5 minutes

        [SerializeField] private LootContainer lootContainer;

        [Header("Visual")] [SerializeField] private GameObject corpseMesh;
        [SerializeField] private TextMeshPro playerNameText;

        private float spawnTime;

        public override void OnStartServer()
        {
            base.OnStartServer();
            spawnTime = Time.time;
        }

        [Server]
        public void InitializeCorpse(string playerName, ItemInstance[] playerItems)
        {
            // Set visual
            ObserversSetCorpseName(playerName);

            // Transfer items to container
            lootContainer.Clear();
            foreach (var item in playerItems)
            {
                lootContainer.TryAddItem(item);
            }

            // Start despawn timer
            Invoke(nameof(DespawnCorpse), despawnTime);
        }

        [ObserversRpc]
        private void ObserversSetCorpseName(string playerName)
        {
            if (playerNameText != null)
            {
                playerNameText.text = $"{playerName}'s Corpse";
            }
        }

        [Server]
        private void DespawnCorpse()
        {
            // Check if empty
            if (lootContainer.ContainerItems.Count == 0)
            {
                ServerManager.Despawn(gameObject);
            }
            else
            {
                // Extend timer if still has items
                Invoke(nameof(DespawnCorpse), 60f);
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            // Auto-despawn if looted empty
            if (lootContainer.ContainerItems.Count == 0)
            {
                float elapsed = Time.time - spawnTime;
                if (elapsed > 10f) // 10 seconds grace period
                {
                    CancelInvoke(nameof(DespawnCorpse));
                    ServerManager.Despawn(gameObject);
                }
            }
        }
    }
}