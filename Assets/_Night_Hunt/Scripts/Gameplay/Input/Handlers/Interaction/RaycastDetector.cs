using System;
using UnityEngine;
using UnityEngine.Serialization;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Loot;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using Object = UnityEngine.Object;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Input.Handlers.Interaction
{
    /// <summary>
    /// Camera-forward raycast detector for interaction targets.
    ///
    /// DESIGN (SRP):
    ///   - Owns only the raycast and target resolution.
    ///   - Exposes <see cref="CurrentInteractable"/> (IInteractable) as the primary API.
    ///   - Typed properties (CurrentWorldItem etc.) are kept for backward compatibility
    ///     but callers should prefer <see cref="CurrentInteractable"/>.
    ///   - Fires <see cref="IInteractable.OnHoverEnter"/> / <see cref="IInteractable.OnHoverExit"/>
    ///     automatically as the target changes.
    /// </summary>
    public class RaycastDetector : MonoBehaviour
    {
        [Header("Settings")] [SerializeField] private UnityEngine.Camera playerCamera;
        [SerializeField] private LayerMask interactableLayerMask = -1;
        [FormerlySerializedAs("maxDistance")]
        [SerializeField] private float _maxDistance = 5f;
        [SerializeField] private bool ignorePlayerLayer = true;

        [Header("Debug")] [SerializeField] private bool showDebugRay = false;
        [SerializeField] private bool logTargetChanges = false;

        // ── Primary API (interface-based) ────────────────────────────────────────

        /// <summary>
        /// The IInteractable currently aimed at (or null).
        /// Prefer this over the typed properties below.
        /// </summary>
        public IInteractable CurrentInteractable { get; private set; }

        // ── Backward-compatible typed properties ─────────────────────────────────

        public WorldItem CurrentWorldItem => CurrentInteractable as WorldItem;
        public WorldContainer CurrentContainer => CurrentInteractable as WorldContainer;
        public WorldCorpse CurrentCorpse => CurrentInteractable as WorldCorpse;

        public bool IsOwner { get; private set; }

        // Backward compat
        [System.Obsolete("Use CurrentWorldItem instead.")]
        public WorldItem CurrentWorldPickup => CurrentWorldItem;

        // ── Private ──────────────────────────────────────────────────────────────

        private IInteractable _previousInteractable;
        private GameObject _interactor;

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void OnEnable()
        {
            NetworkPlayer.OnOwnerReady += HandleOwnerReady;
        }

        private void HandleOwnerReady(NetworkPlayer obj)
        {
            if (obj != null && obj.IsLocalPlayer)
            {
                IsOwner = true;
            }
        }

        private void OnDisable()
        {
            NetworkPlayer.OnOwnerReady -= HandleOwnerReady;
        }

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = UnityEngine.Camera.main;

            // Prefer the player root as the interactor, not the camera rig.
            // This makes CanInteract(distance checks) stable and consistent with gameplay systems.
            _interactor = transform.root != null ? transform.root.gameObject : gameObject;
        }

        private void Update()
        {
            if (!IsOwner) return;
            // Chỉ raycast khi Player layer active (tức là đang trong PlayerAlive / ScoutMode)
            // Uses IsLayerActive instead of so sánh enum state cụ thể → robust hơn khi thêm state mới
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

            if (Physics.Raycast(ray, out RaycastHit hitInfo, _maxDistance, mask,
                    QueryTriggerInteraction.Collide))
            {
                // Try interface first (works for all types implementing IInteractable)
                hit = ComponentResolver.Find<IInteractable>(hitInfo.collider)
                    .InParent()
                    .InRootChildren()
                    .OrLogWarning("[Auto] IInteractable not found")
                    .Resolve();
            }

            // Fire hover enter / exit when target changes
            if (!ReferenceEquals(hit, _previousInteractable))
            {
                var interactor = _interactor != null ? _interactor : gameObject;

                _previousInteractable?.OnHoverExit(interactor);
                hit?.OnHoverEnter(interactor);

                if (logTargetChanges)
                {
                    if (hit != null)
                    {
                        bool can = hit.CanInteract(interactor);
                        string objName = (hit as Component) != null ? ((Component)hit).gameObject.name : hit.ToString();
                        int instanceId = (hit as Object) != null ? ((Object)hit).GetInstanceID() : 0;
                        string colliderName = hitInfo.collider != null ? hitInfo.collider.name : "<none>";

                        float dist = float.NaN;
                        if (hit is Component c && interactor != null)
                            dist = Vector3.Distance(c.transform.position, interactor.transform.position);

                        Debug.Log(
                            $"[RaycastDetector][TargetChanged] " +
                            $"obj='{objName}' id={instanceId} collider='{colliderName}' " +
                            $"label='{hit.InteractLabel}' canInteract={can}" +
                            $"{(float.IsNaN(dist) ? "" : $" dist={dist:F2}m")} " +
                            $"interactor='{(interactor != null ? interactor.name : "<null>")}'");
                    }
                    else
                    {
                        Debug.Log("[RaycastDetector][TargetChanged] none");
                    }
                }

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

            if (CurrentInteractable != null)
            {
                bool can = CurrentInteractable.CanInteract(gameObject);
                Gizmos.color = can ? Color.green : Color.red;
            }
            else
            {
                Gizmos.color = Color.yellow;
            }

            Gizmos.DrawRay(playerCamera.transform.position,
                playerCamera.transform.forward * _maxDistance);
        }
    }
}