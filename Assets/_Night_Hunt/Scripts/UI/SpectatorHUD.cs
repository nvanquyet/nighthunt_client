using NightHunt.Core;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.StatSystem.Core.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// SpectatorHUD — in-game overlay shown when the local player is spectating.
    ///
    /// Displays:
    ///   - Spectated player name
    ///   - Team color badge
    ///   - Health bar (reads NetworkPlayer.CurrentHealth / MaxHealth)
    ///   - Active weapon name
    ///   - Navigation hint: "[Tab] Next   [Q] Prev   [E] Exit Spectate"
    ///
    /// Lifecycle:
    ///   SpectateManager.OnSpectateStarted  → gameObject.SetActive(true)
    ///   SpectateManager.OnSpectateStopped  → gameObject.SetActive(false)
    ///   SpectateManager.OnCurrentPlayerChanged → RefreshDisplay(player)
    ///
    /// Setup:
    ///   Add under GameHUD canvas (02_Map_01.unity), default SetActive(false).
    ///   Wire all [SerializeField] references in Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpectatorHUD : Singleton<SpectatorHUD>
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Player Info")]
        [Tooltip("Shows the spectated player's display name.")]
        [SerializeField] private TextMeshProUGUI _playerNameText;

        [Tooltip("Image tinted with the spectated player's team color.")]
        [SerializeField] private Image _teamColorBadge;

        [Tooltip("Team colors indexed by TeamId (0 = no team / spectator grey).")]
        [SerializeField] private Color[] _teamColors = { Color.grey, Color.blue, Color.red };

        [Header("Health Bar")]
        [SerializeField] private Slider    _healthBar;
        [SerializeField] private TextMeshProUGUI _healthText;

        [Header("Weapon")]
        [Tooltip("Shows the spectated player's currently active weapon name.")]
        [SerializeField] private TextMeshProUGUI _weaponNameText;

        [Header("Navigation — Mobile")]
        [Tooltip("Root object shown only on mobile platforms; contains Prev/Next buttons.")]
        [SerializeField] private GameObject _mobileNavRoot;
        [SerializeField] private Button     _prevButton;
        [SerializeField] private Button     _nextButton;

        [Header("Navigation — Desktop")]
        [Tooltip("Root object shown only on desktop platforms; contains the keyboard hint label.")]
        [SerializeField] private GameObject     _desktopNavRoot;
        [Tooltip("Static label shown while spectating. Set text in Inspector or leave blank to use default.")]
        [SerializeField] private TextMeshProUGUI _navigationHintText;

        private const string DefaultNavigationHint = "[Tab] Next   [Q] Prev   [E] Exit Spectate";

        // ── Tracked player ────────────────────────────────────────────────────

        private NetworkPlayer _trackedPlayer;

        // ── Singleton lifecycle ───────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            gameObject.SetActive(false);

            if (_navigationHintText != null && string.IsNullOrEmpty(_navigationHintText.text))
                _navigationHintText.text = DefaultNavigationHint;

            // Show mobile or desktop navigation controls based on current platform.
            bool isMobile = Application.isMobilePlatform;
            if (_mobileNavRoot  != null) _mobileNavRoot.SetActive(isMobile);
            if (_desktopNavRoot != null) _desktopNavRoot.SetActive(!isMobile);

            if (_prevButton != null)
                _prevButton.onClick.AddListener(() => SpectateManager.Instance?.SwitchSpectatedPlayer(false));
            if (_nextButton != null)
                _nextButton.onClick.AddListener(() => SpectateManager.Instance?.SwitchSpectatedPlayer(true));
        }

        private void OnEnable()
        {
            var sm = SpectateManager.Instance;
            if (sm == null) return;

            sm.OnSpectateStarted        += HandleSpectateStarted;
            sm.OnSpectateStopped        += HandleSpectateStopped;
            sm.OnCurrentPlayerChanged   += RefreshDisplay;
        }

        private void OnDisable()
        {
            var sm = SpectateManager.Instance;
            if (sm == null) return;

            sm.OnSpectateStarted        -= HandleSpectateStarted;
            sm.OnSpectateStopped        -= HandleSpectateStopped;
            sm.OnCurrentPlayerChanged   -= RefreshDisplay;
        }

        private void Update()
        {
            // Tick health bar in real-time (NetworkPlayer health changes each frame).
            if (_trackedPlayer != null)
                UpdateHealthBar(_trackedPlayer);
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        private void HandleSpectateStarted()
        {
            gameObject.SetActive(true);
            var target = SpectateManager.Instance?.GetCurrentPlayer();
            if (target != null) RefreshDisplay(target);
        }

        private void HandleSpectateStopped()
        {
            _trackedPlayer = null;
            gameObject.SetActive(false);
        }

        // ── Display ───────────────────────────────────────────────────────────

        private void RefreshDisplay(NetworkPlayer player)
        {
            if (player == null) return;
            _trackedPlayer = player;

            // Name
            if (_playerNameText != null)
                _playerNameText.text = string.IsNullOrEmpty(player.DisplayName)
                    ? "Unknown"
                    : player.DisplayName;

            // Team color badge
            if (_teamColorBadge != null)
            {
                int  teamId = player.TeamId;
                bool valid  = teamId >= 0 && teamId < _teamColors.Length;
                _teamColorBadge.color = valid ? _teamColors[teamId] : Color.grey;
            }

            // Health bar
            UpdateHealthBar(player);

            // Weapon name — read from NetworkPlayer's current weapon slot if available.
            if (_weaponNameText != null)
            {
                string weaponName = GetWeaponName(player);
                _weaponNameText.text = string.IsNullOrEmpty(weaponName) ? "—" : weaponName;
            }
        }

        private void UpdateHealthBar(NetworkPlayer player)
        {
            if (_healthBar == null && _healthText == null) return;

            // Read health via GameplaySystemsBridge → IPlayerStatSystem.
            var bridge = player.GetComponentInChildren<NightHunt.GameplaySystems.Core.Bridge.GameplaySystemsBridge>();
            float current = 0f, max = 100f;
            if (bridge?.Stat != null)
            {
                current = bridge.Stat.GetStat(PlayerStatType.Health);
                max     = bridge.Stat.GetStat(PlayerStatType.MaxHealth);
            }

            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (_healthBar  != null) _healthBar.value = ratio;
            if (_healthText != null) _healthText.text  = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        private static string GetWeaponName(NetworkPlayer player)
        {
            // Read active weapon from GameplaySystemsBridge.Weapon (IWeaponSystem).
            var bridge = player.GetComponentInChildren<NightHunt.GameplaySystems.Core.Bridge.GameplaySystemsBridge>();
            if (bridge == null) return string.Empty;

            var weaponSystem = bridge.Weapon;
            if (weaponSystem == null) return string.Empty;

            var activeWeapon = weaponSystem.GetActiveWeapon();
            return activeWeapon?.DefinitionID ?? string.Empty;
        }
    }
}
