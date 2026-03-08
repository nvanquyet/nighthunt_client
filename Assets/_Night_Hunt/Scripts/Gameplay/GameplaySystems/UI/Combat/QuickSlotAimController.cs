using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.StatSystem.Core.Interfaces;
using NightHunt.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Orchestrates the aim flow for throwable quick-slot items.
    ///
    /// <para><b>PC Mode:</b>
    ///   Entering aim mode shows the <see cref="RangeIndicator"/> ring and an optional
    ///   world-space cursor at the current mouse ground position.
    ///   Left-click → confirm throw.  Right-click or Escape → cancel.</para>
    ///
    /// <para><b>Mobile Mode (Liên Quân / LoL Wild Rift style):</b>
    ///   Drag begins at the quick-slot button's screen centre.  Dragging in any direction
    ///   computes a world-space aim direction.  Releasing beyond the threshold confirms the
    ///   throw; releasing inside the threshold cancels it.</para>
    ///
    /// <para>After confirmation the static properties
    ///   <see cref="AimWorldTarget"/> and <see cref="AimDirection"/>
    ///   are set so <c>ThrowableHandler</c> can read them.</para>
    ///
    /// Inspector / runtime setup:
    ///   Assign <see cref="RangeIndicator"/> and (optionally) <c>_aimCursor</c> world transform.
    ///   Call <see cref="Initialize"/> when the local player spawns.
    /// </summary>
    public class QuickSlotAimController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Platform Override")]
        [Tooltip("Force mobile-style drag joystick even in the editor for testing.")]
        [SerializeField] private bool _forceMobileMode = false;

        [Header("World Indicators")]
        [Tooltip("RangeIndicator GO in the scene — shows VisionRange ring while aiming a throwable.")]
        [SerializeField] private RangeIndicator _rangeIndicator;

        [Tooltip("Optional world-space dot / crosshair shown at the aim target position.")]
        [SerializeField] private Transform _aimCursor;

        [Header("Mobile Drag Threshold")]
        [Tooltip("Minimum screen-space drag distance (pixels) before a release is treated as a confirm.")]
        [SerializeField] private float _dragThresholdPixels = 50f;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime refs
        // ─────────────────────────────────────────────────────────────────────

        private IPlayerStatSystem _statSystem;
        private IQuickSlotSystem  _quickSlotSystem;
        private Transform         _playerTransform;
        private Camera            _cam;
        private IAimSystem        _aimSystem;

        // ─────────────────────────────────────────────────────────────────────
        //  Aim state
        // ─────────────────────────────────────────────────────────────────────

        private bool    _inAimMode;
        private int     _activeSlot  = -1;
        private Vector2 _mobileDragStart;      // screen-space anchor for mobile

        // ─────────────────────────────────────────────────────────────────────
        //  Static output (read by ThrowableHandler)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Last confirmed world-space throw target position.</summary>
        public static Vector3 AimWorldTarget { get; private set; }

        /// <summary>Normalised world-space aim direction from the player.</summary>
        public static Vector3 AimDirection   { get; private set; }

        /// <summary>True while the controller is in aim mode (for ThrowableHandler / input guards).</summary>
        public static bool    IsAimingPC     { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────────────────────────────

        private bool IsMobile => _forceMobileMode || Application.isMobilePlatform;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _cam = Camera.main;
            HideCursor();
        }

        private void Update()
        {
            if (!_inAimMode || IsMobile) return;

            UpdatePCRaycast();

            // Confirm on left-click (NOT over a UI element so quickslot button taps work).
            if (UnityEngine.Input.GetMouseButtonDown(0) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                ConfirmAim();
                return;
            }

            // Cancel on right-click or Escape.
            if (UnityEngine.Input.GetMouseButtonDown(1) ||
                UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                CancelAim();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API – initialization
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind to the local player's systems.  Call when the local player spawns /
        /// changes (mirrors the pattern in <c>UIRootController</c>).
        /// </summary>
        public void Initialize(
            IPlayerStatSystem statSystem,
            IQuickSlotSystem  quickSlotSystem,
            Transform         playerTransform,
            IAimSystem        aimSystem = null)
        {
            _statSystem      = statSystem;
            _quickSlotSystem = quickSlotSystem;
            _playerTransform = playerTransform;
            _aimSystem       = aimSystem;

            if (_rangeIndicator != null)
                _rangeIndicator.SetFollowTarget(playerTransform);

            if (_cam == null)
                _cam = Camera.main;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API – called by QuickSlotHUDButton
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="QuickSlotHUDButton.OnPointerDown"/> when a slot is pressed.
        ///
        /// If the item in <paramref name="slotIndex"/> is a <c>Throwable</c>, enters aim mode.
        /// Otherwise falls back to direct <see cref="IQuickSlotSystem.UseQuickSlot"/>.
        /// </summary>
        public void TryBeginAim(int slotIndex, RectTransform buttonRT, Vector2 screenAnchor)
        {
            if (_quickSlotSystem == null) return;

            var item = _quickSlotSystem.GetQuickSlotItem(slotIndex);
            if (item == null) return;

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            bool isThrowable = def != null && def.Type == ItemType.Throwable;

            if (!isThrowable)
            {
                // Direct use – no aiming needed.
                if (_quickSlotSystem.CanUseQuickSlot(slotIndex))
                    _quickSlotSystem.UseQuickSlot(slotIndex);
                return;
            }

            // ── Enter aim mode ──────────────────────────────────────────────
            if (_inAimMode) CancelAim();   // cancel previous aim if any

            _activeSlot = slotIndex;
            _inAimMode  = true;
            IsAimingPC  = !IsMobile;

            float range = GetVisionRange();
            if (_rangeIndicator != null)
                _rangeIndicator.ShowWithRange(range);

            if (IsMobile)
            {
                _mobileDragStart = screenAnchor;
            }
            else
            {
                // PC: immediately do a raycast so the ring is ready on first frame.
                UpdatePCRaycast();
            }
        }

        /// <summary>
        /// Called by <see cref="QuickSlotHUDButton"/> IDragHandler during a mobile drag.
        /// </summary>
        public void OnMobileDrag(PointerEventData eventData)
        {
            if (!_inAimMode || !IsMobile) return;

            Vector2 delta       = eventData.position - _mobileDragStart;
            float   range       = GetVisionRange();
            Vector3 worldDir    = ScreenDeltaToWorldDir(delta, range);

            if (_playerTransform != null)
            {
                AimWorldTarget = _playerTransform.position + worldDir;
                AimDirection   = worldDir.normalized;
                MoveCursor(AimWorldTarget);
                // Sync AimSystem so FinalAimPos matches the mobile drag target.
                _aimSystem?.SetThrowableAim(new Vector2(AimDirection.x, AimDirection.z));
            }
        }

        /// <summary>
        /// Called by <see cref="QuickSlotHUDButton"/> IEndDragHandler when the finger lifts.
        /// </summary>
        public void OnMobileDragEnd(PointerEventData eventData)
        {
            if (!_inAimMode || !IsMobile) return;

            float drag = Vector2.Distance(eventData.position, _mobileDragStart);

            if (drag >= _dragThresholdPixels)
                ConfirmAim();
            else
                CancelAim();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PC raycast
        // ─────────────────────────────────────────────────────────────────────

        private void UpdatePCRaycast()
        {
            if (_cam == null || _playerTransform == null) return;

            Ray   ray   = _cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
            Plane plane = new Plane(Vector3.up, _playerTransform.position);

            if (!plane.Raycast(ray, out float dist)) return;

            Vector3 hit    = ray.GetPoint(dist);
            float   range  = GetVisionRange();
            Vector3 offset = hit - _playerTransform.position;

            // Clamp to VisionRange radius.
            if (offset.magnitude > range)
                hit = _playerTransform.position + offset.normalized * range;

            AimWorldTarget = hit;
            AimDirection   = (hit - _playerTransform.position).normalized;
            MoveCursor(hit);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Confirm / Cancel
        // ─────────────────────────────────────────────────────────────────────

        private void ConfirmAim()
        {
            if (!_inAimMode) return;

            int slot = _activeSlot;
            ResetAimState();

            if (_quickSlotSystem != null && _quickSlotSystem.CanUseQuickSlot(slot))
                _quickSlotSystem.UseQuickSlot(slot);
        }

        private void CancelAim()
        {
            ResetAimState();
        }

        private void ResetAimState()
        {
            _inAimMode  = false;
            IsAimingPC  = false;
            _activeSlot = -1;

            if (_rangeIndicator != null) _rangeIndicator.Hide();
            HideCursor();
            // Exit throwable mode in AimSystem so it reverts to normal mouse aim.
            _aimSystem?.SetThrowableAim(Vector2.zero);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private float GetVisionRange()
        {
            if (_statSystem == null) return 10f;                 // safe fallback
            float v = _statSystem.GetStat(PlayerStatType.VisionRange);
            return v > 0f ? v : 10f;
        }

        /// <summary>
        /// Convert a 2D screen-space drag delta into a clamped world-space offset vector.
        /// Camera-relative: right drag → camera right projected to XZ, up drag → camera forward projected to XZ.
        /// </summary>
        private Vector3 ScreenDeltaToWorldDir(Vector2 screenDelta, float range)
        {
            if (_cam == null) return Vector3.zero;

            Vector3 camRight   = _cam.transform.right;   camRight.y   = 0; camRight.Normalize();
            Vector3 camForward = _cam.transform.forward; camForward.y = 0; camForward.Normalize();

            // Normalise screen delta by screen height to get a [0..1] magnitude.
            float normMag = screenDelta.magnitude / Screen.height;
            float clampedMag = Mathf.Min(normMag, 1f) * range;

            Vector2 dir2D = screenDelta.sqrMagnitude > 0.001f ? screenDelta.normalized : Vector2.zero;
            return (camRight * dir2D.x + camForward * dir2D.y) * clampedMag;
        }

        private void MoveCursor(Vector3 worldPos)
        {
            if (_aimCursor == null) return;
            _aimCursor.gameObject.SetActive(true);
            _aimCursor.position = worldPos + Vector3.up * 0.1f;
        }

        private void HideCursor()
        {
            if (_aimCursor != null)
                _aimCursor.gameObject.SetActive(false);
        }
    }
}
