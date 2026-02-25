using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Loot;

namespace NightHunt.Gameplay.Input.Handlers.Interaction
{
    /// <summary>
    /// Camera-forward raycast detector for interaction targets.
    ///
    /// DESIGN (SRP):
    ///   - Owns only the raycast and target resolution.
    ///   - Exposes <see cref="CurrentInteractable"/> (IInteractable) as the primary API.
    ///   - Typed properties (CurrentWorldPickup etc.) are kept for backward compatibility
    ///     but callers should prefer <see cref="CurrentInteractable"/>.
    ///   - Fires <see cref="IInteractable.OnHoverEnter"/> / <see cref="IInteractable.OnHoverExit"/>
    ///     automatically as the target changes.
    /// </summary>
    public class RaycastDetector : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private UnityEngine.Camera playerCamera;
        [SerializeField] private LayerMask interactableLayerMask = -1;
        [SerializeField] private float maxDistance = 5f;
        [SerializeField] private bool ignorePlayerLayer = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugRay = false;

        // ── Primary API (interface-based) ────────────────────────────────────────

        /// <summary>
        /// The IInteractable currently aimed at (or null).
        /// Prefer this over the typed properties below.
        /// </summary>
        public IInteractable CurrentInteractable { get; private set; }

        // ── Backward-compatible typed properties ─────────────────────────────────

        public WorldPickup CurrentWorldPickup       => CurrentInteractable as WorldPickup;
        public ContainerLootSource CurrentContainer => CurrentInteractable as ContainerLootSource;
        public CorpseLootSource CurrentCorpse       => CurrentInteractable as CorpseLootSource;

        // ── Private ──────────────────────────────────────────────────────────────

        private IInteractable _previousInteractable;

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = UnityEngine.Camera.main;
        }

        private void Update()
        {
            // Chỉ raycast khi Player layer active (tức là đang trong PlayerAlive / ScoutMode)
            // Dùng IsLayerActive thay vì so sánh enum state cụ thể → robust hơn khi thêm state mới
            var ilm = NightHunt.Gameplay.Input.Core.InputLayerManager.Instance;
            if (ilm != null && !ilm.IsLayerActive(NightHunt.Gameplay.Input.InputLayer.Player))
            {
                ClearTargets();
                return;
            }

            PerformRaycast();
        }

        // ── Raycast ──────────────────────────────────────────────────────────────

        private void PerformRaycast()
        {
            if (playerCamera == null) return;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            LayerMask mask = interactableLayerMask;
            if (ignorePlayerLayer)
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer != -1)
                    mask &= ~(1 << playerLayer);
            }

            IInteractable hit = null;

            if (Physics.Raycast(ray, out RaycastHit hitInfo, maxDistance, mask,
                QueryTriggerInteraction.Collide))
            {
                // Try interface first (works for all types implementing IInteractable)
                hit = hitInfo.collider.GetComponentInParent<IInteractable>();
            }

            // Fire hover enter / exit when target changes
            if (!ReferenceEquals(hit, _previousInteractable))
            {
                _previousInteractable?.OnHoverExit(gameObject);
                hit?.OnHoverEnter(gameObject);
                _previousInteractable = hit;
            }

            CurrentInteractable = hit;
        }

        private void ClearTargets()
        {
            if (CurrentInteractable != null)
            {
                CurrentInteractable.OnHoverExit(gameObject);
                _previousInteractable = null;
                CurrentInteractable = null;
            }
        }

        // ── Gizmos ───────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!showDebugRay || playerCamera == null) return;

            Gizmos.color = CurrentInteractable != null ? Color.green : Color.yellow;
            Gizmos.DrawRay(playerCamera.transform.position,
                playerCamera.transform.forward * maxDistance);
        }
    }
}
