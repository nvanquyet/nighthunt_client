using UnityEngine;
using FOW;
using NightHunt.Gameplay.Character;

namespace NightHunt.Gameplay.Vision
{
    /// <summary>
    /// Wrapper for FogOfWarRevealer
    /// Manages vision revealer for players
    /// </summary>
    [RequireComponent(typeof(FogOfWarRevealer3D))]
    public class VisionRevealer : MonoBehaviour
    {
        private FogOfWarRevealer3D revealer;
        private CharacterStats characterStats;
        private float baseVisionRadius = 12f;

        private void Awake()
        {
            revealer = GetComponent<FogOfWarRevealer3D>();
            characterStats = GetComponent<CharacterStats>();

            if (revealer == null)
            {
                revealer = gameObject.AddComponent<FogOfWarRevealer3D>();
            }
        }

        private void Start()
        {
            // TODO: Load base vision from CharacterStatsConfig ScriptableObject or CharacterStats component
            // For now, use default value or get from CharacterStats component if available
            // baseVisionRadius should be set in Inspector or use CharacterStats.GetVisionRadius()

            UpdateVisionRadius();
        }

        private void Update()
        {
            UpdateVisionRadius();
        }

        /// <summary>
        /// Update vision radius from character stats
        /// </summary>
        private void UpdateVisionRadius()
        {
            if (revealer == null) return;

            float visionRadius = baseVisionRadius;
            if (characterStats != null)
            {
                visionRadius = characterStats.GetVisionRadius();
            }

            FogOfWarHelper.SetRayDistance(revealer, visionRadius);
        }

        /// <summary>
        /// Set vision radius manually
        /// </summary>
        public void SetVisionRadius(float radius)
        {
            if (revealer != null)
            {
                FogOfWarHelper.SetRayDistance(revealer, radius);
            }
        }

        /// <summary>
        /// Get current vision radius
        /// </summary>
        public float GetVisionRadius()
        {
            return FogOfWarHelper.GetRayDistance(revealer, baseVisionRadius);
        }
    }
}

