using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// On-screen fire button for mobile/controller UI.
    ///
    /// Pointer down starts firing immediately. If the player holds long enough,
    /// the floating joystick appears and forwards camera-relative aim direction
    /// to the CombatInputHandler until pointer up.
    /// </summary>
    public class FireButton : ActionButton, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [Header("Fire Button")]
        [Tooltip("The CombatInputHandler belonging to the local player. Leave null and bind after spawn.")]
        [SerializeField] private CombatInputHandler _combatInputHandler;

        [Header("MOBA Visual Feedback")]
        [Tooltip("2D ring pulse around this button, auto-found if it exists on this GameObject or a child.")]
        [SerializeField] private ButtonPulseRing _pulseRing;

        [Tooltip("World-space range indicator shown while the attack button is held.")]
        [SerializeField] private RangeIndicator _rangeIndicator;

        [Header("Mobile Virtual Joystick")]
        [Tooltip("Floating VariableJoystick child. It should be disabled in the scene by default.")]
        [SerializeField] private VariableJoystick _joystick;

        [Tooltip("Hold duration in seconds before the joystick visual appears. Short taps fire straight ahead.")]
        [SerializeField] private float _holdDelay = 0.25f;

        private Coroutine _holdTimer;
        private bool _joystickStarted;
        private bool _pressActive;
        private PointerEventData _pressEventData;

        protected override void Awake()
        {
            base.Awake();

            if (_combatInputHandler == null)
            {
                Debug.LogWarning("[FireButton] CombatInputHandler is not assigned. " +
                                 "Call Initialize(handler) after the local player spawns.");
            }

            if (_pulseRing == null)
            {
                _pulseRing = ComponentResolver.Find<ButtonPulseRing>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] ButtonPulseRing not found")
                    .Resolve();
            }

            if (_joystick != null)
                _joystick.gameObject.SetActive(false);
        }

        public void Initialize(CombatInputHandler handler)
        {
            _combatInputHandler = handler;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable)
                return;

            base.OnPointerDown(eventData);
            Debug.Log($"[NH_FLOW][09][FireButton.Down] button={name} pointer={eventData.pointerId} pos={eventData.position} handler={(_combatInputHandler != null ? "ok" : "null")}");
            _pressEventData = eventData;
            _joystickStarted = false;
            _pressActive = true;

            _combatInputHandler?.NotifyUIConsumedPress();
            _combatInputHandler?.SimulateFire(true);
            _pulseRing?.Play();

            if (_holdTimer != null)
                StopCoroutine(_holdTimer);

            _holdTimer = StartCoroutine(HoldTimerCo());
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            Debug.Log($"[NH_FLOW][18][FireButton.Up] button={name} pointer={eventData.pointerId} pos={eventData.position} pressActive={_pressActive} joystickStarted={_joystickStarted}");
            FinishPress(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsInteractable)
                return;

            Debug.Log($"[NH_FLOW][15][FireButton.BeginDrag] button={name} pointer={eventData.pointerId} pos={eventData.position} joystickStarted={_joystickStarted}");
            if (!_joystickStarted)
                StartJoystick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsInteractable)
                return;

            if (!_joystickStarted)
                StartJoystick(eventData);

            if (_joystick == null)
                return;

            _joystick.OnDrag(eventData);
            PushJoystickToCombat(eventData, "Drag");
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Debug.Log($"[NH_FLOW][17][FireButton.EndDrag] button={name} pointer={eventData.pointerId} pos={eventData.position} pressActive={_pressActive} joystickStarted={_joystickStarted}");
            FinishPress(eventData);
        }

        private void FinishPress(PointerEventData eventData)
        {
            if (!_pressActive)
                return;

            Debug.Log($"[NH_FLOW][18][FireButton.FinishPress] button={name} pointer={eventData.pointerId} joystickStarted={_joystickStarted} handler={(_combatInputHandler != null ? "ok" : "null")}");
            _pressActive = false;
            StopHoldTimer();

            if (_joystickStarted && _joystick != null)
                PushJoystickToCombat(eventData, "Release");

            _joystickStarted = false;
            _combatInputHandler?.SimulateFire(false);

            if (_joystick != null)
            {
                _joystick.OnPointerUp(eventData);
                _joystick.gameObject.SetActive(false);
            }

            _combatInputHandler?.SetFireMobileJoystick(Vector2.zero, false);
            _rangeIndicator?.Hide();
        }

        public void TriggerAttackFeedback()
        {
            _pulseRing?.Play();
        }

        private void OnDisable()
        {
            // Guard: nếu HUD bị disable giữa lúc đang giữ nút (respawn/death),
            // reset toàn bộ press state để lần enable lại không bị kẹt.
            ResetPressState();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ResetPressState();
        }

        /// <summary>
        /// Reset all press/hold state. Called on Disable and Destroy to guard against
        /// state being stuck when the HUD is toggled mid-press (e.g. respawn/death).
        /// </summary>
        private void ResetPressState()
        {
            StopAllCoroutines();
            _holdTimer = null;

            if (!_pressActive)
                return;

            _pressActive = false;
            _joystickStarted = false;

            _combatInputHandler?.SimulateFire(false);
            _combatInputHandler?.SetFireMobileJoystick(Vector2.zero, false);

            if (_joystick != null)
                _joystick.gameObject.SetActive(false);

            _rangeIndicator?.Hide();
        }

        public void Bind(CombatInputHandler handler)
        {
            if (_combatInputHandler != null)
                _combatInputHandler.SimulateFire(false);

            _combatInputHandler = handler;
        }

        public void BindRangeIndicator(RangeIndicator indicator, Transform playerTransform, float visionRange)
        {
            _rangeIndicator = indicator;
            if (_rangeIndicator == null)
                return;

            _rangeIndicator.SetFollowTarget(playerTransform);
            _rangeIndicator.SetRange(visionRange);
        }

        public void BindPlayerContext(Transform playerTransform, float visionRange)
        {
            if (_rangeIndicator != null)
            {
                _rangeIndicator.SetFollowTarget(playerTransform);
                _rangeIndicator.SetRange(visionRange);
            }

            _combatInputHandler?.BindAttackIndicators(_rangeIndicator, this);
        }

        private IEnumerator HoldTimerCo()
        {
            yield return new WaitForSecondsRealtime(_holdDelay);

            if (!_joystickStarted)
                StartJoystick(_pressEventData);

            _holdTimer = null;
        }

        private void StartJoystick(PointerEventData eventData)
        {
            if (_joystick == null || _combatInputHandler == null)
                return;

            _joystickStarted = true;
            _joystick.gameObject.SetActive(true);
            _joystick.OnPointerDown(eventData);
            PushJoystickToCombat(eventData, "StartJoystick");
            Debug.Log($"[NH_FLOW][15][FireButton.StartJoystick] button={name} pointer={eventData.pointerId} pos={eventData.position}");
        }

        private void StopHoldTimer()
        {
            if (_holdTimer == null)
                return;

            StopCoroutine(_holdTimer);
            _holdTimer = null;
        }

        private static Vector2 CamRelativeXZ(Vector2 dir)
        {
            var cam = Camera.main;
            if (cam == null)
                return dir;

            Vector3 right = cam.transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 world = right * dir.x + forward * dir.y;
            return new Vector2(world.x, world.z);
        }

        private void PushJoystickToCombat(PointerEventData eventData, string source)
        {
            if (_joystick == null)
                return;

            Vector2 raw = ResolveJoystickDirectionForFirstFrame(eventData);
            Vector2 joystickDir = CamRelativeXZ(raw);
            Debug.Log($"[NH_FLOW][16][FireButton.{source}] button={name} raw={raw:F2} camRelative={joystickDir:F2}");

            if (ThrowableAimController.IsAnyAimingActive && ThrowableAimController.Instance != null)
            {
                ThrowableAimController.Instance.OnMobileDrag(raw);
                return;
            }

            _combatInputHandler?.SetFireMobileJoystick(joystickDir, active: true);
        }

        private Vector2 ResolveJoystickDirectionForFirstFrame(PointerEventData eventData)
        {
            Vector2 raw = _joystick != null ? _joystick.Direction : Vector2.zero;
            if (raw.sqrMagnitude > 0.001f)
                return raw;

            if (eventData != null && _joystick != null && _joystick.transform is RectTransform rectTransform)
            {
                Camera eventCamera = eventData.pressEventCamera != null
                    ? eventData.pressEventCamera
                    : eventData.enterEventCamera;
                Vector2 center = RectTransformUtility.WorldToScreenPoint(eventCamera, rectTransform.position);
                Vector2 delta = eventData.position - center;
                float radius = Mathf.Max(1f, Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f);
                if (delta.sqrMagnitude > 4f)
                    return Vector2.ClampMagnitude(delta / radius, 1f);
            }

            // Floating joystick centres under the finger on pointer down, so its raw
            // direction is zero in the first frame. Seed forward aim immediately.
            return Vector2.up;
        }
    }
}
