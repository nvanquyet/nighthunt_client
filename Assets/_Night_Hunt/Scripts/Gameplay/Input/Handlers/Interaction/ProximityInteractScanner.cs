using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Utilities;

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
        [FormerlySerializedAs("scanRadius")]
        [SerializeField] private float _scanRadius = 4f;

        [Tooltip("Seconds between scans. Lower = more responsive, higher = cheaper.")]
        [FormerlySerializedAs("scanInterval")]
        [SerializeField] private float _scanInterval = 0.15f;

        [Tooltip("Layer mask for interactable colliders.")]
        [SerializeField] private LayerMask interactableLayerMask = ~0;

        [Tooltip("Max objects checked per scan (pre-allocated buffer).")]
        [FormerlySerializedAs("maxResults")]
        [SerializeField] private int _maxResults = 16;

        [Header("Debug")]
        [Tooltip("Print the nearby list to console on every change.")]
        [SerializeField] private bool logOnChange = false;

        [Tooltip("Draw wire-sphere gizmo in scene view.")]
        [SerializeField] private bool drawGizmos = true;

        [Tooltip("Show a simple OnGUI overlay listing nearby interactables (editor/debug only).")]
        [SerializeField] private bool showDebugUI = false;

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Adjust at runtime if needed (e.g. perk system increases range).</summary>
        public float ScanRadius
        {
            get => _scanRadius;
            set => _scanRadius = Mathf.Max(0f, value);
        }

        /// <summary>All IInteractable objects currently within radius, sorted closest-first.</summary>
        public IReadOnlyList<IInteractable> NearbyInteractables => _nearby;

        /// <summary>The closest IInteractable, or null when none is in range.</summary>
        public IInteractable ClosestInteractable => _nearby.Count > 0 ? _nearby[0] : null;

        /// <summary>
        /// Tất cả ILootable (rương / xác) hợp lệ trong bán kính, sắp xếp theo khoảng cách.
        /// Mỗi phần tử đã implements GetStorage() → có thể đọc items ngay không cần cast.
        /// </summary>
        public IReadOnlyList<ILootable> NearbyLootables => _nearbyLootables;

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

        /// <summary>
        /// Fired khi danh sách lootable gần đây thay đổi (rương / xác lịt vào hoặc ra khỏi tầm).
        /// Truy cập GetStorage() trực tiếp từ mỗi ILootable trong list để lấy danh sách item bên trong.
        /// </summary>
        public event Action<IReadOnlyList<ILootable>> OnNearbyLootablesChanged;

        // ── Private ──────────────────────────────────────────────────────────────

        private readonly List<IInteractable> _nearby = new List<IInteractable>();
        private readonly List<ILootable> _nearbyLootables = new List<ILootable>();
        private Collider[] _overlapBuffer;
        private float _nextScanTime;
        private IInteractable _previousClosest;

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            _overlapBuffer = new Collider[Mathf.Max(1, _maxResults)];
        }

        private void Update()
        {
            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + _scanInterval;
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
                transform.position, _scanRadius, _overlapBuffer, interactableLayerMask,
                QueryTriggerInteraction.Collide);

            // Collect unique IInteractable references from the hit colliders
            var found = new List<IInteractable>(hitCount);
            for (int i = 0; i < hitCount; i++)
            {
                // Silent resolve — it is normal for colliders (terrain, player body, etc.)
                // to not implement IInteractable. No warning needed.
                var interactable = ComponentResolver.Find<IInteractable>(_overlapBuffer[i])
                                                    .InParent()
                                                    .InRootChildren()
                                                    .Resolve();
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

            // Cập nhật danh sách lootable riêng (rương / xác)
            var lootables = new List<ILootable>();
            foreach (var item in _nearby)
            {
                if (item is ILootable lootable)
                    lootables.Add(lootable);
            }
            bool lootChanged = HasLootableListChanged(lootables);
            if (lootChanged)
            {
                _nearbyLootables.Clear();
                _nearbyLootables.AddRange(lootables);
                OnNearbyLootablesChanged?.Invoke(_nearbyLootables);
            }

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
            if (_nearby.Count == 0 && _nearbyLootables.Count == 0) return;
            _nearby.Clear();
            _nearbyLootables.Clear();
            _previousClosest = null;
            OnNearbyListChanged?.Invoke(_nearby);
            OnClosestChanged?.Invoke(null);
            OnNearbyLootablesChanged?.Invoke(_nearbyLootables);
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

        private bool HasLootableListChanged(List<ILootable> newList)
        {
            if (newList.Count != _nearbyLootables.Count) return true;
            for (int i = 0; i < newList.Count; i++)
                if (!ReferenceEquals(newList[i], _nearbyLootables[i])) return true;
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
                sb.Append($"  [{i}] {_nearby[i].InteractLabel}  ({dist:F1} m)");

                // Nếu là lootable thì log thêm số item bên trong
                if (_nearby[i] is ILootable lootable)
                {
                    var storage = lootable.GetStorage();
                    sb.Append($"  | IsOpen={lootable.IsOpen}, Items={storage.Count}");
                    foreach (var item in storage)
                        sb.Append($" [{item.DefinitionID}×{item.Quantity}]");
                }

                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
        }

        // ── Gizmos ───────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!showDebugUI || !Application.isPlaying)
                return;

            const float width = 320f;
            const float lineHeight = 20f;

            float x = 10f;
            float y = 10f;

            // Đếm trước tổng số dòng cần hiển thị (có thể có sub-lines cho lootable)
            int totalLines = Mathf.Max(1, _nearby.Count);
            foreach (var it in _nearby)
            {
                if (it is ILootable l && l.GetStorage().Count > 0)
                    totalLines += l.GetStorage().Count;
            }

            float boxHeight = lineHeight + totalLines * lineHeight;
            GUI.Box(new Rect(x, y, width, boxHeight), "Nearby Interactables");
            y += lineHeight;

            if (_nearby.Count == 0)
            {
                GUI.Label(new Rect(x + 5f, y, width - 10f, lineHeight), "(none)");
                return;
            }

            for (int i = 0; i < _nearby.Count; i++)
            {
                var it = _nearby[i];
                string label = it?.InteractLabel ?? "<null>";
                float dist = Mathf.Sqrt(SqrDistanceTo(it));

                string suffix = "";
                if (it is ILootable loot)
                    suffix = $"  [{(loot.IsOpen ? "OPEN" : "CLOSED")} {loot.GetStorage().Count} items]";

                GUI.Label(new Rect(x + 5f, y, width - 10f, lineHeight),
                    $"[{i}] {label} ({dist:F1} m){suffix}");
                y += lineHeight;

                // Sub-lines: hiển thị từng item bên trong lootable
                if (it is ILootable lootable && lootable.GetStorage().Count > 0)
                {
                    foreach (var item in lootable.GetStorage())
                    {
                        GUI.Label(new Rect(x + 20f, y, width - 25f, lineHeight),
                            $"  • {item.DefinitionID} ×{item.Quantity}");
                        y += lineHeight;
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            // Outer scan radius — green
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.15f);
            Gizmos.DrawSphere(transform.position, _scanRadius);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, _scanRadius);

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
