using FOW;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking;
using UnityEngine;

namespace NightHunt.Gameplay.FogOfWar
{
    /// <summary>
    /// Controls whether this object should be hidden by FogOfWar for the local client.
    /// - Lấy local team qua SpectateManager.Instance.GetLocalPlayer().TeamId (KHÔNG đổi khi spectate).
    /// - Lấy team của object qua NetworkPlayer.TeamId.
    /// - Nếu object khác team local → gắn FogOfWarHider.
    /// - Nếu cùng team local → bỏ FogOfWarHider (luôn nhìn thấy).
    /// </summary>
    [DisallowMultipleComponent]
    public class FogTeamVisibilityBinder : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _logDecisions;

        private FogOfWarHider _hider;
        private NetworkPlayer _networkPlayer;

        private void Awake()
        {
            _hider = GetComponent<FogOfWarHider>();
            _networkPlayer = GetComponent<NetworkPlayer>();
        }

        private void Start()
        {
            // Try to apply team visibility immediately.
            // If the local player isn't registered yet (late-join scenario where this
            // remote player's NetworkObject arrives before our own), subscribe to
            // SpectateManager.OnLocalPlayerSet and retry once it fires.
            if (!RefreshVisibilityForLocalTeam())
            {
                if (SpectateManager.Instance != null)
                    SpectateManager.Instance.OnLocalPlayerSet += OnLocalPlayerAvailable;
            }
        }

        private void OnDestroy()
        {
            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet -= OnLocalPlayerAvailable;
        }

        private void OnLocalPlayerAvailable(NightHunt.Networking.NetworkPlayer _)
        {
            // Unsubscribe first so we only refresh once.
            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet -= OnLocalPlayerAvailable;

            RefreshVisibilityForLocalTeam();
        }

        /// <summary>
        /// Áp dụng / gỡ FogOfWarHider dựa theo team của local player.
        /// Returns true if the local team was resolved; false if the local player
        /// is not yet registered (caller should retry via OnLocalPlayerSet event).
        /// </summary>
        public bool RefreshVisibilityForLocalTeam()
        {
            int localTeamId;
            if (!TryGetLocalTeamId(out localTeamId))
            {
                // Local player not registered yet.
                // Default to VISIBLE so no player is accidentally hidden while waiting.
                // OnLocalPlayerAvailable() will re-run this method once the local
                // player spawns and registers with SpectateManager.
                Log("No local team available — defaulting to visible and waiting for OnLocalPlayerSet.");
                RemoveHiderIfExists();
                return false;
            }

            int objectTeamId;
            if (!TryGetObjectTeamId(out objectTeamId))
            {
                // Nếu object không có team (neutral) → tuỳ design, ở đây cho luôn visible.
                Log("No object team detected, treating as neutral (visible).");
                RemoveHiderIfExists();
                return true; // resolved: neutral objects are always visible
            }

            bool isEnemyToLocal = objectTeamId != localTeamId;

            if (isEnemyToLocal)
            {
                EnsureHiderExists();
                Log($"Object is enemy (localTeam={localTeamId}, objectTeam={objectTeamId}) → FogOfWarHider ENABLED.");
            }
            else
            {
                RemoveHiderIfExists();
                Log($"Object is ally (localTeam={localTeamId}, objectTeam={objectTeamId}) → FogOfWarHider DISABLED.");
            }

            return true;
        }

        private bool TryGetLocalTeamId(out int teamId)
        {
            teamId = -1;

            if (SpectateManager.Instance == null)
                return false;

            NetworkPlayer local = SpectateManager.Instance.GetLocalPlayer();
            if (local == null)
                return false;

            teamId = local.TeamId;
            return teamId >= 0;
        }

        private bool TryGetObjectTeamId(out int teamId)
        {
            teamId = -1;

            if (_networkPlayer == null)
                _networkPlayer = GetComponent<NetworkPlayer>();

            if (_networkPlayer == null)
                return false;

            teamId = _networkPlayer.TeamId;
            return true;
        }

        private void EnsureHiderExists()
        {
            if (_hider == null)
            {
                _hider = gameObject.AddComponent<FogOfWarHider>();
            }
        }

        private void RemoveHiderIfExists()
        {
            if (_hider != null)
            {
                Destroy(_hider);
                _hider = null;
            }
        }

        private void Log(string msg)
        {
            if (_logDecisions)
            {
                Debug.Log("[FogTeamVisibilityBinder] " + msg, this);
            }
        }
    }
}

