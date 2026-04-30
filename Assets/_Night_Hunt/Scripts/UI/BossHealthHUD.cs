using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Gameplay.Boss;
using NightHunt.Gameplay.Core.Events;

namespace NightHunt.UI
{
    [DisallowMultipleComponent]
    public sealed class BossHealthHUD : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _hpText;
        [SerializeField] private Slider _hpSlider;
        [SerializeField] private float _scanInterval = 0.5f;
        [SerializeField] private float _hideAfterFullHpSeconds = 1.5f;

        private BossController _boss;
        private bool _hudAllowed = true;
        private float _nextScanTime;
        private float _lastDamagedTime = -999f;

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
            GameplayEventBus.Instance?.Subscribe<BossSpawnedEvent>(OnBossSpawned);
            GameplayEventBus.Instance?.Subscribe<BossKilledEvent>(OnBossKilled);
            FindBoss();
        }

        private void OnDisable()
        {
            GameplayEventBus.Instance?.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
            GameplayEventBus.Instance?.Unsubscribe<BossKilledEvent>(OnBossKilled);
            UnsubscribeBoss();
        }

        private void Update()
        {
            if (!_hudAllowed)
            {
                SetVisible(false);
                return;
            }

            if (_boss == null && Time.unscaledTime >= _nextScanTime)
            {
                _nextScanTime = Time.unscaledTime + _scanInterval;
                FindBoss();
            }

            if (_boss == null)
            {
                SetVisible(false);
                return;
            }

            Refresh(_boss.CurrentHp, _boss.MaxHp);
        }

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            FindBoss();
        }

        private void OnBossKilled(BossKilledEvent evt)
        {
            if (_boss != null && (_boss.BossId == evt.BossId || string.IsNullOrEmpty(evt.BossId)))
                Refresh(0f, _boss.MaxHp);

            SetVisible(false);
            UnsubscribeBoss();
        }

        private void FindBoss()
        {
            var boss = FindFirstObjectByType<BossController>();
            if (boss == _boss)
                return;

            UnsubscribeBoss();
            _boss = boss;

            if (_boss != null)
            {
                _boss.OnHealthChanged += OnBossHealthChanged;
                Refresh(_boss.CurrentHp, _boss.MaxHp);
            }
        }

        private void UnsubscribeBoss()
        {
            if (_boss != null)
                _boss.OnHealthChanged -= OnBossHealthChanged;
            _boss = null;
        }

        private void OnBossHealthChanged(float current, float max)
        {
            _lastDamagedTime = Time.unscaledTime;
            Refresh(current, max);
        }

        private void Refresh(float current, float max)
        {
            if (_boss == null || _boss.IsDead || max <= 0f)
            {
                SetVisible(false);
                return;
            }

            bool shouldShow = current < max || Time.unscaledTime - _lastDamagedTime <= _hideAfterFullHpSeconds;
            SetVisible(shouldShow);

            if (!shouldShow)
                return;

            if (_nameText != null)
                _nameText.text = string.IsNullOrWhiteSpace(_boss.BossId) ? "BOSS" : _boss.BossId.ToUpperInvariant();

            if (_hpSlider != null)
                _hpSlider.value = Mathf.Clamp01(current / max);

            if (_hpText != null)
                _hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        private void EnsureView()
        {
            if (_root != null && _nameText != null && _hpText != null && _hpSlider != null)
                return;

            _root = new GameObject("[BossHealthHUD]", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _root.transform.SetParent(transform, false);

            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -22f);
            rootRect.sizeDelta = new Vector2(480f, 52f);

            var bg = _root.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.015f, 0.018f, 0.72f);

            _nameText = CreateText("Name", _root.transform, new Vector2(0f, -6f), new Vector2(452f, 18f), 14, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _hpText = CreateText("Hp", _root.transform, new Vector2(0f, -31f), new Vector2(452f, 18f), 12, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            _hpSlider = CreateSlider(_root.transform);
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
            return text;
        }

        private static Slider CreateSlider(Transform parent)
        {
            var sliderGo = new GameObject("HP", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(parent, false);

            var rect = (RectTransform)sliderGo.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -28f);
            rect.sizeDelta = new Vector2(452f, 8f);

            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(sliderGo.transform, false);
            Stretch((RectTransform)background.transform);
            background.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            Stretch((RectTransform)fillArea.transform);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Stretch((RectTransform)fill.transform);
            fill.GetComponent<Image>().color = new Color(0.9f, 0.13f, 0.16f, 1f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
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
