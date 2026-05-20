using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Utilities;
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
    public class LockdownZone : NetworkBehaviour, IZoneAreaInfo
    {
        // ── IZoneAreaInfo ─────────────────────────────────────────────────────────
        public string  ZoneId   => "lockdown_zone";
        public Vector3 Center   => (_syncIsActive.Value || _syncRadius.Value > 0f)
            ? _syncCenter.Value
            : (_zoneCenter != null ? _zoneCenter.position : transform.position);
        public float   Radius   => _syncRadius.Value > 0f ? _syncRadius.Value : (_syncIsActive.Value ? _initialRadius : 0f);
        public bool    IsActive => IsSpawned && _syncIsActive.Value;

        public bool ContainsPoint(Vector3 worldPos)
            => Vector3.SqrMagnitude(worldPos - Center) <= Radius * Radius;

        [Header("Zone Settings")]
        [SerializeField] private float _initialRadius    = 100f;
        [SerializeField] private float _finalRadius      = 20f;
        [SerializeField] private float _closeTime        = 120f;   // seconds
        [SerializeField] private float _damagePerSecond  = 10f;
        [SerializeField] private float _damageTickInterval = 1f;   // how often to apply damage
        [Tooltip("If true, living players are scattered inside the lockdown zone when Phase 3 starts.")]
        [SerializeField] private bool _teleportPlayersToCenterOnStart = true;
        [Tooltip("Max scatter radius for each player on lockdown start. Clamped to the active zone radius.")]
        [SerializeField] private float _teleportSpreadRadius = 16f;

        [Header("Random Center")]
        [SerializeField] private bool _randomizeCenterOnLockdownStart = true;
        [Tooltip("Preferred lockdown centers. Server picks one randomly when Phase 3 starts.")]
        [SerializeField] private Transform[] _centerCandidates;
        [Tooltip("Fallback random rectangle around the design-time center when no candidates are assigned.")]
        [SerializeField] private Vector2 _fallbackRandomCenterHalfExtents = new Vector2(60f, 60f);

        [Header("Zone Center")]
        [Tooltip("World-space center of the lockdown zone. Defaults to this object's position.")]
        [SerializeField] private Transform _zoneCenter;

        [Header("References")]
        [SerializeField] private MatchPhaseManager _phaseManager;

        // ── Network Sync ────────────────────────────────────────────────────────
        private readonly SyncVar<float> _syncRadius   = new SyncVar<float>();
        private readonly SyncVar<float> _syncProgress = new SyncVar<float>();
        private readonly SyncVar<Vector3> _syncCenter = new SyncVar<Vector3>();
        private readonly SyncVar<bool> _syncIsActive = new SyncVar<bool>();

        // ── Server runtime ───────────────────────────────────────────────────────
        private float _damageTickTimer;
        private bool _lockdownStarted;

        // ── Public ───────────────────────────────────────────────────────────────
        public float CurrentRadius => _syncRadius.Value;
        public float CloseProgress => _syncProgress.Value;
        // Center is already exposed via IZoneAreaInfo (line 28)

        // ── FishNet ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_zoneCenter == null) _zoneCenter = transform;
            if (_phaseManager == null) _phaseManager = FindFirstObjectByType<MatchPhaseManager>();
        }

        private void OnEnable()  => ZoneSystem.Instance?.RegisterZone(this);
        private void OnDisable() => ZoneSystem.Instance?.UnregisterZone(this);

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _syncRadius.OnChange += OnRadiusChanged;
            _syncCenter.OnChange += OnCenterChanged;
            _syncIsActive.OnChange += OnActiveChanged;

            ApplyCenter(Center);
            UpdateVisual(Radius);
            SetZonePresentation(_syncIsActive.Value);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _syncRadius.OnChange -= OnRadiusChanged;
            _syncCenter.OnChange -= OnCenterChanged;
            _syncIsActive.OnChange -= OnActiveChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _syncCenter.Value = ResolveDesignCenter();
            _syncRadius.Value = 0f;
            _syncProgress.Value = 0f;
            _syncIsActive.Value = false;
            SetZonePresentation(false);
        }

        private void Update()
        {
            if (!IsServerStarted) return;

            // Only active during Phase 3
            if (_phaseManager == null || _phaseManager.CurrentPhase != MatchPhaseState.Lockdown)
                return;
            if (!_syncIsActive.Value)
                return;

            UpdateZoneClosing();
            TickDamage();
        }

        [Server]
        public void ActivateForLockdown()
        {
            if (_lockdownStarted) return;
            _lockdownStarted = true;
            _damageTickTimer = 0f;
            _syncCenter.Value = ResolveLockdownCenter();
            _syncRadius.Value = _initialRadius;
            _syncProgress.Value = 0f;
            _syncIsActive.Value = true;

            if (_teleportPlayersToCenterOnStart)
                ScatterAlivePlayersInsideZone();
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
            Vector3 center = Center;

            var players = GetAuthoritativePlayers();
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

        [Server]
        private void ScatterAlivePlayersInsideZone()
        {
            var players = GetAuthoritativePlayers();
            if (players == null) return;

            int index = 0;
            foreach (var np in players)
            {
                if (np == null || !np.IsAlive) continue;

                float safeRadius = Mathf.Max(0f, Mathf.Min(_teleportSpreadRadius, Radius * 0.75f));
                Vector2 offset2 = UnityEngine.Random.insideUnitCircle * safeRadius;
                Vector3 target = Center + new Vector3(offset2.x, 0f, offset2.y);

                var movement = ComponentResolver.Find<IMovementController>(np)
                    .OnSelf()
                    .InChildren()
                    .Resolve();
                if (movement != null)
                    movement.Teleport(target, np.transform.rotation);
                else
                    np.transform.position = target;

                index++;
            }
        }

        // ── Client visual ─────────────────────────────────────────────────────────

        private static NetworkPlayer[] GetAuthoritativePlayers()
        {
            var serverPlayers = RegistryService.Instance?.GetAllPlayers();
            if (serverPlayers != null && serverPlayers.Length > 0)
                return serverPlayers;

            return PlayerPublicRegistry.Instance?.GetAllPlayers();
        }

        private void OnRadiusChanged(float prev, float next, bool asServer)
        {
            UpdateVisual(next);
        }

        private void OnCenterChanged(Vector3 prev, Vector3 next, bool asServer)
        {
            ApplyCenter(next);
        }

        private void OnActiveChanged(bool prev, bool next, bool asServer)
        {
            SetZonePresentation(next);
            UpdateVisual(next ? Radius : 0f);
        }

        private void UpdateVisual(float radius)
        {
            // Scale a child sphere/cylinder mesh to represent the zone boundary.
            // If no visual child is set, scale this object's transform.
            transform.localScale = Vector3.one * radius * 2f;
        }

        private Vector3 ResolveDesignCenter()
        {
            return _zoneCenter != null ? _zoneCenter.position : transform.position;
        }

        private Vector3 ResolveLockdownCenter()
        {
            if (!_randomizeCenterOnLockdownStart)
                return ResolveDesignCenter();

            if (_centerCandidates != null && _centerCandidates.Length > 0)
            {
                for (int attempts = 0; attempts < _centerCandidates.Length; attempts++)
                {
                    var candidate = _centerCandidates[UnityEngine.Random.Range(0, _centerCandidates.Length)];
                    if (candidate != null)
                        return candidate.position;
                }
            }

            Vector3 center = ResolveDesignCenter();
            Vector2 offset = new Vector2(
                UnityEngine.Random.Range(-_fallbackRandomCenterHalfExtents.x, _fallbackRandomCenterHalfExtents.x),
                UnityEngine.Random.Range(-_fallbackRandomCenterHalfExtents.y, _fallbackRandomCenterHalfExtents.y));
            return center + new Vector3(offset.x, 0f, offset.y);
        }

        private void ApplyCenter(Vector3 center)
        {
            if (_zoneCenter != null)
                _zoneCenter.position = center;
            else
                transform.position = center;
        }

        private void SetZonePresentation(bool active)
        {
            var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].enabled = active;

            var colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null)
                    colliders[i].enabled = active;
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
