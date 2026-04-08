using System.Collections.Generic;
using NightHunt.Core;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Config
{
    /// <summary>
    /// Runtime config modifications (phase-based, zone-based)
    /// </summary>
    public class RuntimeConfig : Singleton<RuntimeConfig>
    {

        private readonly Dictionary<string, float> phaseModifiers = new Dictionary<string, float>();
        private readonly Dictionary<string, float> zoneModifiers = new Dictionary<string, float>();



        /// <summary>
        /// Set phase modifier for a stat
        /// </summary>
        public void SetPhaseModifier(string statName, float modifier)
        {
            phaseModifiers[statName] = modifier;
        }

        /// <summary>
        /// Set zone modifier for a stat
        /// </summary>
        public void SetZoneModifier(string statName, float modifier)
        {
            zoneModifiers[statName] = modifier;
        }

        /// <summary>
        /// Get phase modifier
        /// </summary>
        public float GetPhaseModifier(string statName)
        {
            return phaseModifiers.TryGetValue(statName, out float modifier) ? modifier : 1f;
        }

        /// <summary>
        /// Get zone modifier
        /// </summary>
        public float GetZoneModifier(string statName)
        {
            return zoneModifiers.TryGetValue(statName, out float modifier) ? modifier : 1f;
        }

        /// <summary>
        /// Clear all modifiers
        /// </summary>
        public void ClearModifiers()
        {
            phaseModifiers.Clear();
            zoneModifiers.Clear();
        }
    }
}

