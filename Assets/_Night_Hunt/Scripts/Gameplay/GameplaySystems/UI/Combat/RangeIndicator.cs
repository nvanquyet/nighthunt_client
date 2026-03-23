using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// World-space range indicator that follows the local player.
    ///
    /// Used by both <see cref="FireButton"/> (shown while finger/mouse is held)
    /// and <see cref="ItemAimController"/> (shown while aiming a throwable).
    ///
    /// Visual is fully designer-controlled: place any child GO (mesh, decal, particle, etc.)
    /// under this GameObject. <see cref="SetRange"/> scales the root transform uniformly
    /// so the visual matches VisionRange in world units.
    ///
    /// Scene setup:
    ///   1. Create an empty GO in the world (NOT in Canvas).
    ///   2. Add this component.
    ///   3. Add your visual as a child (ring mesh / decal / VFX — anything).
    ///   4. Assign this GO to FireButton._rangeIndicator and/or ItemAimController._rangeIndicator.
    ///   5. Call SetFollowTarget(playerTransform) + SetRange(visionRange) after player spawns.
    /// </summary>
    public class RangeIndicator : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Geometry")]
        [Tooltip("World units that map to localScale = 1. " +
                 "E.g. if your ring mesh has radius 5 at scale 1, set this to 5. " +
                 "Use [Auto-detect Base Radius] context-menu to measure it automatically.")]
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
            float newRange = Mathf.Max(0f, worldRadius);
            // Skip if range unchanged (avoids redundant transform.localScale writes → less flicker).
            if (Mathf.Approximately(_range, newRange)) return;

            _range = newRange;
            float s = _baseRadius > 0f ? _range / _baseRadius : _range;
            transform.localScale = Vector3.one * s;
        }

        /// <summary>Make the indicator visible. Call on PointerDown / begin aim.</summary>
        public void Show()
        {
            // Skip if already active (avoids redundant SetActive calls).
            if (gameObject.activeSelf) return;
            gameObject.SetActive(true);
        }

        /// <summary>Hide the indicator. Call on PointerUp / confirm / cancel aim.</summary>
        public void Hide()
        {
            // Skip if already inactive.
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
        }

        /// <summary>Convenience: set range then show.</summary>
        public void ShowWithRange(float worldRadius)
        {
            SetRange(worldRadius);
            Show();
        }

        // ── Editor helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// [Inspector context-menu] Automatically measures the child Renderer's XZ extent
        /// at scale (1,1,1) and assigns it to _baseRadius.
        /// Run this once after placing your ring mesh child, then never touch _baseRadius again.
        /// </summary>
        [ContextMenu("Auto-detect Base Radius from Child Renderer")]
        private void AutoDetectBaseRadius()
        {
            var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[RangeIndicator] No child Renderer found — cannot auto-detect.");
                return;
            }

            // Temporarily reset scale so bounds are in true local mesh units.
            Vector3 savedScale = transform.localScale;
            transform.localScale = Vector3.one;

            // Encapsulate all child renderer bounds.
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            transform.localScale = savedScale;

            // Radius = largest extent on X or Z (ring is in XZ plane).
            float detectedRadius = Mathf.Max(combined.extents.x, combined.extents.z);
            if (detectedRadius <= 0f)
            {
                Debug.LogWarning("[RangeIndicator] Detected radius is 0 — mesh may have no geometry.");
                return;
            }

            _baseRadius = detectedRadius;
            Debug.Log($"[RangeIndicator] Auto-detected _baseRadius = {_baseRadius:F4} world units at scale 1. " +
                      $"Apply SetRange({_range:F1}) → localScale = {(_range / _baseRadius):F3}");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

#if UNITY_EDITOR
        // Draws two circles in Scene view when the GO is selected:
        //   YELLOW  = reference circle at exact _range world radius (what you want visually).
        //   CYAN    = circle at _baseRadius world units (radius of ring mesh at scale 1).
        // If the yellow circle matches the ring mesh visual → _baseRadius is correct.
        // If the ring is INSIDE yellow → _baseRadius is too small (increase it).
        // If the ring is OUTSIDE yellow → _baseRadius is too large (decrease it).
        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position;

            // Yellow: expected world radius = _range
            if (_range > 0f)
            {
                Handles.color = new Color(1f, 0.9f, 0f, 0.9f);
                Handles.DrawWireDisc(center, Vector3.up, _range);
                Handles.Label(center + Vector3.right * _range,
                    $" ← range {_range:F1} u\n   (scale {(_baseRadius > 0f ? _range / _baseRadius : 0f):F2}×)",
                    EditorStyles.miniLabel);
            }

            // Cyan: base radius (mesh radius at scale 1) — shows what scale=1 looks like
            Handles.color = new Color(0.2f, 0.9f, 1f, 0.5f);
            Handles.DrawWireDisc(center, Vector3.up, _baseRadius);
            Handles.Label(center + Vector3.forward * _baseRadius,
                $" ← _baseRadius {_baseRadius:F2}",
                EditorStyles.miniLabel);
        }
#endif
    }
}
