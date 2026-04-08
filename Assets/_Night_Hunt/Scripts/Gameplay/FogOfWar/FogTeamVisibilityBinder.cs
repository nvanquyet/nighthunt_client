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
        private NightHunt.Gameplay.Deployables.BaseDeployable _deployable;
        private PlayerModelLoader _modelLoader;
        private NetworkPlayer _subscribedLocalPlayer; // tracks local player for team-change callbacks

        public bool IsEnemyToLocal { get; private set; }
        public event System.Action<bool> OnEnemyStateChanged;

        private void Awake()
        {
            _hider = ComponentResolver.Find<FogOfWarHider>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] FogOfWarHider not found")
        .Resolve();

            // BUG 3 FIX: Add InParent() so this component is found even when placed on a child
            // object whose NetworkPlayer lives on the parent hierarchy.
            _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .Resolve();
            
            // Nếu không phải Player, có thể đây là một thiết bị thả xuống đất (Mắt/Trạm Hồi Sinh)
            _deployable = ComponentResolver.Find<NightHunt.Gameplay.Deployables.BaseDeployable>(this)
        .OnSelf()
        .InChildren()
        .Resolve();

            // Note: Missing NetworkPlayer/Deployable warning is deferred to Start() after a
            // coroutine retry, to allow network initialization to complete first.

            _modelLoader = ComponentResolver.Find<PlayerModelLoader>(this)
        .OnSelf().OnRoot().InRootChildren()
        .Resolve();
        }

        private void Start()
        {
            // BUG 3 FIX: If NetworkPlayer was not found in Awake (network not yet initialized),
            // retry once per frame until found, then apply team visibility.
            if (_networkPlayer == null && _deployable == null)
                StartCoroutine(WaitForNetworkOwner());

            // Subscribe to _networkPlayer.OnPublicDataChanged để re-run khi team thay đổi giữa game.
            if (_networkPlayer != null)
                _networkPlayer.OnPublicDataChanged += OnNetworkPlayerDataChanged;

            // ALWAYS subscribe to OnLocalPlayerSet so we catch two cases:
            // (a) Local player not yet registered when Start() runs (late-join).
            // (b) Local player's _playerData.TeamId SyncVar arrives AFTER Start():
            //     the spawn packet may carry default TeamId=0; a separate SyncVar
            //     delta then sets the real team. OnLocalPlayerSet alone won't re-fire,
            //     so we also call SubscribeLocalPlayerTeamChange to listen on the
            //     local player's OnPublicDataChanged directly.
            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet += OnLocalPlayerAvailable;

            // Initial refresh — may read TeamId=0 if SyncVar hasn't landed yet.
            // SubscribeLocalPlayerTeamChange ensures correction when real team arrives.
            RefreshVisibilityForLocalTeam();
            var local = SpectateManager.Instance?.GetLocalPlayer();
            if (local != null)
                SubscribeLocalPlayerTeamChange(local);

            // Refresh renderer list when model finishes loading.
            // RACE: FishNet fires PlayerModelLoader.OnStartClient() (and thus OnModelReady)
            // synchronously during LateUpdate when the spawn packet is processed.
            // Unity defers Start() to the NEXT frame, so by the time we subscribe here
            // the model may already be instantiated. If so, call the handler immediately.
            if (_modelLoader != null)
            {
                _modelLoader.OnModelReady += OnModelReadyForFog;
                if (_modelLoader.CurrentModelInstance != null)
                    OnModelReadyForFog(_modelLoader.CurrentModelInstance);
            }
        }

        private System.Collections.IEnumerator WaitForNetworkOwner()
        {
            int maxFrames = 60; // give up after 60 frames (~1 s at 60 fps)
            for (int i = 0; i < maxFrames; i++)
            {
                yield return null;
                _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
                    .OnSelf().InChildren().InParent().Resolve();
                _deployable = _deployable ?? ComponentResolver.Find<NightHunt.Gameplay.Deployables.BaseDeployable>(this)
                    .OnSelf().InChildren().Resolve();

                if (_networkPlayer != null || _deployable != null)
                {
                    if (_networkPlayer != null)
                        _networkPlayer.OnPublicDataChanged += OnNetworkPlayerDataChanged;
                    var local = SpectateManager.Instance?.GetLocalPlayer();
                    if (local != null)
                        SubscribeLocalPlayerTeamChange(local);
                    RefreshVisibilityForLocalTeam();
                    yield break;
                }
            }
            Debug.LogWarning("[FogTeamVisibilityBinder] Không tìm thấy NetworkPlayer hay BaseDeployable sau 60 frames. Script sẽ không biết tính team!");
        }

        private void OnDestroy()
        {
            if (_networkPlayer != null)
                _networkPlayer.OnPublicDataChanged -= OnNetworkPlayerDataChanged;

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet -= OnLocalPlayerAvailable;

            if (_subscribedLocalPlayer != null)
                _subscribedLocalPlayer.OnPublicDataChanged -= OnLocalTeamChanged;

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

        private void OnLocalPlayerAvailable(NetworkPlayer localPlayer)
        {
            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet -= OnLocalPlayerAvailable;

            SubscribeLocalPlayerTeamChange(localPlayer);
            RefreshVisibilityForLocalTeam();
        }

        /// <summary>
        /// Subscribes to the local player's OnPublicDataChanged so that if the local
        /// player's TeamId SyncVar arrives AFTER Start() already evaluated team visibility
        /// (e.g. spawn packet carried default TeamId=0 before the SyncVar delta landed),
        /// every FogTeamVisibilityBinder instance for remote objects still re-evaluates.
        /// </summary>
        private void SubscribeLocalPlayerTeamChange(NetworkPlayer local)
        {
            if (local == null || local == _subscribedLocalPlayer) return;
            if (_subscribedLocalPlayer != null)
                _subscribedLocalPlayer.OnPublicDataChanged -= OnLocalTeamChanged;
            _subscribedLocalPlayer = local;
            _subscribedLocalPlayer.OnPublicDataChanged += OnLocalTeamChanged;
        }

        private void OnLocalTeamChanged(PlayerPublicData prev, PlayerPublicData next)
        {
            if (prev.TeamId != next.TeamId)
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

            if (_networkPlayer != null)
            {
                teamId = _networkPlayer.TeamId;
                return true;
            }

            if (_deployable != null)
            {
                teamId = _deployable.OwnerTeamId;
                return true;
            }

            return false;
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

