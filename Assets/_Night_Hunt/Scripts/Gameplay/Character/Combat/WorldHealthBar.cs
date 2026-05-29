using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking.Player;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Single world-space health bar component. Add this only to prefabs that need a bar.
    /// The component resolves one IHealthSource in the prefab hierarchy and listens to its
    /// network-synced health events.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldHealthBar : MonoBehaviour
    {
        private enum VisibilityPolicy
        {
            Auto,
            AnyDamage,
            PlayerShooterTeamOrSpectated
        }

        [Header("Follow")]
        [Tooltip("Optional transform the bar follows. If empty, follows the resolved health source transform.")]
        [SerializeField] private Transform _followTarget;

        [Header("References")]
        [Tooltip("Child visual root toggled visible/hidden. Do not assign the GameObject that holds this component.")]
        [SerializeField] private GameObject _barRoot;

        [Tooltip("Slider used as the health fill (value 0-1).")]
        [SerializeField] private Slider _healthSlider;

        [Tooltip("(Optional) TMP text showing 'current / max'.")]
        [SerializeField] private TextMeshProUGUI _healthText;

        [Header("Display")]
        [Tooltip("Auto = player bars use shooter/team rules; non-player bars show on any damage.")]
        [SerializeField] private VisibilityPolicy _visibilityPolicy = VisibilityPolicy.Auto;

        [Tooltip("Seconds after the last damage event before the bar hides automatically.")]
        [SerializeField] private float _hideDelay = 4f;

        [Tooltip("World-space offset from the followed transform.")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 2.5f, 0f);

        [SerializeField] private bool _billboardToCamera = true;

        [Tooltip("Fallback for quick setup. Production prefabs should normally assign references explicitly.")]
        [SerializeField] private bool _buildViewIfMissing = true;

        private IHealthSource _healthSource;
        private Transform _sourceTransform;
        private NetworkPlayer _networkPlayer;
        private UnityEngine.Camera _mainCamera;
        private Coroutine _hideCoroutine;
        private bool _subscribed;
        private float _currentHealth;
        private float _maxHealth;

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveViewReferences();
        }
#endif

        private void Reset()
        {
            ResolveViewReferences();
        }

        /// <summary>
        /// Configure this WorldHealthBar for a runtime-injected deployable context.
        /// Call immediately after AddComponent&lt;WorldHealthBar&gt;() before the object is enabled.
        /// </summary>
        public void InitForDeployable(float verticalOffset = 1.8f)
        {
            _visibilityPolicy = VisibilityPolicy.AnyDamage;
            _offset = new Vector3(0f, verticalOffset, 0f);
            _buildViewIfMissing = true;
            _hideDelay = 4f;
        }

        private void Awake()
        {
#if UNITY_SERVER
            enabled = false;
            return;
#endif
            ResolveSource();
            ResolveNetworkPlayer();

            if (_healthSource == null)
            {
                Debug.LogWarning(
                    $"[WorldHealthBar] '{name}' has no IHealthSource. Add PlayerHealthSystem/BaseDeployable/etc. on this prefab.",
                    this);
                enabled = false;
                return;
            }

            if (!EnsureView())
            {
                enabled = false;
                return;
            }

            SetInitialValues();
            SetVisible(false);
            _mainCamera = UnityEngine.Camera.main;
        }

        private void OnEnable()
        {
            Subscribe();
            Debug.Log($"[DAMAGE][HEALTHBAR] '{name}' subscribed to IHealthSource: {(_healthSource as Component)?.name ?? (_healthSource?.GetType().Name ?? "NULL")}");
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopHideTimer();
        }

        private void LateUpdate()
        {
            if (_barRoot == null || !_barRoot.activeSelf)
                return;

            Transform follow = GetFollowTransform();
            if (follow != null)
                _barRoot.transform.position = follow.position + _offset;

            if (!_billboardToCamera)
                return;

            UnityEngine.Camera cam = GetActiveCamera();
            if (cam == null)
                return;

            _barRoot.transform.LookAt(
                _barRoot.transform.position + cam.transform.rotation * Vector3.forward,
                cam.transform.rotation * Vector3.up);
        }

        private void HandleHealthChanged(HealthChangeEvent evt)
        {
            SetHealth(evt.CurrentHealth, evt.MaxHealth);

            if (evt.IsDamage && ShouldShow(evt))
                ShowBar();
        }

        private bool ShouldShow(HealthChangeEvent evt)
        {
            if (_visibilityPolicy == VisibilityPolicy.AnyDamage)
                return true;

            bool playerPolicy =
                _visibilityPolicy == VisibilityPolicy.PlayerShooterTeamOrSpectated ||
                (_visibilityPolicy == VisibilityPolicy.Auto && _networkPlayer != null);

            if (!playerPolicy)
                return true;

            return ShouldShowForPlayer(evt.InstigatorNetworkObjectId);
        }

        private bool ShouldShowForPlayer(int shooterNetObjId)
        {
            if (_networkPlayer != null && _networkPlayer.IsOwner)
                return false;

            var spectate = SpectateManager.Instance;
            var localPlayer = spectate?.GetLocalPlayer();
            if (localPlayer == null)
                return false;

            var currentObserved = spectate.GetCurrentPlayer();
            if (currentObserved != null && _networkPlayer == currentObserved)
                return true;

            if (shooterNetObjId <= 0)
                return _networkPlayer != null && _networkPlayer.TeamId == localPlayer.TeamId;

            if ((int)localPlayer.ObjectId == shooterNetObjId)
                return true;

            var registry = PlayerPublicRegistry.Instance;
            if (registry == null)
                return false;

            var allPlayers = registry.GetAllPlayers();
            if (allPlayers == null)
                return false;

            foreach (var player in allPlayers)
            {
                if (player == null || (int)player.ObjectId != shooterNetObjId)
                    continue;

                return player.TeamId == localPlayer.TeamId;
            }

            return false;
        }

        private void ResolveSource()
        {
            _healthSource = ComponentResolver.Find<IHealthSource>(this)
                .OnSelf()
                .InParent()
                .InChildren()
                .OrDefault(null)
                .Resolve();

            if (_healthSource is Component sourceComponent)
            {
                _sourceTransform = sourceComponent.transform;

                if (_followTarget == null)
                    _followTarget = sourceComponent.transform;
            }
        }

        private void ResolveNetworkPlayer()
        {
            _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
                .OnSelf()
                .InParent()
                .InChildren()
                .OrDefault(null)
                .Resolve();
        }

        private bool EnsureView()
        {
            ResolveViewReferences();

            if (_barRoot != null && _barRoot == gameObject)
            {
                Debug.LogWarning(
                    $"[WorldHealthBar] '{name}' has Bar Root on the same GameObject. Use a child visual root.",
                    this);
                return false;
            }

            if (_barRoot != null && _healthSlider != null)
                return true;

            if (!_buildViewIfMissing)
            {
                Debug.LogWarning($"[WorldHealthBar] '{name}' is missing Bar Root or Health Slider references.", this);
                return false;
            }

            BuildDefaultView();
            return _barRoot != null && _healthSlider != null;
        }

        private void ResolveViewReferences()
        {
            if (_healthSlider == null)
                _healthSlider = GetComponentInChildren<Slider>(true);

            if (_barRoot == null && _healthSlider != null)
            {
                Canvas canvas = _healthSlider.GetComponentInParent<Canvas>(true);
                _barRoot = canvas != null && canvas.gameObject != gameObject
                    ? canvas.gameObject
                    : _healthSlider.gameObject;
            }
        }

        private void BuildDefaultView()
        {
            var root = new GameObject("HealthBarVisual",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            root.transform.localPosition = _offset;
            root.transform.localScale = Vector3.one * 0.01f;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(120f, 20f);

            var sliderGo = new GameObject("HealthBar", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(root.transform, false);

            var sliderRect = (RectTransform)sliderGo.transform;
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.sizeDelta = new Vector2(110f, 10f);

            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(sliderGo.transform, false);
            Stretch((RectTransform)background.transform);
            background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            Stretch((RectTransform)fillArea.transform);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Stretch((RectTransform)fill.transform);
            fill.GetComponent<Image>().color = new Color(0.18f, 0.9f, 0.36f, 1f);

            _barRoot = root;
            _healthSlider = sliderGo.GetComponent<Slider>();
            _healthSlider.minValue = 0f;
            _healthSlider.maxValue = 1f;
            _healthSlider.value = 1f;
            _healthSlider.interactable = false;
            _healthSlider.targetGraphic = fill.GetComponent<Image>();
            _healthSlider.fillRect = (RectTransform)fill.transform;
        }

        private void SetInitialValues()
        {
            SetHealth(_healthSource.CurrentHealth, _healthSource.MaxHealth);
        }

        private void Subscribe()
        {
            if (_subscribed || _healthSource == null)
                return;

            _healthSource.HealthChanged += HandleHealthChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _healthSource == null)
                return;

            _healthSource.HealthChanged -= HandleHealthChanged;
            _subscribed = false;
        }

        private Transform GetFollowTransform()
        {
            if (_followTarget != null)
                return _followTarget;

            return _sourceTransform != null ? _sourceTransform : transform;
        }

        private UnityEngine.Camera GetActiveCamera()
        {
            if (_mainCamera == null)
                _mainCamera = UnityEngine.Camera.main;

            return _mainCamera;
        }

        private void ShowBar()
        {
            SetVisible(true);

            if (_hideCoroutine != null)
                StopCoroutine(_hideCoroutine);

            _hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(_hideDelay);
            SetVisible(false);
            _hideCoroutine = null;
        }

        private void StopHideTimer()
        {
            if (_hideCoroutine == null)
                return;

            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

        private void SetVisible(bool visible)
        {
            if (_barRoot != null && _barRoot.activeSelf != visible)
                _barRoot.SetActive(visible);
        }

        private void SetHealth(float current, float max)
        {
            _maxHealth = max > 0f ? max : (_maxHealth > 0f ? _maxHealth : 1f);
            _currentHealth = Mathf.Clamp(current, 0f, _maxHealth);
            RefreshSlider();
        }

        private void RefreshSlider()
        {
            if (_healthSlider != null)
                _healthSlider.value = Mathf.Clamp01(_currentHealth / Mathf.Max(1f, _maxHealth));

            if (_healthText != null)
                _healthText.text = $"{Mathf.CeilToInt(_currentHealth)} / {Mathf.CeilToInt(_maxHealth)}";
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
