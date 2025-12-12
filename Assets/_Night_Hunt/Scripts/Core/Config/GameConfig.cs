using UnityEngine;

namespace NightHunt.Core.Config
{
    /// <summary>
    /// Game configuration - designer-friendly settings
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "NightHunt/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Network Settings")]
        public int maxPlayers = 20;
        public int tickRate = 30;
        public float reconciliationThreshold = 0.5f;
        
        [Header("Performance")]
        public int targetFrameRate = 60;
        public bool useObjectPooling = true;
        public int poolInitialSize = 50;
        
        [Header("Mobile Optimization")]
        public bool adaptiveQuality = true;
        public float mobileResolutionScale = 0.8f;
        public bool useDynamicBatching = true;
        
        [Header("Vision System")]
        public float fogUpdateRate = 0.1f;
        public int visionGridSize = 2;
        public LayerMask visionBlockingLayers;
        
        [Header("Input")]
        public float joystickDeadzone = 0.15f;
        public float mouseSensitivity = 1f;
        public bool invertYAxis = false;
    }
}