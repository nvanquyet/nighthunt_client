using System.Collections;
using FishNet.Object;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Weapon
{
    public partial class WeaponSystem
    {
        // -- IWeaponSystem � Reload API -----------------------------------------

        /// <summary>Owner-client calls this (e.g. press R). Runs reload coroutine locally,
        /// and broadcasts state change to remote clients via NetworkSync.</summary>
        public void RequestReload()
        {
            var slot = _activeSlot.Value;
            Debug.Log($"[WEAPON_FLOW] [01][Reload.Request] slot={slot?.ToString() ?? "none"} reloading={_isReloading} isServer={IsServerInitialized} isOwner={IsOwner}");
            if (slot == null || _isReloading) return;
            if (!_weaponCache.TryGetValue(slot.Value, out var inst))
            {
                Debug.LogWarning($"[WEAPON_FLOW] [01][Reload.Blocked] no weapon cache slot={slot.Value}");
                return;
            }
            if (!CanReload(slot.Value))
            {
                Debug.Log($"[WEAPON_FLOW] [01][Reload.Blocked] CanReload=false slot={slot.Value} mag={(int)inst.GetCurrentValue(ItemStatType.MagazineSize)} reserve={(int)inst.GetCurrentValue(ItemStatType.MaxAmmo)}");
                return;
            }

            if (IsServerInitialized)
            {
                StartCoroutine(ReloadCoroutine(slot.Value, inst, syncInventoryOnComplete: true, broadcastRemoteState: true));
                return;
            }

            if (!IsOwner)
                return;

            RequestReloadServerRpc(slot.Value);
            StartCoroutine(ReloadCoroutine(slot.Value, inst));
        }

        [ServerRpc(RequireOwnership = true)]
        private void RequestReloadServerRpc(WeaponSlotType slot)
        {
            if (_isReloading)
                return;

            if (!_weapons.TryGetValue(slot, out var id))
                return;

            var inst = _inventorySystem?.GetItemByInstanceID(id);
            if (inst == null || !CanReload(slot))
                return;

            Debug.Log($"[WEAPON_FLOW] [02][Reload.ServerRpc] slot={slot} owner={Owner?.ClientId}");
            StartCoroutine(ReloadCoroutine(slot, inst, syncInventoryOnComplete: true, broadcastRemoteState: true));
        }

        public int   GetCurrentMagazine(WeaponSlotType slot) =>
            GetWeapon(slot) is { } w ? (int)w.GetCurrentValue(ItemStatType.MagazineSize) : 0;

        public float GetTotalAmmo(WeaponSlotType slot) =>
            GetWeapon(slot)?.GetCurrentValue(ItemStatType.MaxAmmo) ?? 0f;

        public bool CanReload(WeaponSlotType slot)
        {
            if (!_weapons.TryGetValue(slot, out var id)) return false;
            var w = _inventorySystem?.GetItemByInstanceID(id);
            if (w == null) return false;
            if (w.GetCurrentValue(ItemStatType.MaxAmmo) <= 0f) return false;

            int magCap = Mathf.RoundToInt(
                (ItemDatabase.GetDefinition(w.DefinitionID) as WeaponDefinition)
                    ?.GetStatValue(ItemStatType.MagazineSize) ?? 30f);
            bool tacticalOk = _currentWeaponBase?.CanTacticalReload ?? true;
            if ((int)w.GetCurrentValue(ItemStatType.MagazineSize) >= magCap && !tacticalOk)
                return false;
            return true;
        }

        // -- Server-side forced reload (called by server code / admin) ----------
        public void Reload(WeaponSlotType slot)
        {
            if (!IsServerInitialized) return;
            if (!_weapons.TryGetValue(slot, out var id)) return;
            var w = _inventorySystem?.GetItemByInstanceID(id);
            if (w == null) return;
            var def = ItemDatabase.GetDefinition(w.DefinitionID) as WeaponDefinition;
            if (def == null) return;

            int magCap    = Mathf.RoundToInt(def.GetStatValue(ItemStatType.MagazineSize));
            if (magCap <= 0) magCap = 30;
            int current   = (int)w.GetCurrentValue(ItemStatType.MagazineSize);
            int needed    = magCap - current;
            int available = Mathf.FloorToInt(w.GetCurrentValue(ItemStatType.MaxAmmo));
            int actual    = Mathf.Min(needed, available);
            if (actual <= 0) return;

            w.AdjustCurrentValue(ItemStatType.MagazineSize, actual);
            w.AdjustCurrentValue(ItemStatType.MaxAmmo, -actual);
            int newMag = (int)w.GetCurrentValue(ItemStatType.MagazineSize);
            OnWeaponReloaded?.Invoke(slot, newMag);
            _inventorySystem?.SyncItemState(w.InstanceID);
        }

        // -- Internal coroutine -------------------------------------------------

        /// <summary>
        /// Runs on the owner client for prediction and on the server for authoritative ammo state.
        /// The server pass broadcasts reload state to remote clients.
        /// </summary>
        internal IEnumerator ReloadCoroutine(
            WeaponSlotType slot,
            ItemInstance inst,
            bool syncInventoryOnComplete = false,
            bool broadcastRemoteState = false)
        {
            if (_isReloading) yield break;

            // NOTE: Reserve ammo is initialised once in AssignToSlot (first equip).
            // Do NOT lazy-reinit here — a gun that deliberately ran out of reserve must stay empty.
            float magCap     = inst.GetComputedStat(ItemStatType.MagazineSize);
            float reloadTime = inst.GetComputedStat(ItemStatType.ReloadSpeed);
            if (reloadTime <= 0f) reloadTime = 2.5f;

            int needed = (int)magCap - (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
            if (needed <= 0) yield break;

            // -- Start reload --
            _isReloading = true;
            Debug.Log($"[WEAPON_FLOW] [03][Reload.Start] slot={slot} duration={reloadTime:F2} mag={(int)inst.GetCurrentValue(ItemStatType.MagazineSize)} reserve={(int)inst.GetCurrentValue(ItemStatType.MaxAmmo)}");
            OnReloadStateChanged?.Invoke(true);
            if (broadcastRemoteState && IsServerInitialized)
                BroadcastReloadStateObserversRpc(true);

            yield return new WaitForSeconds(reloadTime);

            // -- Complete --
            int reserve = (int)inst.GetCurrentValue(ItemStatType.MaxAmmo);
            int actual  = Mathf.Min(needed, reserve);
            inst.AdjustCurrentValue(ItemStatType.MagazineSize, actual);
            inst.AdjustCurrentValue(ItemStatType.MaxAmmo,     -actual);

            int newMag = (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
            OnWeaponReloaded?.Invoke(slot, newMag);
            OnAmmoChanged?.Invoke(newMag, (int)inst.GetCurrentValue(ItemStatType.MaxAmmo), (int)magCap);

            if (syncInventoryOnComplete)
                _inventorySystem?.SyncItemState(inst.InstanceID);

            _isReloading = false;
            Debug.Log($"[WEAPON_FLOW] [04][Reload.Done] slot={slot} mag={newMag}/{(int)magCap} reserve={(int)inst.GetCurrentValue(ItemStatType.MaxAmmo)}");
            OnReloadStateChanged?.Invoke(false);
            if (broadcastRemoteState && IsServerInitialized)
                BroadcastReloadStateObserversRpc(false);

            if (DebugLogs) Debug.Log($"[WeaponSystem] Reload done: {newMag}/{magCap}");
        }
    }
}
