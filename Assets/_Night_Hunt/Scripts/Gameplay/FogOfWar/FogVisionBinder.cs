using FOW;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Utilities;
using UnityEngine;

namespace NightHunt.Gameplay.FogOfWar
{
    /// <summary>
    /// Binds player VisionRange stat to FogOfWarRevealer3D.ViewRadius.
    /// - Đọc stat qua IPlayerStatSystem (GetStat / OnStatChanged).
    /// - Chỉ dùng cho local client (fog là visual client-side).
    /// - Khi player chết, tắt revealer để không reveal FOW qua fog.
    /// </summary>
    [RequireComponent(typeof(FogOfWarRevealer3D))]
    public class FogVisionBinder : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("Fallback nếu stat system chưa sẵn sàng hoặc không có VisionRange.")]
        [SerializeField] private float defaultViewRadius = 15f;

        private FogOfWarRevealer3D _revealer;
        private IPlayerStatSystem _statSystem;
        private CharacterLifecycleController _lifecycle;
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
        }

        private void OnEnable()
        {
            TryInit();
            ApplyVisionFromStats();

            if (_statSystem != null)
                _statSystem.OnStatChanged += HandleStatChanged;

            if (_lifecycle != null)
            {
                _lifecycle.OnDied      += OnPlayerDied;
                _lifecycle.OnRespawned += OnPlayerRespawned;
            }
        }

        private void OnDisable()
        {
            if (_statSystem != null)
                _statSystem.OnStatChanged -= HandleStatChanged;

            if (_lifecycle != null)
            {
                _lifecycle.OnDied      -= OnPlayerDied;
                _lifecycle.OnRespawned -= OnPlayerRespawned;
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

            _initialized = true;
        }

        // ── Death / Respawn ──────────────────────────────────────────────────────

        /// <summary>Disable FOW revealer so the dead player doesn't reveal fog.</summary>
        private void OnPlayerDied()
        {
            if (_revealer != null)
            {
                _revealer.enabled = false;
                Debug.Log("[FogVisionBinder] Player died — FogOfWarRevealer3D disabled.");
            }
        }

        /// <summary>Re-enable FOW revealer when player respawns.</summary>
        private void OnPlayerRespawned()
        {
            if (_revealer != null)
            {
                _revealer.enabled = true;
                ApplyVisionFromStats(); // restore correct radius
                Debug.Log("[FogVisionBinder] Player respawned — FogOfWarRevealer3D re-enabled.");
            }
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
        }
    }
}
