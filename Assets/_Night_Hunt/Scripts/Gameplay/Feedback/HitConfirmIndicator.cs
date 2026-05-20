using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.Gameplay.Feedback
{
    [DisallowMultipleComponent]
    public sealed class HitConfirmIndicator : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image[] _segments;
        [SerializeField] private float _startScale = 1.12f;
        [SerializeField] private float _endScale = 0.86f;

        private RectTransform _rect;
        private Coroutine _routine;
        private Action _onComplete;

        private void Awake()
        {
            _rect = transform as RectTransform;
            if (_rect == null)
                _rect = gameObject.AddComponent<RectTransform>();

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            EnsureSegments();
        }

        public void Initialize(Color color, float lifetime, Action onComplete = null)
        {
            _onComplete = onComplete;
            EnsureSegments();

            foreach (var segment in _segments)
            {
                if (segment == null) continue;
                segment.color = color;
            }

            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;

            if (_rect != null)
                _rect.localScale = Vector3.one * _startScale;

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(FadeRoutine(Mathf.Max(0.05f, lifetime)));
        }

        private IEnumerator FadeRoutine(float lifetime)
        {
            float elapsed = 0f;
            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 1f - t;
                if (_rect != null)
                    _rect.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, t);
                yield return null;
            }

            _routine = null;
            _onComplete?.Invoke();
        }

        private void EnsureSegments()
        {
            if (_segments != null && _segments.Length >= 4)
                return;

            _segments = new Image[4];
            CreateSegment(0, "TopRight", new Vector2(17f, 17f), -45f);
            CreateSegment(1, "TopLeft", new Vector2(-17f, 17f), 45f);
            CreateSegment(2, "BottomRight", new Vector2(17f, -17f), 45f);
            CreateSegment(3, "BottomLeft", new Vector2(-17f, -17f), -45f);
        }

        private void CreateSegment(int index, string label, Vector2 anchoredPosition, float rotationZ)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(4f, 22f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            _segments[index] = image;
        }
    }
}
