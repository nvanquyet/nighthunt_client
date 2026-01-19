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
            // Load base vision from config
            var config = NightHunt.Data.GameConfigLoader.Instance?.GetCharacterConfig("CHAR_DEFAULT");
            if (config != null)
            {
                baseVisionRadius = config.BaseVisionRadius;
            }

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

