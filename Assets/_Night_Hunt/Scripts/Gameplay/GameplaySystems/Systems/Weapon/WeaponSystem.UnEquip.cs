using System.Collections;
using UnityEngine;
using FishNet.Object;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Weapon
{
    public partial class WeaponSystem
    {
        // -- IWeaponSystem — Public equip API -----------------------------------

        public void EquipWeapon(string instanceID)
        {
            if (!IsServerInitialized) return;
            EquipWeaponServer(instanceID);
        }

        public void EquipWeaponToSlot(string instanceID, WeaponSlotType targetSlot)
        {
            if (!IsServerInitialized) return;
            EquipWeaponToSlotServer(instanceID, targetSlot);
        }

        public void UnequipWeapon(WeaponSlotType slot)
        {
            if (IsServerInitialized) { UnequipWeaponServer(slot); return; }
            if (IsOwner) UnequipWeaponServerRpc(slot);
        }

        public void SwapWeapons(WeaponSlotType s1, WeaponSlotType s2)
        {
            if (!IsServerInitialized) return;
            SwapWeaponsServer(s1, s2);
        }

        public void SelectWeapon(WeaponSlotType slot)
        {
            if (IsServerInitialized) { SelectWeaponServer(slot); return; }
            if (IsOwner) SelectWeaponServerRpc(slot);
        }

        public void HolsterWeapon()
        {
            if (IsServerInitialized) { HolsterWeaponServer(); return; }
            if (IsOwner) HolsterWeaponServerRpc();
        }

        // -- Server implementations ---------------------------------------------

        [Server]
        private void EquipWeaponServer(string instanceID)
        {
            var inst = _inventorySystem.GetItemByInstanceID(instanceID);
            if (inst == null || !(ItemDatabase.GetDefinition(inst.DefinitionID) is WeaponDefinition def))
            {
                Debug.LogWarning($"[WeaponSystem] EquipWeapon: invalid item '{instanceID}'");
                return;
            }

            WeaponSlotType slot = FindAvailableSlot();
            if (_weapons.ContainsKey(slot)) UnequipWeaponServer(slot);

            AssignToSlot(slot, instanceID, inst, def);
        }

        [Server]
        private void EquipWeaponToSlotServer(string instanceID, WeaponSlotType targetSlot)
        {
            var inst = _inventorySystem.GetItemByInstanceID(instanceID);
            if (inst == null || !(ItemDatabase.GetDefinition(inst.DefinitionID) is WeaponDefinition def))
            {
                Debug.LogWarning($"[WeaponSystem] EquipWeaponToSlot: invalid item '{instanceID}'");
                return;
            }

            if (_weapons.ContainsKey(targetSlot)) UnequipWeaponServer(targetSlot);
            AssignToSlot(targetSlot, instanceID, inst, def);
        }

        /// <summary>Common slot assignment — shared by both equip paths.</summary>
        [Server]
        private void AssignToSlot(WeaponSlotType slot, string instanceID, ItemInstance inst, WeaponDefinition def)
        {
            _weapons[slot]      = instanceID;
            _weaponCache[slot]  = inst;
            inst.InventoryIndex = -1;

            // Initialize reserve ammo on first equip.
            float curReserve = inst.GetCurrentValue(ItemStatType.MaxAmmo);
            float maxReserve = inst.GetComputedStat(ItemStatType.MaxAmmo);
            if (curReserve == 0f && maxReserve > 0f)
                inst.SetCurrentValue(ItemStatType.MaxAmmo, maxReserve);
            else if (maxReserve == 0f)
                Debug.LogError($"[WeaponSystem] MaxAmmo stat missing for '{def.DisplayName}'.");

            _inventorySystem.SyncItemState(instanceID);
            ApplyWeaponModifiers(instanceID, def);

            // Auto-select if nothing is active.
            if (_activeSlot.Value == null)
                _activeSlot.Value = slot;

            if (DebugLogs) Debug.Log($"[WeaponSystem] Equipped '{def.DisplayName}' ? {slot}");
        }

        [ServerRpc(RequireOwnership = true)]
        private void UnequipWeaponServerRpc(WeaponSlotType slot) => UnequipWeaponServer(slot);

        [Server]
        private void UnequipWeaponServer(WeaponSlotType slot)
        {
            if (!_weapons.TryGetValue(slot, out var id)) return;
            var inst = _inventorySystem.GetItemByInstanceID(id);
            if (inst == null) { _weapons.Remove(slot); return; }

            if (_activeSlot.Value == slot) HolsterWeaponServer();

            // Detach attachments if configured.
            if (InventoryConfig != null && InventoryConfig.DetachAttachmentsOnUnequip)
            {
                var attachSys = ComponentResolver.Find<IAttachmentSystem>(this)
                    .OnSelf().InChildren().InParent()
                    .OrLogWarning($"[WeaponSystem] DetachAttachmentsOnUnequip=true but IAttachmentSystem not found on '{gameObject.name}'")
                    .Resolve();
                attachSys?.DetachAllFromItem(id);
            }

            RemoveWeaponModifiers(id);
            _weapons.Remove(slot);
            _weaponCache.Remove(slot);
            inst.InventoryIndex = FindNextAvailableInventoryIndex();
            _inventorySystem.SyncItemState(id);

            if (DebugLogs) Debug.Log($"[WeaponSystem] Unequipped slot {slot}");
        }

        [Server]
        private void SwapWeaponsServer(WeaponSlotType s1, WeaponSlotType s2)
        {
            bool has1 = _weapons.TryGetValue(s1, out var id1);
            bool has2 = _weapons.TryGetValue(s2, out var id2);
            if (!has1 && !has2) return;

            if (has1 && has2) { _weapons[s1] = id2; _weapons[s2] = id1; }
            else if (has1)    { _weapons.Remove(s1); _weapons[s2] = id1; }
            else              { _weapons.Remove(s2); _weapons[s1] = id2; }
        }

        [ServerRpc(RequireOwnership = true)]
        private void SelectWeaponServerRpc(WeaponSlotType slot) => SelectWeaponServer(slot);

        [Server]
        private void SelectWeaponServer(WeaponSlotType slot)
        {
            if (!_weapons.ContainsKey(slot)) return;
            if (_activeSlot.Value == slot) { HolsterWeaponServer(); return; }
            _activeSlot.Value = slot;
        }

        [ServerRpc(RequireOwnership = true)]
        private void HolsterWeaponServerRpc() => HolsterWeaponServer();

        [Server]
        private void HolsterWeaponServer()
        {
            if (_activeSlot.Value == null) return;
            _activeSlot.Value = null;
        }

        // -- Stat modifiers -----------------------------------------------------

        [Server]
        private void ApplyWeaponModifiers(string instanceID, WeaponDefinition weaponDef)
        {
            if (_statSystem == null) return;
            foreach (var m in (IEnumerable)weaponDef.GetPlayerModifiers() ?? System.Array.Empty<object>())
            {
                // Cast to your modifier type here — kept generic to compile without full type refs.
                // _statSystem.AddModifier(m.StatType, new StatModifier { ... });
            }
        }

        [Server]
        private void RemoveWeaponModifiers(string instanceID)
        {
            _statSystem?.RemoveAllModifiersFromSource(instanceID);
        }
    }
}