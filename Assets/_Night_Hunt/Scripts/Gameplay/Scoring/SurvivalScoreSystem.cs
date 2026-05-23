using System.Collections;
using UnityEngine;
using FishNet.Object;
using NightHunt.Data;
using NightHunt.Gameplay.Scoring;
using NightHunt.Gameplay.Zone;
using NightHunt.Networking.Player;

namespace NightHunt.Gameplay.Scoring
{
    /// <summary>
    /// Server-only time-driven tick: awards survival + zone-bonus points every _tickInterval seconds
    /// by calling <see cref="ScoringSystem.AwardScore"/>.
    /// Activated by ServerGameManager.BeginMatch() via StartTicking(config).
    ///
    /// Does NOT handle event-driven kills or objective captures — that is <see cref="ScoringSystem"/>'s
    /// responsibility. Attach to the same GameObject as ScoringSystem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivalScoreSystem : NetworkBehaviour
    {
        public static SurvivalScoreSystem Instance { get; private set; }

        [SerializeField] private ScoringSystem _scoring;
        [SerializeField] [Min(0.1f)] private float _tickInterval = 1f;

        private Coroutine _tickCoroutine;

        public override void OnStartServer()
        {
            base.OnStartServer();
            Instance = this;
            if (_scoring == null)
                _scoring = GetComponent<ScoringSystem>() ?? FindFirstObjectByType<ScoringSystem>();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Start ticking. Called by ServerGameManager / ServerBootstrap once SafeZoneManager is active.
        /// </summary>
        [Server]
        public void StartTicking(SafeZoneMatchConfig config)
        {
            _scoring?.SetZoneConfig(config);
            if (_tickCoroutine != null) StopCoroutine(_tickCoroutine);
            _tickCoroutine = StartCoroutine(TickLoop());
        }

        [Server]
        public void StopTicking()
        {
            if (_tickCoroutine != null) StopCoroutine(_tickCoroutine);
            _tickCoroutine = null;
        }

        [Server]
        private IEnumerator TickLoop()
        {
            var wait = new WaitForSeconds(_tickInterval);
            while (true)
            {
                yield return wait;
                Tick();
            }
        }

        [Server]
        private void Tick()
        {
            if (_scoring == null) return;

            SafeZoneManager zone = SafeZoneManager.Instance;
            int phaseIdx         = zone?.ZoneIndex ?? -1;
            SafeZoneMatchConfig cfg = null;
            bool isBonusZone        = false;
            float bonusMul          = 1f;

            if (zone != null && zone.MatchActive)
            {
                // Try to read current phase config for bonus check
                // We don't hold a direct ref to config — read via SafeZoneManager service locator
                // The _scoring._zoneConfig was already injected via SetZoneConfig in StartTicking.
                // We ask SafeZoneManager if the current phase is a bonus zone.
                isBonusZone = IsCurrentZoneBonusZone(zone, out bonusMul);
            }

            var players = PlayerPublicRegistry.Instance?.GetAllPlayers();
            if (players == null) return;

            foreach (var player in players)
            {
                if (player == null || !player.IsAlive) continue;

                uint pid = (uint)player.ObjectId;
                _scoring.AwardSurvivalTick(pid, _tickInterval);

                if (isBonusZone && zone != null && zone.IsInsideSafeZone(player.transform.position))
                    _scoring.AwardZoneBonus(pid, _tickInterval, bonusMul);
            }

            // Flush sync once per tick instead of per-player
            _scoring.ForceSyncScores();
        }

        private static bool IsCurrentZoneBonusZone(SafeZoneManager zone, out float multiplier)
        {
            multiplier = 1f;
            // ScoringSystem holds the config; we peek through its public API
            // Alternatively, read from SafeZoneManager's exposed current phase data
            // For now we broadcast via SafeZoneManager.CurrentPhaseConfig if exposed.
            // Simple approach: trust SafeZoneManager.CurrentPhaseIsBonusZone property (added below).
            multiplier = zone.CurrentZoneBonusMultiplier;
            return zone.IsCurrentZoneBonus;
        }
    }
}
