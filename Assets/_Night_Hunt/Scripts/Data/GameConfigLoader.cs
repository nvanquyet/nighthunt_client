using System.IO;
using UnityEngine;
using System;

namespace NightHunt.Data
{
    /// <summary>
    /// Service to load and parse game configuration from JSON file
    /// Uses Singleton pattern for easy access
    /// </summary>
    public class GameConfigLoader : MonoBehaviour
    {
        private static GameConfigLoader _instance;
        public static GameConfigLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameConfigLoader");
                    _instance = go.AddComponent<GameConfigLoader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Config File")]
        [SerializeField] private string configFileName = "NightHunt_Full_GameDesign_Config_v3.json";
        [SerializeField] private string configPath = "Assets/_Night_Hunt/Data/";

        private GameConfigData _configData;
        private bool _isLoaded = false;

        public GameConfigData ConfigData
        {
            get
            {
                if (!_isLoaded)
                {
                    LoadConfig();
                }
                return _configData;
            }
        }

        public bool IsLoaded => _isLoaded;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                LoadConfig();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Load configuration from JSON file
        /// </summary>
        public void LoadConfig()
        {
            if (_isLoaded && _configData != null)
            {
                return;
            }

            try
            {
                string fullPath = Path.Combine(Application.dataPath, "_Night_Hunt", "Data", configFileName);
                
                // Try Resources folder as fallback
                if (!File.Exists(fullPath))
                {
                    TextAsset jsonFile = Resources.Load<TextAsset>("NightHunt_Full_GameDesign_Config_v3");
                    if (jsonFile != null)
                    {
                        _configData = JsonUtility.FromJson<GameConfigData>(jsonFile.text);
                        _isLoaded = true;
                        Debug.Log($"[GameConfigLoader] Config loaded from Resources: {_configData.WeaponConfig?.Count ?? 0} weapons, {_configData.ItemConfig?.Count ?? 0} items");
                        return;
                    }
                }

                if (File.Exists(fullPath))
                {
                    string jsonContent = File.ReadAllText(fullPath);
                    _configData = JsonUtility.FromJson<GameConfigData>(jsonContent);
                    _isLoaded = true;
                    Debug.Log($"[GameConfigLoader] Config loaded from file: {_configData.WeaponConfig?.Count ?? 0} weapons, {_configData.ItemConfig?.Count ?? 0} items");
                }
                else
                {
                    Debug.LogError($"[GameConfigLoader] Config file not found at: {fullPath}");
                    _configData = new GameConfigData(); // Create empty config
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameConfigLoader] Failed to load config: {e.Message}");
                _configData = new GameConfigData();
            }
        }

        /// <summary>
        /// Get weapon config by ID
        /// </summary>
        public WeaponConfigData GetWeaponConfig(string weaponId)
        {
            if (ConfigData?.WeaponConfig == null) return null;
            return ConfigData.WeaponConfig.Find(w => w.WeaponId == weaponId);
        }

        /// <summary>
        /// Get item config by ID
        /// </summary>
        public ItemConfigData GetItemConfig(string itemId)
        {
            if (ConfigData?.ItemConfig == null) return null;
            return ConfigData.ItemConfig.Find(i => i.ItemId == itemId);
        }

        /// <summary>
        /// Get character config by ID
        /// </summary>
        public CharacterConfigData GetCharacterConfig(string characterId)
        {
            if (ConfigData?.CharacterConfig == null) return null;
            return ConfigData.CharacterConfig.Find(c => c.CharacterId == characterId);
        }

        /// <summary>
        /// Get status effect config by ID
        /// </summary>
        public StatusEffectConfigData GetStatusEffectConfig(string statusId)
        {
            if (ConfigData?.StatusEffectConfig == null) return null;
            return ConfigData.StatusEffectConfig.Find(s => s.StatusId == statusId);
        }

        /// <summary>
        /// Get match phase config by phase name
        /// </summary>
        public MatchPhaseConfigData GetMatchPhaseConfig(string phaseName)
        {
            if (ConfigData?.MatchPhaseConfig == null) return null;
            return ConfigData.MatchPhaseConfig.Find(p => p.Phase == phaseName);
        }

        /// <summary>
        /// Get zone config by ID
        /// </summary>
        public ZoneConfigData GetZoneConfig(string zoneId)
        {
            if (ConfigData?.ZoneConfig == null) return null;
            return ConfigData.ZoneConfig.Find(z => z.ZoneId == zoneId);
        }

        /// <summary>
        /// Get inventory config (should be single entry)
        /// </summary>
        public InventoryConfigData GetInventoryConfig()
        {
            if (ConfigData?.InventoryConfig == null || ConfigData.InventoryConfig.Count == 0) return null;
            return ConfigData.InventoryConfig[0];
        }

        /// <summary>
        /// Get stamina weight config for a given weight
        /// </summary>
        public StaminaWeightConfigData GetStaminaWeightConfig(float currentWeight)
        {
            if (ConfigData?.StaminaWeightConfig == null) return null;

            // Parse threshold strings and find matching config
            foreach (var config in ConfigData.StaminaWeightConfig)
            {
                if (ParseWeightThreshold(config.WeightThreshold, currentWeight))
                {
                    return config;
                }
            }

            return ConfigData.StaminaWeightConfig[0]; // Default to first
        }

        private bool ParseWeightThreshold(string threshold, float weight)
        {
            if (threshold.Contains("<="))
            {
                float max = float.Parse(threshold.Replace("kg", "").Replace("<=", "").Trim());
                return weight <= max;
            }
            else if (threshold.Contains(">"))
            {
                float min = float.Parse(threshold.Replace("kg", "").Replace(">", "").Trim());
                return weight > min;
            }
            else if (threshold.Contains("-"))
            {
                string[] parts = threshold.Replace("kg", "").Split('-');
                float min = float.Parse(parts[0].Trim());
                float max = float.Parse(parts[1].Trim());
                return weight >= min && weight < max;
            }

            return false;
        }

        // ── Beacon ────────────────────────────────────────────────────────────

        /// <summary>Returns beacon config, or sensible defaults if not configured.</summary>
        public BeaconConfigData GetBeaconConfig()
        {
            if (ConfigData?.BeaconConfig != null) return ConfigData.BeaconConfig;
            // Fallback defaults
            return new BeaconConfigData
            {
                MaxActivePerTeam = 3,
                BeaconHealth = 150f,
                PlaceTime = 2f,
                LootSpawnWeight = 0.15f
            };
        }

        // ── Boss ──────────────────────────────────────────────────────────────

        /// <summary>Returns all boss spawn configs.</summary>
        public System.Collections.Generic.List<BossSpawnConfigData> GetAllBossConfigs()
            => ConfigData?.BossSpawnConfig;

        /// <summary>Returns boss config by BossId.</summary>
        public BossSpawnConfigData GetBossConfig(string bossId)
        {
            if (ConfigData?.BossSpawnConfig == null) return null;
            return ConfigData.BossSpawnConfig.Find(b => b.BossId == bossId);
        }

        // ── Match End ─────────────────────────────────────────────────────────

        /// <summary>Returns match-end config, or sensible defaults.</summary>
        public MatchEndConfigData GetMatchEndConfig()
        {
            if (ConfigData?.MatchEndConfig != null) return ConfigData.MatchEndConfig;
            return new MatchEndConfigData
            {
                ResultsDisplayDuration = 10f,
                PostMatchCountdown = 15f,
                CaptureZoneScorePerSecond = 5f,
                CaptureZoneMinPlayers = 1
            };
        }

        /// <summary>Phase 3 respawn delay from phase config (fallback 10 s).</summary>
        public float GetPhase3RespawnDelay()
        {
            var cfg = GetMatchPhaseConfig("Phase3_FinalLockdown");
            return cfg != null && cfg.Phase3RespawnDelay > 0 ? cfg.Phase3RespawnDelay : 10f;
        }

        /// <summary>Warning time before phase ends (fallback 30 s).</summary>
        public float GetPhaseWarningTime(string phaseName)
        {
            var cfg = GetMatchPhaseConfig(phaseName);
            return cfg != null && cfg.WarningTime > 0 ? cfg.WarningTime : 30f;
        }

        // ── Rank ──────────────────────────────────────────────────────────────

        /// <summary>Returns all rank tier configs.</summary>
        public System.Collections.Generic.List<RankTierConfigData> GetAllRankTiers()
            => ConfigData?.RankTierConfig;

        /// <summary>Returns tier config for a given ELO value.</summary>
        public RankTierConfigData GetRankTierForElo(int elo)
        {
            if (ConfigData?.RankTierConfig == null) return null;
            // Find highest tier whose MinElo <= elo
            RankTierConfigData result = null;
            foreach (var tier in ConfigData.RankTierConfig)
            {
                if (elo >= tier.MinElo)
                    result = tier;
            }
            return result;
        }

        /// <summary>Returns matchmaking config, or sensible defaults.</summary>
        public RankMatchmakingConfigData GetRankMatchmakingConfig()
        {
            if (ConfigData?.RankMatchmakingConfig != null) return ConfigData.RankMatchmakingConfig;
            return new RankMatchmakingConfigData
            {
                InitialEloDelta = 300,
                EloDeltaExpandAmount = 200,
                EloDeltaExpandInterval = 15f,
                MaxEloDelta = 1500,
                QueueTimeout = 120f
            };
        }
    }
}

