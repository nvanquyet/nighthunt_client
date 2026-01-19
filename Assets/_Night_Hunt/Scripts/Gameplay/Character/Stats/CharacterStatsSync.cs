using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using NightHunt.Gameplay.Core.Networking;

namespace NightHunt.Gameplay.Character.Stats
{
    /// <summary>
    /// Network sync component for character stats
    /// Syncs critical stats (HP, Stamina) via SyncVar
    /// </summary>
    public class CharacterStatsSync : NetworkBehaviour
    {
        private CharacterStats characterStats;

        // Synchronized variables for critical stats
        private readonly SyncVar<float> networkHP = new SyncVar<float>();
        private readonly SyncVar<float> networkStamina = new SyncVar<float>();

        private void Awake()
        {
            characterStats = GetComponent<CharacterStats>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Subscribe to sync var changes
            networkHP.OnChange += OnHPChanged;
            networkStamina.OnChange += OnStaminaChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            // Unsubscribe from sync var changes
            if (networkHP != null)
                networkHP.OnChange -= OnHPChanged;
            if (networkStamina != null)
                networkStamina.OnChange -= OnStaminaChanged;
        }

        private void Update()
        {
            if (!IsServer) return;

            // Server: Sync stats to clients
            if (characterStats != null)
            {
                networkHP.Value = characterStats.GetHP();
                networkStamina.Value = characterStats.GetStamina();
            }
        }

        /// <summary>
        /// Server: Set HP
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SetHP(float hp)
        {
            networkHP.Value = hp;
            if (characterStats != null)
            {
                characterStats.SetHP(hp);
            }
        }

        /// <summary>
        /// Server: Set Stamina
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SetStamina(float stamina)
        {
            networkStamina.Value = stamina;
            if (characterStats != null)
            {
                characterStats.SetStamina(stamina);
            }
        }

        private void OnHPChanged(float oldHP, float newHP, bool asServer)
        {
            if (!asServer && characterStats != null)
            {
                // Client: Apply server HP
                characterStats.SetHP(newHP);
            }
        }

        private void OnStaminaChanged(float oldStamina, float newStamina, bool asServer)
        {
            if (!asServer && characterStats != null)
            {
                // Client: Apply server Stamina
                characterStats.SetStamina(newStamina);
            }
        }

        /// <summary>
        /// Get network HP
        /// </summary>
        public float GetNetworkHP() => networkHP.Value;

        /// <summary>
        /// Get network Stamina
        /// </summary>
        public float GetNetworkStamina() => networkStamina.Value;
    }
}

