#if !UNITY_SERVER
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Base interactive button for the in-game Combat HUD.
    ///
    /// Features:
    ///   • Press-down scale squeeze via DOTween.
    ///   • Radial cooldown ring driven by a UI Image (fillAmount / Radial360).
    ///   • Interactable / greyed-out state (CanvasGroup alpha + raycast toggle).
    ///   • Icon sprite assignment.
    ///
    /// Usage:
    ///   Attach to a UI GameObject.
    ///   Wire fields in the Inspector:
    ///     _rootTransform   – the Transform to scale on press (usually this.transform).
    ///     _iconImage       – the main icon Image.
    ///     _cooldownRing    – an Image set to Image.Type.Filled / FillMethod.Radial360.
    ///     _canvasGroup     – CanvasGroup for interactable alpha.
    ///   Sub-classes subscribe to OnPointerDown/Up via the provided events.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Visual References")]
        [SerializeField] protected Image            _iconImage;
        [SerializeField] protected Image            _cooldownRing;   // Image.Type = Filled, Radial360

        [Header("Animation")]
        [SerializeField] private float _pressScaleDown  = 0.88f;
        [SerializeField] private float _pressScaleDur   = 0.07f;
        [SerializeField] private float _releaseScaleDur = 0.12f;

        [Header("Interactable")]
        [SerializeField] private float _disabledAlpha   = 0.4f;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────

        private CanvasGroup _canvasGroup;
        private Coroutine   _cooldownCoroutine;
        private bool        _interactable = true;

        // ─────────────────────────────────────────────────────────────────────
        //  Events (for sub-classes)
        // ─────────────────────────────────────────────────────────────────────

        public event Action OnPressed;
        public event Action OnReleased;


        public CanvasGroup Canvas
        {
            get
            {
                if (_canvasGroup == null)
                    _canvasGroup = ComponentResolver.Find<CanvasGroup>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] CanvasGroup not found")
        .Resolve();

                return _canvasGroup;
            }
        }


        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            ResetCooldownRing();
        }

        protected virtual void OnDestroy()
        {
            DOTween.Kill(transform);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  IPointerDownHandler / IPointerUpHandler
        // ─────────────────────────────────────────────────────────────────────

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable) return;

            DOTween.Kill(transform);
            transform.DOScale(_pressScaleDown, _pressScaleDur).SetEase(Ease.InQuad);
            OnPressed?.Invoke();
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            DOTween.Kill(transform);
            transform.DOScale(1f, _releaseScaleDur).SetEase(Ease.OutBack);
            OnReleased?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Sets the button's icon sprite.</summary>
        public void SetIcon(Sprite sprite)
        {
            if (_iconImage == null) return;
            _iconImage.sprite  = sprite;
            _iconImage.enabled = sprite != null;
        }

        /// <summary>
        /// Enables or disables user interaction and adjusts visual alpha.
        /// </summary>
        public void SetInteractable(bool value)
        {
            _interactable                  = value;
            Canvas.interactable      = value;
            Canvas.blocksRaycasts    = value;
            Canvas.alpha             = value ? 1f : _disabledAlpha;
        }

        /// <summary>
        /// Starts a cooldown ring animation that fills from 1 → 0 over
        /// <paramref name="duration"/> seconds.
        /// Calling this while a cooldown is running restarts it.
        /// </summary>
        public void StartCooldown(float duration)
        {
            if (_cooldownRing == null) return;

            if (_cooldownCoroutine != null)
                StopCoroutine(_cooldownCoroutine);

            _cooldownCoroutine = StartCoroutine(CooldownRoutine(duration));
        }

        /// <summary>Cancels any running cooldown and resets the ring.</summary>
        public void CancelCooldown()
        {
            if (_cooldownCoroutine != null)
            {
                StopCoroutine(_cooldownCoroutine);
                _cooldownCoroutine = null;
            }

            ResetCooldownRing();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Internals
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator CooldownRoutine(float duration)
        {
            if (_cooldownRing == null) yield break;

            _cooldownRing.fillAmount = 1f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _cooldownRing.fillAmount = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            ResetCooldownRing();
            _cooldownCoroutine = null;
        }

        private void ResetCooldownRing()
        {
            if (_cooldownRing != null)
                _cooldownRing.fillAmount = 0f;
        }
    }
}
#endif // !UNITY_SERVER
