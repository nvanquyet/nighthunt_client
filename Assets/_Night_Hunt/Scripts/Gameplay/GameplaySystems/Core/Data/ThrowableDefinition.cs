using UnityEngine;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Throwable item definition (grenades, molotovs, flashbangs …)
    /// NEW FILE – drop into Core/Data alongside ConsumableDefinition.
    /// </summary>
    [CreateAssetMenu(fileName = "Throwable_", menuName = "GameplaySystems/Items/Throwable Definition")]
    public class ThrowableDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Throwable;

        [Header("Throw Settings")]
        [Tooltip("Prefab spawned at throw point. Must have Rigidbody + Projectile component.")]
        public GameObject ProjectilePrefab;

        [Tooltip("Launch force applied to the Rigidbody (m/s).")]
        [Min(1f)] public float ThrowForce = 15f;

        [Tooltip("Max effective distance for trajectory arc preview.")]
        [Min(1f)] public float MaxRange = 30f;

        [Tooltip("Time the player holds the item before they can throw (prepare animation window).")]
        [Min(0f)] public float PrepareTime = 0.5f;

        [Header("Projectile Behaviour")]
        [Tooltip("Governs what the projectile does on impact / detonation.")]
        public ThrowableType ThrowableType = ThrowableType.Grenade;

        [Tooltip("Base damage at explosion centre.")]
        [Min(0f)] public float Damage = 100f;

        [Tooltip("Radius for AoE damage / effect falloff (0 = no AoE).")]
        [Min(0f)] public float ExplosionRadius = 5f;

        [Tooltip("Seconds after spawn before automatic detonation. 0 = impact-only.")]
        [Min(0f)] public float FuseTime = 3f;

        [Tooltip("Allow projectile to bounce off surfaces (physic material ignored if false).")]
        public bool CanBounce = true;

        [Header("FX")]
        [Tooltip("VFX prefab instantiated at the explosion point.")]
        public GameObject ExplosionEffectPrefab;

        [Tooltip("Sound played the moment the player releases the item.")]
        public AudioClip ThrowSound;

        [Tooltip("Sound played on impact or explosion.")]
        public AudioClip ImpactSound;

        // ── Editor helpers ─────────────────────────────────────────────────────
#if UNITY_EDITOR
        [ContextMenu("Setup Default Frag Grenade")]
        private void SetupFrag()
        {
            DisplayName = "Frag Grenade"; Description = "Explosive grenade – 3 s fuse.";
            IsStackable = true; MaxStackSize = 3; Weight = 0.4f;
            CanUseWhileMoving = true;
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.QuickSlot };
            ThrowableType = ThrowableType.Grenade;
            ThrowForce = 15f; ExplosionRadius = 5f; FuseTime = 3f; Damage = 100f; CanBounce = true;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Setup Default Smoke Grenade")]
        private void SetupSmoke()
        {
            DisplayName = "Smoke Grenade"; Description = "Deploys smoke screen for 15 s.";
            IsStackable = true; MaxStackSize = 3; Weight = 0.3f;
            ThrowableType = ThrowableType.Smoke;
            ThrowForce = 12f; ExplosionRadius = 8f; FuseTime = 2f; Damage = 0f; CanBounce = false;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// Throwable behaviour categories.
    /// ⚠ Never reorder or renumber existing entries – only append new ones.
    /// </summary>
    public enum ThrowableType
    {
        Grenade     = 0,   // Timed AoE explosion
        Flashbang   = 1,   // Blind / stun
        Smoke       = 2,   // Smoke screen
        Incendiary  = 3,   // Fire – damage over time
        Gas         = 4,   // Poison cloud
        Sticky      = 5,   // Attaches to surface before detonating
        Impact      = 6,   // Detonates on first collision
        Timed       = 7,   // Explicit timer alias
        Proximity   = 8,   // Detonates when enemy enters radius
    }
}