using FOW;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using UnityEngine;

namespace NightHunt.Gameplay.FogOfWar
{
    /// <summary>
    /// Binds player VisionRange stat to FogOfWarRevealer3D.ViewRadius.
    /// - Đọc stat qua IPlayerStatSystem (GetStat / OnStatChanged).
    /// - Chỉ dùng cho local client (fog là visual client-side).
    /// </summary>
    [RequireComponent(typeof(FogOfWarRevealer3D))]
    public class FogVisionBinder : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("Fallback nếu stat system chưa sẵn sàng hoặc không có VisionRange.")]
        [SerializeField] private float defaultViewRadius = 15f;

        private FogOfWarRevealer3D _revealer;
        private IPlayerStatSystem _statSystem;
        private bool _initialized;

        private void Awake()
        {
            _revealer = GetComponent<FogOfWarRevealer3D>();
            _statSystem = GetComponent<IPlayerStatSystem>();

            if (_statSystem == null)
            {
                Debug.LogWarning("[FogVisionBinder] IPlayerStatSystem not found on object, using defaultViewRadius only.", this);
            }
        }

        private void OnEnable()
        {
            TryInit();
            ApplyVisionFromStats();

            if (_statSystem != null)
            {
                _statSystem.OnStatChanged += HandleStatChanged;
            }
        }

        private void OnDisable()
        {
            if (_statSystem != null)
            {
                _statSystem.OnStatChanged -= HandleStatChanged;
            }
        }

        private void TryInit()
        {
            if (_initialized)
                return;

            if (_revealer == null)
                _revealer = GetComponent<FogOfWarRevealer3D>();

            if (_statSystem == null)
                _statSystem = GetComponent<IPlayerStatSystem>();

            _initialized = true;
        }

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

