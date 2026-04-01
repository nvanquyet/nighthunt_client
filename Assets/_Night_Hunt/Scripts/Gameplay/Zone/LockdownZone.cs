using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using System.Collections.Generic;

namespace NightHunt.Gameplay.Zone
{
    /// <summary>
    /// Phase 3 lockdown zone.
    /// Shrinks over time; players outside the zone receive periodic damage.
    ///
    /// Damage pipeline (server-authoritative):
    ///   CheckPlayersOutsideZone() [Server Update]
    ///   → PlayerHealthSystem.ApplyDamageServer(DamageInfo) ← no RPC, already on server
    ///
    /// Network sync:
    ///   _syncRadius / _syncProgress → all clients update visual ring
    /// </summary>
    [DisallowMultipleComponent]
    public class LockdownZone : NetworkBehaviour
    {
        [Header("Zone Settings")]
        [SerializeField] private float _initialRadius    = 100f;
        [SerializeField] private float _finalRadius      = 20f;
        [SerializeField] private float _closeTime        = 120f;   // seconds
        [SerializeField] private float _damagePerSecond  = 10f;
        [SerializeField] private float _damageTickInterval = 1f;   // how often to apply damage

        [Header("Zone Center")]
        [Tooltip("World-space center of the lockdown zone. Defaults to this object's position.")]
        [SerializeField] private Transform _zoneCenter;

        [Header("References")]
        [SerializeField] private MatchPhaseManager _phaseManager;

        // ── Network Sync ────────────────────────────────────────────────────────
        private readonly SyncVar<float> _syncRadius   = new SyncVar<float>();
        private readonly SyncVar<float> _syncProgress = new SyncVar<float>();

        // ── Server runtime ───────────────────────────────────────────────────────
        private float _damageTickTimer;

        // ── Public ───────────────────────────────────────────────────────────────
        public float   CurrentRadius => _syncRadius.Value;
        public float   CloseProgress => _syncProgress.Value;
        public Vector3 Center        => _zoneCenter != null ? _zoneCenter.position : transform.position;

        // ── FishNet ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_zoneCenter == null) _zoneCenter = transform;
            if (_phaseManager == null) _phaseManager = FindFirstObjectByType<MatchPhaseManager>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _syncRadius.OnChange += OnRadiusChanged;
            // Initialise visual immediately for all clients
            UpdateVisual(_initialRadius);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _syncRadius.OnChange -= OnRadiusChanged;
        }

        private void Update()
        {
            if (!IsServerStarted) return;

            // Only active during Phase 3
            if (_phaseManager == null || _phaseManager.CurrentPhase != MatchPhaseState.Lockdown)
                return;

            UpdateZoneClosing();
            TickDamage();
        }

        // ── Server logic ─────────────────────────────────────────────────────────

        [Server]
        private void UpdateZoneClosing()
        {
            float elapsed  = _phaseManager.PhaseElapsedTime;
            float progress = Mathf.Clamp01(elapsed / _closeTime);
            float radius   = Mathf.Lerp(_initialRadius, _finalRadius, progress);

            _syncRadius.Value   = radius;
            _syncProgress.Value = progress;
        }

        [Server]
        private void TickDamage()
        {
            _damageTickTimer += Time.deltaTime;
            if (_damageTickTimer < _damageTickInterval) return;
            _damageTickTimer = 0f;

            float radius = _syncRadius.Value;
            Vector3 center = _zoneCenter.position;

            var players = PlayerPublicRegistry.Instance?.GetAllPlayers();
            if (players == null) return;

            foreach (var np in players)
            {
                if (np == null || !np.IsAlive) continue;

                float dist = Vector3.Distance(center, np.transform.position);
                if (dist <= radius) continue; // inside zone — safe

                var healthSystem = np.GetComponentInChildren<PlayerHealthSystem>();
                if (healthSystem == null) continue;

                float damage = _damagePerSecond * _damageTickInterval;
                var info = new DamageInfo
                {
                    Damage = damage,
                    WeaponId = "zone_lockdown",
                    ShooterNetworkObjectId = -1,   // world damage
                    IsHeadshot = false,
                    HitPoint = np.transform.position,
                    HitNormal = Vector3.up,
                };

                healthSystem.ApplyDamageServer(info);

                Debug.Log($"[LockdownZone] Zone damage {damage:F0} → {np.DisplayName} " +
                          $"(dist {dist:F1} > radius {radius:F1})");
            }
        }

        // ── Client visual ─────────────────────────────────────────────────────────

        private void OnRadiusChanged(float prev, float next, bool asServer)
        {
            UpdateVisual(next);
        }

        private void UpdateVisual(float radius)
        {
            // Scale a child sphere/cylinder mesh to represent the zone boundary.
            // If no visual child is set, scale this object's transform.
            transform.localScale = Vector3.one * radius * 2f;
        }

        // ── Editor gizmos ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = _zoneCenter != null ? _zoneCenter.position : transform.position;
            float radius = Application.isPlaying ? _syncRadius.Value : _initialRadius;

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(center, radius);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.08f);
            Gizmos.DrawSphere(center, radius);
        }
#endif
    }
}
