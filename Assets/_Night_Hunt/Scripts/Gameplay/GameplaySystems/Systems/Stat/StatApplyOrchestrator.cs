using FishNet.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;
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
    /// NETWORK RULE:
    ///   Only runs on the SERVER (applies authoritative stats) and the OWNING CLIENT
    ///   (applies local prediction / HUD stats). Non-owning observers skip all stat
    ///   application to prevent ghost modifiers accumulating on spectated players.
    ///
    /// HOW IT WORKS:
    ///   Any equip / unequip / select / deselect / attach / detach event
    ///   → ScheduleRecalc() (dirty flag)
    ///   → LateUpdate: Recalculate()
    ///     → [Clear] remove all previously applied source IDs from PlayerStatSystem
    ///     → [Rebuild] re-apply current active contributors
    ///     → [ComputeStats] refresh ComputedItemStats for active items
    ///
    /// PLACEMENT: Add to the Player prefab alongside PlayerStatSystem.
    /// </summary>
    public class StatApplyOrchestrator : NetworkBehaviour, IStatApplyOrchestrator
    {
        // ── Source-ID prefix so we can identify and clean up our modifiers ────
        private const string SOURCE_PREFIX = "sao:";

        /// <summary>
        /// Guard: stat application is only meaningful on the server (authoritative source)
        /// and on the owning client (local HUD / prediction). Non-owning clients skip
        /// <see cref="Recalculate"/> entirely to prevent ghost modifiers on spectated players.
        /// </summary>
        private bool ShouldRunLocally => IsServerInitialized || IsOwner;

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
            // Skip on non-owning clients — they have no authority over stat modifiers.
            if (_pendingRecalc && ShouldRunLocally)
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
            // Non-owning observers must not apply stat modifiers.
            // Their player's stats are handled by the server / owning client.
            if (!ShouldRunLocally) return;

            if (_playerStats == null) return;

            DebugStat($"Recalculate begin role={GetDebugRole()} previousSources={_appliedSources.Count} externalContributors={_externalContributors.Count}");

            // ── 1. Clear all modifiers we applied last time ───────────────────
            foreach (var src in _appliedSources)
            {
                DebugStat($"Clear source={src}");
                _playerStats.RemoveAllModifiersFromSource(src);
            }
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

                        DebugStat($"Equipment slot={kv.Key} item={DescribeItem(item)}");
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
                    DebugStat($"Active weapon item={DescribeItem(activeWeapon)}");
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

                int count = 0;
                foreach (var mod in mods)
                {
                    count++;
                    DebugStatModifier("External", src, contributor.GetType().Name, mod);
                    AddToPlayerStat(mod, src);
                }

                if (count > 0)
                    _appliedSources.Add(src);
            }

            DebugStat($"Recalculate end appliedSources={_appliedSources.Count}");
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
            DebugStat($"Register external contributor={contributor.GetType().Name} source={src}");
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
            DebugStat($"Unregister external contributor={contributor.GetType().Name} source={src}");
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
            DebugStat($"Apply {hostType} source={src} item={DescribeItem(item, def)} selected={isHostSelected} modifiers={mods.Length}");
            foreach (var mod in mods)
            {
                DebugStatModifier(hostType.ToString(), src, def.DisplayName, mod);
                AddToPlayerStat(mod, src);
            }

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
                DebugStat($"Apply attachment host={DescribeItem(host)} slotIndex={i} source={src} attachment={DescribeItem(attInst, attDef)} hostType={hostType} modifiers={mods.Length}");
                foreach (var mod in mods)
                {
                    DebugStatModifier("Attachment", src, attDef.DisplayName, mod);
                    AddToPlayerStat(mod, src);
                }

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

        private static bool IsStatDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && cfg.EnableStatDebugLogs;
        }

        private static void DebugStat(string message)
        {
            if (IsStatDebugEnabled())
                Debug.Log($"[STAT_FLOW][StatApplyOrchestrator] {message}");
        }

        private static void DebugStatModifier(string category, string source, string owner, PlayerStatModifier mod)
        {
            if (!IsStatDebugEnabled()) return;
            Debug.Log($"[STAT_FLOW][StatApplyOrchestrator]   {category} owner='{owner}' source={source} -> {mod.StatType} {mod.ModifierType} {mod.Value:F2} ({mod.Description})");
        }

        private static string DescribeItem(ItemInstance item)
        {
            if (item == null) return "null";
            return DescribeItem(item, ItemDatabase.GetDefinition(item.DefinitionID));
        }

        private static string DescribeItem(ItemInstance item, ItemDefinition def)
        {
            if (item == null) return "null";
            string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : item.DefinitionID;
            return $"{name} def={item.DefinitionID} inst={item.InstanceID}";
        }

        private string GetDebugRole()
        {
            if (IsServerInitialized && IsOwner) return "ServerOwner";
            if (IsServerInitialized) return "Server";
            if (IsOwner) return "OwnerClient";
            return "Observer";
        }

        #endregion
    }
}
