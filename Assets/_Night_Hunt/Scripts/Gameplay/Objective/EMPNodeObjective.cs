using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Scoring;
using NightHunt.Networking.Player;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// EMP node objective - Server Authoritative.
    /// Players destroy this node by dealing damage to it (IHittable).
    /// On completion: awards objective score to the destroying team.
    /// </summary>
    public class EMPNodeObjective : NetworkBehaviour, IObjective, IHittable
    {
        [Header("EMP Node Settings")]
        [SerializeField] private string objectiveId = "EMP_NODE";
        [SerializeField] private string objectiveName = "Destroy EMP Node";
        [SerializeField] private float maxHealth = 100f;

        [Header("Score")]
        [Tooltip("Score awarded to the destroying team via MatchEndManager.AddObjectiveScore.")]
        [SerializeField] private float completionScore = 200f;

        private readonly SyncVar<float> _syncHealth     = new SyncVar<float>();
        private readonly SyncVar<bool>  _syncIsCompleted = new SyncVar<bool>();

        // Track which team landed the final blow, resolved from PlayerPublicRegistry.
        private int _lastAttackerTeamId = -1;

        public string ObjectiveId  => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool   IsCompleted  => _syncIsCompleted.Value;
        public float  CurrentHealth => _syncHealth.Value;
        public float  MaxHealth => maxHealth;
        public event System.Action<float, float> OnHealthChanged;

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
            _syncIsCompleted.Value  = false;
            _syncHealth.Value       = maxHealth;
            _lastAttackerTeamId     = -1;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _syncHealth.OnChange += OnHealthSyncChanged;
            EnsureWorldHealthBar();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _syncHealth.OnChange -= OnHealthSyncChanged;
        }

        public void OnUpdate()
        {
            if (!IsServerStarted || _syncIsCompleted.Value) return;

            if (_syncHealth.Value <= 0f)
                OnComplete();
        }

        [Server]
        public void OnComplete()
        {
            if (_syncIsCompleted.Value) return;

            _syncIsCompleted.Value = true;
            _syncHealth.Value      = 0f;

            Debug.Log($"[EMPNodeObjective] '{objectiveName}' destroyed by team {_lastAttackerTeamId}.");

            // Award objective score to the destroying team.
            if (_lastAttackerTeamId >= 0)
            {
                var mem = FindFirstObjectByType<MatchEndManager>();
                if (mem != null)
                    mem.AddObjectiveScore(_lastAttackerTeamId, completionScore);

                var scoring = FindFirstObjectByType<ScoringSystem>();
                if (scoring != null)
                    scoring.AwardObjectiveCapture(_lastAttackerTeamId, 0f);
            }
        }

        public void OnFail() { /* EMP node doesn't fail */ }

        private void OnHealthSyncChanged(float prev, float next, bool asServer)
        {
            OnHealthChanged?.Invoke(next, maxHealth);
        }

        private void EnsureWorldHealthBar()
        {
            if (GetComponentInChildren<WorldHealthBarGeneric>(true) != null)
                return;

            gameObject.AddComponent<WorldHealthBarGeneric>();
        }

        /// <summary>Take damage (server-only helper called by IHittable path).</summary>
        [Server]
        public void TakeDamage(float damage)
        {
            if (!_syncIsCompleted.Value)
                _syncHealth.Value = Mathf.Max(0f, _syncHealth.Value - damage);
        }

        // ── IHittable ──────────────────────────────────────────────────────────

        public void RequestDamage(DamageInfo info) =>
            RequestDamageServerRpc(info.Damage, info.ShooterNetworkObjectId);

        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(float damage, int shooterNetObjId)
        {
            // Resolve attacker team before applying damage so the last attacker
            // that brings health to 0 is the one whose team gets the score.
            if (shooterNetObjId > 0)
            {
                if (InstanceFinder.NetworkManager.ServerManager.Objects.Spawned
                       .TryGetValue(shooterNetObjId, out var nob))
                {
                    var player = nob.GetComponent<NetworkPlayer>();
                    if (player != null)
                        _lastAttackerTeamId = player.TeamId;
                }
            }
            TakeDamage(damage);
        }
    }
}
