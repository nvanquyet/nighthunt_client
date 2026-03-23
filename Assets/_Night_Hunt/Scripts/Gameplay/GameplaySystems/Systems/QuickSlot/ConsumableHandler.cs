using System.Collections;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Data;
using UnityEngine;

namespace NightHunt.GameplaySystems.ItemUse
{
    /// <summary>
    /// Handler for consumable effects (health, stamina, buffs, etc.)
    /// Separated from main ItemUseSystem for better organization
    /// </summary>
    public class ConsumableHandler : MonoBehaviour
    {
        private IPlayerStatSystem _statSystem;
        
        public void Initialize(IPlayerStatSystem statSystem)
        {
            _statSystem = statSystem;
        }
        
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
                // Instant restore
                case ConsumableEffectType.RestoreHealth:
                case ConsumableEffectType.InstantHeal:
                    AdjustStat(PlayerStatType.Health, PlayerStatType.MaxHealth, fx.Value);
                    break;
                
                case ConsumableEffectType.RestoreStamina:
                    AdjustStat(PlayerStatType.Stamina, PlayerStatType.MaxStamina, fx.Value);
                    break;
                
                // Over time
                case ConsumableEffectType.HealOverTime:
                    StartCoroutine(TickStat(PlayerStatType.Health, PlayerStatType.MaxHealth, +fx.Value, fx.Duration));
                    break;
                
                case ConsumableEffectType.StaminaOverTime:
                    StartCoroutine(TickStat(PlayerStatType.Stamina, PlayerStatType.MaxStamina, +fx.Value, fx.Duration));
                    break;
                
                case ConsumableEffectType.DamageOverTime:
                    StartCoroutine(TickStat(PlayerStatType.Health, PlayerStatType.MaxHealth, -fx.Value, fx.Duration));
                    break;
                
                // Temporary modifiers
                case ConsumableEffectType.SpeedBoost:
                    AddTempMod(PlayerStatType.MovementSpeed, fx, percentage: true);
                    break;
                
                case ConsumableEffectType.ArmorBoost:
                    AddTempMod(PlayerStatType.Armor, fx, percentage: false);
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
        
        private void AddTempMod(PlayerStatType stat, ConsumableEffect fx, bool percentage)
        {
            if (_statSystem == null) return;
            
            string src = $"consumable_{stat}_{Time.time:F0}";
            var mod = percentage
                ? StatModifier.CreatePercentage(src, fx.Value, 0, fx.Description)
                : StatModifier.CreateFlat(src, fx.Value, 0, fx.Description);
            
            _statSystem.AddModifier(stat, mod);
            
            if (fx.Duration > 0f)
                StartCoroutine(ExpireMod(stat, src, fx.Duration));
        }
        
        private IEnumerator ExpireMod(PlayerStatType stat, string src, float delay)
        {
            yield return new WaitForSeconds(delay);
            _statSystem?.RemoveModifier(stat, src);
        }
    }
    
}