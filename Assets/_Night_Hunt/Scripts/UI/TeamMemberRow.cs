using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Scoring;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Character.Data;

namespace NightHunt.UI
{
    /// <summary>
    /// Unified team row contract used by both owner and teammate rows.
    /// Fields are optional so scenes can migrate incrementally.
    /// </summary>
    public class TeamMemberRow : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private Image _avatarIcon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Image _teamColorImage;

        [Header("Stats")]
        [SerializeField] private Slider _healthBar;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private Slider _staminaBar;
        [SerializeField] private TextMeshProUGUI _scoreText;

        [Header("State")]
        [SerializeField] private GameObject _aliveIndicator;
        [SerializeField] private GameObject _deadIndicator;
        [SerializeField] private GameObject _spectatingIndicator;

        private static readonly Color[] TeamColors =
        {
            Color.grey,
            new Color(0.33f, 0.67f, 1f),
            new Color(1f, 0.36f, 0.36f),
            new Color(0.38f, 0.9f, 0.56f),
        };

        private NetworkPlayer _player;
        private IGameplayBridge _bridge;
        private bool _isBound;
        private float _health;
        private float _maxHealth = 100f;
        private float _stamina;
        private float _maxStamina = 100f;

        public NetworkPlayer BoundPlayer => _player;

        public void Bind(NetworkPlayer player)
        {
            if (player == null)
            {
                Unbind();
                return;
            }

            if (ReferenceEquals(_player, player) && _isBound)
            {
                RefreshIdentity();
                RefreshStatsFromBridge();
                RefreshAliveVisual(player.IsAlive);
                return;
            }

            Unbind();

            _player = player;
            _bridge = player.GamePlaySystemBridge;
            _isBound = true;

            if (_player != null)
                _player.OnAliveChanged += OnAliveChanged;

            if (_bridge != null)
                _bridge.OnStatChanged += HandleStatChanged;

            GameplayEventBus.Instance?.Subscribe<ScoreDataSyncedEvent>(OnScoreDataSynced);

            RefreshIdentity();
            RefreshStatsFromBridge();
            RefreshAliveVisual(player.IsAlive);
            SetSpectating(false);
            if (_scoreText != null && string.IsNullOrEmpty(_scoreText.text))
                _scoreText.text = "0";
        }

        public void Unbind()
        {
            if (_player != null)
                _player.OnAliveChanged -= OnAliveChanged;

            if (_bridge != null)
                _bridge.OnStatChanged -= HandleStatChanged;

            GameplayEventBus.Instance?.Unsubscribe<ScoreDataSyncedEvent>(OnScoreDataSynced);

            _player = null;
            _bridge = null;
            _isBound = false;
            _health = 0f;
            _stamina = 0f;
            _maxHealth = 100f;
            _maxStamina = 100f;
            SetSpectating(false);
        }

        public void SetSpectating(bool isSpectating)
        {
            if (_spectatingIndicator != null)
                _spectatingIndicator.SetActive(isSpectating);
        }

        private void OnDestroy() => Unbind();

        private void RefreshIdentity()
        {
            if (_player == null) return;

            if (_nameText != null)
                _nameText.text = _player.DisplayName;

            if (_avatarIcon != null)
            {
                var def = CharacterDatabase.Instance?.GetByIndex(_player.CharacterModelIndex);
                _avatarIcon.sprite = def?.Icon;
                _avatarIcon.enabled = _avatarIcon.sprite != null;
            }

            if (_teamColorImage != null)
            {
                int teamId = _player.TeamId;
                _teamColorImage.color = teamId >= 0 && teamId < TeamColors.Length
                    ? TeamColors[teamId]
                    : TeamColors[0];
            }
        }

        private void RefreshStatsFromBridge()
        {
            if (_bridge?.Stat == null) return;

            _health = _bridge.Stat.GetStat(PlayerStatType.Health);
            _maxHealth = Mathf.Max(1f, _bridge.Stat.GetStat(PlayerStatType.MaxHealth));
            _stamina = _bridge.Stat.GetStat(PlayerStatType.Stamina);
            _maxStamina = Mathf.Max(1f, _bridge.Stat.GetStat(PlayerStatType.MaxStamina));

            RefreshHealth();
            RefreshStamina();
        }

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            switch (type)
            {
                case PlayerStatType.Health:
                    _health = newValue;
                    RefreshHealth();
                    break;
                case PlayerStatType.MaxHealth:
                    _maxHealth = Mathf.Max(1f, newValue);
                    RefreshHealth();
                    break;
                case PlayerStatType.Stamina:
                    _stamina = newValue;
                    RefreshStamina();
                    break;
                case PlayerStatType.MaxStamina:
                    _maxStamina = Mathf.Max(1f, newValue);
                    RefreshStamina();
                    break;
            }
        }

        private void RefreshHealth()
        {
            float ratio = _maxHealth > 0f ? _health / _maxHealth : 0f;
            if (_healthBar != null) _healthBar.value = Mathf.Clamp01(ratio);
            if (_healthText != null)
                _healthText.text = $"{Mathf.CeilToInt(_health)} / {Mathf.CeilToInt(_maxHealth)}";
        }

        private void RefreshStamina()
        {
            float ratio = _maxStamina > 0f ? _stamina / _maxStamina : 0f;
            if (_staminaBar != null) _staminaBar.value = Mathf.Clamp01(ratio);
        }

        private void OnAliveChanged(bool isAlive) => RefreshAliveVisual(isAlive);

        private void RefreshAliveVisual(bool isAlive)
        {
            if (_aliveIndicator != null) _aliveIndicator.SetActive(isAlive);
            if (_deadIndicator != null) _deadIndicator.SetActive(!isAlive);

            var cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = isAlive ? 1f : 0.45f;
        }

        private void OnScoreDataSynced(ScoreDataSyncedEvent evt)
        {
            if (_scoreText == null || _player == null || string.IsNullOrEmpty(evt.ScoreDataJson)) return;

            var snapshot = JsonUtility.FromJson<ScoreSnapshot>(evt.ScoreDataJson);
            if (snapshot?.Players == null) return;

            uint playerId = (uint)_player.ObjectId;
            foreach (var score in snapshot.Players)
            {
                if (score.PlayerId != playerId) continue;
                _scoreText.text = score.TotalScore.ToString();
                return;
            }
        }
    }
}
