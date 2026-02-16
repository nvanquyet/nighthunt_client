using System.Collections.Generic;
using UnityEngine;
using GameplaySystems.Core.Data;

namespace GameplaySystems.Inventory
{
    public class ItemDatabase : MonoBehaviour
    {
        #region Singleton
        
        private static ItemDatabase _instance;
        public static ItemDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ItemDatabase>();
                    
                    if (_instance == null)
                    {
                        var go = new GameObject("[ItemDatabase]");
                        _instance = go.AddComponent<ItemDatabase>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Serialized Fields
        
        [Header("Item Definitions")]
        [Tooltip("All item definitions - will be loaded on start")]
        [SerializeField] private ItemDefinition[] _itemDefinitions;
        
        [Header("Settings")]
        [Tooltip("Auto-load all ItemDefinitions from Resources folder")]
        [SerializeField] private bool _autoLoadFromResources = true;
        
        [SerializeField] private string _resourcesPath = "Items";
        
        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;
        [SerializeField] private bool _trackInstances = true;
        
        #endregion
        
        #region Private Data
        
        private Dictionary<string, ItemDefinition> _definitionLookup = new Dictionary<string, ItemDefinition>();
        private Dictionary<string, ItemInstance> _instanceRegistry = new Dictionary<string, ItemInstance>();
        
        // CACHE SYSTEM
        private static ItemDefinition[] _cachedResourceDefinitions;
        private static bool _resourcesLoaded = false;
        private bool _isInitialized = false;
        
        #endregion
        
        #region Properties
        
        public static bool IsInitialized => Instance._isInitialized;
        public static int DefinitionCount => Instance._definitionLookup.Count;
        
        #endregion
        
        #region Lifecycle
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            Initialize();
        }
        
        private void Initialize()
        {
            _definitionLookup.Clear();
            
            // Load from serialized array
            if (_itemDefinitions != null && _itemDefinitions.Length > 0)
            {
                foreach (var def in _itemDefinitions)
                {
                    RegisterDefinition(def);
                }
            }
            
            // Auto-load from Resources
            if (_autoLoadFromResources)
            {
                LoadDefinitionsFromResources();
            }
            
            _isInitialized = true;
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[ItemDatabase] Initialized with {_definitionLookup.Count} item definitions");
            }
        }
        
        #endregion
        
        #region Item Definitions
        
        public static ItemDefinition GetDefinition(string itemID)
        {
            if (string.IsNullOrEmpty(itemID))
                return null;
            
            if (!Instance._isInitialized)
            {
                Debug.LogWarning("[ItemDatabase] Database not initialized yet!");
                return null;
            }
            
            if (Instance._definitionLookup.TryGetValue(itemID, out var definition))
                return definition;
            
            Debug.LogWarning($"[ItemDatabase] Item definition not found: {itemID}");
            return null;
        }
        
        public static void RegisterDefinition(ItemDefinition definition)
        {
            if (definition == null)
                return;
            
            if (string.IsNullOrEmpty(definition.ItemID))
            {
                Debug.LogError($"[ItemDatabase] Cannot register item with empty ItemID: {definition.name}");
                return;
            }
            
            if (Instance._definitionLookup.ContainsKey(definition.ItemID))
            {
                Debug.LogWarning($"[ItemDatabase] Duplicate ItemID: {definition.ItemID}. Overwriting.");
            }
            
            Instance._definitionLookup[definition.ItemID] = definition;
            
            if (Instance._enableDebugLogs)
            {
                Debug.Log($"[ItemDatabase] Registered: {definition.ItemID} ({definition.Type})");
            }
        }
        
        private void LoadDefinitionsFromResources()
        {
            // Use cached data if available
            if (_resourcesLoaded && _cachedResourceDefinitions != null)
            {
                if (_enableDebugLogs)
                {
                    Debug.Log($"[ItemDatabase] Using cached definitions ({_cachedResourceDefinitions.Length} items)");
                }
                List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();
                foreach (var def in _cachedResourceDefinitions)
                {
                    RegisterDefinition(def);
                    itemDefinitions.Add(def);
                }

                _itemDefinitions = itemDefinitions.ToArray();
                return;
            }
            
            // Load from Resources (first time)
            ItemDefinition[] definitions = Resources.LoadAll<ItemDefinition>(_resourcesPath);
            
            if (definitions != null && definitions.Length > 0)
            {
                _cachedResourceDefinitions = definitions;
                _resourcesLoaded = true;
                
                if (_enableDebugLogs)
                {
                    Debug.Log($"[ItemDatabase] Loaded and cached {definitions.Length} definitions from Resources/{_resourcesPath}");
                }
                
                foreach (var def in definitions)
                {
                    RegisterDefinition(def);
                }
            }
            else
            {
                Debug.LogWarning($"[ItemDatabase] No items found in Resources/{_resourcesPath}");
            }
        }
        
        public static List<ItemDefinition> GetAllDefinitions()
        {
            return new List<ItemDefinition>(Instance._definitionLookup.Values);
        }
        
        public static List<ItemDefinition> GetDefinitionsByType(ItemType type)
        {
            var result = new List<ItemDefinition>();
            foreach (var def in Instance._definitionLookup.Values)
            {
                if (def.Type == type)
                    result.Add(def);
            }
            return result;
        }
        
        public static bool HasDefinition(string itemID)
        {
            return !string.IsNullOrEmpty(itemID) && Instance._definitionLookup.ContainsKey(itemID);
        }
        
        #endregion
        
        #region Item Instances (Optional Tracking)
        
        public static void RegisterInstance(ItemInstance instance)
        {
            if (!Instance._trackInstances || instance == null || string.IsNullOrEmpty(instance.InstanceID))
                return;
            
            Instance._instanceRegistry[instance.InstanceID] = instance;
            
            if (Instance._enableDebugLogs)
            {
                Debug.Log($"[ItemDatabase] Registered instance: {instance.InstanceID} ({instance.DefinitionID})");
            }
        }
        
        public static void UnregisterInstance(string instanceID)
        {
            if (!Instance._trackInstances || string.IsNullOrEmpty(instanceID))
                return;
            
            if (Instance._instanceRegistry.Remove(instanceID) && Instance._enableDebugLogs)
            {
                Debug.Log($"[ItemDatabase] Unregistered instance: {instanceID}");
            }
        }
        
        public static ItemInstance GetInstance(string instanceID)
        {
            if (!Instance._trackInstances)
                return null;
            
            Instance._instanceRegistry.TryGetValue(instanceID, out var instance);
            return instance;
        }
        
        public static List<ItemInstance> GetAllInstances()
        {
            if (!Instance._trackInstances)
                return new List<ItemInstance>();
            
            return new List<ItemInstance>(Instance._instanceRegistry.Values);
        }
        
        #endregion
        
        #region Context Menu - Resource Loading
        
        /// <summary>
        /// Load definitions from Resources (uses cache if available)
        /// </summary>
        [ContextMenu("1. Load From Resources (Cached)")]
        private void ContextMenu_LoadFromResources()
        {
            LoadDefinitionsFromResources();
            Debug.Log($"<color=green>✓ Loaded {_definitionLookup.Count} definitions</color>");
        }
        
        /// <summary>
        /// Force reload - bypasses cache and loads fresh from Resources
        /// </summary>
        [ContextMenu("2. Force Reload From Resources")]
        private void ContextMenu_ForceReload()
        {
            _resourcesLoaded = false;
            _cachedResourceDefinitions = null;
            _definitionLookup.Clear();
            
            LoadDefinitionsFromResources();
            
            Debug.Log($"<color=cyan>✓ Force reloaded! Now has {_definitionLookup.Count} definitions</color>");
        }
        
        /// <summary>
        /// Preload and cache all resources without initializing
        /// </summary>
        [ContextMenu("3. Preload Resources (Cache Only)")]
        private void ContextMenu_PreloadResources()
        {
            if (_resourcesLoaded)
            {
                Debug.Log($"<color=yellow>Already cached {_cachedResourceDefinitions.Length} definitions</color>");
                return;
            }
            
            ItemDefinition[] definitions = Resources.LoadAll<ItemDefinition>(_resourcesPath);
            
            if (definitions != null && definitions.Length > 0)
            {
                _cachedResourceDefinitions = definitions;
                _resourcesLoaded = true;
                Debug.Log($"<color=green>✓ Preloaded and cached {definitions.Length} definitions</color>");
            }
            else
            {
                Debug.LogWarning($"No items found in Resources/{_resourcesPath}");
            }
        }
        
        /// <summary>
        /// Clear the resource cache
        /// </summary>
        [ContextMenu("4. Clear Resource Cache")]
        private void ContextMenu_ClearCache()
        {
            int cachedCount = _cachedResourceDefinitions?.Length ?? 0;
            
            _cachedResourceDefinitions = null;
            _resourcesLoaded = false;
            
            Debug.Log($"<color=orange>✓ Cache cleared (had {cachedCount} definitions)</color>");
        }
        
        /// <summary>
        /// Reinitialize the entire database
        /// </summary>
        [ContextMenu("5. Reinitialize Database")]
        private void ContextMenu_Reinitialize()
        {
            _isInitialized = false;
            Initialize();
            Debug.Log($"<color=cyan>✓ Database reinitialized with {_definitionLookup.Count} definitions</color>");
        }
        
        #endregion
        
        #region Context Menu - Information
        
        /// <summary>
        /// Show current cache and database status
        /// </summary>
        [ContextMenu("Info/Show Cache Status")]
        private void ContextMenu_ShowCacheStatus()
        {
            Debug.Log("================== ITEM DATABASE STATUS ==================");
            Debug.Log($"Initialized: {_isInitialized}");
            Debug.Log($"Resources Loaded: {_resourcesLoaded}");
            Debug.Log($"Cached Definitions: {(_cachedResourceDefinitions?.Length ?? 0)}");
            Debug.Log($"Registered Definitions: {_definitionLookup.Count}");
            Debug.Log($"Tracked Instances: {_instanceRegistry.Count}");
            Debug.Log($"Resources Path: Resources/{_resourcesPath}");
            Debug.Log($"Auto Load: {_autoLoadFromResources}");
            Debug.Log("========================================================");
        }
        
        /// <summary>
        /// List all registered definitions
        /// </summary>
        [ContextMenu("Info/List All Definitions")]
        private void ContextMenu_ListDefinitions()
        {
            Debug.Log($"========== ITEM DEFINITIONS ({_definitionLookup.Count}) ==========");
            
            foreach (var kvp in _definitionLookup)
            {
                var def = kvp.Value;
                Debug.Log($"  [{def.Type}] {kvp.Key}: {def.DisplayName} (Weight: {def.Weight}, Stack: {def.MaxStackSize})");
            }
        }
        
        /// <summary>
        /// List definitions by type
        /// </summary>
        [ContextMenu("Info/List Definitions By Type")]
        private void ContextMenu_ListByType()
        {
            var types = System.Enum.GetValues(typeof(ItemType));
            
            Debug.Log("========== DEFINITIONS BY TYPE ==========");
            
            foreach (ItemType type in types)
            {
                var items = GetDefinitionsByType(type);
                if (items.Count > 0)
                {
                    Debug.Log($"\n{type} ({items.Count}):");
                    foreach (var item in items)
                    {
                        Debug.Log($"  - {item.ItemID}: {item.DisplayName}");
                    }
                }
            }
        }
        
        /// <summary>
        /// List all tracked instances
        /// </summary>
        [ContextMenu("Info/List All Instances")]
        private void ContextMenu_ListInstances()
        {
            if (!_trackInstances)
            {
                Debug.Log("[ItemDatabase] Instance tracking is disabled");
                return;
            }
            
            Debug.Log($"========== ACTIVE INSTANCES ({_instanceRegistry.Count}) ==========");
            
            foreach (var kvp in _instanceRegistry)
            {
                var instance = kvp.Value;
                var def = GetDefinition(instance.DefinitionID);
                string name = def?.DisplayName ?? instance.DefinitionID;
                Debug.Log($"  {kvp.Key}: {name} x{instance.Quantity} @ index {instance.InventoryIndex}");
            }
        }
        
        #endregion
        
        #region Context Menu - Validation
        
        /// <summary>
        /// Validate all definitions
        /// </summary>
        [ContextMenu("Validation/Validate All Definitions")]
        private void ContextMenu_ValidateAll()
        {
            int errors = 0;
            int warnings = 0;
            
            Debug.Log("========== VALIDATION STARTED ==========");
            
            foreach (var def in _definitionLookup.Values)
            {
                if (!def.IsValid(out string error))
                {
                    Debug.LogError($"[ERROR] '{def.name}': {error}", def);
                    errors++;
                }
                
                if (def.Weight <= 0)
                {
                    Debug.LogWarning($"[WARNING] '{def.DisplayName}' has zero or negative weight", def);
                    warnings++;
                }
                
                if (def.IsStackable && def.MaxStackSize == 1)
                {
                    Debug.LogWarning($"[WARNING] '{def.DisplayName}' is stackable but MaxStackSize = 1", def);
                    warnings++;
                }
            }
            
            Debug.Log("========== VALIDATION COMPLETE ==========");
            Debug.Log($"Total Definitions: {_definitionLookup.Count}");
            Debug.Log($"<color=red>Errors: {errors}</color>");
            Debug.Log($"<color=yellow>Warnings: {warnings}</color>");
            
            if (errors == 0 && warnings == 0)
            {
                Debug.Log("<color=green>✓ All definitions are valid!</color>");
            }
        }
        
        /// <summary>
        /// Check for duplicate IDs
        /// </summary>
        [ContextMenu("Validation/Check For Duplicates")]
        private void ContextMenu_CheckDuplicates()
        {
            var allDefs = Resources.LoadAll<ItemDefinition>(_resourcesPath);
            var idCount = new Dictionary<string, int>();
            
            foreach (var def in allDefs)
            {
                if (!idCount.ContainsKey(def.ItemID))
                    idCount[def.ItemID] = 0;
                
                idCount[def.ItemID]++;
            }
            
            bool foundDuplicates = false;
            
            Debug.Log("========== CHECKING FOR DUPLICATES ==========");
            
            foreach (var kvp in idCount)
            {
                if (kvp.Value > 1)
                {
                    Debug.LogError($"<color=red>DUPLICATE ID: '{kvp.Key}' appears {kvp.Value} times</color>");
                    foundDuplicates = true;
                }
            }
            
            if (!foundDuplicates)
            {
                Debug.Log("<color=green>✓ No duplicate IDs found!</color>");
            }
        }
        
        #endregion
        
        #region Context Menu - Cleanup
        
        /// <summary>
        /// Clear instance registry
        /// </summary>
        [ContextMenu("Cleanup/Clear Instance Registry")]
        private void ContextMenu_ClearInstances()
        {
            int count = _instanceRegistry.Count;
            _instanceRegistry.Clear();
            Debug.Log($"<color=orange>✓ Cleared {count} tracked instances</color>");
        }
        
        /// <summary>
        /// Reset everything (definitions, cache, instances)
        /// </summary>
        [ContextMenu("Cleanup/Reset Everything")]
        private void ContextMenu_ResetEverything()
        {
            _definitionLookup.Clear();
            _instanceRegistry.Clear();
            _cachedResourceDefinitions = null;
            _resourcesLoaded = false;
            _isInitialized = false;
            
            Debug.Log("<color=red>✓ Database completely reset!</color>");
        }
        
        #endregion
        
        #region Editor
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_resourcesPath))
            {
                _resourcesPath = "Items";
            }
        }
        
        /// <summary>
        /// Open Resources folder in Project window
        /// </summary>
        [ContextMenu("Editor/Select Resources Folder")]
        private void ContextMenu_SelectResourcesFolder()
        {
            string path = $"Assets/Resources/{_resourcesPath}";
            var folder = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            
            if (folder != null)
            {
                UnityEditor.Selection.activeObject = folder;
                UnityEditor.EditorGUIUtility.PingObject(folder);
                Debug.Log($"Selected: {path}");
            }
            else
            {
                Debug.LogWarning($"Folder not found: {path}");
            }
        }
#endif
        
        #endregion
    }
}