using System.Collections;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Weapon
{
    public partial class WeaponSystem
    {
        // -- IWeaponSystem — Reload API -----------------------------------------

        /// <summary>Owner-client calls this (e.g. press R). Runs reload coroutine locally,
        /// and broadcasts state change to remote clients via NetworkSync.</summary>
        public void RequestReload()
        {
            var slot = _activeSlot.Value;
            if (slot == null || _isReloading) return;
            if (!_weaponCache.TryGetValue(slot.Value, out var inst)) return;
            StartCoroutine(ReloadCoroutine(slot.Value, inst));
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
        }

        // -- Internal coroutine -------------------------------------------------

        /// <summary>
        /// Runs on the owner client. Broadcasts reload state to all remote clients
        /// via BroadcastReloadStateServerRpc so their animators stay in sync.
        /// </summary>
        internal IEnumerator ReloadCoroutine(WeaponSlotType slot, ItemInstance inst)
        {
            if (_isReloading) yield break;

            // Lazy-init reserve ammo.
            if (inst.GetCurrentValue(ItemStatType.MaxAmmo) == 0f)
            {
                float max = inst.GetComputedStat(ItemStatType.MaxAmmo);
                if (max > 0f) inst.SetCurrentValue(ItemStatType.MaxAmmo, max);
                else { Debug.LogError($"[WeaponSystem] MaxAmmo stat missing for '{inst.DefinitionID}'."); }
            }

            float magCap     = inst.GetComputedStat(ItemStatType.MagazineSize);
            float reloadTime = inst.GetComputedStat(ItemStatType.ReloadSpeed);
            if (reloadTime <= 0f) reloadTime = 2.5f;

            int needed = (int)magCap - (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
            if (needed <= 0) yield break;

            // -- Start reload --
            _isReloading = true;
            OnReloadStateChanged?.Invoke(true);
            if (IsOwner) BroadcastReloadStateServerRpc(true);     // ? remote animators

            yield return new WaitForSeconds(reloadTime);

            // -- Complete --
            int reserve = (int)inst.GetCurrentValue(ItemStatType.MaxAmmo);
            int actual  = Mathf.Min(needed, reserve);
            inst.AdjustCurrentValue(ItemStatType.MagazineSize, actual);
            inst.AdjustCurrentValue(ItemStatType.MaxAmmo,     -actual);

            int newMag = (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
            OnWeaponReloaded?.Invoke(slot, newMag);
            OnAmmoChanged?.Invoke(newMag, (int)inst.GetCurrentValue(ItemStatType.MaxAmmo), (int)magCap);

            _isReloading = false;
            OnReloadStateChanged?.Invoke(false);
            if (IsOwner) BroadcastReloadStateServerRpc(false);    // ? remote animators

            if (DebugLogs) Debug.Log($"[WeaponSystem] Reload done: {newMag}/{magCap}");
        }
    }
}