using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.Gameplay.Feedback
{
    /// <summary>
    /// Directional hit indicator — fades out then invokes onComplete for pool return.
    /// Never calls Destroy().
    ///
    /// PREFAB REQUIREMENTS:
    ///   • Image component on root or child.
    ///   • RectTransform on root.
    /// </summary>
    public sealed class HitIndicator : MonoBehaviour
    {
        [SerializeField] private Image _image;

        private float     _lifetime;
        private Action    _onComplete;
        private Coroutine _fadeRoutine;

        private void Awake()
        {
            if (_image == null) _image = GetComponentInChildren<Image>();
        }

        private void OnEnable()
        {
            if (_image == null) return;
            var c = _image.color;
            c.a = 1f;
            _image.color = c;
        }

        /// <summary>
        /// Rotate toward the incoming hit direction and start fading.
        /// <paramref name="onComplete"/> is called at the end so the pool can reclaim.
        /// </summary>
        public void Initialize(Vector3 hitDirection, float lifeTime, Action onComplete = null)
        {
            _lifetime   = lifeTime;
            _onComplete = onComplete;

            if (hitDirection.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            float elapsed = 0f;

            while (elapsed < _lifetime)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - (elapsed / _lifetime);

                if (_image != null)
                {
                    var c = _image.color;
                    c.a = alpha;
                    _image.color = c;
                }

                yield return null;
            }

            _fadeRoutine = null;
            _onComplete?.Invoke(); // pool return — no Destroy
        }
    }
}
