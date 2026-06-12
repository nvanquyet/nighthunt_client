using System;
using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Networking.Player;
using NightHunt.Core;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Zone
{
    [DisallowMultipleComponent]
    public sealed class SafeZoneManager : NetworkBehaviour
    {
        public static SafeZoneManager Instance { get; private set; }

        private readonly SyncVar<float>   _syncRadius       = new SyncVar<float>();
        private readonly SyncVar<float>   _syncTargetRadius = new SyncVar<float>();
        private readonly SyncVar<Vector3> _syncCenter       = new SyncVar<Vector3>();
        private readonly SyncVar<Vector3> _syncNextCenter   = new SyncVar<Vector3>();
        private readonly SyncVar<int>     _syncZoneIndex    = new SyncVar<int>(-1);
        private readonly SyncVar<bool>    _syncIsShrinking  = new SyncVar<bool>();
        private readonly SyncVar<float>   _syncShrinkProgress = new SyncVar<float>();
        private readonly SyncVar<float>   _syncCountdownSeconds = new SyncVar<float>(-1f);
        private readonly SyncVar<bool>    _syncMatchActive  = new SyncVar<bool>();

        public float   CurrentRadius    => _syncRadius.Value;
        public Vector3 CurrentCenter    => _syncCenter.Value;
        public Vector3 NextCenter       => _syncNextCenter.Value;
        public int     ZoneIndex        => _syncZoneIndex.Value;
        public bool    IsShrinking      => _syncIsShrinking.Value;
        public float   ShrinkProgress   => _syncShrinkProgress.Value;
        public float   CountdownSeconds => _syncCountdownSeconds.Value;
        public bool    MatchActive      => _syncMatchActive.Value;
        public bool    IsInFinalZone    => _config != null && _syncZoneIndex.Value >= _config.phases.Count - 1;

        public bool IsCurrentZoneBonus
        {
            get
            {
                int idx = _syncZoneIndex.Value;
                if (_config == null || idx < 0 || idx >= _config.phases.Count) return false;
                return _config.phases[idx].isScoreBonusZone;
            }
        }

        public float CurrentZoneBonusMultiplier
        {
            get
            {
                int idx = _syncZoneIndex.Value;
                if (_config == null || idx < 0 || idx >= _config.phases.Count) return 1f;
                return _config.phases[idx].isScoreBonusZone ? _config.phases[idx].zoneBonusMultiplier : 1f;
            }
        }

        public event Action<int> OnZonePhaseStarted;
        public event Action OnFinalZoneExpired;

        private SafeZoneMatchConfig _config;
        public SafeZoneMatchConfig Config => _config;
        private Coroutine           _zoneCoroutine;
        private Coroutine           _damageCoroutine;
        private Coroutine           _forceFinalZoneCoroutine;
        private bool                _matchStarted;

        #region Unity / FishNet lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _syncRadius.OnChange           += OnRadiusChanged;
            _syncZoneIndex.OnChange        += OnZoneIndexChanged;
            _syncIsShrinking.OnChange      += OnShrinkStateChanged;
            _syncCountdownSeconds.OnChange += OnCountdownChanged;
            _syncNextCenter.OnChange       += OnNextCenterChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            StartCoroutine(ReplayCurrentHudStateNextFrames());
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _syncRadius.OnChange           -= OnRadiusChanged;
            _syncZoneIndex.OnChange        -= OnZoneIndexChanged;
            _syncIsShrinking.OnChange      -= OnShrinkStateChanged;
            _syncCountdownSeconds.OnChange -= OnCountdownChanged;
            _syncNextCenter.OnChange       -= OnNextCenterChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private IEnumerator ReplayCurrentHudStateNextFrames()
        {
            yield return null;
            ReplayCurrentHudState();
            yield return null;
            ReplayCurrentHudState();
        }

        #endregion

        #region Server API

        [Server]
        public void SetConfig(SafeZoneMatchConfig config)
        {
            _config = config ?? SafeZoneMatchConfig.Standard();
        }

        [Server]
        public void BeginMatch(SafeZoneMatchConfig config = null)
        {
            if (_matchStarted) return;
            _matchStarted = true;

            if (config != null) _config = config;
            if (_config == null) _config = SafeZoneMatchConfig.Standard();

            _syncCenter.Value       = Vector3.zero;
            _syncRadius.Value       = _config.initialRadius;
            _syncTargetRadius.Value = _config.initialRadius;
            _syncMatchActive.Value  = true;

            _zoneCoroutine   = StartCoroutine(ZoneAdvanceLoop());
            _damageCoroutine = StartCoroutine(DamageTick());
        }

        [Server]
        public void EndMatch()
        {
            if (!_syncMatchActive.Value && !_matchStarted)
                return;

            _syncMatchActive.Value = false;
            _matchStarted = false;
            _syncIsShrinking.Value = false;
            _syncCountdownSeconds.Value = -1f;

            if (_zoneCoroutine != null)
            {
                StopCoroutine(_zoneCoroutine);
                _zoneCoroutine = null;
            }

            if (_forceFinalZoneCoroutine != null)
            {
                StopCoroutine(_forceFinalZoneCoroutine);
                _forceFinalZoneCoroutine = null;
            }

            if (_damageCoroutine != null)
            {
                StopCoroutine(_damageCoroutine);
                _damageCoroutine = null;
            }
        }

        [Server]
        public void SetInitialCenter(Vector3 center)
        {
            _syncCenter.Value = center;
        }

        [Server]
        public bool ForceFinalZoneAfterDelay(float delaySeconds)
        {
            if (!_syncMatchActive.Value || !_matchStarted)
                return false;

            EnsureValidConfig();
            if (IsInFinalZone)
                return false;

            if (_zoneCoroutine != null)
            {
                StopCoroutine(_zoneCoroutine);
                _zoneCoroutine = null;
            }

            if (_forceFinalZoneCoroutine != null)
                StopCoroutine(_forceFinalZoneCoroutine);

            _forceFinalZoneCoroutine = StartCoroutine(ForceFinalZoneAfterDelayRoutine(Mathf.Max(0f, delaySeconds)));
            return true;
        }

        public void ReplayCurrentHudState()
        {
            if (_syncZoneIndex.Value < 0 && _syncRadius.Value <= 0f && _syncCountdownSeconds.Value < 0f)
                return;

            SafeZoneHUDProxy.NotifyRadiusChanged(_syncRadius.Value, _syncCenter.Value);
            SafeZoneHUDProxy.NotifyZoneIndexChanged(_syncZoneIndex.Value);
            SafeZoneHUDProxy.NotifyShrinkStateChanged(_syncIsShrinking.Value);
            SafeZoneHUDProxy.NotifyCountdownChanged(_syncCountdownSeconds.Value);
            SafeZoneHUDProxy.NotifyNextZoneChanged(_syncTargetRadius.Value, _syncNextCenter.Value);
        }

        public bool IsInsideSafeZone(Vector3 worldPos)
        {
            Vector3 delta = worldPos - _syncCenter.Value;
            delta.y = 0f;
            float r = _syncRadius.Value;
            return (delta.x * delta.x + delta.z * delta.z) <= r * r;
        }

        #endregion

        #region Server coroutines

        [Server]
        private IEnumerator ZoneAdvanceLoop()
        {
            EnsureValidConfig();

            for (int i = 0; i < _config.phases.Count; i++)
            {
                SafeZonePhaseConfig phase = _config.phases[i];
                bool isFinal = (i == _config.phases.Count - 1);
                _syncZoneIndex.Value = i;

                Vector3 nextCenter   = Vector3.zero;
                float   targetRadius = 0f;
                if (!isFinal)
                {
                    nextCenter   = ComputeNextCenter(_syncCenter.Value, _syncRadius.Value);
                    targetRadius = Mathf.Max(phase.endRadius, _config.finalZoneMinRadius);
                    _syncTargetRadius.Value = targetRadius;
                    _syncNextCenter.Value   = nextCenter;
                }

                float wait = phase.waitBeforeShrink;
                float elapsed = 0f;
                _syncIsShrinking.Value = false;

                while (elapsed < wait)
                {
                    elapsed += Time.deltaTime;
                    _syncCountdownSeconds.Value = wait - elapsed;
                    yield return null;
                }

                if (isFinal)
                {
                    _syncCountdownSeconds.Value = -1f;
                    OnZonePhaseStarted?.Invoke(i);
                    OnFinalZoneExpired?.Invoke();
                    yield break;
                }

                OnZonePhaseStarted?.Invoke(i);

                float shrinkTime     = phase.shrinkDuration;
                float shrinkElapsed  = 0f;
                float startRadius    = _syncRadius.Value;
                Vector3 startCenter  = _syncCenter.Value;

                _syncIsShrinking.Value = true;

                while (shrinkElapsed < shrinkTime)
                {
                    shrinkElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(shrinkElapsed / shrinkTime);

                    _syncShrinkProgress.Value   = t;
                    _syncRadius.Value           = Mathf.Lerp(startRadius, targetRadius, t);
                    _syncCenter.Value           = Vector3.Lerp(startCenter, nextCenter, t);
                    _syncCountdownSeconds.Value = shrinkTime - shrinkElapsed;

                    yield return null;
                }

                _syncRadius.Value           = targetRadius;
                _syncCenter.Value           = nextCenter;
                _syncIsShrinking.Value      = false;
                _syncShrinkProgress.Value   = 1f;
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
                        ShooterNetworkObjectId = -1,
                        WeaponId               = "zone",
                    };
                    PlayerHealthSystem health = ComponentResolver.Find<PlayerHealthSystem>(player)
                        .OnSelf()
                        .InChildren()
                        .OrLogWarning("[SafeZoneManager] PlayerHealthSystem not found for zone damage")
                        .Resolve();

                    health?.ApplyDamageServer(damageInfo);
                }
            }
        }

        [Server]
        private IEnumerator ForceFinalZoneAfterDelayRoutine(float delaySeconds)
        {
            _syncIsShrinking.Value = false;
            _syncShrinkProgress.Value = 0f;

            float remaining = delaySeconds;
            while (remaining > 0f && _syncMatchActive.Value && _matchStarted)
            {
                _syncCountdownSeconds.Value = remaining;
                remaining -= Time.deltaTime;
                yield return null;
            }

            _forceFinalZoneCoroutine = null;

            if (!_syncMatchActive.Value || !_matchStarted)
                yield break;

            EnterFinalZoneNow();
        }

        [Server]
        private void EnterFinalZoneNow()
        {
            EnsureValidConfig();
            if (_config.phases == null || _config.phases.Count == 0)
                return;

            int finalIndex = _config.phases.Count - 1;
            SafeZonePhaseConfig finalPhase = _config.phases[finalIndex];
            float finalRadius = ResolvePhaseEndRadius(finalPhase);
            Vector3 finalCenter = ResolveForcedFinalCenter();

            _syncZoneIndex.Value = finalIndex;
            _syncCenter.Value = finalCenter;
            _syncNextCenter.Value = finalCenter;
            _syncRadius.Value = finalRadius;
            _syncTargetRadius.Value = finalRadius;
            _syncIsShrinking.Value = false;
            _syncShrinkProgress.Value = 1f;
            _syncCountdownSeconds.Value = -1f;
            _syncMatchActive.Value = true;
            _matchStarted = true;

            OnZonePhaseStarted?.Invoke(finalIndex);
            ReplayCurrentHudState();
            UnityEngine.Debug.Log($"[SafeZoneManager] Forced final zone entered. index={finalIndex} center={finalCenter:F2} radius={finalRadius:F1}");
        }

        #endregion

        #region Zone center randomisation

        [Server]
        private Vector3 ComputeNextCenter(Vector3 currentCenter, float currentRadius)
        {
            if (_config.centerMode == ZoneCenterMode.Fixed)
                return currentCenter;

            float minShift = currentRadius * _config.minCenterShiftPercent;
            float maxShift = currentRadius * _config.maxCenterShiftPercent;

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
                candidate = Vector3.Lerp(candidate, Vector3.zero, 0.3f);

            return candidate;
        }

        private void EnsureValidConfig()
        {
            if (_config != null && _config.phases != null && _config.phases.Count > 0)
                return;

            UnityEngine.Debug.LogError("[SafeZoneManager] No phases configured! Using Standard.");
            _config = SafeZoneMatchConfig.Standard();
        }

        private float ResolvePhaseEndRadius(SafeZonePhaseConfig phase)
        {
            if (phase == null)
                return Mathf.Max(1f, _config != null ? _config.finalZoneMinRadius : 10f);

            float minRadius = _config != null ? _config.finalZoneMinRadius : 10f;
            if (phase.minRadiusOverride > 0f)
                minRadius = phase.minRadiusOverride;

            return Mathf.Max(phase.endRadius, minRadius);
        }

        private Vector3 ResolveForcedFinalCenter()
        {
            if (_config != null && _config.centerMode == ZoneCenterMode.Fixed)
                return _syncCenter.Value;

            Vector3 next = _syncNextCenter.Value;
            if (next.sqrMagnitude > 0.001f)
                return next;

            return ComputeNextCenter(_syncCenter.Value, Mathf.Max(_syncRadius.Value, _config != null ? _config.initialRadius : 1f));
        }

        #endregion

        #region Client SyncVar callbacks

        private void OnRadiusChanged(float prev, float next, bool asServer)
            => SafeZoneHUDProxy.NotifyRadiusChanged(next, _syncCenter.Value);

        private void OnZoneIndexChanged(int prev, int next, bool asServer)
        {
            SafeZoneHUDProxy.NotifyZoneIndexChanged(next);
            SafeZoneHUDProxy.NotifyNextZoneChanged(0f, Vector3.zero);
        }

        private void OnShrinkStateChanged(bool prev, bool next, bool asServer)
            => SafeZoneHUDProxy.NotifyShrinkStateChanged(next);

        private void OnCountdownChanged(float prev, float next, bool asServer)
            => SafeZoneHUDProxy.NotifyCountdownChanged(next);

        private void OnNextCenterChanged(Vector3 prev, Vector3 next, bool asServer)
            => SafeZoneHUDProxy.NotifyNextZoneChanged(_syncTargetRadius.Value, next);

        #endregion
    }

    public static class SafeZoneHUDProxy
    {
        public static event Action<float, Vector3> OnRadiusChanged;
        public static event Action<int>            OnZoneIndexChanged;
        public static event Action<bool>           OnShrinkStateChanged;
        public static event Action<float>          OnCountdownChanged;
        public static event Action<float, Vector3> OnNextZoneChanged;

        internal static void NotifyRadiusChanged(float r, Vector3 c)    => OnRadiusChanged?.Invoke(r, c);
        internal static void NotifyZoneIndexChanged(int idx)             => OnZoneIndexChanged?.Invoke(idx);
        internal static void NotifyShrinkStateChanged(bool shrinking)    => OnShrinkStateChanged?.Invoke(shrinking);
        internal static void NotifyCountdownChanged(float t)             => OnCountdownChanged?.Invoke(t);
        internal static void NotifyNextZoneChanged(float r, Vector3 c)  => OnNextZoneChanged?.Invoke(r, c);
    }
}
