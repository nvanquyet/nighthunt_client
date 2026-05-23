using System;
using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Networking.Player;
using NightHunt.Core;

namespace NightHunt.Gameplay.Zone
{
    /// <summary>
    /// Server-authoritative safe zone manager (PUBG-style shrinking circle).
    ///
    /// Server responsibilities:
    ///   • Advance through SafeZoneMatchConfig.phases sequentially
    ///   • Randomise next zone center within current zone boundary
    ///   • Shrink radius over time (server Update lerp → SyncVar → all clients)
    ///   • Deal periodic damage to players outside the zone
    ///   • Fire OnZonePhaseStarted(int) for BossSpawnManager / ObjectiveSystem
    ///   • Fire OnFinalZoneExpired when the last zone has finished its wait (used by MatchEndManager)
    ///
    /// Client responsibilities:
    ///   • Read SyncVars to drive SafeZoneHUD ring visual + minimap ring
    ///   • Read IsInsideSafeZone(position) for local vignette toggle
    ///
    /// Setup:
    ///   • Attach to a persistent NetworkObject in the Game scene (same GO as ServerGameManager or sibling).
    ///   • Call SetConfig() then BeginMatch() from ServerGameManager after all players ready.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SafeZoneManager : NetworkBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static SafeZoneManager Instance { get; private set; }

        // ── SyncVars (server → all clients) ────────────────────────────────────
        private readonly SyncVar<float>   _syncRadius       = new SyncVar<float>();
        private readonly SyncVar<float>   _syncTargetRadius = new SyncVar<float>();
        private readonly SyncVar<Vector3> _syncCenter       = new SyncVar<Vector3>();
        private readonly SyncVar<Vector3> _syncNextCenter   = new SyncVar<Vector3>();
        private readonly SyncVar<int>     _syncZoneIndex    = new SyncVar<int>(-1);
        private readonly SyncVar<bool>    _syncIsShrinking  = new SyncVar<bool>();
        private readonly SyncVar<float>   _syncShrinkProgress = new SyncVar<float>();
        // Countdown to next event: positive = wait, negative = not in wait phase
        private readonly SyncVar<float>   _syncCountdownSeconds = new SyncVar<float>(-1f);
        private readonly SyncVar<bool>    _syncMatchActive  = new SyncVar<bool>();

        // ── Public read-only accessors ─────────────────────────────────────────
        public float   CurrentRadius    => _syncRadius.Value;
        public Vector3 CurrentCenter    => _syncCenter.Value;
        public Vector3 NextCenter       => _syncNextCenter.Value;
        public int     ZoneIndex        => _syncZoneIndex.Value;
        public bool    IsShrinking      => _syncIsShrinking.Value;
        public float   ShrinkProgress   => _syncShrinkProgress.Value;
        public float   CountdownSeconds => _syncCountdownSeconds.Value;
        public bool    MatchActive      => _syncMatchActive.Value;
        public bool    IsInFinalZone    => _config != null && _syncZoneIndex.Value >= _config.phases.Count - 1;

        /// <summary>True when the current zone phase has isScoreBonusZone=true.</summary>
        public bool IsCurrentZoneBonus
        {
            get
            {
                int idx = _syncZoneIndex.Value;
                if (_config == null || idx < 0 || idx >= _config.phases.Count) return false;
                return _config.phases[idx].isScoreBonusZone;
            }
        }

        /// <summary>Bonus multiplier for the current zone phase (1 if not a bonus zone).</summary>
        public float CurrentZoneBonusMultiplier
        {
            get
            {
                int idx = _syncZoneIndex.Value;
                if (_config == null || idx < 0 || idx >= _config.phases.Count) return 1f;
                return _config.phases[idx].isScoreBonusZone ? _config.phases[idx].zoneBonusMultiplier : 1f;
            }
        }

        // ── Server events ──────────────────────────────────────────────────────
        /// <summary>Fired on server when a new zone phase becomes active. zoneIndex = 0 for first zone.</summary>
        public event Action<int> OnZonePhaseStarted;

        /// <summary>Fired on server when the last zone's wait/shrink period has completed.
        /// MatchEndManager subscribes to this for score-based win resolution.</summary>
        public event Action OnFinalZoneExpired;

        // ── Server runtime ─────────────────────────────────────────────────────
        private SafeZoneMatchConfig _config;
        /// <summary>Current zone config (server). Null until BeginMatch is called.</summary>
        public SafeZoneMatchConfig Config => _config;
        private Coroutine           _zoneCoroutine;
        private Coroutine           _damageCoroutine;
        private bool                _matchStarted;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity / FishNet lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _syncRadius.OnChange       += OnRadiusChanged;
            _syncZoneIndex.OnChange    += OnZoneIndexChanged;
            _syncIsShrinking.OnChange  += OnShrinkStateChanged;
            _syncCountdownSeconds.OnChange += OnCountdownChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _syncRadius.OnChange       -= OnRadiusChanged;
            _syncZoneIndex.OnChange    -= OnZoneIndexChanged;
            _syncIsShrinking.OnChange  -= OnShrinkStateChanged;
            _syncCountdownSeconds.OnChange -= OnCountdownChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Server API

        /// <summary>
        /// Set config before calling BeginMatch. ServerBootstrap calls this after
        /// fetching GET /api/maps/{mapId}/zone-config.
        /// </summary>
        [Server]
        public void SetConfig(SafeZoneMatchConfig config)
        {
            _config = config ?? SafeZoneMatchConfig.Default();
        }

        /// <summary>
        /// Start the zone sequence. Called by ServerGameManager once all players are ready.
        /// </summary>
        [Server]
        public void BeginMatch(SafeZoneMatchConfig config = null)
        {
            if (_matchStarted) return;
            _matchStarted = true;

            if (config != null) _config = config;
            if (_config == null) _config = SafeZoneMatchConfig.Default();

            // Initialise zone 0
            _syncCenter.Value       = Vector3.zero; // map center — map designer can call SetInitialCenter()
            _syncRadius.Value       = _config.initialRadius;
            _syncTargetRadius.Value = _config.initialRadius;
            _syncMatchActive.Value  = true;

            _zoneCoroutine   = StartCoroutine(ZoneAdvanceLoop());
            _damageCoroutine = StartCoroutine(DamageTick());
        }

        /// <summary>
        /// Override the initial map center (call before BeginMatch from ServerGameManager
        /// if you know the map's geographic center).
        /// </summary>
        [Server]
        public void SetInitialCenter(Vector3 center)
        {
            _syncCenter.Value = center;
        }

        /// <summary>Returns true if <paramref name="worldPos"/> is inside the current safe zone.</summary>
        public bool IsInsideSafeZone(Vector3 worldPos)
        {
            Vector3 delta = worldPos - _syncCenter.Value;
            delta.y = 0f;
            float r = _syncRadius.Value;
            return (delta.x * delta.x + delta.z * delta.z) <= r * r;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Server coroutines

        [Server]
        private IEnumerator ZoneAdvanceLoop()
        {
            if (_config.phases == null || _config.phases.Count == 0)
            {
                Debug.LogError("[SafeZoneManager] No phases configured! Using default.");
                _config = SafeZoneMatchConfig.Default();
            }

            for (int i = 0; i < _config.phases.Count; i++)
            {
                SafeZonePhaseConfig phase = _config.phases[i];
                _syncZoneIndex.Value = i;

                // Pre-shrink wait
                float wait = phase.waitBeforeShrink;
                float elapsed = 0f;
                _syncIsShrinking.Value = false;

                while (elapsed < wait)
                {
                    elapsed += Time.deltaTime;
                    _syncCountdownSeconds.Value = wait - elapsed;
                    yield return null;
                }

                // Final zone: stop here, fire expiry event, match end flow takes over
                bool isFinal = (i == _config.phases.Count - 1);
                if (isFinal)
                {
                    _syncCountdownSeconds.Value = -1f;
                    OnZonePhaseStarted?.Invoke(i);
                    OnFinalZoneExpired?.Invoke();
                    yield break;
                }

                OnZonePhaseStarted?.Invoke(i);

                // Compute next zone center (random within current circle)
                Vector3 nextCenter = ComputeNextCenter(_syncCenter.Value, _syncRadius.Value);
                _syncNextCenter.Value = nextCenter;

                float targetRadius = Mathf.Max(phase.endRadius, _config.finalZoneMinRadius);
                _syncTargetRadius.Value = targetRadius;

                // Shrink
                float shrinkTime     = phase.shrinkDuration;
                float shrinkElapsed  = 0f;
                float startRadius    = _syncRadius.Value;
                Vector3 startCenter  = _syncCenter.Value;

                _syncIsShrinking.Value = true;

                while (shrinkElapsed < shrinkTime)
                {
                    shrinkElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(shrinkElapsed / shrinkTime);

                    _syncShrinkProgress.Value = t;
                    _syncRadius.Value         = Mathf.Lerp(startRadius, targetRadius, t);
                    _syncCenter.Value         = Vector3.Lerp(startCenter, nextCenter, t);
                    _syncCountdownSeconds.Value = shrinkTime - shrinkElapsed;

                    yield return null;
                }

                // Snap to exact target
                _syncRadius.Value   = targetRadius;
                _syncCenter.Value   = nextCenter;
                _syncIsShrinking.Value  = false;
                _syncShrinkProgress.Value = 1f;
                _syncCountdownSeconds.Value = -1f;
            }
        }

        [Server]
        private IEnumerator DamageTick()
        {
            while (_syncMatchActive.Value)
            {
                int phaseIdx = _syncZoneIndex.Value;
                if (phaseIdx < 0 || _config == null || phaseIdx >= _config.phases.Count)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                SafeZonePhaseConfig phase = _config.phases[phaseIdx];
                float tickInterval = Mathf.Max(0.1f, phase.damageTick);
                yield return new WaitForSeconds(tickInterval);

                if (!_syncMatchActive.Value) yield break;

                // Damage all players outside the zone
                var players = PlayerPublicRegistry.Instance?.GetAllPlayers();
                if (players == null) continue;

                foreach (var player in players)
                {
                    if (player == null || !player.IsAlive) continue;
                    if (IsInsideSafeZone(player.transform.position)) continue;

                    float damage = phase.damagePerSecond * tickInterval;
                    var damageInfo = new DamageInfo
                    {
                        Damage                 = damage,
                        ShooterNetworkObjectId = -1,  // -1 = world/zone damage
                        WeaponId               = "zone",
                    };
                    player.GetComponent<PlayerHealthSystem>()?.ApplyDamageServer(damageInfo);
                }
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Zone center randomisation

        [Server]
        private Vector3 ComputeNextCenter(Vector3 currentCenter, float currentRadius)
        {
            if (_config.centerMode == ZoneCenterMode.Fixed)
                return currentCenter;

            float minShift = currentRadius * _config.minCenterShiftPercent;
            float maxShift = currentRadius * _config.maxCenterShiftPercent;

            // Clamp shift range to valid values
            if (maxShift <= 0f) return currentCenter;
            if (minShift > maxShift) minShift = 0f;

            float shiftDist = UnityEngine.Random.Range(minShift, maxShift);
            float angle     = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * shiftDist,
                0f,
                Mathf.Sin(angle) * shiftDist
            );

            Vector3 candidate = currentCenter + offset;

            if (_config.centerMode == ZoneCenterMode.CenterBiased)
            {
                // Pull candidate 30% toward map origin
                candidate = Vector3.Lerp(candidate, Vector3.zero, 0.3f);
            }

            return candidate;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Client SyncVar callbacks

        private void OnRadiusChanged(float prev, float next, bool asServer)
            => SafeZoneHUDProxy.NotifyRadiusChanged(next, _syncCenter.Value);

        private void OnZoneIndexChanged(int prev, int next, bool asServer)
            => SafeZoneHUDProxy.NotifyZoneIndexChanged(next);

        private void OnShrinkStateChanged(bool prev, bool next, bool asServer)
            => SafeZoneHUDProxy.NotifyShrinkStateChanged(next);

        private void OnCountdownChanged(float prev, float next, bool asServer)
            => SafeZoneHUDProxy.NotifyCountdownChanged(next);

        #endregion
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Static proxy — decouples SafeZoneManager from SafeZoneHUD.
    /// SafeZoneHUD subscribes to these events instead of holding a direct ref.
    /// </summary>
    public static class SafeZoneHUDProxy
    {
        public static event Action<float, Vector3> OnRadiusChanged;
        public static event Action<int>            OnZoneIndexChanged;
        public static event Action<bool>           OnShrinkStateChanged;
        public static event Action<float>          OnCountdownChanged;

        internal static void NotifyRadiusChanged(float r, Vector3 c)    => OnRadiusChanged?.Invoke(r, c);
        internal static void NotifyZoneIndexChanged(int idx)             => OnZoneIndexChanged?.Invoke(idx);
        internal static void NotifyShrinkStateChanged(bool shrinking)    => OnShrinkStateChanged?.Invoke(shrinking);
        internal static void NotifyCountdownChanged(float t)             => OnCountdownChanged?.Invoke(t);
    }
}
