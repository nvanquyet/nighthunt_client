using System;
using UnityEngine;

namespace NightHunt.Data
{
    /// <summary>
    /// Per-phase configuration for the safe zone system.
    /// Populated at runtime from server JSON via GameConfigData.SafeZoneMatchConfig.
    /// </summary>
    [Serializable]
    public class SafeZonePhaseConfig
    {
        [Tooltip("Zero-based index of this zone phase (0 = first zone, largest).")]
        public int zoneIndex;

        [Tooltip("Starting radius in world-units. Ignored for zone 0 — uses SafeZoneMatchConfig.initialRadius.")]
        public float startRadius;

        [Tooltip("End radius after shrink. For the final zone this is clamped to finalZoneMinRadius.")]
        public float endRadius;

        [Tooltip("Seconds to wait at full size before shrink begins. Players can reposition.")]
        public float waitBeforeShrink = 60f;

        [Tooltip("Seconds to complete the shrink from startRadius to endRadius.")]
        public float shrinkDuration = 90f;

        [Tooltip("Damage per second dealt to players outside the safe zone.")]
        public float damagePerSecond = 5f;

        [Tooltip("Interval (seconds) between damage ticks. Default: 1s.")]
        public float damageTick = 1f;

        [Tooltip("If true, players standing inside this zone receive a score bonus (isScoreBonusZone multiplier).")]
        public bool isScoreBonusZone;

        [Tooltip("Per-zone score bonus multiplier applied ON TOP of base survival rate while inside zone. Only used when isScoreBonusZone=true.")]
        public float zoneBonusMultiplier = 1.5f;

        [Tooltip("> 0 overrides SafeZoneMatchConfig.finalZoneMinRadius for this phase only.")]
        public float minRadiusOverride;
    }
}
