using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.Stat
{
    /// <summary>
    /// Reads the current carry-weight ratio and applies the matching
    /// <see cref="WeightPenaltyConfig.PenaltyTier"/> as player-stat modifiers.
    ///
    /// PLACEMENT: Add to the Player prefab alongside StatApplyOrchestrator and InventorySystem.
    ///
    /// FLOW:
    ///   InventorySystem.OnWeightChanged
    ///   → WeightPenaltyContributor caches the current ratio
    ///   → Notifies StatApplyOrchestrator.ScheduleRecalc()
    ///   → SAO.LateUpdate → Recalculate() → calls GetPlayerStatContributions()
    ///   → Returns the active tier's Modifiers[] (or empty when below all thresholds)
    ///
    /// NETWORK RULE:
    ///   Only the server and the owning client run stat application (SAO already guards this).
    ///   This contributor is passive — it just supplies modifiers when asked.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeightPenaltyContributor : NetworkBehaviour, IStatContributor
    {
        // ── Source ID used in PlayerStatSystem so penalties can be fully cleared ─
        public const string SOURCE_ID = "weight_penalty";

        // ── Cached state ──────────────────────────────────────────────────────
        private float _currentRatio;
        private WeightPenaltyConfig    _config;
        private IStatApplyOrchestrator _orchestrator;
        private IGameplayBridge        _bridge;

        // ── Reusable list — avoids allocation on every Recalculate() ─────────
        private static readonly List<PlayerStatModifier> _empty = new List<PlayerStatModifier>(0);

        // ─────────────────────────────────────────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            _orchestrator = ComponentResolver.Find<IStatApplyOrchestrator>(this)
                .OnSelf().InChildren().InParent()
                .OrLogError("[WeightPenaltyContributor] IStatApplyOrchestrator not found!")
                .Resolve();

            _bridge = ComponentResolver.Find<IGameplayBridge>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[WeightPenaltyContributor] IGameplayBridge not found — weight penalties disabled.")
                .Resolve();

            _config = InventoryConfig.Instance?.WeightPenalties;
        }

        private void OnEnable()
        {
            if (_bridge != null)
                _bridge.OnWeightChanged += HandleWeightChanged;
            _orchestrator?.RegisterExternalContributor(this);
        }

        private void OnDisable()
        {
            if (_bridge != null)
                _bridge.OnWeightChanged -= HandleWeightChanged;
            _orchestrator?.UnregisterExternalContributor(this);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region IStatContributor

        /// <summary>
        /// Returns the active tier's modifiers.
        /// The <paramref name="ctx"/> is intentionally ignored — weight penalties
        /// apply unconditionally regardless of what item is selected.
        /// </summary>
        public IEnumerable<PlayerStatModifier> GetPlayerStatContributions(StatContributionContext ctx)
        {
            if (_config == null) return _empty;

            var tier = _config.GetActiveTier(_currentRatio);
            if (tier == null || tier.Modifiers == null || tier.Modifiers.Length == 0)
                return _empty;

            return tier.Modifiers;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Helpers

        private void HandleWeightChanged(float current, float capacity)
        {
            _currentRatio = capacity > 0f ? current / capacity : 0f;

            // Wake SAO so the new tier is applied at end of frame.
            _orchestrator?.ScheduleRecalc();

            if (DebugLogs)
                Debug.Log($"[WeightPenaltyContributor] Weight ratio = {_currentRatio:F2} " +
                          $"→ tier: {(_config?.GetActiveTier(_currentRatio)?.Label ?? "none")}");
        }

        /// <summary>Expose current ratio for UI (e.g. weight bar color).</summary>
        public float CurrentWeightRatio => _currentRatio;

        /// <summary>Convenience: active tier label, or null when no penalty is active.</summary>
        public string ActiveTierLabel => _config?.GetActiveTier(_currentRatio)?.Label;

        [Header("Debug")]
        [SerializeField] private bool DebugLogs = false;

        #endregion
    }
}
