using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.StatSystem.Core.Data;
using NightHunt.StatSystem.Core.Interfaces;
using NightHunt.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Systems.Stat
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

        // ── Inspector wiring (MonoBehaviour references cast to interfaces) ────
        [Header("Required Systems")]
        [SerializeField] private MonoBehaviour _playerStatSystemMB;
        [SerializeField] private MonoBehaviour _equipmentSystemMB;
        [SerializeField] private MonoBehaviour _weaponSystemMB;
        [SerializeField] private MonoBehaviour _attachmentSystemMB;

        // ── Runtime interfaces ────────────────────────────────────────────────
        private IPlayerStatSystem  _playerStats;
        private IEquipmentSystem   _equipment;
        private IWeaponSystem      _weapons;
        private IAttachmentSystem  _attachments;

        // ── Track source IDs applied last recalculation ───────────────────────
        private readonly HashSet<string> _appliedSources = new HashSet<string>(32);

        // ── Dirty flag: defer to end-of-frame to batch multiple rapid changes ─
        private bool _pendingRecalc;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            _playerStats  = _playerStatSystemMB  as IPlayerStatSystem;
            _equipment    = _equipmentSystemMB   as IEquipmentSystem;
            _weapons      = _weaponSystemMB      as IWeaponSystem;
            _attachments  = _attachmentSystemMB  as IAttachmentSystem;

            LogMissing();
        }

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

        private void ScheduleRecalc() => _pendingRecalc = true;

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

        // ─────────────────────────────────────────────────────────────────────
        #region Debug

        private void LogMissing()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_playerStats  == null) Debug.LogError("[StatApplyOrchestrator] IPlayerStatSystem not found!", this);
            if (_equipment    == null) Debug.LogWarning("[StatApplyOrchestrator] IEquipmentSystem not found — equipment stats won't apply.", this);
            if (_weapons      == null) Debug.LogWarning("[StatApplyOrchestrator] IWeaponSystem not found — weapon stats won't apply.", this);
            if (_attachments  == null) Debug.LogWarning("[StatApplyOrchestrator] IAttachmentSystem not found — attachment stats won't apply.", this);
#endif
        }

        #endregion
    }
}
