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
            // Chỉ cần quyết định 1 lần khi spawn (team không đổi trong match bình thường).
            RefreshVisibilityForLocalTeam();
        }

        /// <summary>
        /// Có thể gọi lại nếu logic team thay đổi (rare).
        /// </summary>
        public void RefreshVisibilityForLocalTeam()
        {
            int localTeamId;
            if (!TryGetLocalTeamId(out localTeamId))
            {
                // Nếu chưa có local player / spectate manager → fallback: không gắn hider (tránh lỗi).
                Log("No local team available, skipping fog visibility binding.");
                return;
            }

            int objectTeamId;
            if (!TryGetObjectTeamId(out objectTeamId))
            {
                // Nếu object không có team (neutral) → tuỳ design, ở đây cho luôn visible.
                Log("No object team detected, treating as neutral (visible).");
                RemoveHiderIfExists();
                return;
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

