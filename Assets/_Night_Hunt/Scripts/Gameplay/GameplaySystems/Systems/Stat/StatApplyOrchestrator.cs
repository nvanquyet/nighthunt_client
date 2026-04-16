using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.Stat
{
    /// <summary>
    /// Orchestrates all item → player stat contributions.
    ///
    /// RULE:
    ///   Equipment (+ its attachments) : applied immediately on equip, cleared on unequip.
    ///   Weapon    (+ its attachments) : applied ONLY when weapon is SELECTED (drawn), cleared on deselect.
    ///
    /// HOW IT WORKS:
    ///   Any equip / unequip / select / deselect / attach / detach event
    ///   → Recalculate()
    ///   → [Clear] remove all previously applied source IDs from PlayerStatSystem
    ///   → [Rebuild] re-apply current active contributors
    ///   → [ComputeStats] refresh ComputedItemStats for active items
    ///
    /// PLACEMENT: Add to the Player prefab alongside PlayerStatSystem.
    /// </summary>
    public class StatApplyOrchestrator : MonoBehaviour, IStatApplyOrchestrator
    {
        // ── Source-ID prefix so we can identify and clean up our modifiers ────
        private const string SOURCE_PREFIX = "sao:";

        // ── Runtime interfaces ────────────────────────────────────────────────
        private IPlayerStatSystem  _playerStats;
        private IEquipmentSystem   _equipment;
        private IWeaponSystem      _weapons;
        private IAttachmentSystem  _attachments;
 
        // ── External contributors (buffs, consumables, skill systems…) ───────
        private readonly List<IStatContributor> _externalContributors = new List<IStatContributor>(4);
        private readonly Dictionary<IStatContributor, string> _externalContributorSourceIds = new Dictionary<IStatContributor, string>(4);

        // ── Track source IDs applied last recalculation ───────────────────────
        private readonly HashSet<string> _appliedSources = new HashSet<string>(32);

        // ── Dirty flag: defer to end-of-frame to batch multiple rapid changes ─
        private bool _pendingRecalc;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake() => ResolveReferences();

        private void ResolveReferences()
        {
            _playerStats = ComponentResolver.Find<IPlayerStatSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogError("[StatApplyOrchestrator] IPlayerStatSystem not found!")
                .Resolve();
            _equipment = ComponentResolver.Find<IEquipmentSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[StatApplyOrchestrator] IEquipmentSystem not found — equipment stats won't apply.")
                .Resolve();
            _weapons = ComponentResolver.Find<IWeaponSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[StatApplyOrchestrator] IWeaponSystem not found — weapon stats won't apply.")
                .Resolve();
            _attachments = ComponentResolver.Find<IAttachmentSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[StatApplyOrchestrator] IAttachmentSystem not found — attachment stats won't apply.")
                .Resolve();
        }

#if UNITY_EDITOR
        [ContextMenu("Validate References")]
        private void OnValidate() => ResolveReferences();
#endif

        private void OnEnable()
        {
            if (_equipment   != null) { _equipment.OnItemEquipped    += OnEquipmentChanged; _equipment.OnItemUnequipped += OnEquipmentChanged; }
            if (_weapons     != null) { _weapons.OnWeaponEquipped    += OnWeaponSlotChanged; _weapons.OnWeaponUnequipped += OnWeaponSlotChanged; _weapons.OnActiveWeaponChanged += OnActiveWeaponChanged; }
            if (_attachments != null) { _attachments.OnAttachmentAttached += OnAttachmentChanged; _attachments.OnAttachmentDetached += OnAttachmentChanged; }
        }

        private void OnDisable()
        {
            if (_equipment   != null) { _equipment.OnItemEquipped    -= OnEquipmentChanged; _equipment.OnItemUnequipped -= OnEquipmentChanged; }
            if (_weapons     != null) { _weapons.OnWeaponEquipped    -= OnWeaponSlotChanged; _weapons.OnWeaponUnequipped -= OnWeaponSlotChanged; _weapons.OnActiveWeaponChanged -= OnActiveWeaponChanged; }
            if (_attachments != null) { _attachments.OnAttachmentAttached -= OnAttachmentChanged; _attachments.OnAttachmentDetached -= OnAttachmentChanged; }
        }

        private void LateUpdate()
        {
            // Batch: execute deferred recalculation at end of frame.
            if (_pendingRecalc)
            {
                _pendingRecalc = false;
                Recalculate();
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Event Handlers (all defer to LateUpdate batch)

        private void OnEquipmentChanged(EquipmentSlotType _, ItemInstance __) => ScheduleRecalc();
        private void OnWeaponSlotChanged(WeaponSlotType _, ItemInstance __)   => ScheduleRecalc();
        private void OnActiveWeaponChanged(WeaponSlotType? __, WeaponSlotType? _) => ScheduleRecalc();
        private void OnAttachmentChanged(string __, int ___, ItemInstance ____) => ScheduleRecalc();

        public void ScheduleRecalc() => _pendingRecalc = true;

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region IStatApplyOrchestrator

        /// <summary>
        /// Full clear-and-rebuild of all item-sourced player stat modifiers.
        /// Call this any time item state changes (equip/unequip/select/attach…).
        /// </summary>
        public void Recalculate()
        {
            if (_playerStats == null) return;

            // ── 1. Clear all modifiers we applied last time ───────────────────
            foreach (var src in _appliedSources)
                _playerStats.RemoveAllModifiersFromSource(src);
            _appliedSources.Clear();

            // ── 2. Equipment slots (always active while equipped) ─────────────
            if (_equipment != null)
            {
                var equipped = _equipment.GetAllEquippedItems();
                if (equipped != null)
                {
                    foreach (var kv in equipped)
                    {
                        var item = kv.Value;
                        if (item == null) continue;

                        ApplyItemPlayerModifiers(item, ItemType.Equipment, isHostSelected: true);
                        ApplyAttachmentPlayerModifiers(item, ItemType.Equipment, isHostSelected: true);
                        ItemStatComputer.Compute(item);
                    }
                }
            }

            // ── 3. Active (selected) weapon only ─────────────────────────────
            if (_weapons != null)
            {
                var activeWeapon = _weapons.GetActiveWeapon();
                if (activeWeapon != null)
                {
                    ApplyItemPlayerModifiers(activeWeapon, ItemType.Weapon, isHostSelected: true);
                    ApplyAttachmentPlayerModifiers(activeWeapon, ItemType.Weapon, isHostSelected: true);
                    ItemStatComputer.Compute(activeWeapon);
                }
            }

            // ── 4. External contributors (buffs, consumables, skills, etc.) ───
            foreach (var kv in _externalContributorSourceIds)
            {
                var contributor = kv.Key;
                var src         = kv.Value;
                if (contributor == null) continue;

                // External contributors are always treated as active (isHostSelected=true).
                // Use Equipment as the host type so ShouldContribute is always true.
                var ctx  = new StatContributionContext(isHostSelected: true, hostItemType: ItemType.Equipment, instanceId: src);
                var mods = contributor.GetPlayerStatContributions(ctx);
                if (mods == null) continue;

                foreach (var mod in mods)
                    AddToPlayerStat(mod, src);

                _appliedSources.Add(src);
            }
        }

        [ContextMenu("Force Recalculate")]
        public void ForceRecalculate() => Recalculate();

        [ContextMenu("Log State")]
        private void LogState()
        {
            Debug.Log($"[StatApplyOrchestrator] appliedSources={_appliedSources.Count} " +
                      $"externalContributors={_externalContributors.Count} " +
                      $"pendingRecalc={_pendingRecalc} " +
                      $"playerStats={(object)_playerStats ?? "null"} " +
                      $"equipment={(object)_equipment ?? "null"} " +
                      $"weapons={(object)_weapons ?? "null"} " +
                      $"attachments={(object)_attachments ?? "null"}");
        }

        public void RegisterExternalContributor(IStatContributor contributor)
        {
            if (contributor == null) return;
            if (_externalContributorSourceIds.ContainsKey(contributor)) return;

            // assign a stable source id for this contributor so its modifiers can be removed later
            string src = SOURCE_PREFIX + "ext:" + Guid.NewGuid().ToString("N");
            _externalContributorSourceIds[contributor] = src;
            _externalContributors.Add(contributor);
            ScheduleRecalc();
        }

        public void UnregisterExternalContributor(IStatContributor contributor)
        {
            if (contributor == null) return;
            if (!_externalContributorSourceIds.TryGetValue(contributor, out var src)) return;

            // Remove any modifiers currently applied from this contributor
            if (_playerStats != null && !string.IsNullOrEmpty(src))
                _playerStats.RemoveAllModifiersFromSource(src);

            _externalContributorSourceIds.Remove(contributor);
            _externalContributors.Remove(contributor);
            ScheduleRecalc();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Apply Helpers

        /// <summary>Apply PlayerStatModifiers from the item's own definition.</summary>
        private void ApplyItemPlayerModifiers(ItemInstance item, ItemType hostType, bool isHostSelected)
        {
            var ctx = new StatContributionContext(isHostSelected, hostType, item.InstanceID);
            if (!ctx.ShouldContribute) return;

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null) return;

            var mods = GetPlayerModifiersFromDef(def);
            if (mods == null || mods.Length == 0) return;

            string src = SOURCE_PREFIX + item.InstanceID;
            foreach (var mod in mods)
                AddToPlayerStat(mod, src);

            _appliedSources.Add(src);
        }

        /// <summary>Apply PlayerStatModifiers from all attachments on the item, following host rule.</summary>
        private void ApplyAttachmentPlayerModifiers(ItemInstance host, ItemType hostType, bool isHostSelected)
        {
            if (host.AttachedItems == null) return;

            var ctx = new StatContributionContext(isHostSelected, hostType, host.InstanceID);
            if (!ctx.ShouldContribute) return;

            for (int i = 0; i < host.AttachedItems.Length; i++)
            {
                var attId = host.AttachedItems[i];
                if (string.IsNullOrEmpty(attId)) continue;

                var attInst = ItemDatabase.GetInstance(attId);
                if (attInst == null) continue;

                var attDef = ItemDatabase.GetDefinition(attInst.DefinitionID) as AttachmentDefinition;
                if (attDef == null) continue;

                var mods = attDef.GetPlayerModifiers();
                if (mods == null || mods.Length == 0) continue;

                string src = SOURCE_PREFIX + attInst.InstanceID;
                foreach (var mod in mods)
                    AddToPlayerStat(mod, src);

                _appliedSources.Add(src);
            }
        }

        /// <summary>Convert a definition-time <see cref="PlayerStatModifier"/> to a runtime <see cref="StatModifier"/> and add it.</summary>
        private void AddToPlayerStat(PlayerStatModifier mod, string src)
        {
            StatModifier runtime = mod.ModifierType switch
            {
                ModifierType.Flat       => StatModifier.CreateFlat(src, mod.Value, 0, mod.Description),
                ModifierType.Percentage => StatModifier.CreatePercentage(src, mod.Value, 0, mod.Description),
                ModifierType.Override   => StatModifier.CreateOverride(src, mod.Value, mod.Description),
                _                       => StatModifier.CreateFlat(src, mod.Value, 0, mod.Description)
            };
            _playerStats.AddModifier(mod.StatType, runtime);
        }

        /// <summary>Polymorphic helper — read PlayerModifiers from any definition subtype.</summary>
        private static PlayerStatModifier[] GetPlayerModifiersFromDef(ItemDefinition def)
        {
            return def switch
            {
                WeaponDefinition    wd => wd.GetPlayerModifiers(),
                EquipmentDefinition ed => ed.GetPlayerModifiers(),
                AttachmentDefinition ad => ad.GetPlayerModifiers(),
                _ => null
            };
        }

        #endregion
    }
}
