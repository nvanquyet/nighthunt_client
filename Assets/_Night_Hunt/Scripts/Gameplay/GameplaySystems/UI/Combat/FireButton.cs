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
            FinishPress(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsInteractable)
                return;

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
            Vector2 joystickDir = CamRelativeXZ(_joystick.Direction);
            _combatInputHandler?.SetFireMobileJoystick(joystickDir, active: true);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            FinishPress(eventData);
        }

        private void FinishPress(PointerEventData eventData)
        {
            if (!_pressActive)
                return;

            _pressActive = false;
            StopHoldTimer();

            if (_joystickStarted && _joystick != null)
            {
                _joystick.OnPointerUp(eventData);
                _joystick.gameObject.SetActive(false);
            }

            _joystickStarted = false;
            _combatInputHandler?.SetFireMobileJoystick(Vector2.zero, false);
            _combatInputHandler?.SimulateFire(false);
            _rangeIndicator?.Hide();
        }

        public void TriggerAttackFeedback()
        {
            _pulseRing?.Play();
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
                return Vector2.zero;

            Vector3 right = cam.transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 world = right * dir.x + forward * dir.y;
            return new Vector2(world.x, world.z);
        }
    }
}
