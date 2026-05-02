using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// 2D expanding ring pulse shown around a UI button when it is activated (MOBA skill feel).
    ///
    /// How to set up:
    ///   1. Create a child Image under the button named "PulseRing".
    ///   2. Assign a circular/donut sprite (or a soft-edge circle).
    ///   3. Add this component to the BUTTON root. Assign _pulseImage in Inspector.
    ///   4. Call <see cref="Play"/> from <see cref="FireButton.OnPointerDown"/> or any attack handler.
    ///
    /// Effect: The ring starts at _startScale*, fades in quickly, expands to _endScale*,
    ///         then fades out. Gives the same "feedback ring" feel as MOBA skill buttons.
    /// </summary>
    public class ButtonPulseRing : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Visual")]
        [Tooltip("The ring Image child — assign manually or auto-find by name 'PulseRing'.")]
        [SerializeField]
        private Image _pulseImage;

        [Header("Animation")]
        [Tooltip("Starting size relative to button size (e.g. 1.0 = same size as button).")]
        [SerializeField]
        private float _startScale = 1.0f;

        [Tooltip("Ending size — ring expands to this scale.")] [SerializeField]
        private float _endScale = 2.2f;

        [Tooltip("Total duration in seconds.")] [SerializeField]
        private float _duration = 0.45f;

        [Tooltip("Ring color at peak opacity.")] [SerializeField]
        private Color _pulseColor = new Color(1f, 0.5f, 0.05f, 0.9f); // orange

        // ── Runtime ────────────────────────────────────────────────────────────

        private RectTransform _pulseRT;
        private Coroutine _anim;
        private Vector2 _baseSize;

        // ── Unity Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_pulseImage == null)
            {
                // Auto-find by conventional name
                var found = transform.Find("PulseRing");
                if (found != null)
                {
                    _pulseImage = ComponentResolver.Find<Image>(found)
                        .OnSelf()
                        .InChildren()
                        .OrLogWarning("[Auto] Image not found")
                        .Resolve();
                }
                else
                {
                    // Auto-create: add a sibling-sized Image child so the pulse works
                    // without any manual prefab setup.
                    var go = new GameObject("PulseRing", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(transform, false);
                    var rt = ComponentResolver.Find<RectTransform>(go)
                        .OnSelf()
                        .InChildren()
                        .OrLogWarning("[Auto] RectTransform not found")
                        .Resolve();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    var img = ComponentResolver.Find<Image>(go)
                        .OnSelf()
                        .InChildren()
                        .OrLogWarning("[Auto] Image not found")
                        .Resolve();
                    img.raycastTarget = false;
                    img.color = new Color(_pulseColor.r, _pulseColor.g, _pulseColor.b, 0f);
                    _pulseImage = img;
                }
            }

            if (_pulseImage != null)
            {
                _pulseRT = _pulseImage.rectTransform;
                _baseSize = _pulseRT.sizeDelta;
                _pulseImage.color = new Color(_pulseColor.r, _pulseColor.g, _pulseColor.b, 0f);
                _pulseImage.gameObject.SetActive(true);
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Play the ring pulse animation once. Safe to call every press (cancels previous).</summary>
        public void Play()
        {
            if (_pulseImage == null) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Animate());
        }

        // ── Animation ──────────────────────────────────────────────────────────

        private IEnumerator Animate()
        {
            float half = _duration * 0.5f;
            // Fade-in phase (scale grows + alpha reaches peak)
            for (float t = 0; t < half; t += Time.deltaTime)
            {
                float p = t / half;
                float s = Mathf.Lerp(_startScale, _endScale, p);
                _pulseRT.sizeDelta = _baseSize * s;
                SetAlpha(p);
                yield return null;
            }

            // Fade-out phase (continue expanding + alpha drops)
            for (float t = 0; t < half; t += Time.deltaTime)
            {
                float p = t / half;
                float s = Mathf.Lerp(_endScale, _endScale * 1.15f, p);
                _pulseRT.sizeDelta = _baseSize * s;
                SetAlpha(1f - p);
                yield return null;
            }

            _pulseRT.sizeDelta = _baseSize;
            SetAlpha(0f);
            _anim = null;
        }

        private void SetAlpha(float a)
        {
            _pulseImage.color = new Color(_pulseColor.r, _pulseColor.g, _pulseColor.b,
                _pulseColor.a * a);
        }
    }
}