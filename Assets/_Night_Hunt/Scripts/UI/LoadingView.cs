using NightHunt.Config;
using NightHunt.Core;
using NightHunt.Gameplay.Core.Events;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Loading View — shown in the 06_MatchLoading scene while all players
    /// connect and spawn.
    ///
    /// Progress states:
    ///   Connecting  →  Server ready  →  Spawning  →  All players ready
    ///
    /// Listens to <see cref="AllPlayersReadyEvent"/> from
    /// <see cref="GameplayEventBus"/> to auto-advance to the game scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingView : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Team Info")]
        [SerializeField] private TextMeshProUGUI teamALabel;
        [SerializeField] private TextMeshProUGUI teamBLabel;
        [SerializeField] private TextMeshProUGUI vsLabel;

        [Header("Progress")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Slider          progressBar;
        [SerializeField] private TextMeshProUGUI progressPercentText;

        [Header("Tips")]
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private string[]        tips = { };

        // ── State ─────────────────────────────────────────────────────────────
        private LoadingStage _stage = LoadingStage.Connecting;
        private float        _progressTarget;
        private float        _progressCurrent;

        private static readonly string[] DefaultTips =
        {
            "Plant beacons to secure respawn points for your team.",
            "The boss spawns at Phase 2 — focus it for powerful loot.",
            "Capture zones generate score every second.",
            "Phase 3 respawns have a delay — protect your last teammate.",
        };

        // ──────────────────────────────────────────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            GameplayEventBus.Instance?.Subscribe<AllPlayersReadyEvent>(OnAllPlayersReady);
        }

        private void OnDestroy()
        {
            GameplayEventBus.Instance?.Unsubscribe<AllPlayersReadyEvent>(OnAllPlayersReady);
        }

        private void Start()
        {
            RefreshTeamInfo();
            SetStage(LoadingStage.Connecting);
            ShowRandomTip();
        }

        private void Update()
        {
            // Smoothly animate progress bar
            _progressCurrent = Mathf.MoveTowards(
                _progressCurrent, _progressTarget, Time.deltaTime * 0.5f);

            if (progressBar != null)
                progressBar.value = _progressCurrent;

            if (progressPercentText != null)
                progressPercentText.text = $"{Mathf.RoundToInt(_progressCurrent * 100f)}%";
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Stage management

        private void SetStage(LoadingStage stage)
        {
            _stage = stage;
            switch (stage)
            {
                case LoadingStage.Connecting:
                    SetStatus("Connecting to server…");
                    _progressTarget = 0.1f;
                    break;

                case LoadingStage.ServerReady:
                    SetStatus("Server ready. Spawning players…");
                    _progressTarget = 0.5f;
                    break;

                case LoadingStage.Spawning:
                    SetStatus("Spawning players…");
                    _progressTarget = 0.75f;
                    break;

                case LoadingStage.AllReady:
                    SetStatus("All players ready! Starting…");
                    _progressTarget = 1f;
                    break;
            }
        }

        private void OnAllPlayersReady(AllPlayersReadyEvent _)
        {
            SetStage(LoadingStage.AllReady);
            // SceneLoader will load the game scene once progress reaches 1.
            // Small delay so the bar finishes animating.
            Invoke(nameof(LoadGameScene), 1.5f);
        }

        private void LoadGameScene()
        {
            SceneId mapId = ResolveTargetMap();
            SceneLoader.LoadGame(mapId);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Helpers

        private void RefreshTeamInfo()
        {
            var room = RoomState.Instance?.CurrentRoom;
            if (teamALabel != null) teamALabel.text = "Team A";
            if (teamBLabel != null) teamBLabel.text = "Team B";
            if (vsLabel    != null) vsLabel.text    = "VS";
        }

        private static SceneId ResolveTargetMap()
        {
            string mapId = RoomState.Instance?.CurrentRoom?.mapId;
            if (!string.IsNullOrWhiteSpace(mapId)
                && MapConfig.TryGetById(mapId, out MapEntry entry))
            {
                return entry.sceneId;
            }

            return SceneId.GameMap_01;
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        private void ShowRandomTip()
        {
            string[] pool = (tips != null && tips.Length > 0) ? tips : DefaultTips;
            if (tipText != null && pool.Length > 0)
                tipText.text = pool[Random.Range(0, pool.Length)];
        }

        /// <summary>
        /// External hook: called by NetworkGameManager or GameBootstrap
        /// once the transport connects.
        /// </summary>
        public void MarkConnected()  => SetStage(LoadingStage.ServerReady);

        /// <summary>Called once the server has started spawning characters.</summary>
        public void MarkSpawning()   => SetStage(LoadingStage.Spawning);

        #endregion
    }

    public enum LoadingStage
    {
        Connecting,
        ServerReady,
        Spawning,
        AllReady
    }
}
