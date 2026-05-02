using UnityEngine;
using FishNet.Object;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Loot;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Weapon
{
    public partial class WeaponSystem
    {
        // ── IWeaponSystem — Public Equip / Unequip / Drop API ─────────────────

        public void EquipWeapon(string instanceID)
        {
            if (IsServerInitialized) { EquipWeaponServer(instanceID); return; }
            if (IsOwner) EquipWeaponServerRpc(instanceID);
        }

        public void EquipWeaponToSlot(string instanceID, WeaponSlotType targetSlot)
        {
            if (IsServerInitialized) { EquipWeaponToSlotServer(instanceID, targetSlot); return; }
            if (IsOwner) EquipWeaponToSlotServerRpc(instanceID, targetSlot);
        }

        public void UnequipWeapon(WeaponSlotType slot)
        {
            if (IsServerInitialized) { UnequipWeaponServer(slot); return; }
            if (IsOwner) UnequipWeaponServerRpc(slot);
        }

        public void SwapWeapons(WeaponSlotType s1, WeaponSlotType s2)
        {
            if (IsServerInitialized) { SwapWeaponsServer(s1, s2); return; }
            if (IsOwner) SwapWeaponsServerRpc(s1, s2);
        }

        /// <summary>
        /// Drop the weapon in <paramref name="slot"/> directly to the world:
        ///   1. Holster if currently drawn.
        ///   2. Detach all attachments (per InventoryConfig).
        ///   3. Spawn the world item via WorldSpawnManager.
        ///   4. Remove the weapon from its holster slot.
        /// Owning client routes to the server automatically.
        /// </summary>
        public void DropWeapon(WeaponSlotType slot)
        {
            if (IsServerInitialized) { DropWeaponServer(slot); return; }
            if (IsOwner) DropWeaponServerRpc(slot);
            else Debug.LogWarning("[WeaponSystem] DropWeapon: caller does not own this object.");
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

        // ── Server Implementations ─────────────────────────────────────────────

        [Server]
        private void EquipWeaponServer(string instanceID)
        {
            var inst = _inventorySystem.GetItemByInstanceID(instanceID);
            if (inst == null || !(ItemDatabase.GetDefinition(inst.DefinitionID) is WeaponDefinition def))
            {
                Debug.LogWarning($"[WeaponSystem] EquipWeapon: invalid item '{instanceID}'");
                return;
            }

            if (TryGetSlotForInstance(instanceID, out var existingSlot))
            {
                if (_activeSlot.Value != existingSlot)
                    _activeSlot.Value = existingSlot;
                return;
            }

            WeaponSlotType slot = FindAvailableSlot(def);
            if (_weapons.ContainsKey(slot))
                UnequipWeaponServer(slot);

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

            if (!CanEquipDefinitionInSlot(def, targetSlot))
            {
                Debug.LogWarning($"[WeaponSystem] EquipWeaponToSlot: '{def.DisplayName}' cannot equip to {targetSlot}");
                return;
            }

            if (TryGetSlotForInstance(instanceID, out var existingSlot))
            {
                if (existingSlot == targetSlot)
                {
                    if (_activeSlot.Value != targetSlot)
                        _activeSlot.Value = targetSlot;
                    return;
                }

                _weapons.Remove(existingSlot);
                _weaponCache.Remove(existingSlot);
                OnWeaponUnequipped?.Invoke(existingSlot, inst);
            }

            if (_weapons.ContainsKey(targetSlot))
                UnequipWeaponServer(targetSlot);

            AssignToSlot(targetSlot, instanceID, inst, def);
        }

        private bool TryGetSlotForInstance(string instanceID, out WeaponSlotType slot)
        {
            foreach (var kvp in _weapons)
            {
                if (kvp.Value == instanceID)
                {
                    slot = kvp.Key;
                    return true;
                }
            }

            slot = WeaponSlotType.None;
            return false;
        }

        [Server]
        private void AssignToSlot(WeaponSlotType slot, string instanceID, ItemInstance inst, WeaponDefinition def)
        {
            _weapons[slot]     = instanceID;
            _weaponCache[slot] = inst;
            inst.InventoryIndex = -1;

            // Initialize reserve ammo on first equip when stats have been computed.
            float curReserve = inst.GetCurrentValue(ItemStatType.MaxAmmo);
            float maxReserve = inst.GetComputedStat(ItemStatType.MaxAmmo);
            if (curReserve == 0f && maxReserve > 0f)
                inst.SetCurrentValue(ItemStatType.MaxAmmo, maxReserve);
            else if (maxReserve == 0f)
                Debug.LogWarning($"[WeaponSystem] MaxAmmo stat missing for '{def.DisplayName}'. Run ItemStatComputer.Compute first.");

            _inventorySystem.SyncItemState(instanceID);

            // Equipping from inventory is also a draw/select action. Keep the
            // HUD active slot, animator, reload button, and server SyncVar aligned.
            if (_activeSlot.Value != slot)
                _activeSlot.Value = slot;

            // Fire event on server so host-mode listeners (StatApplyOrchestrator, host UI) are notified.
            // On pure clients this is fired by OnWeaponsChangedCallback (asServer=false path).
            OnWeaponEquipped?.Invoke(slot, inst);

            if (DebugLogs)
                Debug.Log($"[WeaponSystem] Equipped '{def.DisplayName}' → {slot}");
        }

        [Server]
        private void UnequipWeaponServer(WeaponSlotType slot)
        {
            if (!_weapons.TryGetValue(slot, out var id))
                return;

            var inst = _inventorySystem.GetItemByInstanceID(id);
            if (inst == null)
            {
                _weapons.Remove(slot);
                _weaponCache.Remove(slot);
                return;
            }

            // Holster first so OnActiveWeaponChanged fires cleanly.
            if (_activeSlot.Value == slot)
                HolsterWeaponServer();

            // Detach attachments according to config.
            if (InventoryConfig != null && InventoryConfig.DetachAttachmentsOnUnequip)
                _attachmentSystem?.DetachAllFromItem(id);

            _weapons.Remove(slot);
            _weaponCache.Remove(slot);

            // Fire event on server so host-mode listeners are notified BEFORE cache is cleared.
            OnWeaponUnequipped?.Invoke(slot, inst);

            // Return item to an open inventory slot.
            inst.InventoryIndex = FindNextAvailableInventoryIndex();
            _inventorySystem.SyncItemState(id);

            if (DebugLogs)
                Debug.Log($"[WeaponSystem] Unequipped slot {slot}");
        }

        [Server]
        private void DropWeaponServer(WeaponSlotType slot)
        {
            if (!_weapons.TryGetValue(slot, out var instanceID))
            {
                Debug.LogWarning($"[WeaponSystem] DropWeapon: slot {slot} is empty.");
                return;
            }

            var inst = _inventorySystem.GetItemByInstanceID(instanceID);
            if (inst == null)
            {
                Debug.LogWarning($"[WeaponSystem] DropWeapon: item '{instanceID}' not found in inventory.");
                _weapons.Remove(slot);
                _weaponCache.Remove(slot);
                return;
            }

            // Step 1: Holster if this weapon is currently drawn.
            if (_activeSlot.Value == slot)
                HolsterWeaponServer();

            // Step 2: Detach attachments — return them to inventory or keep on item (per config).
            bool returnAttachments = InventoryConfig == null || InventoryConfig.ReturnAttachmentsToInventoryOnDrop;
            if (inst.AttachedItems != null && inst.AttachedItems.Length > 0)
            {
                if (returnAttachments)
                    _attachmentSystem?.DetachAllFromItem(instanceID);
                // else: attachments remain in the AttachedItems array and drop with the weapon
            }

            ItemInstanceFactory.StripEmptyAttachmentSlots(inst);

            // Step 4: Build the world-item snapshot.
            // Use ToData() directly — preserves the original InstanceID and all current
            // runtime state (ammo, resource, durability, attachments) at this exact moment.
            // Weapons are always single-quantity items so no partial-drop logic is needed.
            var dropData     = inst.ToData();
            dropData.Quantity = inst.Quantity; // explicit, always 1 for weapons

            // Step 5: Calculate drop position (slightly in front of the player).
            Transform origin = transform;
            Vector3 dropPos  = origin.position + origin.forward * (InventoryConfig?.DropDistance ?? 2f);
            dropPos.y = origin.position.y;

            // Step 6: Spawn the world item.
            if (WorldSpawnManager.Instance != null)
                WorldSpawnManager.Instance.SpawnWorldItem(dropData, dropPos, Quaternion.identity);
            else
                Debug.LogError("[WeaponSystem] DropWeapon: WorldSpawnManager.Instance is null — item not spawned.");

            // Step 7: Remove from holster slot.
            _weapons.Remove(slot);
            _weaponCache.Remove(slot);
            // Fire event so host-mode StatApplyOrchestrator clears the weapon's stat modifiers.
            OnWeaponUnequipped?.Invoke(slot, inst);
            _inventorySystem.RemoveItem(instanceID, inst.Quantity);

            if (DebugLogs)
                Debug.Log($"[WeaponSystem] Dropped weapon '{inst.DefinitionID}' from slot {slot}.");
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

            // Rebuild server-side weapon cache after swap so cache stays consistent
            // with _weapons (host-mode: OnWeaponsChangedCallback.asServer=true is skipped).
            RebuildWeaponCache();
        }

        [Server]
        private void SelectWeaponServer(WeaponSlotType slot)
        {
            if (!_weapons.ContainsKey(slot)) return;
            if (_activeSlot.Value == slot) { HolsterWeaponServer(); return; }
            _activeSlot.Value = slot;
        }

        [Server]
        private void HolsterWeaponServer()
        {
            if (_activeSlot.Value == null) return;
            _activeSlot.Value = null;
        }

        // ── ServerRpc forwarding ───────────────────────────────────────────────

        [ServerRpc(RequireOwnership = true)]
        private void EquipWeaponServerRpc(string instanceID)
            => EquipWeaponServer(instanceID);

        [ServerRpc(RequireOwnership = true)]
        private void EquipWeaponToSlotServerRpc(string instanceID, WeaponSlotType targetSlot)
            => EquipWeaponToSlotServer(instanceID, targetSlot);

        [ServerRpc(RequireOwnership = true)]
        private void UnequipWeaponServerRpc(WeaponSlotType slot)
            => UnequipWeaponServer(slot);

        [ServerRpc(RequireOwnership = true)]
        private void DropWeaponServerRpc(WeaponSlotType slot)
            => DropWeaponServer(slot);

        [ServerRpc(RequireOwnership = true)]
        private void SwapWeaponsServerRpc(WeaponSlotType s1, WeaponSlotType s2)
            => SwapWeaponsServer(s1, s2);

        [ServerRpc(RequireOwnership = true)]
        private void SelectWeaponServerRpc(WeaponSlotType slot)
            => SelectWeaponServer(slot);

        [ServerRpc(RequireOwnership = true)]
        private void HolsterWeaponServerRpc()
            => HolsterWeaponServer();
    }
}
