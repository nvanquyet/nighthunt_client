using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Stat
{
    /// <summary>
    /// Stateless utility: computes an <see cref="ItemInstance"/>'s final stats
    /// from its base <see cref="ItemStatConfig"/> + all attached <see cref="AttachmentDefinition"/> modifiers.
    ///
    /// DESIGN:
    /// - Static / no state — call any time, safe to call from orchestrator or lazily.
    /// - Does NOT push anything to PlayerStatSystem (that is the Orchestrator's job).
    /// - Result is stored back onto <see cref="ItemInstance.SetComputedStats"/>.
    ///
    /// MODIFIER EVALUATION ORDER (per stat):
    ///   1. Base value from ItemStatConfig.
    ///   2. Sum all Flat attachment modifiers.
    ///   3. Multiply all Percentage attachment modifiers (additive accumulation, then apply once).
    /// </summary>
    public static class ItemStatComputer
    {
        // Reusable buffers — avoids per-call allocation on hot path.
        private static readonly Dictionary<ItemStatType, float> _resultBuffer =
            new Dictionary<ItemStatType, float>(16);
        private static readonly Dictionary<ItemStatType, float> _flatAccum =
            new Dictionary<ItemStatType, float>(8);
        private static readonly Dictionary<ItemStatType, float> _pctAccum =
            new Dictionary<ItemStatType, float>(8);

        /// <summary>
        /// Compute and store final stats onto <paramref name="instance"/>.
        /// Reads base stats from the item's definition, then applies all attachment modifiers.
        /// After writing new stats, clamps / scales current values so they stay valid
        /// relative to the new maximums (durability scales proportionally; ammo only clamps).
        /// </summary>
        public static void Compute(ItemInstance instance)
        {
            if (instance == null) return;

            var def = ItemDatabase.GetDefinition(instance.DefinitionID);
            if (def == null)
            {
                instance.SetComputedStats(new Dictionary<ItemStatType, float>());
                return;
            }

            _resultBuffer.Clear();
            _flatAccum.Clear();
            _pctAccum.Clear();

            // ── Step 0: Snapshot old computed stats (for ClampCurrentValues) ─
            var oldStats = instance.GetComputedStatsSnapshot(); // null on first compute

            // ── Step 1: Seed with base stats from StatConfig ──────────────────
            SeedBaseStats(def, _resultBuffer);

            // ── Step 2: Collect attachment modifiers ──────────────────────────
            if (instance.AttachedItems != null)
            {
                for (int i = 0; i < instance.AttachedItems.Length; i++)
                {
                    var attId = instance.AttachedItems[i];
                    if (string.IsNullOrEmpty(attId)) continue;

                    var attInst = ItemDatabase.GetInstance(attId);
                    if (attInst == null) continue;

                    var attDef = ItemDatabase.GetDefinition(attInst.DefinitionID) as AttachmentDefinition;
                    if (attDef == null) continue;

                    var mods = attDef.GetItemModifiers();
                    if (mods == null) continue;

                    foreach (var mod in mods)
                    {
                        if (mod.ModifierType == ModifierType.Flat)
                        {
                            _flatAccum.TryGetValue(mod.StatType, out var cur);
                            _flatAccum[mod.StatType] = cur + mod.Value;
                        }
                        else if (mod.ModifierType == ModifierType.Percentage)
                        {
                            _pctAccum.TryGetValue(mod.StatType, out var cur);
                            _pctAccum[mod.StatType] = cur + mod.Value;
                        }
                    }
                }
            }

            // ── Step 3: Apply accumulated modifiers to base values ────────────
            foreach (var kv in _flatAccum)
            {
                _resultBuffer.TryGetValue(kv.Key, out var base_);
                _resultBuffer[kv.Key] = base_ + kv.Value;
            }

            foreach (var kv in _pctAccum)
            {
                _resultBuffer.TryGetValue(kv.Key, out var afterFlat);
                // e.g. +20% means * 1.20
                _resultBuffer[kv.Key] = afterFlat * (1f + kv.Value / 100f);
            }

            // ── Step 4: Write back (creates a snapshot copy) ─────────────────
            var newStats = new Dictionary<ItemStatType, float>(_resultBuffer);
            instance.SetComputedStats(newStats);

            // ── Step 5: Sync / clamp current values ───────────────────────────
            if (oldStats == null)
            {
                // First compute — seed _currentValues from the serialised backing fields
                // so later recomputes can correctly proportionally-scale them.
                instance.SeedCurrentValuesFromBackingFields(newStats);
            }
            else
            {
                // Subsequent compute (e.g. attachment equip/unequip) — adjust current values
                // relative to the new maximums.
                // Equipment (armor, flashlight): scale proportionally so HP fraction is preserved.
                // Weapons / attachments: clamp only — equipping an Ext-Mag doesn't auto-fill rounds.
                var adjustMode = (def is EquipmentDefinition)
                    ? CurrentValueAdjustMode.Proportional
                    : CurrentValueAdjustMode.ClampOnly;

                instance.ClampCurrentValuesToNewMax(oldStats, newStats, adjustMode);
            }
        }

        // ── Private Helpers ──────────────────────────────────────────────────

        public static Dictionary<ItemStatType, float> GetBaseStats(ItemDefinition def)
        {
            var result = new Dictionary<ItemStatType, float>(16);
            if (def != null)
                SeedBaseStats(def, result);
            return result;
        }

        public static float GetBaseStat(ItemDefinition def, ItemStatType type)
        {
            var stats = GetBaseStats(def);
            return stats.TryGetValue(type, out float value) ? value : 0f;
        }

        private static void SeedBaseStats(ItemDefinition def, Dictionary<ItemStatType, float> result)
        {
            // Try each concrete definition type that carries a StatConfig.
            switch (def)
            {
                case WeaponDefinition wd when wd.StatConfig?.Stats != null:
                    foreach (var s in wd.StatConfig.Stats)
                        result[s.Type] = s.DefaultValue;
                    break;

                case EquipmentDefinition ed when ed.StatConfig?.Stats != null:
                    foreach (var s in ed.StatConfig.Stats)
                        result[s.Type] = s.DefaultValue;
                    break;

                case AttachmentDefinition ad when ad.StatConfig?.Stats != null:
                    foreach (var s in ad.StatConfig.Stats)
                        result[s.Type] = s.DefaultValue;
                    break;
            }
        }
    }
}
