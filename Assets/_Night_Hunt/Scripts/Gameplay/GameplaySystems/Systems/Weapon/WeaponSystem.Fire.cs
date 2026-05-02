using System.Collections;
using NightHunt.Data;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Weapon
{
    public partial class WeaponSystem
    {
        // -- IWeaponSystem Fire API ---------------------------------------------

        /// <summary>Begin firing (owner-side). Auto mode spawns the auto-fire coroutine.</summary>
        public void StartFire()
        {
            if (_isFiring) return;
            _isFiring = true;
            Debug.Log($"[WEAPON_FLOW] [02][Fire.Start] slot={_activeSlot.Value?.ToString() ?? "none"} mode={GetCurrentFireMode()} aim={_aimDirection:F2}");

            if (GetCurrentFireMode() == FireMode.Auto)
            {
                if (DebugLogs)
                    Debug.Log($"[FIRE_FLOW] WeaponSystem.StartFire AUTO: starting coroutine slot={_activeSlot.Value}");
                if (_autoFireCoroutine != null) StopCoroutine(_autoFireCoroutine);
                _autoFireCoroutine = StartCoroutine(AutoFireCoroutine());
            }
            else
            {
                if (DebugLogs)
                    Debug.Log($"[FIRE_FLOW] WeaponSystem.StartFire SINGLE: firing once slot={_activeSlot.Value}");
                TryFireOnce();
            }
        }

        /// <summary>Release fire input. Stops auto-fire.</summary>
        public void StopFire()
        {
            _isFiring = false;
            if (DebugLogs)
                Debug.Log("[FIRE_FLOW] WeaponSystem.StopFire");
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

            if (_currentWeaponBase != null && !_currentWeaponBase.AllowFireModeToggle)
            {
                _fireModes[slot.Value] = _currentWeaponBase.DefaultFireMode;
                return _currentWeaponBase.DefaultFireMode;
            }

            string weaponId = _weaponCache.TryGetValue(slot.Value, out var inst) ? inst.DefinitionID : "unknown";
            int saved = PlayerPrefs.GetInt($"firemode_{weaponId}_{(int)slot.Value}", -1);
            if (saved >= 0) { _fireModes[slot.Value] = (FireMode)saved; return (FireMode)saved; }

            var fallback = _currentWeaponBase?.DefaultFireMode ?? FireMode.Auto;
            _fireModes[slot.Value] = fallback;
            return fallback;
        }

        public void SetFireMode(FireMode mode)
        {
            var slot = _activeSlot.Value;
            if (slot == null) return;
            if (_currentWeaponBase != null && !_currentWeaponBase.AllowFireModeToggle)
                mode = _currentWeaponBase.DefaultFireMode;
            _fireModes[slot.Value] = mode;
            string weaponId = _weaponCache.TryGetValue(slot.Value, out var inst) ? inst.DefinitionID : "unknown";
            PlayerPrefs.SetInt($"firemode_{weaponId}_{(int)slot.Value}", (int)mode);
        }

        // -- Private fire internals ---------------------------------------------

        private IEnumerator AutoFireCoroutine()
        {
            float nextFireTime = Time.time;
            while (_isFiring)
            {
                int shotsThisFrame = 0;
                while (_isFiring && Time.time + 0.0001f >= nextFireTime && shotsThisFrame < 3)
                {
                    TryFireOnce();
                    shotsThisFrame++;

                    float delay = Mathf.Max(0.01f, GetCurrentFireDelay());
                    nextFireTime += delay;

                    // Avoid dumping a large stale backlog after a hitch or pause.
                    if (nextFireTime < Time.time - delay * 2f)
                        nextFireTime = Time.time + delay;
                }

                yield return null;
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
        /// Core single-shot logic. Deducts ammo, resolves the best target via registry,
        /// computes the real 3-D fire direction (with elevation), applies gun pitch, and
        /// delegates ballistics to WeaponBase.
        /// Owner-only; remote clients receive animation/VFX via NetworkSync RPCs.
        /// </summary>
        internal void TryFireOnce()
        {
            var slot = _activeSlot.Value;
            if (slot == null)
            {
                Debug.LogWarning("[FIRE_FLOW] TryFireOnce blocked: active slot is null.");
                return;
            }
            if (_isReloading)
            {
                Debug.Log($"[WEAPON_FLOW] [03][Fire.Blocked] reloading slot={slot.Value}");
                return;
            }
            if (!_weaponCache.TryGetValue(slot.Value, out var inst))
            {
                Debug.LogWarning($"[FIRE_FLOW] TryFireOnce blocked: no weapon cache for slot={slot.Value}.");
                return;
            }

            var weaponDef = ItemDatabase.GetDefinition(inst.DefinitionID) as WeaponDefinition;
            if (weaponDef != null && weaponDef.WeaponClass == WeaponClass.Melee)
            {
                FireMelee(slot.Value);
                return;
            }

            if (_currentWeaponBase == null)
            {
                Debug.LogWarning(
                    $"[WeaponSystem] TryFireOnce blocked: weapon model is not ready or has no WeaponBase. " +
                    $"Ammo was NOT consumed. Slot={slot.Value} item={inst.DefinitionID} GameObject={gameObject.name}",
                    gameObject);
                return;
            }

            // Ammo check.
            int mag = (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
            if (mag <= 0)
            {
                float reserve = inst.GetCurrentValue(ItemStatType.MaxAmmo);
                Debug.Log($"[WEAPON_FLOW] [03][Fire.EmptyMag] slot={slot.Value} reserve={reserve}");
                if (reserve > 0f) RequestReload();
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

            // Target acquisition and elevation.
            // 1. Fire origin. If the muzzle model still points behind the player on the
            // first frame of aiming, use a shoulder-level fallback in front of the root.
            Vector3 origin = ResolveFireOrigin(_aimDirection);

            // 2. Default: fire horizontally in the aim direction (as before).
            Vector3 finalFireDir    = _aimDirection;
            float   elevationAngle  = 0f;

            // 3. Query the registry for the best target within the acquisition cone.
            //    _bulletTargetConfig == null means registry disabled, pure physics raycast fallback.
            if (_bulletTargetConfig != null && BulletTargetRegistry.Instance != null)
            {
                float maxRange = _currentWeaponBase != null ? _currentWeaponBase.MaxRange : 150f;

                var result = BulletTargetRegistry.FindBestTarget(
                    origin, _aimDirection, maxRange, _bulletTargetConfig);

                if (result.HasTarget)
                {
                    // Real 3D direction from muzzle to target centre, including Y.
                    finalFireDir = (result.HitPoint - origin).normalized;

                    // Elevation angle = angle between horizontal plane and the 3D fire direction.
                    // Mathf.Asin gives degrees from the XZ plane.
                    // Clamped to [-1, 1] to avoid NaN on floating-point rounding near +/-1.
                    elevationAngle = Mathf.Asin(Mathf.Clamp(finalFireDir.y, -1f, 1f))
                                   * Mathf.Rad2Deg;

                    if (DebugLogs)
                        Debug.Log($"[WeaponSystem] Acquired: {result.Target.TargetType} " +
                                  $"angle={result.AngleDeg:F1} deg elev={elevationAngle:F1} deg");
                }
            }

            // 4. Apply elevation to weapon model (gun pitches up/down visually).
            //    Also stores _currentElevationAngle for network broadcast.
            _weaponModelController?.SetElevationAngle(elevationAngle);
            _currentElevationAngle = elevationAngle;

            Vector3 ballisticFireDir = ClampFireElevation(finalFireDir, _aimDirection, _maxFireElevationAngle);

            // Fire events and ballistics.
            // Raise local fire event (drives VFX + animation on owner).
            OnShotFired?.Invoke(slot.Value, _aimDirection);

            // Delegate ballistics to weapon prefab component.
            // finalFireDir has correct 3-D elevation; spread is applied inside WeaponBase.
            if (_currentWeaponBase != null)
            {
                var config = BuildWeaponConfigData(inst);
                config.ApplyDamage = false;
                Vector3 visualFireDir = ApplyClientRecoil(ballisticFireDir, config);
                LogProjectile($"TryFireOnce local shot slot={slot.Value} weaponId='{inst.DefinitionID}' origin={origin:F2} dir={visualFireDir:F2} intentDir={ballisticFireDir:F2} mag={(int)inst.GetCurrentValue(ItemStatType.MagazineSize)} reserve={(int)inst.GetCurrentValue(ItemStatType.MaxAmmo)} speed={config.ProjectileSpeed:F1} endpointBefore={_lastFireEndpoint:F2}");
                _currentWeaponBase.Fire(origin, visualFireDir, config, (int)ObjectId);

                if (IsServerInitialized)
                    ResolveAuthoritativeShotServer(slot.Value, ballisticFireDir, consumeAmmo: false);
                else if (IsOwner)
                    RequestAuthoritativeShotServerRpc(slot.Value, ballisticFireDir);

                // Spawn projectile visual on remote clients using the clamped 3D direction.
                // For hitscan weapons, _lastFireEndpoint is the raycast hit point or max range.
                if (IsOwner && _currentWeaponBase.ProjectilePrefab != null)
                {

                    LogProjectile($"BroadcastProjectileServerRpc origin={origin:F2} dir={visualFireDir:F2} endpoint={_lastFireEndpoint:F2} weaponId='{config.WeaponId}'");
                    BroadcastProjectileServerRpc(origin, visualFireDir, config, _lastFireEndpoint);
                }
                else if (IsOwner && _currentWeaponBase.ProjectilePrefab == null)
                {
                    Debug.LogWarning($"[SHOOT.PLAYER] TryFireOnce: ProjectilePrefab is NULL on '{_currentWeaponBase.gameObject.name}'. " +
                                     $"Remote clients will NOT see bullet visual. Assign prefab in weapon model component.");
                }
            }
            else
            {
                // â”€â”€ DIAGNOSTIC â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // Diagnostic: animation and ammo updates happen but no bullet is fired.
                // Root cause: the weapon model prefab is missing a HitscanWeapon or
                // ProjectileWeapon component, OR the model hasn't finished loading yet.
                //
                // Fix checklist:
                //   1. Open the weapon's VisualPrefab in the Project window.
                //   2. Add HitscanWeapon (rifles/pistols) OR ProjectileWeapon (rockets) component.
                //   3. Assign 'Fire Point' (muzzle Transform child) on that component.
                //   4. Assign 'Projectile Prefab' (a ProjectileComponent prefab) on that component.
                //   5. Optionally assign 'Left Hand IK Target' for the IK grip anchor.
                Debug.LogWarning(
                    $"[WeaponSystem] TryFireOnce: _currentWeaponBase is NULL. " +
                    $"weapon model has no HitscanWeapon / ProjectileWeapon component, " +
                    $"or WeaponModelController hasn't finished loading the model yet. " +
                    $"Slot={slot.Value}  GameObject={gameObject.name}",
                    gameObject);
            }

            // Broadcast shot to remote clients: horizontal aim dir (for body rotation) +
            // elevation angle (for gun pitch on their weapon model).
            if (IsOwner)
                BroadcastShotFiredServerRpc(slot.Value, _aimDirection, elevationAngle);
        }

        private Vector3 ResolveFireOrigin(Vector3 aimDirection)
        {
            Transform root = transform.root != null ? transform.root : transform;
            Vector3 origin = _fireOrigin != null ? _fireOrigin.position : root.position;

            Vector3 flatAim = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
            if (flatAim.sqrMagnitude <= 0.0001f)
                flatAim = Vector3.ProjectOnPlane(root.forward, Vector3.up);
            if (flatAim.sqrMagnitude <= 0.0001f)
                flatAim = Vector3.forward;
            flatAim.Normalize();

            Vector3 flatToOrigin = Vector3.ProjectOnPlane(origin - root.position, Vector3.up);
            bool muzzleBehindRoot = Vector3.Dot(flatToOrigin, flatAim) < -0.05f;
            if (!muzzleBehindRoot)
                return origin;

            Vector3 fallback = root.position + Vector3.up * 1.25f + flatAim * 0.7f;
            LogProjectile($"ResolveFireOrigin fallback: muzzle was behind root. raw={origin:F2} fallback={fallback:F2} aim={flatAim:F2} root={root.position:F2}");
            return fallback;
        }

        private void FireMelee(WeaponSlotType slot)
        {
            // Melee has no magazine/projectile path. This event drives:
            // - CharacterAnimationController Shoot/melee attack trigger
            // - CharacterAnimationController melee fallback hit delay
            // - WeaponAudioController swing/attack sound through weapon profile
            OnShotFired?.Invoke(slot, _aimDirection);

            if (IsOwner)
                BroadcastShotFiredServerRpc(slot, _aimDirection, 0f);
        }

        private WeaponConfigData BuildWeaponConfigData(ItemInstance inst)
        {
            // DamageHeadMul is on HitscanWeapon only; projectile weapons use the default.
            float headMul = 2f;
            if (_currentWeaponBase is HitscanWeapon hw)
                headMul = hw.DamageHeadMultiplier;

            var weaponDef = ItemDatabase.GetDefinition(inst.DefinitionID) as WeaponDefinition;
            bool isProjectileWeapon = weaponDef != null
                ? weaponDef.WeaponClass == WeaponClass.Launcher
                : _currentWeaponBase is ProjectileWeapon;
            bool isHitscan = !isProjectileWeapon;

            return new WeaponConfigData
            {
                WeaponId        = inst.DefinitionID,
                DisplayName     = inst.DefinitionID,
                BallisticType   = isHitscan ? "Hitscan" : "Projectile",
                DamageBody      = (int)inst.GetComputedStat(ItemStatType.Damage),
                DamageHeadMul   = headMul,
                FireRate        = inst.GetComputedStat(ItemStatType.FireRate),
                ReloadTime      = inst.GetComputedStat(ItemStatType.ReloadSpeed),
                MagazineSize    = (int)inst.GetComputedStat(ItemStatType.MagazineSize),
                ReserveAmmo     = (int)inst.GetCurrentValue(ItemStatType.MaxAmmo),
                ProjectileSpeed   = _currentWeaponBase?.ProjectileSpeed ?? 50f,
                MaxRange          = _currentWeaponBase?.MaxRange        ?? 150f,
                GravityScale      = _currentWeaponBase?.GravityScale    ?? 0f,
                // Clamp bullet visual travel to the owner's VisionRange so projectiles
                // never fly beyond the visible circle (same radius used by AimSystem).
                VisionRangeClamp  = _statSystem != null
                    ? _statSystem.GetStat(NightHunt.Gameplay.StatSystem.Core.Types.PlayerStatType.VisionRange)
                    : 0f,
                ApplyDamage       = true,
                SpreadBase      = inst.GetComputedStat(ItemStatType.SpreadBase),
                SpreadMoveMul   = inst.GetComputedStat(ItemStatType.SpreadPenalty),
                RecoilHorizontal = inst.GetComputedStat(ItemStatType.RecoilHorizontal),
                RecoilVertical   = inst.GetComputedStat(ItemStatType.RecoilVertical),
            };
        }

        private Vector3 ApplyClientRecoil(Vector3 direction, WeaponConfigData config)
        {
            float spread = config.SpreadBase;
            if (_currentWeaponBase != null)
                spread = Mathf.Max(_currentWeaponBase.CurrentSpread, _currentWeaponBase.SpreadBase);

            Vector2 recoil = DamageCalculator.CalculateRecoil(config, spread);
            if (recoil.sqrMagnitude <= 0.0001f)
                return direction.normalized;

            Vector3 recoiled = DamageCalculator.ApplyRecoil(direction.normalized, recoil);
            return ClampFireElevation(recoiled, direction, _maxFireElevationAngle);
        }

        private static Vector3 ClampFireElevation(Vector3 requestedDirection, Vector3 fallbackDirection, float maxElevationAngle)
        {
            Vector3 dir = requestedDirection.sqrMagnitude > 0.0001f
                ? requestedDirection.normalized
                : fallbackDirection.normalized;

            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (flat.sqrMagnitude <= 0.0001f)
                flat = Vector3.ProjectOnPlane(fallbackDirection, Vector3.up);

            if (flat.sqrMagnitude <= 0.0001f)
                return dir;

            flat.Normalize();

            float currentElevation = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            float clampedElevation = Mathf.Clamp(currentElevation, -maxElevationAngle, maxElevationAngle);
            if (Mathf.Approximately(currentElevation, clampedElevation))
                return dir;

            float radians = clampedElevation * Mathf.Deg2Rad;
            return (flat * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians)).normalized;
        }

        private void HandleWeaponFireResult(Vector3 origin, Vector3 endpoint)
        {
            // Cache the endpoint so TryFireOnce can pass it to BroadcastProjectileServerRpc
            // AFTER Fire() returns.  HandleWeaponFireResult is called FROM inside Fire() via
            // the OnFireResult event, which means _lastFireEndpoint is fully populated before
            // BroadcastProjectileServerRpc is reached.
            _lastFireEndpoint = endpoint;
            var slot = _activeSlot.Value;
            if (slot != null) OnHitscanResult?.Invoke(slot.Value, origin, endpoint);
        }
    }
}
