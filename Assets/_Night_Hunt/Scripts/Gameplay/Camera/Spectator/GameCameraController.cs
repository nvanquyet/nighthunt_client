using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Input.Handlers.Spectator;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace NightHunt.Gameplay.Camera.Spectator
{
    /// <summary>
    /// Trung tâm điều khiển camera gameplay + spectator.
    /// - Theo dõi SpectateManager để set Follow/LookAt cho CinemachineCamera.
    /// - Lắng nghe CharacterLifecycleController của local player để auto vào/ra spectate.
    /// - Lắng nghe SpectatorInputHandler để next/prev player.
    /// </summary>
    public sealed class GameCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CinemachineCamera _virtualCamera;
        [SerializeField] private CharacterLifecycleController _localLifecycle;
        [SerializeField] private SpectatorInputHandler _spectatorInput;

        [Header("Settings")]
        [SerializeField] private bool _autoSpectateOnDeath = true;

        private NetworkPlayer _localPlayer;

        // ── Static registration (Bug #22 Fix) ────────────────────────────────
        // Players call RegisterLocalLifecycle() on OnStartNetwork (IsOwner)
        // instead of GameCameraController using FindFirstObjectByType which would
        // find ANY player in a multiplayer session.
        private static GameCameraController _instance;

        /// <summary>
        /// Called by the owning player's CharacterLifecycleController on spawn.
        /// Replaces the unsafe FindFirstObjectByType fallback.
        /// </summary>
        public static void RegisterLocalLifecycle(CharacterLifecycleController lc)
        {
            if (_instance != null)
                _instance.SetLocalLifecycle(lc);
        }

        private void SetLocalLifecycle(CharacterLifecycleController lc)
        {
            if (_localLifecycle != null)
            {
                _localLifecycle.OnDied -= HandleLocalPlayerDied;
                _localLifecycle.OnRespawned -= HandleLocalPlayerRespawned;
            }
            _localLifecycle = lc;
            if (_localLifecycle != null)
            {
                _localLifecycle.OnDied += HandleLocalPlayerDied;
                _localLifecycle.OnRespawned += HandleLocalPlayerRespawned;
            }
        }

        private void Awake()
        {
            _instance = this;

            if (_virtualCamera == null)
                _virtualCamera = FindFirstObjectByType<CinemachineCamera>();

            // NOTE: _localLifecycle intentionally NOT resolved via FindFirstObjectByType.
            // Use Inspector assignment OR let the local player register via
            // GameCameraController.RegisterLocalLifecycle() on spawn.

            if (_spectatorInput == null)
                _spectatorInput = FindFirstObjectByType<SpectatorInputHandler>();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnEnable()
        {
            if (SpectateManager.Instance != null)
            {
                _localPlayer = SpectateManager.Instance.GetLocalPlayer();
                SpectateManager.Instance.OnCurrentPlayerChanged += HandleCurrentPlayerChanged;
            }

            // Lifecycle events are managed by SetLocalLifecycle() (called on player spawn).
            // Inspector-assigned _localLifecycle also wired there — no duplicate subscribe needed.

            if (_spectatorInput != null)
            {
                _spectatorInput.OnNextPlayer += HandleNextPlayerRequested;
                _spectatorInput.OnPreviousPlayer += HandlePreviousPlayerRequested;
                _spectatorInput.OnExitSpectator += HandleExitSpectatorRequested;
            }
        }

        private void Start()
        {
            // If Inspector-assigned, wire it through SetLocalLifecycle so subscribe logic is unified.
            if (_localLifecycle != null)
                SetLocalLifecycle(_localLifecycle);

            if (SpectateManager.Instance != null)
            {
                var current = SpectateManager.Instance.GetCurrentPlayer();
                if (current != null)
                    SetCameraTarget(current.transform);
            }
        }

        private void OnDisable()
        {
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnCurrentPlayerChanged -= HandleCurrentPlayerChanged;
            }

            // Lifecycle events managed by SetLocalLifecycle — no unsubscribe needed here.

            if (_spectatorInput != null)
            {
                _spectatorInput.OnNextPlayer -= HandleNextPlayerRequested;
                _spectatorInput.OnPreviousPlayer -= HandlePreviousPlayerRequested;
                _spectatorInput.OnExitSpectator -= HandleExitSpectatorRequested;
            }
        }

        #region Local lifecycle handlers

        private void HandleLocalPlayerDied()
        {
            if (!_autoSpectateOnDeath || SpectateManager.Instance == null)
                return;

            TryStartSpectatingNextAlive();
        }

        private void HandleLocalPlayerRespawned()
        {
            if (SpectateManager.Instance == null)
                return;

            SpectateManager.Instance.StopSpectating();

            var current = SpectateManager.Instance.GetCurrentPlayer();
            if (current != null)
                SetCameraTarget(current.transform);
        }

        #endregion

        #region SpectateManager handlers

        private void HandleCurrentPlayerChanged(NetworkPlayer player)
        {
            if (player != null)
                SetCameraTarget(player.transform);
        }

        #endregion

        #region Spectator input handlers

        private void HandleNextPlayerRequested()
        {
            if (SpectateManager.Instance == null)
                return;

            SpectateManager.Instance.SwitchSpectatedPlayer(true);
        }

        private void HandlePreviousPlayerRequested()
        {
            if (SpectateManager.Instance == null)
                return;

            SpectateManager.Instance.SwitchSpectatedPlayer(false);
        }

        private void HandleExitSpectatorRequested()
        {
            if (SpectateManager.Instance == null)
                return;

            if (_localLifecycle != null && !_localLifecycle.IsDead)
            {
                SpectateManager.Instance.StopSpectating();
                var current = SpectateManager.Instance.GetCurrentPlayer();
                if (current != null)
                    SetCameraTarget(current.transform);
            }
        }

        #endregion

        #region Helpers

        private void SetCameraTarget(Transform target)
        {
            if (_virtualCamera == null || target == null)
                return;

            _virtualCamera.Follow = target;
            _virtualCamera.LookAt = target;
        }

        private void TryStartSpectatingNextAlive()
        {
            if (SpectateManager.Instance == null || PlayerPublicRegistry.Instance == null)
                return;

            if (_localPlayer == null)
                _localPlayer = SpectateManager.Instance.GetLocalPlayer();

            var allPlayers = PlayerPublicRegistry.Instance.GetAllPlayers();
            if (allPlayers == null || allPlayers.Length == 0)
                return;

            NetworkPlayer firstCandidate = null;

            for (int i = 0; i < allPlayers.Length; i++)
            {
                var p = allPlayers[i];
                if (p == null || p == _localPlayer)
                    continue;

                // Nếu muốn giới hạn cùng team, bật đoạn dưới:
                // if (p.TeamId != _localPlayer.TeamId)
                //     continue;

                firstCandidate = p;
                break;
            }

            if (firstCandidate != null)
            {
                SpectateManager.Instance.StartSpectating(firstCandidate);
            }
        }

        #endregion
    }
}

