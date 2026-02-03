using UnityEngine;
using NightHunt.Networking;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Initializes PlayerInventoryCache when player spawns.
    /// Attach this to NetworkPlayer or a child object.
    /// </summary>
    public class PlayerInventoryCacheInitializer : MonoBehaviour
    {
        private NetworkPlayer networkPlayer;
        
        void Awake()
        {
            // Find NetworkPlayer in hierarchy
            networkPlayer = ComponentFinder.FindInHierarchy<NetworkPlayer>(this);
        }
        
        void Start()
        {
            if (networkPlayer != null && networkPlayer.IsLocalPlayer)
            {
                // Cache player for UI access
                if (PlayerInventoryCache.Instance != null)
                {
                    PlayerInventoryCache.Instance.CachePlayer(networkPlayer);
                }
            }
        }
        
        void OnDestroy()
        {
            // Clear cache when player is destroyed
            if (networkPlayer != null && networkPlayer.IsLocalPlayer)
            {
                if (PlayerInventoryCache.Instance != null)
                {
                    PlayerInventoryCache.Instance.ClearCache();
                }
            }
        }
    }
}
