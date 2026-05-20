using UnityEngine;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Feedback;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.UI;
using UnityEngine.SceneManagement;

namespace NightHunt.Audio
{
    /// <summary>
    /// CombatAudioController — handles all combat-related 2D audio feedback:
    ///   • Hit marker tick / headshot sound (local player perspective)
    ///   • Kill confirm + multi-kill announcer
    ///   • Low health heartbeat (activate when HP < threshold)
    ///   • Player death stinger
    ///   • BGM transitions (Home → Match → Results)
    ///
    /// PLACEMENT:
    ///   Place ONE instance on a persistent "Systems" GameObject (DontDestroyOnLoad not needed
    ///   if placed in GameScene, but keep it alive for match duration).
    ///   For BGM control it must persist across scene loads → place on AudioManager GO or subscribe
    ///   to sceneLoaded in this component.
    ///
    /// LOCAL PLAYER DETECTION:
    ///   Uses CombatFeedbackEvents.LocalHitConfirmed, raised only on the local
    ///   shooter's client through a server TargetRpc after authoritative damage.
    ///   PlayerHealthSystem.OnAnyPlayerDied is static, so kill/death audio is filtered locally.
    ///
    /// KILL STREAK:
    ///   Tracked locally (resets on death). Multi-kill window = 4 seconds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatAudioController : MonoBehaviour
    {
        [Header("Kill Streak")]
        [Tooltip("Window in seconds to count consecutive kills as a multi-kill. Reset after window.")]
        [SerializeField, Min(1f)] private float multiKillWindow = 4f;

        // ── Runtime ────────────────────────────────────────────────────────────
        private IPlayerStatSystem _statSystem;
        private bool              _lowHealthActive;
        private int               _killStreak;
        private float             _killStreakTimer;

        // Local player identity (set via Initialize after local player spawns)
        private string _localPlayerName;
        private int    _localNetworkObjectId = -1;
        private bool   _eventBusSubscribed;
        private bool   _uiNavigatorSubscribed;
        private UINavigator _subscribedNavigator;

        // ── BGM State ──────────────────────────────────────────────────────────
        private BGMState _currentBGM = BGMState.None;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            CombatFeedbackEvents.LocalHitConfirmed += HandleLocalHitConfirmed;
            PlayerHealthSystem.OnAnyPlayerDied  += HandleAnyPlayerDied;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TrySubscribeEventBus();
            TrySubscribeNavigator();
            SyncBGMToScene(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            CombatFeedbackEvents.LocalHitConfirmed -= HandleLocalHitConfirmed;
            PlayerHealthSystem.OnAnyPlayerDied  -= HandleAnyPlayerDied;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeEventBus();
            UnsubscribeNavigator();

            StopHeartbeat();
            Cleanup();
        }

        private void Update()
        {
            TrySubscribeEventBus();
            TrySubscribeNavigator();

            // Kill streak timer
            if (_killStreak > 0)
            {
                _killStreakTimer -= Time.deltaTime;
                if (_killStreakTimer <= 0f)
                    _killStreak = 0;
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Call after local player spawns — needed to filter hit marker to local player.
        /// </summary>
        public void Initialize(IPlayerStatSystem statSystem, string localPlayerName)
            => Initialize(statSystem, localPlayerName, -1);

        public void Initialize(IPlayerStatSystem statSystem, string localPlayerName, int localNetworkObjectId)
        {
            Cleanup();
            _statSystem = statSystem;
            _localPlayerName = localPlayerName;
            _localNetworkObjectId = localNetworkObjectId;

            if (_statSystem != null)
                _statSystem.OnStatChanged += HandleStatChanged;
        }

        /// <summary>Unregister from player stat system on player despawn.</summary>
        public void Cleanup()
        {
            if (_statSystem != null)
                _statSystem.OnStatChanged -= HandleStatChanged;
            _statSystem = null;
        }

        // ── BGM API ────────────────────────────────────────────────────────────

        /// <summary>Switch to Home screen BGM with crossfade.</summary>
        public void PlayBGMHome()   => SwitchBGM(BGMState.Home);

        /// <summary>Switch to In-Match BGM.</summary>
        public void PlayBGMMatch()  => SwitchBGM(BGMState.Match);

        /// <summary>Switch to intense/boss BGM variant.</summary>
        public void PlayBGMIntense() => SwitchBGM(BGMState.Intense);

        /// <summary>Switch to Results screen BGM.</summary>
        public void PlayBGMResults() => SwitchBGM(BGMState.Results);

        /// <summary>Fade out all BGM.</summary>
        public void StopBGM()       => SwitchBGM(BGMState.None);

        private void SwitchBGM(BGMState state)
        {
            if (_currentBGM == state) return;
            _currentBGM = state;

            if (!AudioManager.HasInstance) return;
            var lib = AudioManager.Instance.Library;
            if (lib == null) return;

            AudioClip clip = state switch
            {
                BGMState.Home    => lib.bgmHome,
                BGMState.Match   => lib.bgmMatch,
                BGMState.Intense => lib.bgmIntense,
                BGMState.Results => lib.bgmResults,
                _                => null
            };

            AudioManager.Instance.PlayMusic(clip);
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandleLocalHitConfirmed(CombatHitFeedbackInfo feedback)
        {
            if (!AudioManager.HasInstance) return;

            AudioManager.Instance.PlayHitMarker(feedback.IsHeadshot);
        }

        private void HandleAnyPlayerDied(string victim, string killer, string weaponId)
        {
            if (!AudioManager.HasInstance) return;

            // Local player died — play death stinger
            if (victim == _localPlayerName)
            {
                StopHeartbeat();
                AudioManager.Instance.PlayAnnouncer(AudioManager.Instance.Library?.playerDeathStinger);
                _killStreak = 0;
            }
        }

        private void HandlePlayerKilled(PlayerKilledEvent evt)
        {
            // Kill confirm only for local player as killer
            // Compare killerName — set _localPlayerName via Initialize()
            if (_localNetworkObjectId > 0)
            {
                if (evt.KillerNetworkObjectId != (uint)_localNetworkObjectId)
                    return;
            }
            else
            {
                if (string.IsNullOrEmpty(_localPlayerName)) return;
                if (evt.KillerName != _localPlayerName) return;
            }
            if (!AudioManager.HasInstance) return;

            _killStreak++;
            _killStreakTimer = multiKillWindow;

            if (_killStreak >= 2)
                AudioManager.Instance.PlayMultiKill(_killStreak);
            else
                AudioManager.Instance.PlayKillConfirm();
        }

        private void HandleStatChanged(PlayerStatType type, float oldVal, float newVal)
        {
            if (type != PlayerStatType.Health) return;
            if (!AudioManager.HasInstance) return;

            var lib = AudioManager.Instance.Library;
            if (lib == null) return;

            // Get max health to compute percentage
            float maxHealth = _statSystem?.GetBaseStat(PlayerStatType.Health) ?? 100f;
            float percent   = maxHealth > 0f ? newVal / maxHealth : 1f;

            bool shouldBeat = percent <= lib.lowHealthThreshold && percent > 0f;

            if (shouldBeat && !_lowHealthActive)
            {
                AudioManager.Instance.StartHeartbeat();
                _lowHealthActive = true;
            }
            else if (!shouldBeat && _lowHealthActive)
            {
                StopHeartbeat();
            }
        }

        private void StopHeartbeat()
        {
            _lowHealthActive = false;
            AudioManager.Instance?.StopHeartbeat();
        }

        // ── Enums ──────────────────────────────────────────────────────────────

        private void TrySubscribeEventBus()
        {
            if (_eventBusSubscribed || GameplayEventBus.Instance == null)
                return;

            GameplayEventBus.Instance.Subscribe<PlayerKilledEvent>(HandlePlayerKilled);
            _eventBusSubscribed = true;
        }

        private void UnsubscribeEventBus()
        {
            if (!_eventBusSubscribed)
                return;

            GameplayEventBus.Instance?.Unsubscribe<PlayerKilledEvent>(HandlePlayerKilled);
            _eventBusSubscribed = false;
        }

        private void TrySubscribeNavigator()
        {
            if (!UINavigator.HasInstance)
            {
                _uiNavigatorSubscribed = false;
                _subscribedNavigator = null;
                return;
            }

            if (_uiNavigatorSubscribed && _subscribedNavigator == UINavigator.Instance)
                return;

            UnsubscribeNavigator();

            _subscribedNavigator = UINavigator.Instance;
            _subscribedNavigator.OnPanelChanged += HandleUIPanelChanged;
            _uiNavigatorSubscribed = true;
            HandleUIPanelChanged(_subscribedNavigator.CurrentPanel);
        }

        private void UnsubscribeNavigator()
        {
            if (!_uiNavigatorSubscribed)
                return;

            if (_subscribedNavigator != null)
                _subscribedNavigator.OnPanelChanged -= HandleUIPanelChanged;
            _subscribedNavigator = null;
            _uiNavigatorSubscribed = false;
        }

        private void HandleUIPanelChanged(PanelType panel)
        {
            switch (panel)
            {
                case PanelType.Login:
                case PanelType.Home:
                case PanelType.Lobby:
                case PanelType.CustomLobby:
                case PanelType.Settings:
                    PlayBGMHome();
                    break;
                default:
                    PlayBGMMatch();
                    break;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SyncBGMToScene(scene);
        }

        private void SyncBGMToScene(Scene scene)
        {
            if (!scene.IsValid())
                return;

            string sceneName = scene.name ?? string.Empty;
            if (sceneName.Contains("01_Home"))
                PlayBGMHome();
            else if (sceneName.Contains("02_Map") || sceneName.Contains("Map_"))
                PlayBGMMatch();
        }

        private enum BGMState { None, Home, Match, Intense, Results }
    }
}
