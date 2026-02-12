using UnityEngine;
using NightHunt.Inventory.Core.Data;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.Database
{
    /// <summary>
    /// Singleton database for all ItemDefinitions.
    /// Loads from Resources folder at runtime.
    /// Used by network sync to resolve item IDs.
    /// </summary>
    public class ItemDefinitionDatabase : MonoBehaviour
    {
        private static ItemDefinitionDatabase instance;
        public static ItemDefinitionDatabase Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<ItemDefinitionDatabase>();
                    
                    if (instance == null)
                    {
                        GameObject go = new GameObject("ItemDefinitionDatabase");
                        instance = go.AddComponent<ItemDefinitionDatabase>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }
        
        [Header("Configuration")]
        [SerializeField] private string resourcesPath = "Items"; // Path in Resources folder
        [SerializeField] private bool loadOnAwake = true;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // Database storage
        private Dictionary<string, ItemDefinition> itemDatabase = new Dictionary<string, ItemDefinition>();
        private bool isLoaded = false;
        
        // === Lifecycle ===
        
        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (loadOnAwake)
            {
                LoadAllDefinitions();
            }
        }
        
        // === Public API ===
        
        /// <summary>
        /// Load all ItemDefinitions from Resources folder.
        /// </summary>
        public void LoadAllDefinitions()
        {
            if (isLoaded)
            {
                Log("Database already loaded");
                return;
            }
            
            itemDatabase.Clear();
            
            // Load from Resources
            ItemDefinition[] definitions = Resources.LoadAll<ItemDefinition>(resourcesPath);
            
            if (definitions == null || definitions.Length == 0)
            {
                LogWarning($"No ItemDefinitions found in Resources/{resourcesPath}");
                return;
            }
            
            // Add to database
            foreach (var definition in definitions)
            {
                if (definition == null)
                    continue;
                
                if (string.IsNullOrEmpty(definition.ItemId))
                {
                    LogWarning($"ItemDefinition {definition.name} has no ItemId - skipping");
                    continue;
                }
                
                if (itemDatabase.ContainsKey(definition.ItemId))
                {
                    LogWarning($"Duplicate ItemId found: {definition.ItemId} - using first occurrence");
                    continue;
                }
                
                itemDatabase[definition.ItemId] = definition;
            }
            
            isLoaded = true;
            Log($"Loaded {itemDatabase.Count} ItemDefinitions from Resources/{resourcesPath}");
        }
        
        /// <summary>
        /// Get ItemDefinition by ID.
        /// </summary>
        public ItemDefinition GetDefinition(string itemId)
        {
            if (!isLoaded)
            {
                LogWarning("Database not loaded - loading now");
                LoadAllDefinitions();
            }
            
            if (string.IsNullOrEmpty(itemId))
                return null;
            
            if (itemDatabase.TryGetValue(itemId, out ItemDefinition definition))
            {
                return definition;
            }
            
            LogWarning($"ItemDefinition not found: {itemId}");
            return null;
        }
        
        /// <summary>
        /// Check if definition exists.
        /// </summary>
        public bool HasDefinition(string itemId)
        {
            if (!isLoaded)
                LoadAllDefinitions();
            
            return itemDatabase.ContainsKey(itemId);
        }
        
        /// <summary>
        /// Get all definitions.
        /// </summary>
        public ItemDefinition[] GetAllDefinitions()
        {
            if (!isLoaded)
                LoadAllDefinitions();
            
            return itemDatabase.Values.ToArray();
        }
        
        /// <summary>
        /// Get definitions by type.
        /// </summary>
        public ItemDefinition[] GetDefinitionsByType(Core.Enums.ItemType itemType)
        {
            if (!isLoaded)
                LoadAllDefinitions();
            
            return itemDatabase.Values
                .Where(d => d.ItemType == itemType)
                .ToArray();
        }
        
        /// <summary>
        /// Get definitions by rarity.
        /// </summary>
        public ItemDefinition[] GetDefinitionsByRarity(ItemRarity rarity)
        {
            if (!isLoaded)
                LoadAllDefinitions();
            
            return itemDatabase.Values
                .Where(d => d.Rarity == rarity)
                .ToArray();
        }
        
        /// <summary>
        /// Search definitions by name.
        /// </summary>
        public ItemDefinition[] SearchByName(string searchTerm)
        {
            if (!isLoaded)
                LoadAllDefinitions();
            
            if (string.IsNullOrEmpty(searchTerm))
                return new ItemDefinition[0];
            
            searchTerm = searchTerm.ToLower();
            
            return itemDatabase.Values
                .Where(d => d.DisplayName.ToLower().Contains(searchTerm))
                .ToArray();
        }
        
        /// <summary>
        /// Get count of loaded definitions.
        /// </summary>
        public int GetDefinitionCount()
        {
            return itemDatabase.Count;
        }
        
        /// <summary>
        /// Reload database (useful for editor updates).
        /// </summary>
        public void ReloadDatabase()
        {
            isLoaded = false;
            LoadAllDefinitions();
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                UnityEngine.Debug.Log($"[ItemDefinitionDatabase] {message}");
        }
        
        void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning($"[ItemDefinitionDatabase] {message}");
        }
        
        #if UNITY_EDITOR
        [ContextMenu("Debug: Print All Definitions")]
        void DebugPrintAll()
        {
            if (!isLoaded)
                LoadAllDefinitions();
            
            UnityEngine.Debug.Log("=== ITEM DEFINITION DATABASE ===");
            UnityEngine.Debug.Log($"Total Items: {itemDatabase.Count}");
            
            foreach (var kvp in itemDatabase)
            {
                var def = kvp.Value;
                UnityEngine.Debug.Log($"[{def.ItemType}] {def.DisplayName} ({def.ItemId}) - {def.Rarity}");
            }
        }
        
        [ContextMenu("Debug: Reload Database")]
        void DebugReload()
        {
            ReloadDatabase();
        }
        #endif
    }
}