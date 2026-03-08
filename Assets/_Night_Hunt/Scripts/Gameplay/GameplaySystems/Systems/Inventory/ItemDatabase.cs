using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.Inventory
{
    /// <summary>
    /// Scene-singleton registry for <see cref="ItemDefinition"/> assets and live <see cref="ItemInstance"/> objects.
    /// Provides O(1) lookups by ID and type-filtered queries.
    /// </summary>
    public class ItemDatabase : MonoBehaviour
    {
        #region Singleton (Thread-Safe)
        
        private static readonly object _lock = new object();
        private static ItemDatabase _instance;
        
        public static ItemDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
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
                    }
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Serialized Fields
        
        [Header("Item Definitions")]
        [SerializeField] private ItemDefinition[] _itemDefinitions;
        
        [Header("Settings")]
        [SerializeField] private bool _autoLoadFromResources = true;
        [SerializeField] private string _resourcesPath = "Items";
        
        [Header("Performance (Mobile Optimization)")]
        [Tooltip("Pre-build type lookup cache on init (recommended)")]
        [SerializeField] private bool _prewarmTypeLookup = true;
        
        [Tooltip("Max cached instances (0 = unlimited). Recommended: 100 for mobile")]
        [SerializeField] private int _maxCachedInstances = 100;
        
        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;
        [SerializeField] private bool _trackInstances = true;
        
        #endregion
        
        #region Private Data - Optimized Caching
        
        // CORE LOOKUPS - O(1) access
        private Dictionary<string, ItemDefinition> _definitionLookup = new Dictionary<string, ItemDefinition>(32);
        private Dictionary<string, ItemInstance> _instanceRegistry = new Dictionary<string, ItemInstance>(64);
        
        // PERFORMANCE CACHES - Pre-computed for fast access
        private Dictionary<ItemType, List<ItemDefinition>> _definitionsByType = new Dictionary<ItemType, List<ItemDefinition>>(8);
        
        // RESOURCE CACHING - Load once, reuse forever
        private static ItemDefinition[] _cachedResourceDefinitions;
        private static bool _resourcesLoaded = false;
        
        // STATE
        private bool _isInitialized = false;
        
        #endregion
        
        #region Properties
        
        public static bool IsInitialized => Instance._isInitialized;
        public static int DefinitionCount => Instance._definitionLookup.Count;
        public static int InstanceCount => Instance._instanceRegistry.Count;
        
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
            var startTime = Time.realtimeSinceStartup;
            
            _definitionLookup.Clear();
            _definitionsByType.Clear();
            
            // Load from serialized array
            if (_itemDefinitions != null && _itemDefinitions.Length > 0)
            {
                foreach (var def in _itemDefinitions)
                    RegisterDefinition(def);
            }
            
            // Auto-load from Resources
            if (_autoLoadFromResources)
                LoadDefinitionsFromResources();
            
            // PERFORMANCE: Pre-warm type lookup cache
            if (_prewarmTypeLookup)
                BuildTypeLookupCache();
            
            _isInitialized = true;
            
            var elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[ItemDatabase] Initialized in {elapsed:F2}ms " +
                         $"({_definitionLookup.Count} definitions, " +
                         $"{_definitionsByType.Count} types cached)");
            }
        }
        
        #endregion
        
        #region Item Definitions - OPTIMIZED
        
        /// <summary>
        /// O(1) lookup by ID
        /// </summary>
        public static ItemDefinition GetDefinition(string itemID)
        {
            if (string.IsNullOrEmpty(itemID))
                return null;
            
            if (!Instance._isInitialized)
            {
                Debug.LogWarning("[ItemDatabase] Database not initialized!");
                return null;
            }
            
            return Instance._definitionLookup.TryGetValue(itemID, out var definition) 
                ? definition 
                : null;
        }
        
        public static void RegisterDefinition(ItemDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.ItemID))
            {
                Debug.LogError("[ItemDatabase] Invalid definition");
                return;
            }
            
            Instance._definitionLookup[definition.ItemID] = definition;
            
            // PERFORMANCE: Update type cache
            if (Instance._prewarmTypeLookup)
                AddToTypeCache(definition);
            
            if (Instance._enableDebugLogs)
                Debug.Log($"[ItemDatabase] Registered: {definition.ItemID} ({definition.Type})");
        }
        
        private void LoadDefinitionsFromResources()
        {
            // PERFORMANCE: Use cached data if available
            if (_resourcesLoaded && _cachedResourceDefinitions != null)
            {
                if (_enableDebugLogs)
                    Debug.Log($"[ItemDatabase] Using cached definitions ({_cachedResourceDefinitions.Length} items)");
                
                foreach (var def in _cachedResourceDefinitions)
                    RegisterDefinition(def);
                
                return;
            }
            
            // Load from Resources (first time only)
            var definitions = Resources.LoadAll<ItemDefinition>(_resourcesPath);
            
            if (definitions != null && definitions.Length > 0)
            {
                _cachedResourceDefinitions = definitions;
                _resourcesLoaded = true;
                
                foreach (var def in definitions)
                    RegisterDefinition(def);
                
                if (_enableDebugLogs)
                    Debug.Log($"[ItemDatabase] Loaded & cached {definitions.Length} from Resources/{_resourcesPath}");
            }
            else
            {
                Debug.LogWarning($"[ItemDatabase] No items found in Resources/{_resourcesPath}");
            }
        }
        
        /// <summary>
        /// OPTIMIZED: O(1) instead of LINQ filtering
        /// Returns cached list by type
        /// </summary>
        public static List<ItemDefinition> GetDefinitionsByType(ItemType type)
        {
            if (Instance._definitionsByType.TryGetValue(type, out var list))
                return new List<ItemDefinition>(list); // Return copy
            
            // Fallback: Build on-demand if cache disabled
            var result = new List<ItemDefinition>();
            foreach (var def in Instance._definitionLookup.Values)
            {
                if (def.Type == type)
                    result.Add(def);
            }
            
            return result;
        }
        
        public static List<ItemDefinition> GetAllDefinitions()
        {
            return new List<ItemDefinition>(Instance._definitionLookup.Values);
        }
        
        public static bool HasDefinition(string itemID)
        {
            return !string.IsNullOrEmpty(itemID) && Instance._definitionLookup.ContainsKey(itemID);
        }
        
        #endregion
        
        #region Item Instances - OPTIMIZED WITH POOLING
        
        public static void RegisterInstance(ItemInstance instance)
        {
            if (!Instance._trackInstances || instance == null || string.IsNullOrEmpty(instance.InstanceID))
                return;
            
            // MEMORY: Enforce instance limit for mobile
            if (Instance._maxCachedInstances > 0 && Instance._instanceRegistry.Count >= Instance._maxCachedInstances)
            {
                // Remove oldest instances (simple FIFO)
                var toRemove = Instance._instanceRegistry.Count - Instance._maxCachedInstances + 1;
                var removed = 0;
                
                var keys = new List<string>(Instance._instanceRegistry.Keys);
                foreach (var key in keys)
                {
                    if (removed >= toRemove) break;
                    Instance._instanceRegistry.Remove(key);
                    removed++;
                }
                
                if (Instance._enableDebugLogs)
                    Debug.LogWarning($"[ItemDatabase] Instance cache limit reached, removed {removed} oldest instances");
            }
            
            Instance._instanceRegistry[instance.InstanceID] = instance;
            
            if (Instance._enableDebugLogs)
                Debug.Log($"[ItemDatabase] Registered instance: {instance.InstanceID}");
        }
        
        public static void UnregisterInstance(string instanceID)
        {
            if (!Instance._trackInstances || string.IsNullOrEmpty(instanceID))
                return;
            
            if (Instance._instanceRegistry.Remove(instanceID) && Instance._enableDebugLogs)
                Debug.Log($"[ItemDatabase] Unregistered instance: {instanceID}");
        }
        
        public static ItemInstance GetInstance(string instanceID)
        {
            if (!Instance._trackInstances)
                return null;
            
            return Instance._instanceRegistry.TryGetValue(instanceID, out var instance) 
                ? instance 
                : null;
        }
        
        public static List<ItemInstance> GetAllInstances()
        {
            if (!Instance._trackInstances)
                return new List<ItemInstance>();
            
            return new List<ItemInstance>(Instance._instanceRegistry.Values);
        }
        
        /// <summary>
        /// PERFORMANCE: Bulk unregister for clearing inventories
        /// </summary>
        public static void UnregisterInstances(IEnumerable<string> instanceIDs)
        {
            if (!Instance._trackInstances) return;
            
            foreach (var id in instanceIDs)
            {
                if (!string.IsNullOrEmpty(id))
                    Instance._instanceRegistry.Remove(id);
            }
        }
        
        #endregion
        
        #region Performance Helpers
        
        /// <summary>
        /// PERFORMANCE: Build type lookup cache for O(1) GetDefinitionsByType()
        /// </summary>
        private void BuildTypeLookupCache()
        {
            _definitionsByType.Clear();
            
            foreach (var def in _definitionLookup.Values)
                AddToTypeCache(def);
            
            if (_enableDebugLogs)
                Debug.Log($"[ItemDatabase] Type cache built: {_definitionsByType.Count} types");
        }
        
        private static void AddToTypeCache(ItemDefinition definition)
        {
            if (!Instance._definitionsByType.TryGetValue(definition.Type, out var list))
            {
                list = new List<ItemDefinition>();
                Instance._definitionsByType[definition.Type] = list;
            }
            
            if (!list.Contains(definition))
                list.Add(definition);
        }
        
        /// <summary>
        /// MEMORY: Clear all caches (useful for scene transitions)
        /// </summary>
        public static void ClearCaches()
        {
            Instance._definitionLookup.Clear();
            Instance._instanceRegistry.Clear();
            Instance._definitionsByType.Clear();
            
            // Don't clear resource cache - keep it loaded
            
            if (Instance._enableDebugLogs)
                Debug.Log("[ItemDatabase] Caches cleared");
        }
        
        #endregion
        
        #region Validation
        
        [ContextMenu("Validation/Validate All Definitions")]
        private void ValidateAll()
        {
            int errors = 0, warnings = 0;
            
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
                    Debug.LogWarning($"[WARNING] '{def.DisplayName}' has zero/negative weight", def);
                    warnings++;
                }
                
                if (def.IsStackable && def.MaxStackSize == 1)
                {
                    Debug.LogWarning($"[WARNING] '{def.DisplayName}' stackable but MaxStackSize=1", def);
                    warnings++;
                }
            }
            
            Debug.Log("========== VALIDATION COMPLETE ==========");
            Debug.Log($"Total: {_definitionLookup.Count} | Errors: {errors} | Warnings: {warnings}");
            
            if (errors == 0 && warnings == 0)
                Debug.Log("<color=green>✓ All definitions valid!</color>");
        }
        
        [ContextMenu("Validation/Check For Duplicates")]
        private void CheckDuplicates()
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
            
            foreach (var kvp in idCount)
            {
                if (kvp.Value > 1)
                {
                    Debug.LogError($"<color=red>DUPLICATE ID: '{kvp.Key}' x{kvp.Value}</color>");
                    foundDuplicates = true;
                }
            }
            
            if (!foundDuplicates)
                Debug.Log("<color=green>✓ No duplicates found!</color>");
        }
        
        #endregion
        
        #region Context Menu
        
        [ContextMenu("Reload/Force Reload From Resources")]
        private void ForceReload()
        {
            _resourcesLoaded = false;
            _cachedResourceDefinitions = null;
            ClearCaches();
            Initialize();
            Debug.Log($"<color=cyan>✓ Reloaded! {_definitionLookup.Count} definitions</color>");
        }
        
        [ContextMenu("Info/Show Performance Stats")]
        private void ShowPerformanceStats()
        {
            var defMem = _definitionLookup.Count * 64; // ~64 bytes per entry
            var instMem = _instanceRegistry.Count * 512; // ~512 bytes per instance
            var typeMem = _definitionsByType.Count * 128; // ~128 bytes per type list
            
            Debug.Log("========== PERFORMANCE STATS ==========");
            Debug.Log($"Initialized: {_isInitialized}");
            Debug.Log($"Definitions: {_definitionLookup.Count} (~{defMem / 1024f:F2} KB)");
            Debug.Log($"Instances: {_instanceRegistry.Count} (~{instMem / 1024f:F2} KB)");
            Debug.Log($"Type Cache: {_definitionsByType.Count} types (~{typeMem / 1024f:F2} KB)");
            Debug.Log($"Total Memory: ~{(defMem + instMem + typeMem) / 1024f:F2} KB");
            Debug.Log($"Resource Cache: {(_resourcesLoaded ? "Loaded" : "Not Loaded")}");
            Debug.Log("=======================================");
        }
        
        #endregion
    }
}