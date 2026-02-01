using UnityEngine;
using NightHunt.Gameplay.Character;
using NightHunt.Data;
using System.Collections;
using System.Collections.Generic;

namespace NightHunt.Gameplay.Vision
{
    /// <summary>
    /// Manages vision/fog of war system
    /// Handles vision radius, fog of war, and vision modifiers
    /// </summary>
    public class VisionSystem : MonoBehaviour
    {
        [Header("Vision Settings")]
        [SerializeField] private float baseVisionRadius = 12f;
        [SerializeField] private LayerMask visionBlockingLayers = 1 << 0; // Default layer
        [SerializeField] private bool useFogOfWar = true;

        [Header("Visual")]
        [SerializeField] private GameObject visionIndicatorPrefab;
        [SerializeField] private Material fogOfWarMaterial;

        private CharacterStats characterStats;
        private float currentVisionRadius;
        private List<VisionModifierData> activeModifiers = new List<VisionModifierData>();

        // Fog of war
        private Texture2D fogOfWarTexture;
        private bool[,] revealedCells;
        private int textureSize = 256;

        private void Awake()
        {
            characterStats = GetComponent<CharacterStats>();
            currentVisionRadius = baseVisionRadius;
        }

        private void Start()
        {
            // TODO: Load base vision from CharacterStatsConfig ScriptableObject or CharacterStats component
            // For now, use default value or get from CharacterStats component if available
            // baseVisionRadius should be set in Inspector or use CharacterStats.GetVisionRadius()
            // currentVisionRadius = baseVisionRadius;

            // Initialize fog of war
            if (useFogOfWar)
            {
                InitializeFogOfWar();
            }
        }

        private void Update()
        {
            UpdateVisionRadius();
            UpdateFogOfWar();
        }

        /// <summary>
        /// Update vision radius based on stats and modifiers
        /// </summary>
        private void UpdateVisionRadius()
        {
            if (characterStats != null)
            {
                currentVisionRadius = characterStats.GetVisionRadius();
            }
            else
            {
                currentVisionRadius = baseVisionRadius;
            }

            // Apply modifiers
            float finalRadius = currentVisionRadius;
            foreach (var modifier in activeModifiers)
            {
                if (modifier.OpType == "Multiply")
                {
                    finalRadius *= modifier.Value;
                }
                else if (modifier.OpType == "Add")
                {
                    finalRadius += modifier.Value;
                }
            }

            currentVisionRadius = Mathf.Max(0f, finalRadius);
        }

        /// <summary>
        /// Apply vision modifier
        /// </summary>
        public void ApplyVisionModifier(string modifierId)
        {
            // TODO: Implement VisionModifierConfig ScriptableObject system to replace GameConfigLoader
            // For now, vision modifiers are disabled
            return;
            /* OLD CODE - REMOVED (GameConfigLoader dependency)
            var modifier = GameConfigLoader.Instance?.ConfigData?.VisionModifiers?.Find(m => m.ModifierId == modifierId);
            if (modifier == null) return;
            */

            // TODO: Implement vision modifier system when VisionModifierConfig ScriptableObject is implemented
            // Check if already applied and if stackable
            // var existing = activeModifiers.Find(m => m.ModifierId == modifierId);
            // if (existing != null && !modifier.Stackable)
            // {
            //     return; // Already applied and not stackable
            // }
            //
            // activeModifiers.Add(modifier);
            //
            // // Remove after duration if temporary
            // if (modifier.Duration > 0f)
            // {
            //     StartCoroutine(RemoveModifierAfterDuration(modifierId, modifier.Duration));
            // }
        }

        /// <summary>
        /// Remove vision modifier
        /// </summary>
        public void RemoveVisionModifier(string modifierId)
        {
            activeModifiers.RemoveAll(m => m.ModifierId == modifierId);
        }

        private IEnumerator RemoveModifierAfterDuration(string modifierId, float duration)
        {
            yield return new WaitForSeconds(duration);
            RemoveVisionModifier(modifierId);
        }

        /// <summary>
        /// Check if a position is visible (within vision radius and not blocked)
        /// </summary>
        public bool IsPositionVisible(Vector3 position)
        {
            float distance = Vector3.Distance(transform.position, position);
            if (distance > currentVisionRadius)
            {
                return false;
            }

            // Check line of sight
            Vector3 direction = (position - transform.position).normalized;
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, distance, visionBlockingLayers))
            {
                // Something is blocking vision
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get vision radius
        /// </summary>
        public float GetVisionRadius() => currentVisionRadius;

        /// <summary>
        /// Initialize fog of war system
        /// </summary>
        private void InitializeFogOfWar()
        {
            revealedCells = new bool[textureSize, textureSize];
            fogOfWarTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        }

        /// <summary>
        /// Update fog of war based on current position
        /// </summary>
        private void UpdateFogOfWar()
        {
            if (!useFogOfWar || fogOfWarTexture == null) return;

            // Reveal cells within vision radius
            Vector3 worldPos = transform.position;
            int centerX = Mathf.RoundToInt(worldPos.x);
            int centerZ = Mathf.RoundToInt(worldPos.z);

            int radius = Mathf.RoundToInt(currentVisionRadius);

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    float distance = Mathf.Sqrt(x * x + z * z);
                    if (distance <= radius)
                    {
                        int texX = centerX + x + textureSize / 2;
                        int texZ = centerZ + z + textureSize / 2;

                        if (texX >= 0 && texX < textureSize && texZ >= 0 && texZ < textureSize)
                        {
                            revealedCells[texX, texZ] = true;
                        }
                    }
                }
            }

            // Update texture
            UpdateFogOfWarTexture();
        }

        private void UpdateFogOfWarTexture()
        {
            if (fogOfWarTexture == null) return;

            for (int x = 0; x < textureSize; x++)
            {
                for (int z = 0; z < textureSize; z++)
                {
                    Color color = revealedCells[x, z] ? Color.clear : Color.black;
                    fogOfWarTexture.SetPixel(x, z, color);
                }
            }

            fogOfWarTexture.Apply();
        }

        /// <summary>
        /// Get fog of war texture
        /// </summary>
        public Texture2D GetFogOfWarTexture() => fogOfWarTexture;
    }
}

