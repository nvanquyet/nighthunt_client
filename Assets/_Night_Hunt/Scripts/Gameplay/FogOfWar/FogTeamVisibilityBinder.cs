using FOW;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using UnityEngine;
using NightHunt.Utilities;

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
        private HiderDisableRenderers _hiderBehavior;
        private NetworkPlayer _networkPlayer;
        private PlayerModelLoader _modelLoader;

        public bool IsEnemyToLocal { get; private set; }
        public event System.Action<bool> OnEnemyStateChanged;

        private void Awake()
        {
            _hider = ComponentResolver.Find<FogOfWarHider>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] FogOfWarHider not found")
        .Resolve();
            _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] NetworkPlayer not found")
        .Resolve();

            _modelLoader = ComponentResolver.Find<PlayerModelLoader>(this)
        .OnSelf().OnRoot().InRootChildren()
        .Resolve();
        }

        private void Start()
        {
            // Subscribe to _networkPlayer.OnPublicDataChanged để re-run khi team thay đổi giữa game.
            if (_networkPlayer != null)
                _networkPlayer.OnPublicDataChanged += OnNetworkPlayerDataChanged;

            // Try to apply team visibility immediately.
            // If the local player isn't registered yet (late-join scenario where this
            // remote player's NetworkObject arrives before our own), subscribe to
            // SpectateManager.OnLocalPlayerSet and retry once it fires.
            if (!RefreshVisibilityForLocalTeam())
            {
                if (SpectateManager.Instance != null)
                    SpectateManager.Instance.OnLocalPlayerSet += OnLocalPlayerAvailable;
            }

            // Refresh renderer list when model finishes loading (model is loaded asynchronously).
            if (_modelLoader != null)
                _modelLoader.OnModelReady += OnModelReadyForFog;
        }

        private void OnDestroy()
        {
            if (_networkPlayer != null)
                _networkPlayer.OnPublicDataChanged -= OnNetworkPlayerDataChanged;

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet -= OnLocalPlayerAvailable;

            if (_modelLoader != null)
                _modelLoader.OnModelReady -= OnModelReadyForFog;
        }

        /// <summary>
        /// Gọi lại RefreshVisibilityForLocalTeam mỗi khi data của object này thay đổi (vd. team switch).
        /// </summary>
        private void OnNetworkPlayerDataChanged(PlayerPublicData prev, PlayerPublicData next)
        {
            if (prev.TeamId != next.TeamId)
                RefreshVisibilityForLocalTeam();
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
                SetEnemyState(false);
                return false;
            }

            int objectTeamId;
            if (!TryGetObjectTeamId(out objectTeamId))
            {
                // Nếu object không có team (neutral) → tuỳ design, ở đây cho luôn visible.
                Log("No object team detected, treating as neutral (visible).");
                RemoveHiderIfExists();
                SetEnemyState(false);
                return true; // resolved: neutral objects are always visible
            }

            bool isEnemyToLocal = objectTeamId != localTeamId;
            SetEnemyState(isEnemyToLocal);

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

        private void SetEnemyState(bool isEnemy)
        {
            if (IsEnemyToLocal != isEnemy)
            {
                IsEnemyToLocal = isEnemy;
                OnEnemyStateChanged?.Invoke(IsEnemyToLocal);
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
                _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] NetworkPlayer not found")
        .Resolve();

            if (_networkPlayer == null)
                return false;

            teamId = _networkPlayer.TeamId;
            return true;
        }

        private void EnsureHiderExists()
        {
            if (_hider != null) return;

            _hider = gameObject.AddComponent<FogOfWarHider>();

            // Add renderer-toggling behavior so renderers are actually hidden/shown
            // when the FOW system marks this object as revealed or concealed.
            // Without this companion, FogOfWarHider fires OnActiveChanged but nothing acts on it.
            _hiderBehavior = gameObject.AddComponent<HiderDisableRenderers>();
            RefreshHiderRenderers();
        }

        private void RemoveHiderIfExists()
        {
            if (_hider == null) return;

            // Explicitly disable the hider FIRST so FogOfWarHider.OnDisable() fires
            // synchronously: it calls SetActive(true) → OnActiveChanged(true) →
            // HiderBehavior.OnReveal() → renderers re-enabled BEFORE the behavior is destroyed.
            _hider.enabled = false;

            if (_hiderBehavior != null)
            {
                Destroy(_hiderBehavior);
                _hiderBehavior = null;
            }

            Destroy(_hider);
            _hider = null;
        }

        /// <summary>
        /// Called when the character model finishes loading. Re-captures all child renderers
        /// so the HiderDisableRenderers behavior covers the freshly instantiated mesh.
        /// </summary>
        private void OnModelReadyForFog(GameObject _) => RefreshHiderRenderers();

        /// <summary>
        /// (Re-)populates HiderDisableRenderers with every Renderer currently on this
        /// object and its children. Safe to call multiple times (e.g. after model swap).
        /// </summary>
        private void RefreshHiderRenderers()
        {
            if (_hiderBehavior == null) return;
            _hiderBehavior.ModifyHiddenRenderers(GetComponentsInChildren<Renderer>(includeInactive: true));
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

