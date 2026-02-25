using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;

namespace NightHunt.Gameplay.Input.Handlers.Interaction
{
    /// <summary>
    /// Scans for IInteractable objects within a sphere around the player each frame
    /// (throttled by <see cref="ScanInterval"/>).
    ///
    /// DESIGN (SRP):
    ///   - Owns ONLY proximity detection. No input, no inventory, no UI.
    ///   - Consumers subscribe to events or read <see cref="NearbyInteractables"/> /
    ///     <see cref="ClosestInteractable"/> each frame.
    ///
    /// USAGE:
    ///   Attach to the player GameObject alongside InteractionInputHandler.
    ///   Assign the correct <see cref="interactableLayerMask"/>.
    /// </summary>
    public class ProximityInteractScanner : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────────

        [Header("Scan Settings")]
        [Tooltip("Radius of the overlap-sphere used to find interactables.")]
        [SerializeField] private float scanRadius = 4f;

        [Tooltip("Seconds between scans. Lower = more responsive, higher = cheaper.")]
        [SerializeField] private float scanInterval = 0.15f;

        [Tooltip("Layer mask for interactable colliders.")]
        [SerializeField] private LayerMask interactableLayerMask = ~0;

        [Tooltip("Max objects checked per scan (pre-allocated buffer).")]
        [SerializeField] private int maxResults = 16;

        [Header("Debug")]
        [Tooltip("Print the nearby list to console on every change.")]
        [SerializeField] private bool logOnChange = false;

        [Tooltip("Draw wire-sphere gizmo in scene view.")]
        [SerializeField] private bool drawGizmos = true;

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Adjust at runtime if needed (e.g. perk system increases range).</summary>
        public float ScanRadius
        {
            get => scanRadius;
            set => scanRadius = Mathf.Max(0f, value);
        }

        /// <summary>All IInteractable objects currently within radius, sorted closest-first.</summary>
        public IReadOnlyList<IInteractable> NearbyInteractables => _nearby;

        /// <summary>The closest IInteractable, or null when none is in range.</summary>
        public IInteractable ClosestInteractable => _nearby.Count > 0 ? _nearby[0] : null;

        // ── Events ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired whenever the nearby list changes (items enter or leave range).
        /// Passes the new read-only list.
        /// </summary>
        public event Action<IReadOnlyList<IInteractable>> OnNearbyListChanged;

        /// <summary>
        /// Fired when the closest interactable changes (including to null).
        /// </summary>
        public event Action<IInteractable> OnClosestChanged;

        // ── Private ──────────────────────────────────────────────────────────────

        private readonly List<IInteractable> _nearby = new List<IInteractable>();
        private Collider[] _overlapBuffer;
        private float _nextScanTime;
        private IInteractable _previousClosest;

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            _overlapBuffer = new Collider[Mathf.Max(1, maxResults)];
        }

        private void Update()
        {
            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + scanInterval;
            PerformScan();
        }

        private void OnDisable()
        {
            ClearAll();
        }

        // ── Scan ─────────────────────────────────────────────────────────────────

        private void PerformScan()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position, scanRadius, _overlapBuffer, interactableLayerMask,
                QueryTriggerInteraction.Collide);

            // Collect unique IInteractable references from the hit colliders
            var found = new List<IInteractable>(hitCount);
            for (int i = 0; i < hitCount; i++)
            {
                var interactable = _overlapBuffer[i].GetComponentInParent<IInteractable>();
                if (interactable == null) continue;
                if (found.Contains(interactable)) continue; // already found via another collider
                found.Add(interactable);
            }

            // Sort by distance (closest first) using SqrMagnitude for speed
            found.Sort((a, b) =>
            {
                float da = SqrDistanceTo(a);
                float db = SqrDistanceTo(b);
                return da.CompareTo(db);
            });

            // Diff with previous list
            bool changed = HasListChanged(found);
            if (!changed) return;

            _nearby.Clear();
            _nearby.AddRange(found);
            OnNearbyListChanged?.Invoke(_nearby);

            if (logOnChange)
                LogNearby();

            // Notify closest changed
            IInteractable newClosest = _nearby.Count > 0 ? _nearby[0] : null;
            if (newClosest != _previousClosest)
            {
                _previousClosest = newClosest;
                OnClosestChanged?.Invoke(newClosest);
            }
        }

        /// <summary>
        /// Force-clear the list (called on disable / player death).
        /// </summary>
        public void ClearAll()
        {
            if (_nearby.Count == 0) return;
            _nearby.Clear();
            _previousClosest = null;
            OnNearbyListChanged?.Invoke(_nearby);
            OnClosestChanged?.Invoke(null);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private float SqrDistanceTo(IInteractable interactable)
        {
            if (interactable is Component c)
                return (c.transform.position - transform.position).sqrMagnitude;
            return float.MaxValue;
        }

        private bool HasListChanged(List<IInteractable> newList)
        {
            if (newList.Count != _nearby.Count) return true;
            for (int i = 0; i < newList.Count; i++)
                if (!ReferenceEquals(newList[i], _nearby[i])) return true;
            return false;
        }

        /// <summary>
        /// Print the current nearby list to the console.
        /// Called automatically when <see cref="logOnChange"/> is true,
        /// or call manually from InteractionInputHandler.
        /// </summary>
        public void LogNearby()
        {
            if (_nearby.Count == 0)
            {
                Debug.Log("[ProximityScanner] No interactables nearby.");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[ProximityScanner] {_nearby.Count} interactable(s) in range:");
            for (int i = 0; i < _nearby.Count; i++)
            {
                float dist = Mathf.Sqrt(SqrDistanceTo(_nearby[i]));
                sb.AppendLine($"  [{i}] {_nearby[i].InteractLabel}  ({dist:F1} m)");
            }
            Debug.Log(sb.ToString());
        }

        // ── Gizmos ───────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            // Outer scan radius — green
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.15f);
            Gizmos.DrawSphere(transform.position, scanRadius);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, scanRadius);

            // Lines to nearby items
            if (!Application.isPlaying) return;
            Gizmos.color = Color.yellow;
            foreach (var item in _nearby)
            {
                if (item is Component c)
                    Gizmos.DrawLine(transform.position, c.transform.position);
            }
        }
    }
}
