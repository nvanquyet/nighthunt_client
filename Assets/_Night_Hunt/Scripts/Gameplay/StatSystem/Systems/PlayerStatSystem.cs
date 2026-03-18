using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.Gameplay.StatSystem.Systems
{
    /// <summary>
    /// Player stat management system - NetworkBehaviour
    /// 
    /// RESPONSIBILITIES:
    /// - Manage all player stats (health, armor, weight, etc.)
    /// - Apply stat modifiers from equipment/items
    /// - Calculate final stat values (base + modifiers)
    /// - Sync stat values to clients via SyncList
    /// 
    /// NETWORK ARCHITECTURE:
    /// - Server-authoritative: All calculations on server
    /// - Client receives final values via SyncList<StatData>
    /// - Events fire on both server and clients after sync
    /// 
    /// USAGE:
    /// - Access via: player.GetComponent<IPlayerStatSystem>()
    /// - Add modifiers: AddModifier(PlayerStatType.Armor, StatModifier.CreateFlat("vest_01", 50))
    /// - Get stats: float armor = GetStat(PlayerStatType.Armor)
    /// </summary>
    public class PlayerStatSystem : NetworkBehaviour, IPlayerStatSystem, IDisposable
    {
        #region Serialized Fields
        
        [Header("Configuration")]
        [SerializeField] private PlayerStatConfig _statConfig;
        [SerializeField] private GameplayConfig _gameplayConfig;
        [SerializeField] private NightHuntDebugConfig _debugConfig;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugUI = false;
        
        #endregion
        
        #region Network Synced Data
        
        /// <summary>
        /// Network-synced stat data
        /// Automatically syncs from server to all clients
        /// Contains final calculated values only
        /// </summary>
        private readonly SyncList<StatData> _syncedStats = new SyncList<StatData>();
        
        #endregion
        
        #region Local Data (Server Only)
        
        /// <summary>
        /// Local cache for fast stat lookups (all clients)
        /// Rebuilt when sync data changes
        /// </summary>
        private Dictionary<PlayerStatType, StatData> _statCache = new Dictionary<PlayerStatType, StatData>();

        /// <summary>
        /// True once _statCache has been populated (server: after InitializeStatsServer; client: after first sync).
        /// Suppresses per-frame warning noise during the brief window before data arrives.
        /// </summary>
        private bool _isCacheReady;
        
        /// <summary>
        /// Active modifiers for each stat type (server only)
        /// Not synced - only final values are synced via _syncedStats
        /// </summary>
        private Dictionary<PlayerStatType, List<StatModifier>> _modifiers = new Dictionary<PlayerStatType, List<StatModifier>>();
        
        /// <summary>
        /// Dirty flags for stats that need recalculation (server only)
        /// Processed each frame to batch calculations
        /// </summary>
        private HashSet<PlayerStatType> _dirtyStats = new HashSet<PlayerStatType>();
        
        /// <summary>
        /// Last weight percent for detecting overweight changes
        /// </summary>
        private float _lastWeightPercent = 0f;
        
        #endregion
        
        #region Events
        
        public event Action<PlayerStatType, float, float> OnStatChanged;
        public event Action<float, float> OnWeightChanged;
        public event Action<float> OnOverweightChanged;
        
        #endregion
        
        #region NetworkBehaviour Lifecycle
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            // Subscribe to sync events
            _syncedStats.OnChange += OnStatSyncChanged;
            
            if (IsServerInitialized)
            {
                InitializeStatsServer();
            }
            else
            {
                // Client: Build cache from synced data
                RebuildStatCache();
            }
        }
        
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            // Unsubscribe from sync events
            _syncedStats.OnChange -= OnStatSyncChanged;
        }
        
        private void Update()
        {
            // Server: Process dirty stats each frame (batched)
            if (IsServerInitialized && _dirtyStats.Count > 0)
            {
                ProcessDirtyStats();
            }
        }
        
        #endregion
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            // Unsubscribe from all events
            _syncedStats.OnChange -= OnStatSyncChanged;
            
            // Clear collections
            _statCache.Clear();
            _modifiers.Clear();
            _dirtyStats.Clear();
        }
        
        #endregion
        
        #region Initialization (Server Only)
        
        [Server]
        private void InitializeStatsServer()
        {
            if (_statConfig == null)
            {
                Debug.LogError("[PlayerStatSystem] PlayerStatConfig is null! Please assign in Inspector.");
                return;
            }
            
            if (_gameplayConfig == null)
            {
                Debug.LogError("[PlayerStatSystem] GameplayConfig is null! Please assign in Inspector.");
                return;
            }
            
            // Initialize all stats from config
            foreach (var statDef in _statConfig.Stats)
            {
                var statData = StatData.Create(
                    statDef.Type,
                    statDef.DefaultValue,
                    statDef.MinValue,
                    statDef.MaxValue
                );
                
                _syncedStats.Add(statData);
                _statCache[statDef.Type] = statData;
                _modifiers[statDef.Type] = new List<StatModifier>();
            }
            
            _isCacheReady = true;

            if (_debugConfig != null && _debugConfig.EnableStatDebugLogs)
            {
                Debug.Log($"[PlayerStatSystem] Initialized {_syncedStats.Count} stats for player");
            }
        }
        
        #endregion
        
        #region IPlayerStatSystem - Getters
        
        public float GetStat(PlayerStatType type)
        {
            if (_statCache.TryGetValue(type, out var stat))
                return stat.CurrentValue;

            // Suppress warning while cache hasn't been populated yet (client waiting for first sync).
            if (_isCacheReady)
                Debug.LogWarning($"[PlayerStatSystem] Stat not found: {type}");
            return 0f;
        }
        
        public float GetBaseStat(PlayerStatType type)
        {
            if (_statCache.TryGetValue(type, out var stat))
                return stat.BaseValue;

            if (_isCacheReady)
                Debug.LogWarning($"[PlayerStatSystem] Stat not found: {type}");
            return 0f;
        }
        
        public float GetStatModifier(PlayerStatType type)
        {
            return GetStat(type) - GetBaseStat(type);
        }
        
        public Dictionary<PlayerStatType, float> GetAllStats()
        {
            var result = new Dictionary<PlayerStatType, float>();
            foreach (var kvp in _statCache)
            {
                result[kvp.Key] = kvp.Value.CurrentValue;
            }
            return result;
        }

        /// <summary>
        /// Directly set the current value of a stat (server only)
        /// 
        /// PARAMETERS:
        /// - type: Stat type to set
        /// - value: New value to set
        /// 
        /// RETURNS:
        /// - None
        /// 
        /// NETWORK:
        /// - Server-only operation
        /// - Used by ItemUseSystem for consumable effects (heal, stamina restore, etc.)
        /// - Value is clamped to [MinValue, MaxValue] defined in PlayerStatConfig
        /// </summary>
        public void SetCurrentStat(PlayerStatType type, float value)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[PlayerStatSystem] SetCurrentStat can only be called on server!");
                return;
            }

            if (!_statCache.TryGetValue(type, out var stat))
            {
                Debug.LogWarning($"[PlayerStatSystem] SetCurrentStat: stat not found: {type}");
                return;
            }

            // Dynamic ceiling for IsCurrentValue stats
            float clampMax = stat.MaxValue;
            var relatedMax = _statConfig.GetRelatedMaxStat(type);
            if (relatedMax.HasValue && _statCache.TryGetValue(relatedMax.Value, out var maxStat))
            {
                clampMax = maxStat.CurrentValue;
            }

            float clamped  = Mathf.Clamp(value, stat.MinValue, clampMax);
            float oldValue = stat.CurrentValue;

            if (Mathf.Abs(clamped - oldValue) <= 0.001f) return;

            stat.CurrentValue = clamped;

            int index = FindStatIndex(type);
            if (index >= 0) _syncedStats[index] = stat;

            _statCache[type] = stat;
            OnStatChanged?.Invoke(type, oldValue, clamped);

            if (type == PlayerStatType.CurrentWeight || type == PlayerStatType.WeightCapacity)
            {
                OnWeightChanged?.Invoke(GetCurrentWeight(), GetWeightCapacity());
                CheckOverweightStatus();
            }

            if (_debugConfig != null && _debugConfig.EnableStatDebugLogs)
                Debug.Log($"[PlayerStatSystem] SetCurrentStat {type}: {oldValue:F2} → {clamped:F2}");
        }
        
        #endregion
        
        #region IPlayerStatSystem - Weight System
        
        public float GetCurrentWeight()
        {
            return GetStat(PlayerStatType.CurrentWeight);
        }
        
        public float GetWeightCapacity()
        {
            return GetStat(PlayerStatType.WeightCapacity);
        }
        
        public float GetWeightPercent()
        {
            float capacity = GetWeightCapacity();
            if (capacity <= 0.001f)
                return 0f;
            
            return GetCurrentWeight() / capacity;
        }
        
        public bool CanCarryWeight(float additionalWeight)
        {
            // Note: This is informational only
            // Inventory can still exceed capacity (just with penalties)
            float totalWeight = GetCurrentWeight() + additionalWeight;
            float capacity = GetWeightCapacity();
            
            return totalWeight <= capacity;
        }
        
        public float GetMovementSpeedMultiplier()
        {
            if (_gameplayConfig == null)
            {
                Debug.LogWarning("[PlayerStatSystem] GameplayConfig is null, returning 1.0");
                return 1f;
            }
            
            float weightPercent = GetWeightPercent();
            return _gameplayConfig.CalculateMovementSpeedMultiplier(weightPercent);
        }
        
        #endregion
        
        #region IPlayerStatSystem - Modifier Management (Server Only)
        
        public void AddModifier(PlayerStatType type, StatModifier modifier)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[PlayerStatSystem] AddModifier can only be called on server!");
                return;
            }
            
            AddModifierServer(type, modifier);
        }
        
        [Server]
        private void AddModifierServer(PlayerStatType type, StatModifier modifier)
        {
            if (!_modifiers.TryGetValue(type, out var modList))
            {
                Debug.LogWarning($"[PlayerStatSystem] Cannot add modifier for unknown stat type: {type}");
                return;
            }
            
            // Remove existing modifier from same source (replace behavior)
            modList.RemoveAll(m => m.SourceID == modifier.SourceID);
            
            // Add new modifier
            modList.Add(modifier);
            
            // Mark stat as dirty for recalculation
            _dirtyStats.Add(type);
            
            if (_debugConfig != null && _debugConfig.EnableStatDebugLogs)
            {
                Debug.Log($"[PlayerStatSystem] Added modifier: {type} {modifier.Type} {modifier.Value:F1} from {modifier.SourceID}");
            }
        }
        
        public void RemoveModifier(PlayerStatType type, string sourceID)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[PlayerStatSystem] RemoveModifier can only be called on server!");
                return;
            }
            
            RemoveModifierServer(type, sourceID);
        }
        
        [Server]
        private void RemoveModifierServer(PlayerStatType type, string sourceID)
        {
            if (!_modifiers.TryGetValue(type, out var modList))
                return;
            
            int removed = modList.RemoveAll(m => m.SourceID == sourceID);
            
            if (removed > 0)
            {
                _dirtyStats.Add(type);
                
                if (_debugConfig != null && _debugConfig.EnableStatDebugLogs)
                {
                    Debug.Log($"[PlayerStatSystem] Removed {removed} modifier(s) for {type} from {sourceID}");
                }
            }
        }
        
        public void RemoveAllModifiersFromSource(string sourceID)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[PlayerStatSystem] RemoveAllModifiersFromSource can only be called on server!");
                return;
            }
            
            RemoveAllModifiersFromSourceServer(sourceID);
        }
        
        [Server]
        private void RemoveAllModifiersFromSourceServer(string sourceID)
        {
            int totalRemoved = 0;
            
            foreach (var kvp in _modifiers)
            {
                var modList = kvp.Value;
                int removed = modList.RemoveAll(m => m.SourceID == sourceID);
                
                if (removed > 0)
                {
                    _dirtyStats.Add(kvp.Key);
                    totalRemoved += removed;
                }
            }
            
            if (_debugConfig != null && _debugConfig.EnableStatDebugLogs && totalRemoved > 0)
            {
                Debug.Log($"[PlayerStatSystem] Removed total {totalRemoved} modifier(s) from source: {sourceID}");
            }
        }
        
        public List<StatModifier> GetModifiers(PlayerStatType type)
        {
            if (_modifiers.TryGetValue(type, out var modList))
                return new List<StatModifier>(modList);
            
            return new List<StatModifier>();
        }
        
        public PlayerStatConfig GetStatConfig()
        {
            return _statConfig;
        }
        
        #endregion
        
        #region Stat Calculation (Server Only)
        
        [Server]
        private void ProcessDirtyStats()
        {
            foreach (var statType in _dirtyStats)
            {
                RecalculateStat(statType);
            }
            
            _dirtyStats.Clear();
        }
        
        [Server]
        private void RecalculateStat(PlayerStatType type)
        {
            if (!_statCache.TryGetValue(type, out var stat))
                return;
            
            float oldValue = stat.CurrentValue;
            float newValue = CalculateStatValue(type, stat);
            
            // Clamp: floor is always MinValue.
            // For IsCurrentValue stats, ceiling is the current value of RelatedMaxStatType (dynamic).
            // For flat stats, ceiling is stat.MaxValue (static from config).
            float clampMax = stat.MaxValue;
            var relatedMax = _statConfig.GetRelatedMaxStat(type);
            if (relatedMax.HasValue && _statCache.TryGetValue(relatedMax.Value, out var maxStat))
            {
                clampMax = maxStat.CurrentValue;
            }
            newValue = Mathf.Clamp(newValue, stat.MinValue, clampMax);
            
            // Only update if changed significantly (avoid floating point noise)
            if (Mathf.Abs(newValue - oldValue) > 0.001f)
            {
                stat.CurrentValue = newValue;
                
                // Update SyncList (triggers network sync)
                int index = FindStatIndex(type);
                if (index >= 0)
                {
                    _syncedStats[index] = stat;
                }
                
                // Update cache
                _statCache[type] = stat;
                
                // Trigger events on server
                OnStatChanged?.Invoke(type, oldValue, newValue);
                
                // Special handling for weight changes
                if (type == PlayerStatType.CurrentWeight || type == PlayerStatType.WeightCapacity)
                {
                    OnWeightChanged?.Invoke(GetCurrentWeight(), GetWeightCapacity());
                    CheckOverweightStatus();
                }
                
                if (_debugConfig != null && _debugConfig.EnableStatDebugLogs)
                {
                    Debug.Log($"[PlayerStatSystem] {type}: {oldValue:F2} → {newValue:F2}");
                }
            }
        }
        
        /// <summary>
        /// Calculate final stat value from base + modifiers
        /// Order: Override → Flat → Percentage
        /// </summary>
        private float CalculateStatValue(PlayerStatType type, StatData stat)
        {
            if (!_modifiers.TryGetValue(type, out var modList) || modList.Count == 0)
                return stat.BaseValue;
            
            // Check for override modifiers first (highest priority)
            var overrideModifier = modList.FirstOrDefault(m => m.Type == ModifierType.Override);
            if (!string.IsNullOrEmpty(overrideModifier.SourceID))
            {
                return overrideModifier.Value;
            }
            
            // Sort by priority (lower = first)
            var sortedMods = modList.OrderBy(m => m.Priority).ToList();
            
            float value = stat.BaseValue;
            
            // Apply flat modifiers
            foreach (var mod in sortedMods.Where(m => m.Type == ModifierType.Flat))
            {
                value += mod.Value;
            }
            
            // Apply percentage modifiers
            foreach (var mod in sortedMods.Where(m => m.Type == ModifierType.Percentage))
            {
                value *= (1f + mod.Value / 100f);
            }
            
            return value;
        }
        
        /// <summary>
        /// Check if overweight status changed significantly
        /// Triggers OnOverweightChanged event
        /// </summary>
        private void CheckOverweightStatus()
        {
            float currentPercent = GetWeightPercent();
            
            // Only trigger if status changed significantly (> 1% change)
            if (Mathf.Abs(currentPercent - _lastWeightPercent) > 0.01f)
            {
                OnOverweightChanged?.Invoke(currentPercent);
                _lastWeightPercent = currentPercent;
            }
        }
        
        /// <summary>
        /// Find stat index in SyncList
        /// </summary>
        private int FindStatIndex(PlayerStatType type)
        {
            for (int i = 0; i < _syncedStats.Count; i++)
            {
                if (_syncedStats[i].Type == type)
                    return i;
            }
            return -1;
        }
        
        #endregion
        
        #region Network Callbacks (Client Side)
        
        /// <summary>
        /// Called when SyncList changes (client side)
        /// Rebuilds cache and triggers events
        /// </summary>
        private void OnStatSyncChanged(SyncListOperation op, int index, StatData oldValue, StatData newValue, bool asServer)
        {
            // Server already handled in RecalculateStat
            if (asServer)
                return;
            
            // Client: Update cache and trigger events
            switch (op)
            {
                case SyncListOperation.Add:
                case SyncListOperation.Set:
                    _statCache[newValue.Type] = newValue;
                    OnStatChanged?.Invoke(newValue.Type, oldValue.CurrentValue, newValue.CurrentValue);
                    break;
                
                case SyncListOperation.RemoveAt:
                    _statCache.Remove(oldValue.Type);
                    break;
                
                case SyncListOperation.Clear:
                    _statCache.Clear();
                    break;
            }
            
            // Weight events
            if (newValue.Type == PlayerStatType.CurrentWeight || newValue.Type == PlayerStatType.WeightCapacity)
            {
                OnWeightChanged?.Invoke(GetCurrentWeight(), GetWeightCapacity());
                
                float currentPercent = GetWeightPercent();
                if (Mathf.Abs(currentPercent - _lastWeightPercent) > 0.01f)
                {
                    OnOverweightChanged?.Invoke(currentPercent);
                    _lastWeightPercent = currentPercent;
                }
            }
        }
        
        /// <summary>
        /// Rebuild stat cache from synced data
        /// Called on client after network start
        /// </summary>
        private void RebuildStatCache()
        {
            _statCache.Clear();
            
            foreach (var stat in _syncedStats)
            {
                _statCache[stat.Type] = stat;
            }

            if (_syncedStats.Count > 0)
                _isCacheReady = true;
        }
        
        #endregion
        
        #region Debug UI
        
        private void OnGUI()
        {
            if (!_showDebugUI || !IsOwner)
                return;
            
            GUILayout.BeginArea(new Rect(10, 100, 350, 700));
            GUILayout.Label("=== PLAYER STATS ===");
            
            // Display all stats
            foreach (var kvp in _statCache.OrderBy(k => k.Key))
            {
                var stat = kvp.Value;
                var modifier = GetStatModifier(kvp.Key);
                
                string modStr = modifier != 0 ? $" ({modifier:+0.0;-0.0})" : "";
                GUILayout.Label($"{kvp.Key}: {stat.CurrentValue:F1}{modStr} / {stat.MaxValue:F0}");
            }
            
            GUILayout.Space(10);
            
            // Weight system info
            float weightPercent = GetWeightPercent();
            float speedMult = GetMovementSpeedMultiplier();
            
            GUILayout.Label($"Weight: {GetCurrentWeight():F1} / {GetWeightCapacity():F1} ({weightPercent:P0})");
            GUILayout.Label($"Speed Multiplier: {speedMult:P0}");
            
            // Weight status with color
            if (_gameplayConfig != null)
            {
                Color statusColor = _gameplayConfig.GetWeightStatusColor(weightPercent);
                string statusText = _gameplayConfig.GetWeightStatusText(weightPercent);
                
                GUI.color = statusColor;
                GUILayout.Label($"STATUS: {statusText}");
                GUI.color = Color.white;
            }
            
            GUILayout.EndArea();
        }
        
        #endregion
        
        #region Debug
        
        [ContextMenu("Log All Stats")]
        private void LogAllStats()
        {
            Debug.Log($"=== Player Stats ({_statCache.Count}) ===");
            
            foreach (var kvp in _statCache.OrderBy(k => k.Key))
            {
                var stat = kvp.Value;
                var modifier = GetStatModifier(kvp.Key);
                Debug.Log($"  {kvp.Key}: Base={stat.BaseValue:F1}, Current={stat.CurrentValue:F1}, Modifier={modifier:F1}");
            }
            
            Debug.Log($"\nWeight: {GetCurrentWeight():F1}/{GetWeightCapacity():F1} ({GetWeightPercent():P0})");
            Debug.Log($"Movement Speed Multiplier: {GetMovementSpeedMultiplier():P0}");
        }
        
        [ContextMenu("Log All Modifiers")]
        private void LogAllModifiers()
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("Modifiers only available on server!");
                return;
            }
            
            Debug.Log("=== Active Modifiers ===");
            
            int totalCount = 0;
            foreach (var kvp in _modifiers.OrderBy(k => k.Key))
            {
                if (kvp.Value.Count == 0)
                    continue;
                
                Debug.Log($"\n{kvp.Key} ({kvp.Value.Count} modifiers):");
                foreach (var mod in kvp.Value.OrderBy(m => m.Priority))
                {
                    Debug.Log($"  {mod}");
                    totalCount++;
                }
            }
            
            Debug.Log($"\nTotal: {totalCount} modifiers active");
        }
        
        #endregion
    }
}
