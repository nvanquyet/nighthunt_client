using FishNet.Object.Prediction;
using FishNet.Transporting;
using NightHunt.Networking.Prediction.FishNet;
using UnityEngine;

namespace NightHunt.Networking.Prediction.Modules.Attack
{
    public class PredictedAttack : FishNetPredictedBehaviour<AttackReplicateData, AttackReconcileData>
    {
        [SerializeField] private bool autoHitOnServer = true;
        [SerializeField] private float defaultDamage = 10f;

        private AttackReplicateData _pendingAttack;
        private bool _hasPending;

        public void Fire(int weaponId, Vector3 origin, Vector3 direction)
        {
            _pendingAttack = new AttackReplicateData(weaponId, origin, direction);
            _hasPending = true;
            PlayLocalEffects(origin, direction);
        }

        protected override void TimeManager_OnTick()
        {
            if (IsOwner && _hasPending)
            {
                PerformReplicate(_pendingAttack);
                _hasPending = false;
            }
        }

        [Replicate]
        private void PerformReplicate(AttackReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            bool asServer = base.IsServerStarted;
            bool replaying = IsReplaying(state);

            _pendingAttack = data; // keep for reconcile build
            OnReplicate(data, asServer, replaying);

            if (asServer && !replaying)
            {
                var reconcile = CreateReconcileData();
                PerformReconcile(reconcile, Channel.Unreliable);
            }
        }

        [Reconcile]
        private void PerformReconcile(AttackReconcileData data, Channel channel = Channel.Unreliable)
        {
            bool asServer = base.IsServerStarted;
            OnReconcile(data, asServer);
        }

        public override void CreateReconcile()
        {
            if (!IsServerStarted)
                return;

            var data = CreateReconcileData();
            PerformReconcile(data, Channel.Unreliable);
        }

        protected virtual void OnReplicate(AttackReplicateData data, bool asServer, bool replaying)
        {
            // Server-side hit validation should be placed here.
        }

        protected virtual void OnReconcile(AttackReconcileData data, bool asServer)
        {
            if (asServer)
                return;

            if (!data.Success)
            {
                RollbackLocalEffects();
            }
        }

        protected override AttackReconcileData CreateReconcileData()
        {
            bool success = autoHitOnServer;
            int hitTargetId = success ? 1 : -1;
            float damage = success ? defaultDamage : 0f;
            Vector3 hitPos = _pendingAttack.FireOrigin + _pendingAttack.FireDirection * 5f;

            return new AttackReconcileData(_pendingAttack.WeaponId, success, hitTargetId, damage, hitPos);
        }

        private void PlayLocalEffects(Vector3 origin, Vector3 direction)
        {
            // Placeholder VFX/SFX
        }

        private void RollbackLocalEffects()
        {
            // Placeholder rollback for predicted projectile/VFX
        }
    }
}