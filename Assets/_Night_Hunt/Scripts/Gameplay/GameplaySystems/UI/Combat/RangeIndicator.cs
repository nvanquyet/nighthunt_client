using UnityEngine;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// World-space range indicator that follows the local player.
    ///
    /// Used by both <see cref="FireButton"/> (shown while finger/mouse is held)
    /// and <see cref="QuickSlotAimController"/> (shown while aiming a throwable).
    ///
    /// Visual is fully designer-controlled: place any child GO (mesh, decal, particle, etc.)
    /// under this GameObject. <see cref="SetRange"/> scales the root transform uniformly
    /// so the visual matches VisionRange in world units.
    ///
    /// Scene setup:
    ///   1. Create an empty GO in the world (NOT in Canvas).
    ///   2. Add this component.
    ///   3. Add your visual as a child (ring mesh / decal / VFX — anything).
    ///   4. Assign this GO to FireButton._rangeIndicator and/or QuickSlotAimController._rangeIndicator.
    ///   5. Call SetFollowTarget(playerTransform) + SetRange(visionRange) after player spawns.
    /// </summary>
    public class RangeIndicator : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Geometry")]
        [Tooltip("World units that map to localScale = 1. " +
                 "E.g. if your ring mesh has diameter 2 at scale 1, set this to 1. " +
                 "SetRange(visionRange) will scale accordingly.")]
        [SerializeField] private float _baseRadius = 1f;

        [Tooltip("Y position of this GO relative to the player's feet. " +
                 "Keeps the ring on the ground even if the player origin is at waist height.")]
        [SerializeField] private float _yOffset = 0.05f;

        // ── Runtime ────────────────────────────────────────────────────────────

        private Transform _followTarget;
        private float     _range;

        // ── Unity Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            gameObject.SetActive(false);   // hidden by default — Show() / Hide() control visibility
        }

        private void LateUpdate()
        {
            if (_followTarget == null) return;
            Vector3 p = _followTarget.position;
            p.y = p.y + _yOffset;
            transform.position = p;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Bind the player transform to follow. Call once after player spawns.</summary>
        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
        }

        /// <summary>
        /// Set the indicator radius in world units (= VisionRange from stat system).
        /// Scales the root GO uniformly so any child visual matches the radius.
        /// </summary>
        public void SetRange(float worldRadius)
        {
            _range = Mathf.Max(0f, worldRadius);
            float s = _baseRadius > 0f ? _range / _baseRadius : _range;
            transform.localScale = Vector3.one * s;
        }

        /// <summary>Make the indicator visible. Call on PointerDown / begin aim.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>Hide the indicator. Call on PointerUp / confirm / cancel aim.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Convenience: set range then show.</summary>
        public void ShowWithRange(float worldRadius)
        {
            SetRange(worldRadius);
            Show();
        }
    }
}
