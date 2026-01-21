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
                // In edit-mode (editor tooling), DontDestroyOnLoad throws.
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }
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
        /// Get item config by ID (legacy - returns ItemConfigData)
        /// </summary>
        public ItemConfigData GetItemConfig(string itemId)
        {
            if (ConfigData?.ItemConfig == null) return null;
            return ConfigData.ItemConfig.Find(i => i.ItemId == itemId);
        }

        /// <summary>
        /// Get item config as BaseItemConfig (new structure)
        /// Converts from legacy ItemConfigData if needed
        /// </summary>
        public BaseItemConfig GetItemConfigBase(string itemId)
        {
            if (ConfigData?.ItemConfig == null) return null;
            
            var legacy = ConfigData.ItemConfig.Find(i => i.ItemId == itemId);
            if (legacy == null) return null;

            // Convert from legacy to new structure
            return ItemConfigLoader.ConvertFromLegacy(legacy);
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
    }
}

