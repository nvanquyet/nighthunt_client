using UnityEngine;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Placed on the MODEL PREFAB root (e.g. Soldier_LOD0_2.0_White.prefab).
    /// Exposes key skeleton Transforms as explicitly Inspector-assigned references.
    ///
    /// PURPOSE:
    ///   Provides a single, model-agnostic source of truth for bone references
    ///   that other systems (audio, VFX, etc.) need at runtime after the model
    ///   is dynamically loaded via PlayerModelLoader.
    ///
    ///   Using Inspector-assigned refs instead of searching by bone name means:
    ///     - Works with any skeleton naming convention.
    ///     - Misconfiguration is obvious in the Editor (missing ref = yellow warning).
    ///     - No silent null from a renamed bone found nowhere.
    ///
    /// SETUP (per model prefab):
    ///   1. Open the model prefab.
    ///   2. Add this component to the root GameObject (already done on Character 01).
    ///   3. Drag the correct bone Transforms into ankleLeft / ankleRight.
    ///      Character 01 / SciFi Bip001 rig:
    ///        "Bip001 L Foot"  →  ankleLeft
    ///        "Bip001 R Foot"  →  ankleRight
    ///   4. Optionally drag spine / head bones for other systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterRig : MonoBehaviour
    {
        [Header("Feet — used by CharacterAudioController for 3D footstep position")]
        [Tooltip("The left ankle / foot bone of this model's skeleton.")]
        public Transform ankleLeft;

        [Tooltip("The right ankle / foot bone of this model's skeleton.")]
        public Transform ankleRight;

        [Header("Spine / Head — reserved for future systems (VFX, hit reactions, etc.)")]
        [Tooltip("Optional: spine root bone. Leave null if not needed.")]
        public Transform spine;

        [Tooltip("Optional: head bone. Leave null if not needed.")]
        public Transform head;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (ankleLeft == null)
                Debug.LogWarning($"[CharacterRig] '{name}': ankleLeft is not assigned. " +
                                 $"Footstep audio will fall back to transform root position.", this);
            if (ankleRight == null)
                Debug.LogWarning($"[CharacterRig] '{name}': ankleRight is not assigned. " +
                                 $"Footstep audio will fall back to transform root position.", this);
        }
#endif
    }
}
