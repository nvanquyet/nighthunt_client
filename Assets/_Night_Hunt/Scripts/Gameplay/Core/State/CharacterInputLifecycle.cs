using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;
using UnityEngine;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Core.State
{
    /// <summary>
    /// Bật/tắt input dựa trên lifecycle event, không sửa code input hiện tại.
    /// </summary>
    [RequireComponent(typeof(CharacterLifecycleController))]
    public sealed class CharacterInputLifecycle : MonoBehaviour
    {
        private CharacterLifecycleController _lifecycle;

        private void Awake()
        {
            _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] CharacterLifecycleController not found")
        .Resolve();
        }

        private void OnEnable()
        {
            if (_lifecycle == null)
                return;

            _lifecycle.OnDied += HandleDied;
            _lifecycle.OnRespawned += HandleRespawned;
        }

        private void OnDisable()
        {
            if (_lifecycle == null)
                return;

            _lifecycle.OnDied -= HandleDied;
            _lifecycle.OnRespawned -= HandleRespawned;
        }

        private void HandleDied()
        {
            // Single source of truth: chỉ gọi InputLayerManager
            // InputLayerManager sẽ disable toàn bộ ActionMap phù hợp với PlayerDead preset
            InputLayerManager.Instance?.TransitionToState(InputState.PlayerDead);
        }

        private void HandleRespawned()
        {
            // Single source of truth: chỉ gọi InputLayerManager
            InputLayerManager.Instance?.TransitionToState(InputState.PlayerAlive);
        }
    }
}

