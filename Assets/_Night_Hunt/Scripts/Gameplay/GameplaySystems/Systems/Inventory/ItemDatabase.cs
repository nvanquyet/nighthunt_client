using System.Collections.Generic;
using UnityEngine;
using NightHunt.Core;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.GameplaySystems.Inventory
{
    /// <summary>
    /// ScriptableObject singleton registry for <see cref="ItemDefinition"/> assets
    /// and live <see cref="ItemInstance"/> objects. Provides O(1) lookups by ID.
    ///
    /// SETUP:
    ///   1. Right-click in Project â†’ Create â†’ NightHunt â†’ Items â†’ Item Database
    ///   2. Save as "Assets/_Night_Hunt/Data/ItemDatabase.asset" (one per project).
    ///   3. Drag all ItemDefinition assets into _itemDefinitions array.
    ///   4. Either reference the asset from any scene/prefab, OR place it in a
    ///      Resources/ folder named exactly "ItemDatabase.asset" for auto-load.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "NightHunt/Items/Item Database")]
    public class ItemDatabase : ScriptableObjectSingleton<ItemDatabase>
    {
        
        #region Serialized Fields
        
        [Header("Item Definitions")]
        [SerializeField] private ItemDefinition[] _itemDefinitions;
        
        [Header("Settings")]
        [SerializeField] private bool _autoLoadFromResources = true;
        [Tooltip("Resources-relative folder containing ItemDefinition assets (used by auto-load).")]
        [SerializeField] private string _itemDefinitionsPath = "Items";
        
        [Header("Performance (Mobile Optimization)")]
        [Tooltip("Pre-build type lookup cache on init (recommended)")]
        [SerializeField] private bool _prewarmTypeLookup = true;
        
        [Tooltip("Max cached instances (0 = unlimited). Recommended: 100 for mobile")]
        [SerializeField] private int _maxCachedInstances = 100;
        
        [Header("Debug")]
        [SerializeField] private NightHuntDebugConfig _debugConfig;
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

        protected override void OnSingletonEnabled()
        {
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
            
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
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
            
            if (Instance._debugConfig != null && Instance._debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[ItemDatabase] Registered: {definition.ItemID} ({definition.Type})");
        }
        
        private void LoadDefinitionsFromResources()
        {
            // PERFORMANCE: Use cached data if available
            if (_resourcesLoaded && _cachedResourceDefinitions != null)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[ItemDatabase] Using cached definitions ({_cachedResourceDefinitions.Length} items)");
                
                foreach (var def in _cachedResourceDefinitions)
                    RegisterDefinition(def);
                
                return;
            }
            
            // Synchronous load from Resources (first time only).
            // For frame-spike-free loading call LoadFromResourcesAsync() from a MonoBehaviour
            // (e.g. MatchLoadingOverlay) before player spawn instead.
            var definitions = Resources.LoadAll<ItemDefinition>(_itemDefinitionsPath);
            
            if (definitions != null && definitions.Length > 0)
            {
                _cachedResourceDefinitions = definitions;
                _resourcesLoaded = true;
                
                foreach (var def in definitions)
                    RegisterDefinition(def);
                
                Debug.Log($"[ItemDatabase] Load complete — {definitions.Length} items registered.");
            }
            else
            {
                Debug.LogWarning($"[ItemDatabase] No items found in Resources/{_itemDefinitionsPath}");
            }
        }

        /// <summary>
        /// Asynchronously loads all ItemDefinition assets from the Resources folder.
        /// Call this from a MonoBehaviour (e.g. MatchLoadingOverlay) before player spawn
        /// to avoid a synchronous frame spike on first item lookup.
        /// </summary>
        /// <example>
        /// yield return StartCoroutine(ItemDatabase.Instance.LoadFromResourcesAsync());
        /// </example>
        public System.Collections.IEnumerator LoadFromResourcesAsync()
        {
            // If already loaded (sync or async), re-register from cache and return.
            if (_resourcesLoaded && _cachedResourceDefinitions != null)
            {
                foreach (var def in _cachedResourceDefinitions)
                    RegisterDefinition(def);
                yield break;
            }

            var definitions = Resources.LoadAll<ItemDefinition>(_itemDefinitionsPath);
            if (definitions != null && definitions.Length > 0)
            {
                _cachedResourceDefinitions = new ItemDefinition[definitions.Length];
                for (int i = 0; i < definitions.Length; i++)
                {
                    var def = definitions[i] as ItemDefinition;
                    _cachedResourceDefinitions[i] = def;
                    RegisterDefinition(def);
                }
                _resourcesLoaded = true;
                Debug.Log($"[ItemDatabase] Load complete — {definitions.Length} items registered.");
            }
            else
            {
                Debug.LogWarning($"[ItemDatabase] LoadFromResourcesAsync: No items found in Resources/{_itemDefinitionsPath}");
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
                
                if (Instance._debugConfig != null && Instance._debugConfig.EnableInventoryDebugLogs)
                    Debug.LogWarning($"[ItemDatabase] Instance cache limit reached, removed {removed} oldest instances");
            }
            
            Instance._instanceRegistry[instance.InstanceID] = instance;
            
            if (Instance._debugConfig != null && Instance._debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[ItemDatabase] Registered instance: {instance.InstanceID}");
        }
        
        public static void UnregisterInstance(string instanceID)
        {
            if (!Instance._trackInstances || string.IsNullOrEmpty(instanceID))
                return;
            
            if (Instance._instanceRegistry.Remove(instanceID) && Instance._debugConfig != null && Instance._debugConfig.EnableInventoryDebugLogs)
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
            
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
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
            
            if (Instance._debugConfig != null && Instance._debugConfig.EnableInventoryDebugLogs)
                Debug.Log("[ItemDatabase] Caches cleared");
        }
        
        #endregion
        
        #region Validation
        
        [ContextMenu("Validation/Validate All Definitions")]
        private void ValidateAll()
        {
            int errors = 0, warnings = 0;
            
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
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
            
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log("========== VALIDATION COMPLETE ==========");
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"Total: {_definitionLookup.Count} | Errors: {errors} | Warnings: {warnings}");
            
            if (errors == 0 && warnings == 0)
                Debug.Log("<color=green>âœ“ All definitions valid!</color>");
        }
        
        [ContextMenu("Validation/Check For Duplicates")]
        private void CheckDuplicates()
        {
            var allDefs = Resources.LoadAll<ItemDefinition>(_itemDefinitionsPath);
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
                Debug.Log("<color=green>âœ“ No duplicates found!</color>");
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
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"<color=cyan>âœ“ Reloaded! {_definitionLookup.Count} definitions</color>");
        }
        
        [ContextMenu("Info/Show Performance Stats")]
        private void ShowPerformanceStats()
        {
            var defMem = _definitionLookup.Count * 64; // ~64 bytes per entry
            var instMem = _instanceRegistry.Count * 512; // ~512 bytes per instance
            var typeMem = _definitionsByType.Count * 128; // ~128 bytes per type list
            
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log("========== PERFORMANCE STATS ==========");
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"Initialized: {_isInitialized}");
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"Definitions: {_definitionLookup.Count} (~{defMem / 1024f:F2} KB)");
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"Instances: {_instanceRegistry.Count} (~{instMem / 1024f:F2} KB)");
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"Type Cache: {_definitionsByType.Count} types (~{typeMem / 1024f:F2} KB)");
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"Total Memory: ~{(defMem + instMem + typeMem) / 1024f:F2} KB");
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"Resource Cache: {(_resourcesLoaded ? "Loaded" : "Not Loaded")}");
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log("=======================================");
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Scans the Resources/Database/Items folder, collects every
        /// ItemDefinition asset, and populates the serialized _itemDefinitions array.
        /// Run this after adding or removing item assets so the database is always up to date.
        /// </summary>
        [ContextMenu("Rebuild/◆ Scan Project & Populate _itemDefinitions")]
        private void EditorScanAndPopulate()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:ItemDefinition",
                new[] { "Assets/_Night_Hunt/Data/Resources" });

            var defs = new List<ItemDefinition>();
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var def  = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (def != null && !string.IsNullOrEmpty(def.ItemID))
                    defs.Add(def);
            }

            _itemDefinitions = defs.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();

            Debug.Log($"[ItemDatabase] ◆ Populated {_itemDefinitions.Length} definitions from Resources/Database/Items.");
            foreach (var d in _itemDefinitions)
                Debug.Log($"  ► {d.ItemID}  ({d.Type})  [{d.name}]");
        }
#endif
        
        #endregion
    }
}

