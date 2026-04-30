using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if !UNITY_SERVER
using DG.Tweening;
#endif
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Base interactive button for the in-game combat HUD.
    ///
    /// Provides press animation, cooldown ring support, icon assignment, and
    /// CanvasGroup-based interactable state for derived HUD buttons.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Visual References")]
        [SerializeField] protected Image _iconImage;
        [SerializeField] protected Image _cooldownRing;

        [Header("Animation")]
        [SerializeField] private float _pressScaleDown = 0.88f;
        [SerializeField] private float _pressScaleDur = 0.07f;
        [SerializeField] private float _releaseScaleDur = 0.12f;

        [Header("Interactable")]
        [SerializeField] private float _disabledAlpha = 0.4f;

        private CanvasGroup _canvasGroup;
        private Coroutine _cooldownCoroutine;
        private bool _interactable = true;

        protected bool IsInteractable => _interactable;

        public event Action OnPressed;
        public event Action OnReleased;

        public CanvasGroup Canvas
        {
            get
            {
                if (_canvasGroup == null)
                {
                    _canvasGroup = ComponentResolver.Find<CanvasGroup>(this)
                        .OnSelf()
                        .InChildren()
                        .OrLogWarning("[Auto] CanvasGroup not found")
                        .Resolve();
                }

                return _canvasGroup;
            }
        }

        protected virtual void Awake()
        {
            ResetCooldownRing();
        }

        protected virtual void OnDestroy()
        {
#if !UNITY_SERVER
            DOTween.Kill(transform);
#endif
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable)
                return;

#if !UNITY_SERVER
            DOTween.Kill(transform);
            transform.DOScale(_pressScaleDown, _pressScaleDur).SetEase(Ease.InQuad);
#endif
            OnPressed?.Invoke();
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
#if !UNITY_SERVER
            DOTween.Kill(transform);
            transform.DOScale(1f, _releaseScaleDur).SetEase(Ease.OutBack);
#endif
            OnReleased?.Invoke();
        }

        public void SetIcon(Sprite sprite)
        {
            if (_iconImage == null)
                return;

            _iconImage.sprite = sprite;
            _iconImage.enabled = sprite != null;
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;

            Canvas.interactable = value;
            Canvas.blocksRaycasts = value;
            Canvas.alpha = value ? 1f : _disabledAlpha;
        }

        public void StartCooldown(float duration)
        {
            if (_cooldownRing == null)
                return;

            if (_cooldownCoroutine != null)
                StopCoroutine(_cooldownCoroutine);

            _cooldownCoroutine = StartCoroutine(CooldownRoutine(duration));
        }

        public void CancelCooldown()
        {
            if (_cooldownCoroutine != null)
            {
                StopCoroutine(_cooldownCoroutine);
                _cooldownCoroutine = null;
            }

            ResetCooldownRing();
        }

        private IEnumerator CooldownRoutine(float duration)
        {
            if (_cooldownRing == null)
                yield break;

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
