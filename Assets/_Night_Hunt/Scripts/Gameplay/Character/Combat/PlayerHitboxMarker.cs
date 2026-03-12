using UnityEngine;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Placed on each hitbox collider child of a player prefab.
    /// Links back to the root <see cref="PlayerHealthSystem"/> and marks
    /// whether hits on this collider count as a headshot.
    ///
    /// Inspector setup:
    ///   • IsHeadshot  — true on head colliders, false on body/legs.
    ///   • HealthSystem — drag the root PlayerHealthSystem here (auto-resolved if null).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHitboxMarker : MonoBehaviour
    {
        [Tooltip("True for head colliders; body / limb colliders should be false.")]
        [SerializeField] public bool IsHeadshot;

        [Tooltip("Root PlayerHealthSystem. Auto-resolved from parent if not assigned.")]
        [SerializeField] private PlayerHealthSystem _healthSystemRef;

        public PlayerHealthSystem HealthSystem
        {
            get
            {
                if (_healthSystemRef == null)
                    _healthSystemRef = ComponentResolver.Find<PlayerHealthSystem>(this)
        .InParent()
        .InRootChildren()
        .OrLogWarning("[Auto] PlayerHealthSystem not found")
        .Resolve();
                return _healthSystemRef;
            }
        }

        // Let physics ignore self-collisions on the same player via layer masks in the Physics settings.
    }
}
