using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Data;

namespace NightHunt.GameplaySystems.Core.Configs
{
    /// <summary>
    /// Configures weight-based player-stat penalty tiers.
    ///
    /// Each tier activates when (currentWeight / maxWeight) >= ThresholdRatio.
    /// The HIGHEST matching tier wins — lower tiers are superseded.
    ///
    /// Attach to <see cref="InventoryConfig"/> via the WeightPenalties field.
    /// The <see cref="WeightPenaltyContributor"/> component reads this config and
    /// registers itself with StatApplyOrchestrator to apply penalties at runtime.
    ///
    /// Example tiers:
    ///   0.80 → "Heavy"      MoveSpeed -10%
    ///   1.00 → "Overweight" MoveSpeed -30%, StaminaRegen -20%   (requires AllowExceedMaxWeight=true)
    ///   1.50 → "Overloaded" MoveSpeed -60%, StaminaRegen -50%, JumpHeight override 0
    /// </summary>
    [CreateAssetMenu(
        fileName = "WeightPenaltyConfig",
        menuName  = "NightHunt/Config/Weight Penalty Config")]
    public class WeightPenaltyConfig : ScriptableObject
    {
        [System.Serializable]
        public class PenaltyTier
        {
            [Tooltip("Penalty activates when currentWeight / maxWeight >= this value.")]
            [Range(0f, 3f)]
            public float ThresholdRatio = 1.0f;

            [Tooltip("Display label shown in the UI weight bar at this tier (e.g. 'Overweight').")]
            public string Label = "Overweight";

            [Tooltip("Bar / indicator color in the weight UI when this tier is active.")]
            public Color BarColor = new Color(0.9f, 0.2f, 0.2f, 1f);

            [Tooltip("Stat modifiers applied to the player while this tier is the highest active tier.")]
            public PlayerStatModifier[] Modifiers;
        }

        [Tooltip("Penalty tiers sorted ASCENDING by ThresholdRatio. " +
                 "The system picks the highest tier whose threshold is met. " +
                 "Leave empty to disable weight penalties.")]
        public PenaltyTier[] Tiers;

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the highest matching tier for the given weight ratio,
        /// or <c>null</c> if no tier's threshold is met.
        /// </summary>
        public PenaltyTier GetActiveTier(float ratio)
        {
            if (Tiers == null || Tiers.Length == 0) return null;

            PenaltyTier active = null;
            foreach (var tier in Tiers)
            {
                if (ratio >= tier.ThresholdRatio)
                    active = tier; // keep overwriting so the last (highest) match wins
            }
            return active;
        }

        /// <summary>
        /// Returns the first tier whose threshold is ABOVE the current ratio —
        /// i.e. the next penalty level the player is approaching.
        /// Useful for displaying "X kg until next penalty" in the UI.
        /// </summary>
        public PenaltyTier GetNextTier(float ratio)
        {
            if (Tiers == null) return null;
            foreach (var tier in Tiers)
            {
                if (ratio < tier.ThresholdRatio)
                    return tier;
            }
            return null;
        }

        /// <summary>Normal weight bar color (below all thresholds).</summary>
        public Color NormalBarColor = new Color(0.2f, 0.8f, 0.3f, 1f);

        /// <summary>
        /// Returns the bar color appropriate for the current ratio:
        /// uses the active tier's color, or NormalBarColor when no tier is active.
        /// </summary>
        public Color GetBarColor(float ratio)
        {
            var active = GetActiveTier(ratio);
            return active != null ? active.BarColor : NormalBarColor;
        }
    }
}
