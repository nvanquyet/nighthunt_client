using UnityEngine;
using FishNet.Object;
using NightHunt.Gameplay.Core;

namespace NightHunt.Gameplay.Core
{
    /// <summary>
    /// Network-aware initializer cho gameplay systems
    /// Chỉ initialize trên server
    /// </summary>
    public class GameplayInitializer : NetworkBehaviour
    {
        [Header("Bootstrap Reference")]
        [SerializeField] private GameplayBootstrap bootstrap;

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Initialize bootstrap on server
            if (bootstrap == null)
            {
                bootstrap = FindFirstObjectByType<GameplayBootstrap>();
            }

            if (bootstrap != null && !bootstrap.IsInitialized)
            {
                bootstrap.Initialize();
                Debug.Log("[GameplayInitializer] Gameplay systems initialized on server.");
            }
            else
            {
                Debug.LogWarning("[GameplayInitializer] GameplayBootstrap not found or already initialized!");
            }
        }
    }
}

