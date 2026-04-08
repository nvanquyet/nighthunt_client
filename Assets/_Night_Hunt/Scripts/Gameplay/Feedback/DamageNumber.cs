using System;
using System.Collections;
using UnityEngine;
using TMPro;

namespace NightHunt.Gameplay.Feedback
{
    /// <summary>
    /// Floating damage number. Animates upward with fade-out, then invokes onComplete
    /// so the pool can reclaim it — never calls Destroy().
    ///
    /// PREFAB REQUIREMENTS:
    ///   • RectTransform on root (or any child — resolved once in Awake).
    ///   • TextMeshProUGUI on root or child.
    /// </summary>
    public sealed class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;

        private RectTransform _rectTransform;
        private float         _lifetime;
        private float         _speed;
        private Vector3       _startPosition;
        private Action        _onComplete;
        private Coroutine     _animRoutine;

        // -----------------------------------------------------------------
        // Unity
        // -----------------------------------------------------------------

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_text == null) _text = GetComponentInChildren<TextMeshProUGUI>();
        }

        // Called by pool before re-activating — resets visual state.
        private void OnEnable()
        {
            if (_text != null)
            {
                var c = _text.color;
                c.a = 1f;
                _text.color = c;
            }
        }

        // -----------------------------------------------------------------
        // API
        // -----------------------------------------------------------------

        /// <summary>
        /// Start the floating animation.
        /// <paramref name="onComplete"/> is invoked when animation ends so the pool
        /// can deactivate and reclaim this instance.
        /// </summary>
        public void Initialize(Vector3 screenPosition, float damage, Color color,
                               float lifeTime, float moveSpeed, Action onComplete = null)
        {
            _onComplete = onComplete;
            _lifetime   = lifeTime;
            _speed      = moveSpeed;

            if (_text != null)
            {
                _text.text  = Mathf.CeilToInt(damage).ToString();
                _text.color = color;
            }

            if (_rectTransform != null)
            {
                _rectTransform.position = screenPosition;
                _startPosition = screenPosition;
            }

            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimateNumber());
        }

        // -----------------------------------------------------------------
        // Internal
        // -----------------------------------------------------------------

        private IEnumerator AnimateNumber()
        {
            float elapsed = 0f;
            // Small horizontal jitter so stacked numbers don't overlap.
            float xOffset = UnityEngine.Random.Range(-40f, 40f);

            while (elapsed < _lifetime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / _lifetime;

                if (_rectTransform != null)
                {
                    _rectTransform.position = _startPosition
                        + new Vector3(xOffset, _speed * elapsed * 100f, 0f);
                }

                if (_text != null)
                {
                    var c = _text.color;
                    c.a = 1f - progress;
                    _text.color = c;
                }

                yield return null;
            }

            _animRoutine = null;
            _onComplete?.Invoke(); // pool return happens here — no Destroy
        }
    }
}
