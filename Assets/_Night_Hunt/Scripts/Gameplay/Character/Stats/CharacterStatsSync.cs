using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Character.Stats;
using NightHunt.Inventory.Stats;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using UnityEngine.Serialization;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Network sync for character stats.
    /// Syncs ALL final calculated stats, not just HP/Stamina.
    /// </summary>
    public class CharacterStatsSync : NetworkBehaviour
    {
        [FormerlySerializedAs("characterStats")]
        [Header("References")]
        [SerializeField] private PlayerStats playerStats;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // SyncVars for all stats
        private readonly SyncVar<float> networkHP = new SyncVar<float>();
        private readonly SyncVar<float> networkStamina = new SyncVar<float>();
        private readonly SyncVar<float> networkMoveSpeed = new SyncVar<float>();
        private readonly SyncVar<float> networkWeightCapacity = new SyncVar<float>();
        private readonly SyncVar<float> networkVisionRadius = new SyncVar<float>();
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            // Subscribe to SyncVar changes
            networkHP.OnChange += (prev, next, asServer) => OnStatChanged("HP", prev, next, asServer);
            networkStamina.OnChange += (prev, next, asServer) => OnStatChanged("Stamina", prev, next, asServer);
            networkMoveSpeed.OnChange += (prev, next, asServer) => OnStatChanged("MoveSpeed", prev, next, asServer);
            networkWeightCapacity.OnChange += (prev, next, asServer) => OnStatChanged("WeightCapacity", prev, next, asServer);
            networkVisionRadius.OnChange += (prev, next, asServer) => OnStatChanged("VisionRadius", prev, next, asServer);
            
            // If server, subscribe to stat changes
            if (IsServer)
            {
                CharacterStatsEvents.OnStatsChanged += OnLocalStatsChanged;
            }
        }
        
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            if (IsServer)
            {
                CharacterStatsEvents.OnStatsChanged -= OnLocalStatsChanged;
            }
        }
        
        #region Server: Sync Stats
        
        void OnLocalStatsChanged()
        {
            if (!IsServer) return;
            
            if (playerStats == null) return;
            
            // Update all SyncVars
            networkHP.Value = playerStats.GetCurrentHealth();
            networkStamina.Value = playerStats.GetCurrentStamina();
            networkMoveSpeed.Value = playerStats.GetMoveSpeed();
            networkWeightCapacity.Value = playerStats.GetWeightCapacity();
            networkVisionRadius.Value = playerStats.GetVisionRadius();
            
            Log($"[SERVER] Synced all stats");
        }
        
        #endregion
        
        #region Client: Apply Synced Stats
        
        void OnStatChanged(string statName, float prev, float next, bool asServer)
        {
            if (asServer) return;
            
            Log($"[CLIENT] Stat changed: {statName} {prev:F2} → {next:F2}");
            
            // Fire event for UI update
            CharacterStatsEvents.InvokeStatsChanged();
        }
        
        #endregion
        
        #region Public API (for UI/other systems to read synced values)
        
        public float GetNetworkHP() => networkHP.Value;
        public float GetNetworkStamina() => networkStamina.Value;
        public float GetNetworkMoveSpeed() => networkMoveSpeed.Value;
        public float GetNetworkWeightCapacity() => networkWeightCapacity.Value;
        public float GetNetworkVisionRadius() => networkVisionRadius.Value;
        #endregion
        
        void Log(string msg)
        {
            if (enableDebugLogs)
                Debug.Log($"[CharacterStatsSync] {msg}");
        }
    }
}