using UnityEngine;
using FishNet.Object;
using NightHunt.Data;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Character.Combat.Weapons;

namespace NightHunt.GameplaySystems.Weapon
{
    /// <summary>
    /// All FishNet RPCs for WeaponSystem. Three sync channels:
    ///
    ///   1. AIM DIRECTION  — owner pushes at 20 Hz; drives remote character body rotation.
    ///   2. SHOT FIRED     — owner pushes per shot; fires remote animator trigger + VFX event.
    ///   3. RELOAD STATE   — owner pushes start/end; drives remote animator reload bool.
    ///   4. PROJECTILE VFX — server broadcasts visual-only projectile to all observers.
    ///
    /// Remote clients never call TryFireOnce() — they only receive animation/VFX events.
    /// Damage is always computed on the authoritative side (owner or server hitscan).
    /// </summary>
    public partial class WeaponSystem
    {
        // ── 1. Aim direction sync (20 Hz) ──────────────────────────────────────
        private float _aimSyncTimer;

        private void Update()
        {
            if (!IsOwner) return;

            _aimSyncTimer += Time.deltaTime;
            if (_aimSyncTimer >= _aimSyncInterval)
            {
                _aimSyncTimer = 0f;
                SyncAimDirectionServerRpc(_aimDirection);
            }
        }

        [ServerRpc(RequireOwnership = true, DataLength = 12)] // Vector3 = 12 bytes
        private void SyncAimDirectionServerRpc(Vector3 dir)
            => BroadcastAimDirectionObserversRpc(dir);

        /// <summary>ExcludeOwner=true — owner already has the value locally.</summary>
        [ObserversRpc(ExcludeOwner = true, BufferLast = true)]
        private void BroadcastAimDirectionObserversRpc(Vector3 dir)
        {
            _aimDirection = dir;
            // CharacterAnimationController reads _aimDirection via GetAimDirection() each frame —
            // no extra event needed here.
        }

        // ── 2. Shot fired broadcast ────────────────────────────────────────────

        [ServerRpc(RequireOwnership = true)]
        internal void BroadcastShotFiredServerRpc(WeaponSlotType slot, Vector3 dir, float elevationAngle)
            => BroadcastShotFiredObserversRpc(slot, dir, elevationAngle);

        /// <summary>
        /// Fires OnShotFired on every remote client so:
        ///   • CharacterAnimationController triggers the "Shoot" animator parameter.
        ///   • WeaponVFXController flashes the aim trail.
        ///   • WeaponModelController applies the elevation pitch so the remote gun
        ///     visually tilts up/down to match the shooter's actual fire angle.
        ///
        /// dir = horizontal aim direction (for body rotation / animator).
        /// elevationAngle = pitch degrees applied to the weapon model only (not body).
        /// </summary>
        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastShotFiredObserversRpc(WeaponSlotType slot, Vector3 dir, float elevationAngle)
        {
            _aimDirection = dir;              // keep horizontal aim in sync at shot moment
            OnShotFired?.Invoke(slot, dir);   // → CharacterAnimationController, WeaponVFXController

            // Apply gun pitch on this remote client's weapon model.
            _weaponModelController?.SetElevationAngle(elevationAngle);
            _currentElevationAngle = elevationAngle;

            // Play muzzle flash on remote client's local weapon model instance.
            _currentWeaponBase?.PlayMuzzleFlash();
        }

        // ── 3. Reload state broadcast ──────────────────────────────────────────

        [ServerRpc(RequireOwnership = true)]
        internal void BroadcastReloadStateServerRpc(bool isReloading)
            => BroadcastReloadStateObserversRpc(isReloading);

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastReloadStateObserversRpc(bool isReloading)
        {
            _isReloading = isReloading;
            OnReloadStateChanged?.Invoke(isReloading); // → CharacterAnimationController
        }

        // ── 4. Projectile VFX broadcast ────────────────────────────────────────

        [ServerRpc(RequireOwnership = true)]
        internal void BroadcastProjectileServerRpc(Vector3 origin, Vector3 direction, WeaponConfigData config)
            => ShowProjectileOnClientsRpc(origin, direction, config);

        [ObserversRpc]
        private void ShowProjectileOnClientsRpc(Vector3 origin, Vector3 direction, WeaponConfigData config)
        {
            // Owner already spawned the authoritative projectile locally.
            if (IsOwner) return;

            var pool = ProjectilePool.Instance;
            if (pool == null || _currentWeaponBase?.ProjectilePrefab == null) return;

            var proj = pool.Get(_currentWeaponBase.ProjectilePrefab, origin, Quaternion.LookRotation(direction));
            bool isHitscan = config.BallisticType == "Hitscan";
            proj?.Initialize(config, direction, useHitscan: isHitscan);
        }
    }
}