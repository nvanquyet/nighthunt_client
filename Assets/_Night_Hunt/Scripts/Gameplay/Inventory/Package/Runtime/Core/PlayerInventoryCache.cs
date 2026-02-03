using UnityEngine;
using NightHunt.Networking;
using NightHunt.Inventory.Domain;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Cache for player inventory components.
    /// Used by UI (which is separate from player Canvas) to access player inventory systems.
    /// </summary>
    public class PlayerInventoryCache : MonoBehaviour
    {
        private static PlayerInventoryCache instance;
        
        private NetworkPlayer cachedPlayer;
        private InventoryManager cachedInventoryManager;
        private EquipmentManager cachedEquipmentManager;
        private WeaponManager cachedWeaponManager;
        private QuickSlot.QuickSlotManager cachedQuickSlotManager;
        
        public static PlayerInventoryCache Instance => instance;
        
        public NetworkPlayer Player => cachedPlayer;
        public InventoryManager InventoryManager => cachedInventoryManager;
        public EquipmentManager EquipmentManager => cachedEquipmentManager;
        public WeaponManager WeaponManager => cachedWeaponManager;
        public QuickSlot.QuickSlotManager QuickSlotManager => cachedQuickSlotManager;
        
        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        
        /// <summary>
        /// Cache player and all inventory components.
        /// Call this when player spawns or when UI needs to access player.
        /// </summary>
        public void CachePlayer(NetworkPlayer player)
        {
            if (player == null)
            {
                ClearCache();
                return;
            }
            
            cachedPlayer = player;
            
            // Find components in hierarchy (they might be on child objects)
            cachedInventoryManager = ComponentFinder.FindInHierarchy<InventoryManager>(player);
            cachedEquipmentManager = ComponentFinder.FindInHierarchy<EquipmentManager>(player);
            cachedWeaponManager = ComponentFinder.FindInHierarchy<WeaponManager>(player);
            cachedQuickSlotManager = ComponentFinder.FindInHierarchy<QuickSlot.QuickSlotManager>(player);
            
            Debug.Log($"[PlayerInventoryCache] Cached player: {player.name}");
        }
        
        /// <summary>
        /// Clear cache when player disconnects.
        /// </summary>
        public void ClearCache()
        {
            cachedPlayer = null;
            cachedInventoryManager = null;
            cachedEquipmentManager = null;
            cachedWeaponManager = null;
            cachedQuickSlotManager = null;
        }
        
        /// <summary>
        /// Check if cache is valid (player exists and is active).
        /// </summary>
        public bool IsCacheValid()
        {
            return cachedPlayer != null && cachedPlayer.gameObject.activeInHierarchy;
        }
    }
}
