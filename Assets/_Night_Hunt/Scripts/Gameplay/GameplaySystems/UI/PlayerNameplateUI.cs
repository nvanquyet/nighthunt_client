using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI
{
    /// <summary>
    /// World-space nameplate displayed above each player showing their name and health bar.
    ///
    /// This is a pure client-side component — it never runs logic on a dedicated server.
    ///
    /// Setup:
    ///   1. Add a child GameObject "Nameplate" to the player prefab with:
    ///        - Canvas (World Space, Render Mode = World Space)
    ///        - Set Canvas Sorting Layer to "Nameplates" (create if needed)
    ///        - Set Canvas Scale to 0.01 so it renders at ~1 cm per pixel
    ///   2. Inside the canvas add:
    ///        - TextMeshProUGUI  "NameText"   (player name)
    ///        - Slider           "HealthBar"  (health bar, interactable = false)
    ///        - (Optional) TextMeshProUGUI "HealthText" for "85 / 100" style label
    ///   3. Add this component to the player prefab root (or the Canvas root).
    ///   4. Assign _nameplateRoot, _nameText, _healthBar (and optionally _healthText).
    ///
    /// The component subscribes to:
    ///   - NetworkPlayer.OnPublicDataChanged → refresh display name
    ///   - NetworkPlayer.OnAliveChanged      → hide nameplate when dead
    ///   - IPlayerStatSystem.OnStatChanged   → live health updates
    /// </summary>
    public class PlayerNameplateUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Root GameObject of the nameplate canvas — toggled for distance/alive culling")]
        [SerializeField] private GameObject _nameplateRoot;

        [Tooltip("Text element that displays the player's name")]
        [SerializeField] private TextMeshProUGUI _nameText;

        [Tooltip("Slider used as the health bar (min=0, max=1)")]
        [SerializeField] private Slider _healthBar;

        [Tooltip("(Optional) Text showing health as 'current / max'")]
        [SerializeField] private TextMeshProUGUI _healthText;

        [Header("Visibility")]
        [Tooltip("Maximum distance (metres) at which the nameplate is shown")]
        [SerializeField] private float _visibleDistance = 20f;

        [Tooltip("Hide this nameplate when we are the owner of this player (first-person view)")]
        [SerializeField] private bool _hideForLocalPlayer = true;

        [Header("Billboard")]
        [Tooltip("World-space offset above the player pivot")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 2.2f, 0f);

        [Tooltip("Rotate to always face the main camera")]
        [SerializeField] private bool _billboard = true;

        // ── Runtime ───────────────────────────────────────────────────────────

        private NetworkPlayer       _networkPlayer;
        private IPlayerStatSystem   _statSystem;
        private Camera              _mainCamera;

        // Cached max health so we can compute bar fill without a second GetStat call.
        private float _maxHealth = 100f;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
                .OnSelf()
                .InParent()
                .OrLogWarning("[PlayerNameplateUI] NetworkPlayer not found")
                .Resolve();

            _statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .OrLogWarning("[PlayerNameplateUI] IPlayerStatSystem not found")
                .Resolve();

            _mainCamera = Camera.main;

            HideNameplate();
        }

        private void OnEnable()
        {
            if (_networkPlayer != null)
            {
                _networkPlayer.OnPublicDataChanged += OnPublicDataChanged;
                _networkPlayer.OnAliveChanged      += OnAliveChanged;
            }

            if (_statSystem != null)
                _statSystem.OnStatChanged += OnStatChanged;
        }

        private void OnDisable()
        {
            if (_networkPlayer != null)
            {
                _networkPlayer.OnPublicDataChanged -= OnPublicDataChanged;
                _networkPlayer.OnAliveChanged      -= OnAliveChanged;
            }

            if (_statSystem != null)
                _statSystem.OnStatChanged -= OnStatChanged;
        }

        private void Start()
        {
            // Dedicated servers have no Camera.main — skip all UI entirely.
            if (Camera.main == null) { enabled = false; return; }

            // Hide nameplate for the local (owning) player when configured.
            if (_hideForLocalPlayer && _networkPlayer != null && _networkPlayer.IsOwner)
            {
                HideNameplate();
                enabled = false;
                return;
            }

            RefreshName();
            RefreshHealth();
        }

        private void Update()
        {
            if (_nameplateRoot == null) return;

            // Distance culling.
            if (_mainCamera == null) _mainCamera = Camera.main;
            bool inRange = _mainCamera != null &&
                           Vector3.Distance(_mainCamera.transform.position,
                                             transform.position + _offset) <= _visibleDistance;

            // Respect alive state — don't show namplate over a dead body.
            bool shouldShow = inRange && (_networkPlayer == null || _networkPlayer.IsAlive);
            _nameplateRoot.SetActive(shouldShow);

            if (!shouldShow) return;

            // Reposition above the player.
            _nameplateRoot.transform.position = transform.position + _offset;

            // Billboard: rotate to face the camera.
            if (_billboard && _mainCamera != null)
            {
                _nameplateRoot.transform.LookAt(
                    _nameplateRoot.transform.position + _mainCamera.transform.rotation * Vector3.forward,
                    _mainCamera.transform.rotation * Vector3.up);
            }
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnPublicDataChanged(PlayerPublicData prev, PlayerPublicData next)
        {
            RefreshName();
        }

        private void OnAliveChanged(bool isAlive)
        {
            if (!isAlive)
                HideNameplate();
            // When alive again Update() re-shows it via distance check.
        }

        private void OnStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            if (type == PlayerStatType.Health || type == PlayerStatType.MaxHealth)
                RefreshHealth();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void RefreshName()
        {
            if (_nameText == null || _networkPlayer == null) return;
            _nameText.text = _networkPlayer.DisplayName;
        }

        private void RefreshHealth()
        {
            if (_statSystem == null) return;

            float current = _statSystem.GetStat(PlayerStatType.Health);
            _maxHealth    = _statSystem.GetStat(PlayerStatType.MaxHealth);
            if (_maxHealth <= 0f) _maxHealth = 100f;

            float fill = current / _maxHealth;

            if (_healthBar != null)
                _healthBar.value = Mathf.Clamp01(fill);

            if (_healthText != null)
                _healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(_maxHealth)}";
        }

        private void HideNameplate()
        {
            if (_nameplateRoot != null)
                _nameplateRoot.SetActive(false);
        }
    }
}
