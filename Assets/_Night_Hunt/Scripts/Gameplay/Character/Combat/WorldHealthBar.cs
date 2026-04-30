using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Spectator;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// World-space health bar shown above a character when they are hit by the local player
    /// or a teammate. Hidden by default; shown for <see cref="_hideDelay"/> seconds from the
    /// last qualifying hit.
    ///
    /// Setup on the player prefab:
    ///   1. Create a child GameObject "WorldHealthBarRoot" at Y ≈ 2.5.
    ///   2. Add a Canvas (World Space, Scale 0.01) to the child.
    ///   3. Inside the canvas add a Slider "HealthBar" and an optional TMP text "HealthText".
    ///   4. Attach this component to the player prefab root (or the canvas root).
    ///   5. Assign _barRoot, _healthSlider (and optionally _healthText) in the Inspector.
    ///   6. DO NOT enable the component on dedicated servers — guard with
    ///      <c>if (IsServer &amp;&amp; !IsClient) { enabled = false; return; }</c> if needed.
    ///
    /// The component subscribes to:
    ///   • PlayerHealthSystem.OnHitReceived (instance event) → show/reset timer
    ///   • IPlayerStatSystem.OnStatChanged → refresh bar fill while visible
    /// </summary>
    public class WorldHealthBar : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Root GameObject of the health bar canvas. Toggled visible/hidden.")]
        [SerializeField] private GameObject _barRoot;

        [Tooltip("Slider used as the health fill (value 0–1).")]
        [SerializeField] private Slider _healthSlider;

        [Tooltip("(Optional) Text showing 'current / max' health.")]
        [SerializeField] private TextMeshProUGUI _healthText;

        [Header("Display Settings")]
        [Tooltip("Seconds after the last qualifying hit before the bar is hidden automatically.")]
        [SerializeField] private float _hideDelay = 7f;

        [Tooltip("World-space height offset above the character pivot.")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 2.5f, 0f);

        // ── Runtime ────────────────────────────────────────────────────────────

        private PlayerHealthSystem  _healthSystem;
        private NetworkPlayer       _networkPlayer;
        private IPlayerStatSystem   _statSystem;
        private UnityEngine.Camera  _mainCamera;
        private Coroutine           _hideCoroutine;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            // Dedicated server has no Camera — skip entirely.
            if (UnityEngine.Camera.main == null) { enabled = false; return; }

            _healthSystem = ComponentResolver.Find<PlayerHealthSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[WorldHealthBar] PlayerHealthSystem not found")
                .Resolve();

            _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[WorldHealthBar] NetworkPlayer not found")
                .Resolve();

            _statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[WorldHealthBar] IPlayerStatSystem not found")
                .Resolve();

            EnsureView();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (_healthSystem != null)
                _healthSystem.OnHitReceived += HandleHitReceived;

            if (_statSystem != null)
                _statSystem.OnStatChanged += HandleStatChanged;
        }

        private void OnDisable()
        {
            if (_healthSystem != null)
                _healthSystem.OnHitReceived -= HandleHitReceived;

            if (_statSystem != null)
                _statSystem.OnStatChanged -= HandleStatChanged;
        }

        private void LateUpdate()
        {
            if (_barRoot == null || !_barRoot.activeSelf) return;

            // Reposition above the character.
            _barRoot.transform.position = transform.position + _offset;

            // Billboard — always face the local camera.
            if (_mainCamera == null) _mainCamera = UnityEngine.Camera.main;
            if (_mainCamera != null)
                _barRoot.transform.LookAt(
                    _barRoot.transform.position + _mainCamera.transform.rotation * Vector3.forward,
                    _mainCamera.transform.rotation * Vector3.up);
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void HandleHitReceived(DamageInfo info)
        {
            if (!ShouldShowForShooter(info.ShooterNetworkObjectId)) return;

            RefreshHealthBar();
            SetVisible(true);

            // Restart auto-hide timer.
            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfterDelay());

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableHealthBarDebugLogs)
                Debug.Log($"[WorldHealthBar] Showing for '{_networkPlayer?.DisplayName}' " +
                          $"— shooterNetObjId={info.ShooterNetworkObjectId} dmg={info.Damage:F0}");
        }

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            // Only refresh while the bar is actually visible (avoid unnecessary work).
            if (_barRoot == null || !_barRoot.activeSelf) return;
            if (type == PlayerStatType.Health || type == PlayerStatType.MaxHealth)
                RefreshHealthBar();
        }

        /// <summary>
        /// Returns true if the shot was fired by the local player or one of their teammates.
        /// </summary>
        private bool ShouldShowForShooter(int shooterNetObjId)
        {
            // Don't show on the local player's own character (self-damage / self-heal).
            if (_networkPlayer != null && _networkPlayer.IsOwner) return false;

            var localPlayer = SpectateManager.Instance?.GetLocalPlayer();
            if (localPlayer == null) return false;

            var currentObserved = SpectateManager.Instance?.GetCurrentPlayer();
            if (currentObserved != null && _networkPlayer == currentObserved)
                return true;

            // -1 / 0 = world / boss / anti-camp damage. Show it for teammates so
            // the local/spectate camera still gets a world-space health reference.
            if (shooterNetObjId <= 0)
                return _networkPlayer != null && _networkPlayer.TeamId == localPlayer.TeamId;

            // Local player is the shooter → always show.
            if ((int)localPlayer.ObjectId == shooterNetObjId) return true;

            // Teammate is the shooter → show.
            var registry = PlayerPublicRegistry.Instance;
            if (registry == null) return false;

            var allPlayers = registry.GetAllPlayers();
            if (allPlayers == null) return false;

            foreach (var player in allPlayers)
            {
                if (player == null || (int)player.ObjectId != shooterNetObjId) continue;
                return player.TeamId == localPlayer.TeamId;
            }

            return false;
        }

        private void RefreshHealthBar()
        {
            if (_statSystem == null) return;

            float current   = _statSystem.GetStat(PlayerStatType.Health);
            float maxHealth = _statSystem.GetStat(PlayerStatType.MaxHealth);
            if (maxHealth <= 0f) maxHealth = 100f;

            if (_healthSlider != null)
                _healthSlider.value = Mathf.Clamp01(current / maxHealth);

            if (_healthText != null)
                _healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maxHealth)}";
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

        private void EnsureView()
        {
            if (_barRoot != null && _healthSlider != null)
                return;

            var root = new GameObject("WorldHealthBarRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
