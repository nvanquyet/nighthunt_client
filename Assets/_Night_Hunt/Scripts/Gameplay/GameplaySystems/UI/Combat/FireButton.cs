using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.Input.Handlers.Movement;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// On-screen fire button for mobile / controller UI.
    ///
    /// PointerDown  → <see cref="CombatInputHandler.SimulateFire"/>(true)  — begins firing.
    /// PointerUp    → <see cref="CombatInputHandler.SimulateFire"/>(false) — stops  firing.
    ///
    /// MOBA-style visual feedback:
    ///   • <see cref="ButtonPulseRing"/>  — 2-D expanding ring around this button (UI-space).
    ///   • <see cref="RangeIndicator"/>   — world-space ring shown while button is held (Show on Down, Hide on Up).
    ///
    /// Inspector setup:
    ///   • <c>_combatInputHandler</c>  — leave null to auto-find at Awake.
    ///   • <c>_pulseRing</c>           — auto-found on same GO.
    ///   • <c>_rangeIndicator</c>      — assign RangeIndicator GO from scene, then call
    ///     <see cref="BindRangeIndicator"/> after player spawns to set follow-target + range.
    ///
    /// Mobile joystick aim:
    ///   Drag on this button → forwards drag direction to CombatInputHandler so the
    ///   character rotates while firing.  Finger lift clears the override.
    /// </summary>
    public class FireButton : ActionButton, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Fire Button")]
        [Tooltip("The CombatInputHandler belonging to the local player. " +
                 "Leave null to auto-find in scene at Awake.")]
        [SerializeField] private CombatInputHandler _combatInputHandler;

        [Header("MOBA Visual Feedback")]
        [Tooltip("2D ring pulse around this button (auto-found if on same GO).")]
        [SerializeField] private ButtonPulseRing _pulseRing;

        [Tooltip("World-space RangeIndicator GO in the scene. " +
                 "Call BindRangeIndicator() after player spawns to set follow-target + VisionRange.")]
        [SerializeField] private RangeIndicator _rangeIndicator;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();

            if (_combatInputHandler == null)
                _combatInputHandler = FindObjectOfType<CombatInputHandler>();

            if (_combatInputHandler == null)
                Debug.LogWarning("[FireButton] No CombatInputHandler found in scene. " +
                                 "Assign it manually or ensure it exists before this Awake.");

            if (_pulseRing == null)
                _pulseRing = GetComponent<ButtonPulseRing>();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Pointer Events
        // ─────────────────────────────────────────────────────────────────────

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            _combatInputHandler?.SimulateFire(true);
            _pulseRing?.Play();
            _rangeIndicator?.Show();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            _combatInputHandler?.SimulateFire(false);
            _combatInputHandler?.SetMobileAimDirection(Vector3.zero, active: false);
            _rangeIndicator?.Hide();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Mobile Joystick Aim  (IDragHandler / IBeginDragHandler / IEndDragHandler)
        // ─────────────────────────────────────────────────────────────────────
        // When the player holds the fire button and drags on mobile, the drag
        // direction is turned into a world-space aim direction so the character
        // faces that way while firing — identical to MOBA joystick aim.

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Nothing — drag tracking starts on the first OnDrag call.
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_combatInputHandler == null) return;

            // delta from press origin in screen pixels → normalised 2-D direction
            Vector2 screenDelta = eventData.position - eventData.pressPosition;
            if (screenDelta.sqrMagnitude < 4f) return; // dead-zone: 2 px radius

            // Map screen XY → world XZ using camera orientation so dragging "up"
            // on screen always means camera-forward in world space, regardless of
            // the camera's yaw (matches QuickSlotAimController.ScreenDeltaToWorldDir).
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 camRight   = cam.transform.right;   camRight.y   = 0f; camRight.Normalize();
            Vector3 camForward = cam.transform.forward; camForward.y = 0f; camForward.Normalize();

            Vector2 dir2D  = screenDelta.normalized;
            Vector3 worldDir = camRight * dir2D.x + camForward * dir2D.y;
            if (worldDir.sqrMagnitude < 0.001f) return;

            _combatInputHandler.SetMobileAimDirection(worldDir.normalized, active: true);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _combatInputHandler?.SetMobileAimDirection(Vector3.zero, active: false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Visual Feedback
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Trigger the 2D button pulse ring. Called from keyboard/hardware fire path.
        /// (Range indicator is not shown for keyboard fire — it is only shown while the
        /// touch/mouse button is physically held via OnPointerDown/OnPointerUp.)
        /// </summary>
        public void TriggerAttackFeedback()
        {
            _pulseRing?.Play();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API — runtime rebinding
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Rebind to a new CombatInputHandler (called when local player changes).</summary>
        public void Bind(CombatInputHandler handler)
        {
            // If currently pressed, stop firing on the old handler before switching.
            if (_combatInputHandler != null)
                _combatInputHandler.SimulateFire(false);

            _combatInputHandler = handler;
        }

        /// <summary>
        /// Bind the RangeIndicator to the local player after they spawn.
        /// Sets the follow-target and VisionRange so the ring is correctly sized and positioned.
        /// </summary>
        public void BindRangeIndicator(RangeIndicator indicator, Transform playerTransform, float visionRange)
        {
            _rangeIndicator = indicator;
            if (_rangeIndicator != null)
            {
                _rangeIndicator.SetFollowTarget(playerTransform);
                _rangeIndicator.SetRange(visionRange);
            }
        }

        /// <summary>
        /// Update the already-assigned <see cref="_rangeIndicator"/> with the local player's
        /// transform and VisionRange.  Call after player spawns when the indicator was
        /// pre-assigned in the Inspector (avoids replacing the designer reference).
        /// Also forwards the indicator reference to the <see cref="CombatInputHandler"/>
        /// so keyboard/gamepad fire also shows the ring consistently.
        /// </summary>
        public void BindPlayerContext(Transform playerTransform, float visionRange)
        {
            if (_rangeIndicator != null)
            {
                _rangeIndicator.SetFollowTarget(playerTransform);
                _rangeIndicator.SetRange(visionRange);
            }
            // Keep CombatInputHandler in sync so keyboard fire shows the same ring.
            _combatInputHandler?.BindAttackIndicators(_rangeIndicator, this);
        }
    }
}
