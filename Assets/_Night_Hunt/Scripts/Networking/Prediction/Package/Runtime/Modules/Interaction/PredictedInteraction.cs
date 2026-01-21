using FishNet.Object.Prediction;
using FishNet.Transporting;
using NightHunt.Networking.Prediction.FishNet;
using UnityEngine;

namespace NightHunt.Networking.Prediction.Modules.Interaction
{
    public class PredictedInteraction : FishNetPredictedBehaviour<InteractionReplicateData, InteractionReconcileData>
    {
        [SerializeField] private bool autoApproveOnServer = true;

        private InteractionReplicateData _pendingRequest;
        private bool _hasPending;

        public void RequestInteraction(int targetId, int actionType)
        {
            _pendingRequest = new InteractionReplicateData(targetId, actionType);
            _hasPending = true;
        }

        protected override void TimeManager_OnTick()
        {
            if (IsOwner && _hasPending)
            {
                PerformReplicate(_pendingRequest);
                _hasPending = false;
            }
        }

        [Replicate]
        private void PerformReplicate(InteractionReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            bool asServer = base.IsServerStarted;
            bool replaying = IsReplaying(state);

            OnReplicate(data, asServer, replaying);

            if (asServer && !replaying)
            {
                var reconcile = CreateReconcileData(data);
                PerformReconcile(reconcile, Channel.Unreliable);
            }
        }

        [Reconcile]
        private void PerformReconcile(InteractionReconcileData data, Channel channel = Channel.Unreliable)
        {
            bool asServer = base.IsServerStarted;
            OnReconcile(data, asServer);
        }

        public override void CreateReconcile()
        {
            if (!IsServerStarted)
                return;

            // Create a reconcile from current server-side decision state and broadcast.
            var data = CreateReconcileData();
            PerformReconcile(data, Channel.Unreliable);
        }

        protected virtual void OnReplicate(InteractionReplicateData data, bool asServer, bool replaying)
        {
            if (!asServer && !replaying)
            {
                // Optimistic visual can be placed here.
            }
        }

        protected virtual void OnReconcile(InteractionReconcileData data, bool asServer)
        {
            if (asServer)
                return;

            if (!data.Success)
            {
                // Rollback visual/state if server rejected
            }
        }

        protected override InteractionReconcileData CreateReconcileData()
        {
            // Default to last pending request with auto approval; override to inject validation.
            return CreateReconcileData(_pendingRequest);
        }

        private InteractionReconcileData CreateReconcileData(InteractionReplicateData source)
        {
            bool approved = autoApproveOnServer;
            return new InteractionReconcileData(source.TargetId, source.ActionType, approved);
        }
    }
}