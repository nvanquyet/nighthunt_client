using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Loot;
using UnityEngine;

namespace NightHunt.InteractionSystem.Combat
{
    public class PlayerHealthComponent : HealthComponentBase
    {
        [Header("Player Health")] [SerializeField]
        private float baseHealth = 100f;

        [SerializeField] private float healthRegenRate = 5f; // HP per second
        [SerializeField] private float regenDelay = 5f; // Seconds after damage

        [Header("Armor")] [SerializeField] private ArmorComponent armorComponent;

        [Header("Death")] [SerializeField] private GameObject corpsePrefab;
        [SerializeField] private float respawnDelay = 10f;

        private float lastDamageTime;
        private bool isRegenerating;

        public override void OnStartServer()
        {
            maxHealth = baseHealth;
            base.OnStartServer();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (IsDead) return;

            // Health regeneration
            if (Time.time - lastDamageTime > regenDelay)
            {
                if (!isRegenerating)
                {
                    isRegenerating = true;
                }

                if (currentHealth < maxHealth)
                {
                    Heal(healthRegenRate * Time.deltaTime);
                }
            }
        }

        protected override float CalculateDamage(DamagePayload damage)
        {
            float finalDamage = damage.rawDamage;

            // Apply armor reduction
            if (armorComponent != null)
            {
                finalDamage = armorComponent.CalculateDamageReduction(finalDamage, damage.damageType);
            }

            // Headshot multiplier
            if (damage.isHeadshot)
            {
                finalDamage *= 2f;
            }

            // Critical hit
            if (damage.isCritical)
            {
                finalDamage *= 1.5f;
            }

            return finalDamage;
        }

        protected override void OnDamageTaken(DamagePayload damage, float finalDamage)
        {
            lastDamageTime = Time.time;
            isRegenerating = false;

            // Notify UI
            ObserversShowDamage(finalDamage, damage.isHeadshot);

            // Damage visual effects
            if (damage.isHeadshot)
            {
                ObserversPlayHeadshotEffect();
            }
        }

        protected override void OnDeath(NetworkConnection killer)
        {
            // Spawn corpse with inventory
            SpawnPlayerCorpse();

            // Clear inventory
            var inventory = GetComponent<GridInventoryComponent>();
            if (inventory != null)
            {
                inventory.ClearInventory();
            }

            // Notify clients
            ObserversOnPlayerDeath();

            // Schedule respawn
            Invoke(nameof(RespawnPlayer), respawnDelay);
        }

        [Server]
        private void SpawnPlayerCorpse()
        {
            GameObject corpseObj = Instantiate(corpsePrefab, transform.position, transform.rotation);
            ServerManager.Spawn(corpseObj);

            PlayerCorpseLoot corpse = corpseObj.GetComponent<PlayerCorpseLoot>();

            var inventory = GetComponent<GridInventoryComponent>();
            string playerName = "Player"; // TODO: Get actual player name

            corpse.InitializeCorpse(playerName, inventory.Items.ToArray());
        }

        [Server]
        private void RespawnPlayer()
        {
            // Restore health
            currentHealth = maxHealth;

            // TODO: Move to spawn point
            // TODO: Give default items

            ObserversOnPlayerRespawn();
        }

        [ObserversRpc]
        private void ObserversShowDamage(float damage, bool isHeadshot)
        {
            // TODO: Show damage number UI
            Debug.Log($"Took {damage} damage" + (isHeadshot ? " (HEADSHOT)" : ""));
        }

        [ObserversRpc]
        private void ObserversPlayHeadshotEffect()
        {
            // TODO: Play headshot visual/sound effect
        }

        [ObserversRpc]
        private void ObserversOnPlayerDeath()
        {
            // TODO: Play death animation
            // TODO: Disable player controls
        }

        [ObserversRpc]
        private void ObserversOnPlayerRespawn()
        {
            // TODO: Play respawn effect
            // TODO: Enable player controls
        }
    }
}