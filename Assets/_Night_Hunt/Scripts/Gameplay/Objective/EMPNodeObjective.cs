using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Character.Combat;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// EMP node objective - Server Authoritative
    /// </summary>
    public class EMPNodeObjective : NetworkBehaviour, IObjective, IHittable
    {
        [Header("EMP Node Settings")]
        [SerializeField] private string objectiveId = "EMP_NODE";
        [SerializeField] private string objectiveName = "Destroy EMP Node";
        [SerializeField] private float maxHealth = 100f;

        private readonly SyncVar<float> _syncHealth = new SyncVar<float>();
        private readonly SyncVar<bool> _syncIsCompleted = new SyncVar<bool>();

        public string ObjectiveId => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool IsCompleted => _syncIsCompleted.Value;
        
        public float Progress 
        {
            get
            {
                if (maxHealth <= 0) return 0f;
                return 1f - (_syncHealth.Value / maxHealth);
            }
        }

        public void OnStart()
        {
            if (!IsServerStarted) return;
            _syncIsCompleted.Value = false;
            _syncHealth.Value = maxHealth;
        }

        public void OnUpdate()
        {
            if (!IsServerStarted || _syncIsCompleted.Value) return;

            if (_syncHealth.Value <= 0f)
            {
                OnComplete();
            }
        }

        [Server]
        public void OnComplete()
        {
            if (_syncIsCompleted.Value) return;

            _syncIsCompleted.Value = true;
            _syncHealth.Value = 0f;
            Debug.Log($"[EMPNodeObjective] EMP node destroyed: {objectiveName}");
        }

        public void OnFail()
        {
            // EMP node doesn't fail
        }

        /// <summary>
        /// Take damage
        /// </summary>
        [Server]
        public void TakeDamage(float damage)
        {
            if (!_syncIsCompleted.Value)
            {
                _syncHealth.Value = Mathf.Max(0f, _syncHealth.Value - damage);
            }
        }

        // ── IHittable ──────────────────────────────────────────────────────────
        public void RequestDamage(DamageInfo info) => RequestDamageServerRpc(info.Damage);

        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(float damage) => TakeDamage(damage);
    }
}

