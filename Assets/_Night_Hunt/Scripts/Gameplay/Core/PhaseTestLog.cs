using System;
using NightHunt.GameplaySystems.Core.Configs;
using UnityEngine;

namespace NightHunt.Diagnostics
{
    public enum PhaseTestLogCategory
    {
        Input,
        Weapon,
        Animation,
        IK,
        Interaction,
        ItemUse,
        Deploy,
        Throwable,
        Death,
        Spectate,
        Score,
        Physics,
        Projectile
    }

    /// <summary>
    /// Copy-friendly runtime diagnostics for large phase-test passes.
    /// Controlled by NightHuntDebugConfig so production logs can stay quiet.
    /// </summary>
    public static class PhaseTestLog
    {
        public static bool IsEnabled(PhaseTestLogCategory category, string eventName = null, string message = null)
        {
            var cfg = NightHuntDebugConfig.Instance;
            if (cfg == null || !cfg.EnablePhaseTestLogs)
                return false;

            if (!IsCategoryEnabled(cfg, category))
                return false;

            string filter = cfg.PhaseTestLogFilter;
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return MatchesFilter(filter, category.ToString(), eventName, message);
        }

        public static void Log(PhaseTestLogCategory category, string eventName, string message, UnityEngine.Object context = null)
        {
            if (!IsEnabled(category, eventName, message))
                return;

            if (context != null)
                Debug.Log(Format(category, eventName, message), context);
            else
                Debug.Log(Format(category, eventName, message));
        }

        public static void Warning(PhaseTestLogCategory category, string eventName, string message, UnityEngine.Object context = null)
        {
            if (!IsEnabled(category, eventName, message))
                return;

            if (context != null)
                Debug.LogWarning(Format(category, eventName, message), context);
            else
                Debug.LogWarning(Format(category, eventName, message));
        }

        public static string DescribeLayer(GameObject go)
        {
            if (go == null)
                return "null";

            int layer = go.layer;
            string layerName = LayerMask.LayerToName(layer);
            return string.IsNullOrWhiteSpace(layerName) ? layer.ToString() : $"{layerName}({layer})";
        }

        public static string DescribeObject(GameObject go)
        {
            if (go == null)
                return "null";

            return $"{go.name} layer={DescribeLayer(go)} tag={go.tag}";
        }

        private static string Format(PhaseTestLogCategory category, string eventName, string message)
        {
            string evt = string.IsNullOrWhiteSpace(eventName) ? "Event" : eventName;
            return $"[PHASE_TEST][{category.ToString().ToUpperInvariant()}][{evt}] t={Time.time:F3} frame={Time.frameCount} {message}";
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesFilter(string filter, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            var tokens = filter.Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (string.IsNullOrEmpty(token))
                    continue;

                for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
                {
                    if (Contains(values[valueIndex], token))
                        return true;
                }
            }

            return false;
        }

        private static bool IsCategoryEnabled(NightHuntDebugConfig cfg, PhaseTestLogCategory category)
        {
            return category switch
            {
                PhaseTestLogCategory.Input => cfg.PhaseTestLogInput,
                PhaseTestLogCategory.Weapon => cfg.PhaseTestLogWeapon,
                PhaseTestLogCategory.Animation => cfg.PhaseTestLogAnimation,
                PhaseTestLogCategory.IK => cfg.PhaseTestLogIK,
                PhaseTestLogCategory.Interaction => cfg.PhaseTestLogInteraction,
                PhaseTestLogCategory.ItemUse => cfg.PhaseTestLogItemUse,
                PhaseTestLogCategory.Deploy => cfg.PhaseTestLogDeploy,
                PhaseTestLogCategory.Throwable => cfg.PhaseTestLogThrowable,
                PhaseTestLogCategory.Death => cfg.PhaseTestLogDeath,
                PhaseTestLogCategory.Spectate => cfg.PhaseTestLogSpectate,
                PhaseTestLogCategory.Score => cfg.PhaseTestLogScore,
                PhaseTestLogCategory.Physics => cfg.PhaseTestLogPhysics,
                PhaseTestLogCategory.Projectile => cfg.PhaseTestLogProjectile || cfg.EnableProjectileDebugLogs,
                _ => true
            };
        }
    }
}
