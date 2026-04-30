using System;
using System.Collections;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Data;
using UnityEngine;

namespace NightHunt.GameplaySystems.ItemUse
{
    /// <summary>
    /// Handler for consumable effects (health, stamina, buffs, etc.)
    ///
    /// Bug #15 fix: Implements IStatContributor so temporary modifiers survive
    /// StatApplyOrchestrator's Recalculate() cycles (clear-and-rebuild pattern).
    /// Register via Initialize(statSystem, orchestrator) — orchestrator will poll
    /// GetPlayerStatContributions() on every Recalculate(), keeping mods alive
    /// as long as they remain in _activeMods.
    ///
    /// Separated from main ItemUseSystem for better organization.
    /// </summary>
    public class ConsumableHandler : MonoBehaviour, IStatContributor
    {
        private IPlayerStatSystem _statSystem;
        private IStatApplyOrchestrator _orchestrator;

        // Active temporary modifier list — orchestrator polls this each Recalculate()
        private readonly List<ActiveTempMod> _activeMods = new(8);

        // Struct: pairs a PlayerStatModifier with its expiry time (Time.time based)
        private struct ActiveTempMod
        {
            public PlayerStatModifier Mod;
            public float ExpireAt; // <= 0 means permanent until removed explicitly
        }

        /// <summary>
        /// Initialize with stat system and optional orchestrator.
        /// If orchestrator is provided, self-registers as IStatContributor so temp
        /// mods survive recalc. If null, falls back to legacy direct AddModifier path.
        /// </summary>
        public void Initialize(IPlayerStatSystem statSystem, IStatApplyOrchestrator orchestrator = null)
        {
            _statSystem   = statSystem;
            _orchestrator = orchestrator;
            orchestrator?.RegisterExternalContributor(this);
            DebugConsumable($"Initialize statSystem={(_statSystem != null)} orchestrator={(_orchestrator != null)}");
        }

        // ── IStatContributor ─────────────────────────────────────────────────────
        // Called by StatApplyOrchestrator during Recalculate() — return currently active mods.
        public IEnumerable<PlayerStatModifier> GetPlayerStatContributions(StatContributionContext ctx)
        {
            float now = Time.time;
            for (int i = _activeMods.Count - 1; i >= 0; i--)
            {
                var m = _activeMods[i];
                if (m.ExpireAt > 0f && now >= m.ExpireAt)
                {
                    _activeMods.RemoveAt(i);
                    continue;
                }
                yield return m.Mod;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────────

        public void ApplyEffects(ConsumableDefinition def)
        {
            var effects = def.GetEffects(); // Read from StatConfig
            if (effects == null || effects.Length == 0)
            {
                Debug.LogWarning($"[ConsumableHandler] '{def.DisplayName}' has no effects");
                return;
            }

            foreach (var effect in effects)
                ApplySingleEffect(effect);
        }

        private void ApplySingleEffect(ConsumableEffect fx)
        {
            switch (fx.EffectType)
            {
                // Instant restore (current value — not a modifier)
                case ConsumableEffectType.RestoreHealth:
                case ConsumableEffectType.InstantHeal:
                    AdjustStat(PlayerStatType.Health, PlayerStatType.MaxHealth, fx.Value);
                    break;

                case ConsumableEffectType.RestoreStamina:
                    AdjustStat(PlayerStatType.Stamina, PlayerStatType.MaxStamina, fx.Value);
                    break;

                // Over time (uses coroutine ticking, no modifier system)
                case ConsumableEffectType.HealOverTime:
                    StartCoroutine(TickStat(PlayerStatType.Health, PlayerStatType.MaxHealth, +fx.Value, fx.Duration));
                    break;

                case ConsumableEffectType.StaminaOverTime:
                    StartCoroutine(TickStat(PlayerStatType.Stamina, PlayerStatType.MaxStamina, +fx.Value, fx.Duration));
                    break;

                case ConsumableEffectType.DamageOverTime:
                    StartCoroutine(TickStat(PlayerStatType.Health, PlayerStatType.MaxHealth, -fx.Value, fx.Duration));
                    break;

                // Temporary modifiers — now routed through IStatContributor
                case ConsumableEffectType.SpeedBoost:
                    AddTempMod(PlayerStatType.MovementSpeed, fx, percentage: true);
                    break;

                case ConsumableEffectType.ArmorBoost:
                    AddTempMod(PlayerStatType.Armor, fx, percentage: false);
                    break;

                case ConsumableEffectType.VisionIncrease:
                    AddTempMod(PlayerStatType.VisionRange, fx, percentage: false);
                    break;

                case ConsumableEffectType.NoiseReduce:
                    Debug.Log($"[ConsumableHandler] {fx.EffectType} -> hook into audio/noise stat system");
                    break;

                case ConsumableEffectType.IncreaseMaxHealth:
                    AddTempMod(PlayerStatType.MaxHealth, fx, percentage: false);
                    break;

                case ConsumableEffectType.IncreaseMaxStamina:
                    AddTempMod(PlayerStatType.MaxStamina, fx, percentage: false);
                    break;

                // Hooks
                case ConsumableEffectType.ApplyBuff:
                case ConsumableEffectType.ApplyDebuff:
                case ConsumableEffectType.Cure:
                case ConsumableEffectType.Revive:
                case ConsumableEffectType.DamageBoost:
                    Debug.Log($"[ConsumableHandler] {fx.EffectType} → hook into buff/combat system");
                    break;

                case ConsumableEffectType.DeployBeacon:
                case ConsumableEffectType.PlaceVisionNode:
                case ConsumableEffectType.PlaceExplosiveTrap:
                case ConsumableEffectType.PlaceSlowField:
                    Debug.LogWarning($"[ConsumableHandler] {fx.EffectType} is a deploy effect and should be configured as DeployableDefinition, not ConsumableDefinition.");
                    break;

                default:
                    Debug.LogWarning($"[ConsumableHandler] Unhandled effect: {fx.EffectType}");
                    break;
            }
        }

        private void AdjustStat(PlayerStatType stat, PlayerStatType maxStat, float delta)
        {
            if (_statSystem == null) return;
            float cur = _statSystem.GetStat(stat);
            float max = _statSystem.GetStat(maxStat);
            _statSystem.SetCurrentStat(stat, Mathf.Clamp(cur + delta, 0f, max));
        }

        private IEnumerator TickStat(PlayerStatType stat, PlayerStatType maxStat, float total, float dur)
        {
            int ticks = Mathf.Max(1, Mathf.CeilToInt(dur));
            float step = total / ticks;
            float interval = dur / ticks;

            for (int i = 0; i < ticks; i++)
            {
                AdjustStat(stat, maxStat, step);
                yield return new WaitForSeconds(interval);
            }
        }

        /// <summary>
        /// Add a temporary stat modifier.
        /// If orchestrator is registered, the mod is included in future Recalculate() calls
        /// and automatically expires when Duration elapses (next Recalculate polls expiry).
        /// Falls back to legacy direct AddModifier if no orchestrator.
        /// </summary>
        private void AddTempMod(PlayerStatType stat, ConsumableEffect fx, bool percentage)
        {
            if (_orchestrator != null)
            {
                // Route through IStatContributor: orchestrator will apply during Recalculate()
                var mod = new ActiveTempMod
                {
                    Mod = new PlayerStatModifier
                    {
                        StatType     = stat,
                        Value        = fx.Value,
                        ModifierType = percentage ? ModifierType.Percentage : ModifierType.Flat,
                        Description  = fx.Description
                    },
                    ExpireAt = fx.Duration > 0f ? Time.time + fx.Duration : 0f
                };
                _activeMods.Add(mod);
                _orchestrator.ScheduleRecalc();
                DebugConsumable($"AddTempMod via orchestrator stat={stat} value={fx.Value} type={(percentage ? "Percentage" : "Flat")} duration={fx.Duration}");

                // Schedule cleanup coroutine so we trigger a recalc exactly on expiry
                if (fx.Duration > 0f)
                    StartCoroutine(ExpireModAfter(fx.Duration));
            }
            else
            {
                // Legacy path (no orchestrator wired): direct AddModifier with GUID key
                string src = $"consumable_{stat}_{Guid.NewGuid():N}";
                var mod = percentage
                    ? StatModifier.CreatePercentage(src, fx.Value, 0, fx.Description)
                    : StatModifier.CreateFlat(src, fx.Value, 0, fx.Description);
                _statSystem?.AddModifier(stat, mod);
                DebugConsumable($"AddTempMod legacy stat={stat} source={src} value={fx.Value} type={(percentage ? "Percentage" : "Flat")} duration={fx.Duration}");
                if (fx.Duration > 0f)
                    StartCoroutine(ExpireModLegacy(stat, src, fx.Duration));
            }
        }

        // Orchestrator path: poll expiry is handled in GetPlayerStatContributions;
        // this coroutine triggers a recalc the moment expiry fires.
        private IEnumerator ExpireModAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            DebugConsumable("Temporary modifier expired; scheduling stat recalc.");
            _orchestrator?.ScheduleRecalc();
        }

        // Legacy path expiry (used when no orchestrator)
        private IEnumerator ExpireModLegacy(PlayerStatType stat, string src, float delay)
        {
            yield return new WaitForSeconds(delay);
            DebugConsumable($"Legacy temporary modifier expired source={src} stat={stat}");
            _statSystem?.RemoveModifier(stat, src);
        }

        private static void DebugConsumable(string message)
        {
            var cfg = NightHuntDebugConfig.Instance;
            if (cfg != null && cfg.EnableConsumableDebugLogs)
                Debug.Log($"[ConsumableHandler] {message}");
        }

        private void OnDestroy()
        {
            _orchestrator?.UnregisterExternalContributor(this);
        }
    }
}
