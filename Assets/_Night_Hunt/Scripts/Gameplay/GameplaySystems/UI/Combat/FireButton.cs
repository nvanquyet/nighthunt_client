using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// On-screen fire button for mobile / controller UI.
    ///
    /// PointerDown  ΓåÆ <see cref="CombatInputHandler.SimulateFire"/>(true)  ΓÇö begins firing.
    /// PointerUp    ΓåÆ <see cref="CombatInputHandler.SimulateFire"/>(false) ΓÇö stops  firing.
    ///
    /// MOBA-style visual feedback:
    ///   ΓÇó <see cref="ButtonPulseRing"/>  ΓÇö 2-D expanding ring around this button (UI-space).
    ///   ΓÇó <see cref="RangeIndicator"/>   ΓÇö world-space ring shown while button is held (Show on Down, Hide on Up).
    ///
    /// Inspector setup:
    ///   ΓÇó <c>_combatInputHandler</c>  ΓÇö leave null to auto-find at Awake.
    ///   ΓÇó <c>_pulseRing</c>           ΓÇö auto-found on same GO.
    ///   ΓÇó <c>_rangeIndicator</c>      ΓÇö assign RangeIndicator GO from scene, then call
    ///     <see cref="BindRangeIndicator"/> after player spawns to set follow-target + range.
    ///
    /// Mobile joystick aim:
    ///   Drag on this button ΓåÆ forwards drag direction to CombatInputHandler so the
    ///   character rotates while firing.  Finger lift clears the override.
    /// </summary>
    public class FireButton : ActionButton, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Inspector
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

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

        [Header("Mobile Virtual Joystick")]
        [Tooltip("VariableJoystick on a child GO ΓÇö must be DISABLED in the scene by default. " +
                 "Set mode = Floating. FireButton enables it after the hold delay and disables it on release.")]
        [SerializeField] private VariableJoystick _joystick;
        [Tooltip("Hold duration in seconds before the joystick visual appears. Short taps fire straight ahead.")]
        [SerializeField] private float _holdDelay = 0.25f;

        // ΓöÇΓöÇ Runtime joystick state ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        private Coroutine        _holdTimer;
        private bool             _joystickStarted;
        /// <summary>PointerDown event cached on press; used to position the joystick once the hold delay fires.</summary>
        private PointerEventData _pressEventData;

        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Unity Lifecycle
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        protected override void Awake()
        {
            base.Awake();

            if (_combatInputHandler == null)
                Debug.LogWarning("[FireButton] CombatInputHandler not assigned. " +
                                 "Call Initialize(handler) after the local player spawns.");

            if (_pulseRing == null)
                _pulseRing = ComponentResolver.Find<ButtonPulseRing>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] ButtonPulseRing not found")
        .Resolve();

            // Joystick must start hidden so it does not capture pointer events instead of this button.
            if (_joystick != null) _joystick.gameObject.SetActive(false);
        }

        /// <summary>
        /// Bind this button to the local playerΓÇÖs CombatInputHandler.
        /// Must be called by the HUD orchestrator (CombatHUDPanel) after the local player spawns.
        /// </summary>
        public void Initialize(CombatInputHandler handler)
        {
            _combatInputHandler = handler;
        }

        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Pointer Events
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            _pressEventData  = eventData;   // save press position; joystick will use it after hold delay
            _joystickStarted = false;
            _combatInputHandler?.SimulateFire(true);   // frame 1: aim = current player facing direction
            _pulseRing?.Play();

            Debug.Log($"[FireButton] OnPointerDown ΓÇö screenPos={eventData.position:F0}  " +
                      $"joystickAssigned={_joystick != null}  handler={_combatInputHandler != null}");

            // Start hold timer ΓÇö joystick enables only after _holdDelay seconds.
            if (_holdTimer != null) StopCoroutine(_holdTimer);
            _holdTimer = StartCoroutine(HoldTimerCo());
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            StopHoldTimer();
            if (_joystickStarted && _joystick != null)
            {
                _joystick.OnPointerUp(eventData);
                _joystick.gameObject.SetActive(false);   // hide + remove from raycast
            }
            _joystickStarted = false;
            _combatInputHandler?.SetFireMobileJoystick(Vector2.zero, false);
            _combatInputHandler?.SimulateFire(false);
            _rangeIndicator?.Hide();
        }

        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Mobile Virtual Joystick (IDragHandler / IBeginDragHandler / IEndDragHandler)
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Hold-delay pattern:
        //    PointerDown  ΓåÆ SimulateFire(true) + start hold timer
        //    < holdDelay  ΓåÆ quick tap ΓÇö fires straight, joystick not shown
        //    ΓëÑ holdDelay  ΓåÆ VariableJoystick appears at press position
        //    OnDrag       ΓåÆ package handles clamp/thumb; read Direction; camera-relative XZ
        //    PointerUp    ΓåÆ hide joystick, clear aim, SimulateFire(false)

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_joystickStarted) StartJoystick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_joystickStarted) StartJoystick(eventData);
            if (_joystick == null) return;   // no joystick assigned ΓÇö keep frame-1 direction

            _joystick.OnDrag(eventData);
            Vector2 joystickDir = CamRelativeXZ(_joystick.Direction);

            Debug.Log($"[FireButton] OnDrag ΓÇö rawDir={_joystick.Direction:F2}  " +
                      $"camRelXZ={joystickDir:F2}  mag={joystickDir.magnitude:F2}");

            _combatInputHandler?.SetFireMobileJoystick(joystickDir, active: true);
        }

        // IEndDragHandler — OnPointerUp fires BEFORE OnEndDrag (Unity EventSystem dispatch order).
        // All cleanup is already handled in OnPointerUp (joystick hide + SimulateFire(false)).
        // OnEndDrag is only a safety net for the edge case where drag ends without a PointerUp
        // (e.g., pointer left the screen while dragging).
        public void OnEndDrag(PointerEventData eventData)
        {
            // _joystickStarted was already set to false in OnPointerUp, so this block
            // only runs in the pointer-left-screen edge case.
            if (_joystickStarted && _joystick != null)
            {
                _joystick.OnPointerUp(eventData);
                _joystick.gameObject.SetActive(false);
            }
            _joystickStarted = false;
            _combatInputHandler?.SetFireMobileJoystick(Vector2.zero, false);
            // Call SimulateFire(false) if still firing (edge case without PointerUp).
            _combatInputHandler?.SimulateFire(false);
        }

        private IEnumerator HoldTimerCo()
        {
            yield return new WaitForSecondsRealtime(_holdDelay);
            // Use the cached press event so the joystick appears at the original touch point.
            if (!_joystickStarted) StartJoystick(_pressEventData);
            _holdTimer = null;
        }

        private void StartJoystick(PointerEventData eventData)
        {
            // BUG 10 FIX: Guard against calling joystick before player has spawned and
            // Initialize(handler) has been called. The joystick's internal _background field
            // can be null if OnPointerDown fires before Unity initializes the VariableJoystick.
            if (_joystick == null || _combatInputHandler == null) return;

            _joystickStarted = true;
            _joystick.gameObject.SetActive(true);    // make visible + enable raycasting
            _joystick.OnPointerDown(eventData);      // Floating mode: positions background at touch point
        }

        private void StopHoldTimer()
        {
            if (_holdTimer != null) { StopCoroutine(_holdTimer); _holdTimer = null; }
        }

        private static Vector2 CamRelativeXZ(Vector2 dir)
        {
            var cam = Camera.main;
            if (cam == null) return Vector2.zero;
            Vector3 right   = cam.transform.right;   right.y   = 0f; right.Normalize();
            Vector3 forward = cam.transform.forward; forward.y = 0f; forward.Normalize();
            Vector3 world   = right * dir.x + forward * dir.y;
            return new Vector2(world.x, world.z);
        }

        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Visual Feedback
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        /// <summary>
        /// Trigger the 2D button pulse ring. Called from keyboard/hardware fire path.
        /// (Range indicator is not shown for keyboard fire ΓÇö it is only shown while the
        /// touch/mouse button is physically held via OnPointerDown/OnPointerUp.)
        /// </summary>
        public void TriggerAttackFeedback()
        {
            _pulseRing?.Play();
        }

        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Public API ΓÇö runtime rebinding
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

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
