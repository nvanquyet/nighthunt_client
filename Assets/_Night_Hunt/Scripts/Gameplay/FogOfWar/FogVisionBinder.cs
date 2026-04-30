using FOW;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.Utilities;
using UnityEngine;

namespace NightHunt.Gameplay.FogOfWar
{
    /// <summary>
    /// Binds player VisionRange stat to FogOfWarRevealer3D.ViewRadius.
    /// - Read stat qua IPlayerStatSystem (GetStat / OnStatChanged).
    /// - Chỉ dùng cho local client (fog là visual client-side).
    /// - Khi player chết, tắt revealer để không reveal FOW qua fog.
    /// </summary>
    [RequireComponent(typeof(FogOfWarRevealer3D))]
    public class FogVisionBinder : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("Fallback nếu stat system not ready hoặc not available VisionRange.")]
        [SerializeField] private float defaultViewRadius = 15f;

        private FogOfWarRevealer3D _revealer;
        private IPlayerStatSystem _statSystem;
        private CharacterLifecycleController _lifecycle;
        private FogTeamVisibilityBinder _teamBinder;
        private bool _initialized;

        private void Awake()
        {
            _revealer = GetComponent<FogOfWarRevealer3D>();

            // IPlayerStatSystem lives on a different GO (player root or sibling child).
            // Search up then across full root hierarchy via ComponentResolver (consistent with codebase pattern).
            _statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
                .OnSelf().InParent().InRootChildren()
                .OrLogWarning("[FogVisionBinder] IPlayerStatSystem not found — using defaultViewRadius only")
                .Resolve();

            // CharacterLifecycleController is on the player root.
            _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(this)
                .OnSelf().InParent().InRootChildren()
                .OrLogWarning("[FogVisionBinder] CharacterLifecycleController not found — death vision disable will not work")
                .Resolve();

            _teamBinder = ComponentResolver.Find<FogTeamVisibilityBinder>(this)
                .OnSelf().InParent().InRootChildren()
                .Resolve();
        }

        private void OnEnable()
        {
            TryInit();
            ApplyVisionFromStats();

            if (_statSystem != null)
                _statSystem.OnStatChanged += HandleStatChanged;

            if (_lifecycle != null)
            {
                _lifecycle.OnDied      += RefreshRevealerState;
                _lifecycle.OnRespawned += RefreshRevealerState;
            }

            if (_teamBinder != null)
            {
                _teamBinder.OnEnemyStateChanged += HandleEnemyStateChanged;
            }

            RefreshRevealerState();
        }

        private void OnDisable()
        {
            if (_statSystem != null)
                _statSystem.OnStatChanged -= HandleStatChanged;

            if (_lifecycle != null)
            {
                _lifecycle.OnDied      -= RefreshRevealerState;
                _lifecycle.OnRespawned -= RefreshRevealerState;
            }

            if (_teamBinder != null)
            {
                _teamBinder.OnEnemyStateChanged -= HandleEnemyStateChanged;
            }
        }

        private void TryInit()
        {
            if (_initialized)
                return;

            if (_revealer == null)
                _revealer = GetComponent<FogOfWarRevealer3D>();

            if (_statSystem == null)
                _statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
                    .OnSelf().InParent().InRootChildren()
                    .Resolve();

            if (_lifecycle == null)
                _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(this)
                    .OnSelf().InParent().InRootChildren()
                    .Resolve();

            if (_teamBinder == null)
                _teamBinder = ComponentResolver.Find<FogTeamVisibilityBinder>(this)
                    .OnSelf().InParent().InRootChildren()
                    .Resolve();

            _initialized = true;
        }

        // ── State Handling ──────────────────────────────────────────────────────

        private void HandleEnemyStateChanged(bool isEnemy)
        {
            RefreshRevealerState();
        }

        private void RefreshRevealerState()
        {
            if (_revealer == null) return;

            bool isAlive = _lifecycle == null || !_lifecycle.IsDead;
            bool isAlly = _teamBinder == null || !_teamBinder.IsEnemyToLocal;

            _revealer.enabled = isAlive && isAlly;

            // Only update stats radius if we're actually enabling it
            if (_revealer.enabled)
            {
                ApplyVisionFromStats();
            }

            Debug.Log($"[FogVisionBinder] RefreshRevealerState: isAlive={isAlive}, isAlly={isAlly} -> Enabled={_revealer.enabled}");
        }

        // ── Stat Handling ────────────────────────────────────────────────────────

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            if (type != PlayerStatType.VisionRange)
                return;

            ApplyVisionFromStats();
        }

        private void ApplyVisionFromStats()
        {
            if (_revealer == null)
                return;

            float radius = defaultViewRadius;

            if (_statSystem != null)
            {
                try
                {
                    float v = _statSystem.GetStat(PlayerStatType.VisionRange);
                    // v == 0 means stat cache not yet populated (client waiting for sync).
                    // Keep defaultViewRadius until real data arrives.
                    if (v > 0f)
                        radius = v;
                }
                catch
                {
                    // Fail-safe: giữ default nếu có vấn đề khi đọc stat.
                }
            }

            _revealer.ViewRadius = radius;

            var cfg = NightHuntDebugConfig.Instance;
            if (cfg != null && cfg.EnableStatDebugLogs)
            {
                Debug.Log($"[STAT_FLOW][FogVisionBinder] Apply VisionRange radius={radius:F2} statSystem={(_statSystem != null)} revealerEnabled={_revealer.enabled}", this);
            }
        }
    }
}
