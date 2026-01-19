using UnityEngine;
using FOW;
using NightHunt.Gameplay.Character;

namespace NightHunt.Gameplay.Vision
{
    /// <summary>
    /// Integration with FogOfWar plugin
    /// </summary>
    public class FogOfWarIntegration : MonoBehaviour
    {
        [Header("Fog Of War Settings")]
        [SerializeField] private FogOfWarRevealer3D fogOfWarRevealer;
        [SerializeField] private bool autoSetupRevealer = true;

        private CharacterStats characterStats;
        private VisionSystem visionSystem;

        private void Awake()
        {
            characterStats = GetComponent<CharacterStats>();
            visionSystem = GetComponent<VisionSystem>();

            if (autoSetupRevealer)
            {
                SetupRevealer();
            }
        }

        private void Start()
        {
            UpdateRevealerRadius();
        }

        private void Update()
        {
            // Update revealer radius based on vision stats
            UpdateRevealerRadius();
        }

        /// <summary>
        /// Setup FogOfWar revealer component
        /// </summary>
        private void SetupRevealer()
        {
            if (fogOfWarRevealer == null)
            {
                fogOfWarRevealer = GetComponent<FogOfWarRevealer3D>();
                if (fogOfWarRevealer == null)
                {
                    fogOfWarRevealer = gameObject.AddComponent<FogOfWarRevealer3D>();
                }
            }

            // Configure revealer settings
            if (fogOfWarRevealer != null)
            {
                // Set initial radius
                if (characterStats != null)
                {
                    FogOfWarHelper.SetRayDistance(fogOfWarRevealer, characterStats.GetVisionRadius());
                }
            }
        }

        /// <summary>
        /// Update revealer radius from vision stats
        /// </summary>
        private void UpdateRevealerRadius()
        {
            if (fogOfWarRevealer == null || visionSystem == null) return;

            float visionRadius = visionSystem.GetVisionRadius();
            FogOfWarHelper.SetRayDistance(fogOfWarRevealer, visionRadius);
        }

        /// <summary>
        /// Get FogOfWar revealer component
        /// </summary>
        public FogOfWarRevealer3D GetRevealer() => fogOfWarRevealer;
    }
}

