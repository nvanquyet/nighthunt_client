using UnityEngine;
using FishNet.Object;
using FOW;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Player
{
    /// <summary>
    /// PLAYER VISION SYSTEM - Fog of war and visibility
    /// 
    /// Server Authority:
    /// - Server tracks which players can see which entities
    /// - Vision checks performed on server for anti-cheat
    /// - Client receives visibility updates via VisionService
    /// 
    /// Responsibilities:
    /// - Define vision parameters (range, angle, obstacles)
    /// - Perform line-of-sight checks
    /// - Register with VisionService for fog of war
    /// </summary>
    [RequireComponent(typeof(NetworkPlayer))]
    public class PlayerVisionSystem : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkPlayer networkPlayer;
        [SerializeField] private FogOfWarRevealer3D fogOfWarRevealer3D;

        //[SerializeField] 
        
        
        public void Initialize()
        {
            
        }
    }
}