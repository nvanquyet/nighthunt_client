using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Data
{
    /// <summary>
    /// Defines how the safe zone center is randomised between phases.
    /// </summary>
    public enum ZoneCenterMode
    {
        /// <summary>Fully random within the previous zone boundary (PUBG-style).</summary>
        PureRandom,
        /// <summary>Biased toward the map's geographic centre — arena-style, keeps play in the middle.</summary>
        CenterBiased,
        /// <summary>Fixed center — no movement. Suitable for tiny 1v1 arenas.</summary>
        Fixed,
    }

    /// <summary>
    /// Top-level match configuration for the safe zone system.
    /// Fetched from the server at match start via GET /api/maps/{mapId}/zone-config.
    /// Populated into <see cref="NightHunt.Gameplay.Zone.SafeZoneManager.SetConfig"/> by ServerBootstrap.
    /// </summary>
    [Serializable]
    public class SafeZoneMatchConfig
    {
        // ── Zone phases ────────────────────────────────────────────────────────
        [Tooltip("Ordered list of zone phases, starting with phase 0 (largest zone).")]
        public List<SafeZonePhaseConfig> phases = new();

        // ── Zone shape ─────────────────────────────────────────────────────────
        [Tooltip("World-space radius of the very first zone (phase 0 start). Covers most of the map.")]
        public float initialRadius = 400f;

        [Tooltip("The shrink stops here — the final circle never collapses to a point.")]
        public float finalZoneMinRadius = 25f;

        // ── Center randomisation ───────────────────────────────────────────────
        [Tooltip("Strategy used to pick the next zone center.")]
        public ZoneCenterMode centerMode = ZoneCenterMode.PureRandom;

        [Tooltip("Maximum distance the next center can be from the current center, expressed as a fraction (0-1) of the current radius. E.g. 0.6 = center can shift up to 60% of current radius.")]
        [Range(0f, 1f)]
        public float maxCenterShiftPercent = 0.6f;

        [Tooltip("Minimum distance the center must shift (avoids trivially identical zones).")]
        [Range(0f, 1f)]
        public float minCenterShiftPercent = 0.1f;

        // ── Respawn ────────────────────────────────────────────────────────────
        [Tooltip("If false, any respawn beacons are destroyed when the final zone begins.")]
        public bool beaconAllowedInFinalZone = false;

        // ── Scoring ────────────────────────────────────────────────────────────
        [Tooltip("Flat score awarded per second to each living player.")]
        public float baseSurvivalPtsPerSecond = 1f;

        [Tooltip("Score awarded per second to all members of the team holding the capture zone.")]
        public float captureZoneScorePerSecond = 20f;

        [Tooltip("Base score awarded for killing another player.")]
        public float killScore = 100f;

        [Tooltip("Base score awarded to every killer-team member when their team kills a boss.")]
        public float bossKillScore = 300f;

        [Tooltip("Fraction of a victim's current score stolen by the killer (final zone only). E.g. 0.15 = 15%.")]
        [Range(0f, 1f)]
        public float killScoreStealPercent = 0.15f;

        // ── Presets / factory ─────────────────────────────────────────────────

        /// <summary>
        /// The 3 inner "base" phases shared by every map.
        /// All maps end with these same zones (R: 100 → 50 → 25 → 10).
        /// Damage doubles with each step because the area quartered — players are
        /// forced into ever-tighter circles near end-game.
        ///
        ///  Phase  start  end   wait  shrink  dmg/s   notes
        ///  ─────  ─────  ───   ────  ──────  ─────   ─────
        ///  BASE0   100   50    60s    60s      8      midgame
        ///  BASE1    50   25    45s    45s     15      bonus zone (score ×2)
        ///  BASE2    25   10    30s    30s     25      endgame / final zone
        /// </summary>
        public static List<SafeZonePhaseConfig> CoreInnerPhases(int startIndex = 0)
        {
            return new List<SafeZonePhaseConfig>
            {
                new SafeZonePhaseConfig { zoneIndex = startIndex,     startRadius = 100f, endRadius = 50f, waitBeforeShrink = 60f, shrinkDuration = 60f, damagePerSecond = 8f  },
                new SafeZonePhaseConfig { zoneIndex = startIndex + 1, startRadius = 50f,  endRadius = 25f, waitBeforeShrink = 45f, shrinkDuration = 45f, damagePerSecond = 15f, isScoreBonusZone = true, zoneBonusMultiplier = 2f },
                new SafeZonePhaseConfig { zoneIndex = startIndex + 2, startRadius = 25f,  endRadius = 10f, waitBeforeShrink = 30f, shrinkDuration = 30f, damagePerSecond = 25f, minRadiusOverride = 10f },
            };
        }

        /// <summary>
        /// Standard config for a medium-to-large 4v4+ map (400m → 10m, 5 phases).
        /// Two outer phases are prepended before the 3 shared CoreInnerPhases.
        ///
        ///  Phase  start  end   wait   shrink  dmg/s   notes
        ///  ─────  ─────  ───   ────   ──────  ─────
        ///    0    400    200   120s   90s      3       early game
        ///    1    200    100    90s   75s      5
        ///   [2-4] inner phases (CoreInnerPhases)
        /// </summary>
        public static SafeZoneMatchConfig Standard()
        {
            var phases = new List<SafeZonePhaseConfig>
            {
                new SafeZonePhaseConfig { zoneIndex = 0, startRadius = 400f, endRadius = 200f, waitBeforeShrink = 120f, shrinkDuration = 90f, damagePerSecond = 3f },
                new SafeZonePhaseConfig { zoneIndex = 1, startRadius = 200f, endRadius = 100f, waitBeforeShrink =  90f, shrinkDuration = 75f, damagePerSecond = 5f },
            };
            phases.AddRange(CoreInnerPhases(startIndex: 2));
            return new SafeZoneMatchConfig
            {
                initialRadius             = 400f,
                finalZoneMinRadius        = 10f,
                centerMode                = ZoneCenterMode.PureRandom,
                maxCenterShiftPercent     = 0.6f,
                minCenterShiftPercent     = 0.1f,
                beaconAllowedInFinalZone  = false,
                baseSurvivalPtsPerSecond  = 1f,
                captureZoneScorePerSecond = 20f,
                killScore                 = 100f,
                bossKillScore             = 300f,
                killScoreStealPercent     = 0.15f,
                phases                    = phases,
            };
        }

        /// <summary>
        /// Small 1v1 config (100m → 10m, 3 phases = just CoreInnerPhases).
        /// Uses Fixed center so the zone never drifts on tiny arenas.
        /// Shorter timers than standard — 1v1 matches resolve faster.
        ///
        ///  Phase  start  end   wait   shrink  dmg/s   notes
        ///  ─────  ─────  ───   ────   ──────  ─────
        ///    0    100    50    45s    45s      8
        ///    1     50    25    30s    30s     15       bonus zone
        ///    2     25    10    20s    20s     25       endgame
        /// </summary>
        public static SafeZoneMatchConfig Small1v1()
        {
            return new SafeZoneMatchConfig
            {
                initialRadius             = 100f,
                finalZoneMinRadius        = 10f,
                centerMode                = ZoneCenterMode.Fixed,
                maxCenterShiftPercent     = 0f,
                minCenterShiftPercent     = 0f,
                beaconAllowedInFinalZone  = false,
                baseSurvivalPtsPerSecond  = 1f,
                captureZoneScorePerSecond = 20f,
                killScore                 = 100f,
                bossKillScore             = 300f,
                killScoreStealPercent     = 0.15f,
                phases = new List<SafeZonePhaseConfig>
                {
                    new SafeZonePhaseConfig { zoneIndex = 0, startRadius = 100f, endRadius = 50f, waitBeforeShrink = 45f, shrinkDuration = 45f, damagePerSecond = 8f  },
                    new SafeZonePhaseConfig { zoneIndex = 1, startRadius = 50f,  endRadius = 25f, waitBeforeShrink = 30f, shrinkDuration = 30f, damagePerSecond = 15f, isScoreBonusZone = true, zoneBonusMultiplier = 2f },
                    new SafeZonePhaseConfig { zoneIndex = 2, startRadius = 25f,  endRadius = 10f, waitBeforeShrink = 20f, shrinkDuration = 20f, damagePerSecond = 25f, minRadiusOverride = 10f },
                },
            };
        }

        /// <summary>
        /// Returns a preset SafeZoneMatchConfig based on mapId.
        /// Used as a client-side fallback when the backend is unreachable.
        /// Mirrors the configs seeded in V30 migration.
        /// </summary>
        public static SafeZoneMatchConfig ForMap(string mapId)
        {
            return mapId?.ToLowerInvariant() switch
            {
                "map_02" => Small1v1(),
                _        => Standard(),
            };
        }

        /// <summary>
        /// Backward-compatible alias — returns Standard() so existing callers get the
        /// 5-phase standard config.  New code should call Standard() or ForMap() directly.
        /// </summary>
        public static SafeZoneMatchConfig Default() => Standard();
    }
}
