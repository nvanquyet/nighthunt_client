using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Objective;

namespace NightHunt.UI
{
    [DisallowMultipleComponent]
    public sealed class ObjectiveCaptureHUD : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private float _refreshInterval = 0.15f;
        [SerializeField] private float _completionToastSeconds = 2f;

        private readonly List<IObjective> _objectives = new();
        private bool _hudAllowed = true;
        private float _nextRefreshTime;
        private float _showCompletedUntil;
        private string _completedTitle;
        private int _completedTeamId = -1;

        public void SetHudVisible(bool visible)
        {
            _hudAllowed = visible;
            if (!visible)
                SetVisible(false);
        }

        private void Awake()
        {
            EnsureView();
            SetVisible(false);
        }

        private void OnEnable()
        {
            GameplayEventBus.Instance?.Subscribe<ObjectiveCapturedEvent>(OnObjectiveCaptured);
            RefreshObjectiveCache();
        }

        private void OnDisable()
        {
            GameplayEventBus.Instance?.Unsubscribe<ObjectiveCapturedEvent>(OnObjectiveCaptured);
        }

        private void Update()
        {
            if (!_hudAllowed)
            {
                SetVisible(false);
                return;
            }

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + _refreshInterval;

            if (Time.unscaledTime < _showCompletedUntil)
            {
                ShowCompleted();
                return;
            }

            var objective = PickVisibleObjective();
            if (objective == null)
            {
                SetVisible(false);
                return;
            }

            ShowObjective(objective);
        }

        private void RefreshObjectiveCache()
        {
            _objectives.Clear();
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IObjective objective)
                    _objectives.Add(objective);
            }
        }

        private IObjective PickVisibleObjective()
        {
            if (_objectives.Count == 0)
                RefreshObjectiveCache();

            IObjective best = null;
            float bestProgress = 0f;

            for (int i = _objectives.Count - 1; i >= 0; i--)
            {
                var objective = _objectives[i];
                if (objective == null)
                {
                    _objectives.RemoveAt(i);
                    continue;
                }

                if (objective.IsCompleted)
                    continue;

                float progress = Mathf.Clamp01(objective.Progress);
                if (progress <= 0.001f)
                    continue;

                if (best == null || progress > bestProgress)
                {
                    best = objective;
                    bestProgress = progress;
                }
            }

            return best;
        }

        private void ShowObjective(IObjective objective)
        {
            float progress = Mathf.Clamp01(objective.Progress);
            SetVisible(true);

            if (_titleText != null)
                _titleText.text = objective.ObjectiveName;

            if (_progressSlider != null)
                _progressSlider.value = progress;

            if (_statusText != null)
                _statusText.text = BuildStatusText(objective, progress);
        }

        private string BuildStatusText(IObjective objective, float progress)
        {
            int percent = Mathf.RoundToInt(progress * 100f);

            if (objective is CaptureZoneObjective capture)
            {
                if (capture.IsContested)
                    return $"CONTESTED  {percent}%";

                int team = capture.ControllingTeamId;
                if (team >= 0)
                    return $"TEAM {team} CAPTURING  {percent}%";

                return $"DECAYING  {percent}%";
            }

            return $"IN PROGRESS  {percent}%";
        }

        private void OnObjectiveCaptured(ObjectiveCapturedEvent evt)
        {
            _completedTitle = evt.ObjectiveName;
            _completedTeamId = evt.CapturingTeamId;
            _showCompletedUntil = Time.unscaledTime + _completionToastSeconds;
        }

        private void ShowCompleted()
        {
            SetVisible(true);

            if (_titleText != null)
                _titleText.text = _completedTitle;

            if (_progressSlider != null)
                _progressSlider.value = 1f;

            if (_statusText != null)
            {
                _statusText.text = _completedTeamId >= 0
                    ? $"TEAM {_completedTeamId} CAPTURED"
                    : "COMPLETED";
            }
        }

        private void EnsureView()
        {
            if (_root != null && _titleText != null && _statusText != null && _progressSlider != null)
                return;

            _root = new GameObject("[ObjectiveCaptureHUD]", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _root.transform.SetParent(transform, false);

            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -92f);
            rootRect.sizeDelta = new Vector2(360f, 58f);

            var bg = _root.GetComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.025f, 0.72f);

            _titleText = CreateText("Title", _root.transform, new Vector2(0f, -8f), new Vector2(332f, 20f), 15, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _statusText = CreateText("Status", _root.transform, new Vector2(0f, -34f), new Vector2(332f, 18f), 12, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            _progressSlider = CreateSlider(_root.transform);
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static Slider CreateSlider(Transform parent)
        {
            var sliderGo = new GameObject("Progress", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(parent, false);

            var rect = (RectTransform)sliderGo.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -30f);
            rect.sizeDelta = new Vector2(332f, 6f);

            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(sliderGo.transform, false);
            Stretch((RectTransform)background.transform);
            background.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            Stretch((RectTransform)fillArea.transform);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Stretch((RectTransform)fill.transform);
            fill.GetComponent<Image>().color = new Color(0.12f, 0.82f, 0.72f, 1f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;
            slider.targetGraphic = fill.GetComponent<Image>();
            slider.fillRect = (RectTransform)fill.transform;
            return slider;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible)
                _root.SetActive(visible);
        }
    }
}
