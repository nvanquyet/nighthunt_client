using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Deployables;
using NightHunt.Gameplay.Boss;
using NightHunt.Gameplay.Objective;
using NightHunt.Gameplay.Spectator;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Generic world-space health bar that works for <b>any</b> entity:
    ///   • Players / bosses → subscribe to <see cref="PlayerHealthSystem.OnHitReceived"/>
    ///   • Deployables     → subscribe to <see cref="BaseDeployable.OnHealthChangedClient"/>
    ///
    /// The component auto-discovers its source in Awake() using ComponentResolver.
    /// It can be placed on the root prefab GO or on any child (e.g. a "WorldHUD" child canvas).
    ///
    /// Setup (runtime auto-create if nothing is assigned):
    ///   1. Attach this component to the root GO or any child.
    ///   2. Optionally assign _barRoot, _healthSlider, _healthText in the Inspector.
    ///      If none are assigned the bar is procedurally built in EnsureView().
    ///   3. Adjust _hideDelay and _offset as needed.
    ///
    /// Billboard: always faces the active spectator / main camera via LateUpdate.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldHealthBarGeneric : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("References (optional — auto-built if missing)")]
        [Tooltip("Root GO of the bar canvas. Toggled visible/hidden.")]
        [SerializeField] private GameObject _barRoot;

        [Tooltip("Slider used as the health fill (value 0–1).")]
        [SerializeField] private Slider _healthSlider;

        [Tooltip("(Optional) TMP text showing 'current / max'.")]
        [SerializeField] private TextMeshProUGUI _healthText;

        [Header("Display")]
        [Tooltip("Seconds after the last hit before the bar hides automatically.")]
        [SerializeField] private float _hideDelay = 4f;

        [Tooltip("World-space offset applied to the bar root position.")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 2.5f, 0f);

        // ── Source refs ────────────────────────────────────────────────────────

        private PlayerHealthSystem _playerHealthSystem;
        private BaseDeployable     _deployable;
        private BossController     _boss;
        private EMPNodeObjective   _empNode;

        // ── State ──────────────────────────────────────────────────────────────

        private UnityEngine.Camera _mainCamera;
        private Coroutine _hideCoroutine;

        private float _currentHP;
        private float _maxHP;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
#if UNITY_SERVER
            enabled = false;
            return;
#endif
            // Auto-discover source — prefer PlayerHealthSystem, fall back to BaseDeployable.
            _playerHealthSystem = ComponentResolver.Find<PlayerHealthSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrDefault(null)
                .Resolve();

            _deployable = ComponentResolver.Find<BaseDeployable>(this)
                .OnSelf().InChildren().InParent()
                .OrDefault(null)
                .Resolve();

            _boss = ComponentResolver.Find<BossController>(this)
                .OnSelf().InChildren().InParent()
                .OrDefault(null)
                .Resolve();

            _empNode = ComponentResolver.Find<EMPNodeObjective>(this)
                .OnSelf().InChildren().InParent()
                .OrDefault(null)
                .Resolve();

            if (_playerHealthSystem == null && _deployable == null && _boss == null && _empNode == null)
            {
                Debug.LogWarning("[WorldHealthBarGeneric] No supported health source found. Bar will never show.", this);
                enabled = false;
                return;
            }

            EnsureView();
            SetVisible(false);

            _mainCamera = UnityEngine.Camera.main;
        }

        private void OnEnable()
        {
            if (_playerHealthSystem != null)
                _playerHealthSystem.OnHitReceived += HandlePlayerHit;

            if (_deployable != null)
                _deployable.OnHealthChangedClient += HandleDeployableHPChanged;

            if (_boss != null)
                _boss.OnHealthChanged += HandleFloatHPChanged;

            if (_empNode != null)
                _empNode.OnHealthChanged += HandleFloatHPChanged;
        }

        private void OnDisable()
        {
            if (_playerHealthSystem != null)
                _playerHealthSystem.OnHitReceived -= HandlePlayerHit;

            if (_deployable != null)
                _deployable.OnHealthChangedClient -= HandleDeployableHPChanged;

            if (_boss != null)
                _boss.OnHealthChanged -= HandleFloatHPChanged;

            if (_empNode != null)
                _empNode.OnHealthChanged -= HandleFloatHPChanged;
        }

        private void Start()
        {
            // Set initial values.
            if (_deployable != null)
            {
                _maxHP     = _deployable.MaxHP > 0 ? _deployable.MaxHP : 100f;
                _currentHP = _deployable.CurrentHP;
                RefreshSlider();
            }
            else if (_boss != null)
            {
                _maxHP     = _boss.MaxHp > 0f ? _boss.MaxHp : 100f;
                _currentHP = _boss.CurrentHp;
                RefreshSlider();
            }
            else if (_empNode != null)
            {
                _maxHP     = _empNode.MaxHealth > 0f ? _empNode.MaxHealth : 100f;
                _currentHP = _empNode.CurrentHealth;
                RefreshSlider();
            }
        }

        private void LateUpdate()
        {
            if (_barRoot == null || !_barRoot.activeSelf) return;

            // Reposition.
            _barRoot.transform.position = transform.position + _offset;

            // Billboard toward active camera.
            UnityEngine.Camera cam = GetActiveCamera();
            if (cam != null)
                _barRoot.transform.LookAt(
                    _barRoot.transform.position + cam.transform.rotation * Vector3.forward,
                    cam.transform.rotation * Vector3.up);
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        private void HandlePlayerHit(DamageInfo info)
        {
            // Players expose HP through the stat system; we just show the bar on any hit.
            ShowBar();
        }

        private void HandleDeployableHPChanged(int oldHP, int newHP)
        {
            if (newHP >= oldHP) return; // Only show when taking damage, not on heal.

            _currentHP = newHP;
            _maxHP     = _deployable != null && _deployable.MaxHP > 0 ? _deployable.MaxHP : _maxHP;

            RefreshSlider();
            ShowBar();
        }

        private void HandleFloatHPChanged(float currentHP, float maxHP)
        {
            bool tookDamage = _maxHP <= 0f || currentHP < _currentHP;
            _currentHP = currentHP;
            _maxHP = maxHP > 0f ? maxHP : _maxHP;

            RefreshSlider();

            if (tookDamage)
                ShowBar();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ShowBar()
        {
            SetVisible(true);

            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(_hideDelay);
            SetVisible(false);
            _hideCoroutine = null;
        }

        private void SetVisible(bool visible)
        {
            if (_barRoot != null && _barRoot.activeSelf != visible)
                _barRoot.SetActive(visible);
        }

        private void RefreshSlider()
        {
            if (_healthSlider == null) return;

            float max = _maxHP > 0f ? _maxHP : 1f;
            _healthSlider.value = Mathf.Clamp01(_currentHP / max);

            if (_healthText != null)
                _healthText.text = $"{Mathf.CeilToInt(_currentHP)} / {Mathf.CeilToInt(max)}";
        }

        private UnityEngine.Camera GetActiveCamera()
        {
            if (_mainCamera == null) _mainCamera = UnityEngine.Camera.main;
            return _mainCamera;
        }

        // ── Procedural view builder ────────────────────────────────────────────

        private void EnsureView()
        {
            if (_barRoot != null && _healthSlider != null) return;

            var root = new GameObject("WorldHealthBarRoot",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            root.transform.localPosition = _offset;
            root.transform.localScale    = Vector3.one * 0.01f;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(120f, 20f);

            // Slider.
            var sliderGo = new GameObject("HealthBar", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(root.transform, false);
            var sliderRect = (RectTransform)sliderGo.transform;
            sliderRect.anchorMin  = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax  = new Vector2(0.5f, 0.5f);
            sliderRect.pivot      = new Vector2(0.5f, 0.5f);
            sliderRect.sizeDelta  = new Vector2(110f, 10f);

            // Background.
            var bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bg.transform.SetParent(sliderGo.transform, false);
            Stretch((RectTransform)bg.transform);
            bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // Fill area + fill.
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            Stretch((RectTransform)fillArea.transform);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Stretch((RectTransform)fill.transform);
            fill.GetComponent<Image>().color = new Color(0.18f, 0.9f, 0.36f, 1f);

            _barRoot = root;
            _healthSlider = sliderGo.GetComponent<Slider>();
            _healthSlider.minValue     = 0f;
            _healthSlider.maxValue     = 1f;
            _healthSlider.value        = 1f;
            _healthSlider.interactable = false;
            _healthSlider.fillRect     = (RectTransform)fill.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
