using System.Collections;
using NightHunt.Data;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Weapon
{
    public partial class WeaponSystem
    {
        // ── IWeaponSystem — Fire API ───────────────────────────────────────────

        /// <summary>Begin firing (owner-side). Auto mode spawns the auto-fire coroutine.</summary>
        public void StartFire()
        {
            if (_isFiring) return;
            _isFiring = true;

            if (GetCurrentFireMode() == FireMode.Auto)
            {
                if (_autoFireCoroutine != null) StopCoroutine(_autoFireCoroutine);
                _autoFireCoroutine = StartCoroutine(AutoFireCoroutine());
            }
            else
            {
                TryFireOnce();
            }
        }

        /// <summary>Release fire input — stops auto-fire.</summary>
        public void StopFire()
        {
            _isFiring = false;
            if (_autoFireCoroutine != null)
            {
                StopCoroutine(_autoFireCoroutine);
                _autoFireCoroutine = null;
            }
        }

        /// <summary>Current aim direction. Updated every frame by CombatInputHandler.</summary>
        public void SetAimDirection(Vector3 worldDir)
        {
            _aimDirection = worldDir.sqrMagnitude > 0.001f ? worldDir.normalized : Vector3.forward;
        }

        public Vector3 GetAimDirection() => _aimDirection;

        /// <summary>Called by WeaponModelController after each weapon swap.</summary>
        public void SetFireOrigin(Transform muzzle) => _fireOrigin = muzzle;

        /// <summary>Called by WeaponModelController after each weapon swap.</summary>
        public void SetCurrentWeaponBase(WeaponBase wb)
        {
            if (_currentWeaponBase != null)
                _currentWeaponBase.OnFireResult -= HandleWeaponFireResult;

            _currentWeaponBase = wb;

            if (_currentWeaponBase != null)
                _currentWeaponBase.OnFireResult += HandleWeaponFireResult;
        }

        public FireMode GetCurrentFireMode()
        {
            var slot = _activeSlot.Value;
            if (slot == null) return FireMode.Auto;
            if (_fireModes.TryGetValue(slot.Value, out var mode)) return mode;

            int saved = PlayerPrefs.GetInt($"firemode_{(int)slot.Value}", -1);
            if (saved >= 0) { _fireModes[slot.Value] = (FireMode)saved; return (FireMode)saved; }

            var fallback = _currentWeaponBase?.DefaultFireMode ?? FireMode.Auto;
            _fireModes[slot.Value] = fallback;
            return fallback;
        }

        public void SetFireMode(FireMode mode)
        {
            var slot = _activeSlot.Value;
            if (slot == null) return;
            _fireModes[slot.Value] = mode;
            PlayerPrefs.SetInt($"firemode_{(int)slot.Value}", (int)mode);
        }

        // ── Private fire internals ─────────────────────────────────────────────

        private IEnumerator AutoFireCoroutine()
        {
            while (_isFiring)
            {
                TryFireOnce();
                yield return new WaitForSeconds(GetCurrentFireDelay());
            }
            _autoFireCoroutine = null;
        }

        private float GetCurrentFireDelay()
        {
            var slot = _activeSlot.Value;
            if (slot == null) return 0.1f;
            if (_weaponCache.TryGetValue(slot.Value, out var inst))
            {
                float rpm = inst.GetComputedStat(ItemStatType.FireRate);
                if (rpm > 0f) return 60f / rpm;
            }
            return 0.1f;
        }

        /// <summary>
        /// Core single-shot logic. Deducts ammo, raises events, delegates ballistics to WeaponBase.
        /// Owner-only; remote clients receive animation/VFX via NetworkSync RPCs.
        /// </summary>
        internal void TryFireOnce()
        {
            var slot = _activeSlot.Value;
            if (slot == null || _isReloading) return;
            if (!_weaponCache.TryGetValue(slot.Value, out var inst)) return;

            // Ammo check.
            int mag = (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
            if (mag <= 0)
            {
                float reserve = inst.GetCurrentValue(ItemStatType.MaxAmmo);
                if (reserve > 0f) StartCoroutine(ReloadCoroutine(slot.Value, inst));
                else              OnWeaponDepleted?.Invoke(slot.Value);
                return;
            }

            // Deduct ammo locally (server-auth ammo is handled separately if needed).
            inst.AdjustCurrentValue(ItemStatType.MagazineSize, -1f);
            float magCap = inst.GetComputedStat(ItemStatType.MagazineSize);
            OnAmmoChanged?.Invoke(
                (int)inst.GetCurrentValue(ItemStatType.MagazineSize),
                (int)inst.GetCurrentValue(ItemStatType.MaxAmmo),
                (int)magCap);

            // Raise local fire event (drives VFX + animation on owner).
            OnShotFired?.Invoke(slot.Value, _aimDirection);

            // Delegate ballistics to weapon prefab component.
            if (_currentWeaponBase != null)
            {
                Vector3 origin = _fireOrigin != null ? _fireOrigin.position : transform.position;
                var config     = BuildWeaponConfigData(inst);
                _currentWeaponBase.Fire(origin, _aimDirection, config, (int)ObjectId);

                // Spawn projectile visual on remote clients.
                if (IsOwner && _currentWeaponBase.ProjectilePrefab != null)
                    BroadcastProjectileServerRpc(origin, _aimDirection, config);
            }

            // Broadcast shot event to remote clients (animation + VFX).
            if (IsOwner)
                BroadcastShotFiredServerRpc(slot.Value, _aimDirection);
        }

        private WeaponConfigData BuildWeaponConfigData(ItemInstance inst)
        {
            return new WeaponConfigData
            {
                WeaponId        = inst.DefinitionID,
                DisplayName     = inst.DefinitionID,
                BallisticType   = _currentWeaponBase?.BallisticType.ToString() ?? "Hitscan",
                DamageBody      = (int)inst.GetComputedStat(ItemStatType.Damage),
                DamageHeadMul   = _currentWeaponBase?.DamageHeadMultiplier ?? 2f,
                FireRate        = inst.GetComputedStat(ItemStatType.FireRate),
                ReloadTime      = inst.GetComputedStat(ItemStatType.ReloadSpeed),
                MagazineSize    = (int)inst.GetComputedStat(ItemStatType.MagazineSize),
                ReserveAmmo     = (int)inst.GetCurrentValue(ItemStatType.MaxAmmo),
                ProjectileSpeed = _currentWeaponBase?.ProjectileSpeed ?? 50f,
                MaxRange        = _currentWeaponBase?.MaxRange        ?? 150f,
                GravityScale    = _currentWeaponBase?.GravityScale    ?? 0f,
                SpreadBase      = inst.GetComputedStat(ItemStatType.SpreadBase),
            };
        }

        private void HandleWeaponFireResult(Vector3 origin, Vector3 endpoint)
        {
            var slot = _activeSlot.Value;
            if (slot != null) OnHitscanResult?.Invoke(slot.Value, origin, endpoint);
        }
    }
}