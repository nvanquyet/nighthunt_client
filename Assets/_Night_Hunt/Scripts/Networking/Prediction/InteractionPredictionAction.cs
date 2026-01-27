using NightHunt.Networking.Prediction;
using NightHunt.Utils;
using NightHunt.InteractionSystem.Core.Interfaces;
using FishNet.Object;
using UnityEngine;

namespace NightHunt.Networking.Prediction
{
    /// <summary>
    /// Predicted action cho 1 lần interaction (ví dụ: nhặt loot).
    /// - StartPrediction: ẩn loot (renderer + collider) ngay trên client.
    /// - Confirm: server chấp nhận → giữ nguyên state đã ẩn.
    /// - Rollback: server từ chối → bật lại renderer + collider.
    /// </summary>
    public class InteractionPredictionAction : IPredictedAction
    {
        private readonly NetworkObject _targetObject;
        private readonly IInteractable _interactable;
        private bool _predicted;

        public InteractionPredictionAction(NetworkObject targetObject, IInteractable interactable)
        {
            _targetObject = targetObject;
            _interactable = interactable;
            _predicted = false;
        }

        public void StartPrediction()
        {
            if (_interactable == null || _targetObject == null)
                return;

            // NOTE:
            // Không gọi CanInteract() ở đây vì:
            // - Không có player GameObject reference trong action này.
            // - CanInteract() cần NetworkInventory của player, không phải targetObject.
            // - Server sẽ validate CanInteract() với đúng player GameObject.

            // PREDICTION: Thực hiện visual update ngay lập tức để làm smooth.
            // Nếu server reject, sẽ rollback trong Rollback().
            // var networkLoot = _targetObject.GetComponent<NetworkLootItem>();
            // if (networkLoot != null)
            // {
            //     GameLogger.LogDebug("Client prediction: Hiding loot item visually", "InteractionPredictionAction");
            //
            //     // Hide visual (client-side only, server sẽ sync sau).
            //     var renderers = _targetObject.GetComponentsInChildren<Renderer>();
            //     foreach (var renderer in renderers)
            //     {
            //         renderer.enabled = false;
            //     }
            //
            //     // Hide collider để không thể interact lại.
            //     var colliders = _targetObject.GetComponentsInChildren<Collider>();
            //     foreach (var collider in colliders)
            //     {
            //         collider.enabled = false;
            //     }
            // }

            _predicted = true;
        }

        public void Confirm()
        {
            // Server accept → prediction đúng, không cần làm gì thêm ngoài log.
            if (_predicted)
            {
                //GameLogger.LogDebug("[InteractionPredictionAction] Prediction confirmed by server.", "InteractionPredictionAction");
                Debug.Log("[InteractionPredictionAction] Prediction confirmed by server.");
            }
        }

        public void Rollback()
        {
            if (!_predicted || _targetObject == null)
                return;

            // var networkLoot = _targetObject.GetComponent<NetworkLootItem>();
            // if (networkLoot != null)
            // {
            //     GameLogger.LogDebug("[InteractionPredictionAction] Rollback: Showing loot item again (server rejected).", "InteractionPredictionAction");
            //
            //     // Restore visual (client-side rollback).
            //     var renderers = _targetObject.GetComponentsInChildren<Renderer>();
            //     foreach (var renderer in renderers)
            //     {
            //         renderer.enabled = true;
            //     }
            //
            //     // Restore collider.
            //     var colliders = _targetObject.GetComponentsInChildren<Collider>();
            //     foreach (var collider in colliders)
            //     {
            //         collider.enabled = true;
            //     }
            // }
        }
    }
}


