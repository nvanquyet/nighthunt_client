using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;
using UnityEngine;
using FishNet.Object;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Core.State
{
    /// <summary>
    /// Bật/tắt input dựa trên lifecycle event, không sửa code input hiện tại.
    /// QUAN TRỌNG: Chỉ chạy cho LOCAL player (IsOwner).
    /// Không có guard này thì khi player khác chết, InputLayerManager của client đang sống cũng bị freeze.
    /// </summary>
    [RequireComponent(typeof(CharacterLifecycleController))]
    public sealed class CharacterInputLifecycle : MonoBehaviour
    {
        private CharacterLifecycleController _lifecycle;
        // NetworkObject cần thiết để check IsOwner — character này có phải của local client không.
        private NetworkObject _networkObject;

        private void Awake()
        {
            _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] CharacterLifecycleController not found")
        .Resolve();

            _networkObject = ComponentResolver.Find<NetworkObject>(this)
        .OnSelf()
        .InParent()
        .OrLogWarning("[Auto] NetworkObject not found on CharacterInputLifecycle")
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
            // Guard: chỉ ảnh hưởng input của local client khi CHÍNH character này chết.
            // Nếu không có guard, khi bất kỳ player nào chết (SyncList update broadcast
            // sang all clients) thì InputLayerManager của CLIENT ĐANG SỐNG cũng bị freeze.
            if (_networkObject != null && !_networkObject.IsOwner)
                return;

            InputLayerManager.Instance?.TransitionToState(InputState.PlayerDead);
        }

        private void HandleRespawned()
        {
            // Cùng guard — chỉ restore input khi local player của client này hồi sinh.
            if (_networkObject != null && !_networkObject.IsOwner)
                return;

            InputLayerManager.Instance?.TransitionToState(InputState.PlayerAlive);
        }
    }
}

