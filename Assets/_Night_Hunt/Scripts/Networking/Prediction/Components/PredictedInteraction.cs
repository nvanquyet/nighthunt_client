using UnityEngine;
using FishNet.Object;
using NightHunt.Networking.Prediction.Core;
using NightHunt.Networking.Prediction.Input;
using System;

namespace NightHunt.Networking.Prediction.Components
{
    /// <summary>
    /// State structure cho Interaction prediction.
    /// </summary>
    [System.Serializable]
    public struct InteractionState : System.IEquatable<InteractionState>
    {
        public uint itemId;
        public bool hasItem;
        public float weight;

        public InteractionState(uint itemId, bool hasItem, float weight)
        {
            this.itemId = itemId;
            this.hasItem = hasItem;
            this.weight = weight;
        }

        public bool Equals(InteractionState other)
        {
            return itemId == other.itemId && 
                   hasItem == other.hasItem && 
                   Mathf.Approximately(weight, other.weight);
        }

        public override bool Equals(object obj)
        {
            return obj is InteractionState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(itemId, hasItem, weight);
        }
    }

    /// <summary>
    /// Input structure cho Interaction prediction.
    /// </summary>
    [System.Serializable]
    public struct InteractionInput : IInputData
    {
        public uint itemId;
        public NetworkObject targetObject;
        public bool isPickup;

        public InteractionInput(uint itemId, NetworkObject targetObject, bool isPickup)
        {
            this.itemId = itemId;
            this.targetObject = targetObject;
            this.isPickup = isPickup;
        }

        public bool HasChanged(IInputData other)
        {
            if (other is InteractionInput otherInput)
            {
                return itemId != otherInput.itemId ||
                       targetObject != otherInput.targetObject ||
                       isPickup != otherInput.isPickup;
            }
            return true;
        }

        public void Reset()
        {
            itemId = 0;
            targetObject = null;
            isPickup = false;
        }
    }

    /// <summary>
    /// Base class cho interaction prediction.
    /// Hỗ trợ item pickup, equipment swap, beacon placement, v.v.
    /// </summary>
    public abstract class PredictedInteraction : PredictedObject<InteractionState, InteractionInput>
    {
        [Header("Interaction Settings")]
        [SerializeField] private float maxInteractionRange = 5f;

        /// <summary>
        /// Max interaction range.
        /// </summary>
        public float MaxInteractionRange => maxInteractionRange;

        protected override InteractionState GetInitialState()
        {
            return new InteractionState(0, false, 0f);
        }

        protected override bool TryGetInput(out InteractionInput input)
        {
            // Input sẽ được set từ external code khi player interact
            input = default;
            return false;
        }

        /// <summary>
        /// Trigger interaction từ external code.
        /// </summary>
        /// <param name="itemId">Item ID (nếu là pickup)</param>
        /// <param name="targetObject">Target NetworkObject</param>
        /// <param name="isPickup">Is pickup interaction</param>
        public void TriggerInteraction(uint itemId, NetworkObject targetObject, bool isPickup)
        {
            if (!IsOwner || !IsPredictionEnabled)
                return;

            var input = new InteractionInput(itemId, targetObject, isPickup);
            ProcessInteraction(input);
        }

        /// <summary>
        /// Process interaction và predict state.
        /// </summary>
        private void ProcessInteraction(InteractionInput input)
        {
            _currentState = PredictInteraction(input, _currentState);
            ApplyState(_currentState);

            // Gửi interaction request lên server
            SendInteractionToServer(input);
        }

        /// <summary>
        /// Predict interaction state.
        /// Override method này trong derived classes.
        /// </summary>
        /// <param name="input">Interaction input</param>
        /// <param name="current">Current state</param>
        /// <returns>New predicted state</returns>
        protected abstract InteractionState PredictInteraction(InteractionInput input, InteractionState current);

        protected override InteractionState PredictState(InteractionInput input, InteractionState currentState)
        {
            return PredictInteraction(input, currentState);
        }

        protected override void ApplyState(InteractionState state)
        {
            // Apply interaction state (ví dụ: update inventory UI, hide/show items)
            ApplyInteractionState(state);
        }

        /// <summary>
        /// Apply interaction state.
        /// Override method này trong derived classes.
        /// </summary>
        /// <param name="state">State cần apply</param>
        protected abstract void ApplyInteractionState(InteractionState state);

        /// <summary>
        /// Gửi interaction request lên server.
        /// Override method này để customize RPC.
        /// </summary>
        /// <param name="input">Interaction input</param>
        protected virtual void SendInteractionToServer(InteractionInput input)
        {
            // Default: Gửi qua ServerRpc
            // Derived classes sẽ implement cụ thể
        }

        /// <summary>
        /// Validate interaction trên server.
        /// Override method này trong derived classes.
        /// </summary>
        /// <param name="input">Interaction input</param>
        /// <returns>True nếu interaction hợp lệ</returns>
        public virtual bool ValidateInteraction(InteractionInput input)
        {
            if (input.targetObject == null)
                return false;

            // Check distance
            float distance = Vector3.Distance(transform.position, input.targetObject.transform.position);
            if (distance > maxInteractionRange)
                return false;

            return true;
        }

        /// <summary>
        /// Get item weight (nếu là item).
        /// Override method này trong derived classes.
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>Item weight</returns>
        protected virtual float GetItemWeight(uint itemId)
        {
            return 0f;
        }
    }
}

