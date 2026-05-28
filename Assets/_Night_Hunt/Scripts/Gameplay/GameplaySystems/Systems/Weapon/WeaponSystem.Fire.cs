using System.Collections;
using FishNet.Managing.Timing;
using NightHunt.Data;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Diagnostics;

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
            PhaseTestLog.Log(
                PhaseTestLogCategory.Weapon,
                "FireStart",
                $"obj={ObjectId} owner={Owner?.ClientId} slot={_activeSlot.Value?.ToString() ?? "none"} mode={GetCurrentFireMode()} aim={_aimDirection:F2}",
                this);

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
            PhaseTestLog.Log(
                PhaseTestLogCategory.Weapon,
                "FireStop",
                $"obj={ObjectId} owner={Owner?.ClientId} slot={_activeSlot.Value?.ToString() ?? "none"}",
                this);
            if (_autoFireCoroutine != null)
            {
                StopCoroutine(_autoFireCoroutine);
                _autoFireCoroutine = null;
            }
        }

        /// <summary>Current aim direction. Updated every frame by CombatInputHandler.</summary>
        public void SetAimDirection(Vector3 worldDir)
        {
            _aimDirection = ResolveFlatFireDirection(worldDir);
        }

        public Vector3 GetAimDirection() => _aimDirection;

        private Vector3 ResolveFlatFireDirection(Vector3 requestedDirection)
        {
            Vector3 flat = Vector3.ProjectOnPlane(requestedDirection, Vector3.up);
            if (flat.sqrMagnitude <= 0.001f)
                flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flat.sqrMagnitude <= 0.001f)
                flat = Vector3.forward;
            return flat.normalized;
        }

        /// <summary>Called by WeaponModelController after each weapon swap.</summary>
        public void SetFireOrigin(Transform muzzle)
        {
            _fireOrigin = muzzle;
            CaptureFireOriginSnapshot(muzzle);
            LogProjectile($"SetFireOrigin muzzle={(muzzle != null ? muzzle.name : "null")} pos={(muzzle != null ? muzzle.position.ToString("F2") : "null")}");
        }

        /// <summary>Called by WeaponModelController after each weapon swap.</summary>
        public void SetCurrentWeaponBase(WeaponBase wb)
        {
            if (_currentWeaponBase != null)
                _currentWeaponBase.OnFireResultDetailed -= HandleWeaponFireResult;

            _currentWeaponBase = wb;

            if (_currentWeaponBase != null)
            {
                _currentWeaponBase.OnFireResultDetailed += HandleWeaponFireResult;
                if (_currentWeaponBase.FirePoint != null &&
                    (_fireOrigin == null || !_fireOrigin.IsChildOf(_currentWeaponBase.transform)))
                    SetFireOrigin(_currentWeaponBase.FirePoint);
            }
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
                PhaseTestLog.Warning(PhaseTestLogCategory.Weapon, "FireBlocked", "reason=active-slot-null", this);
                return;
            }
            if (_isReloading)
            {
                Debug.Log($"[WEAPON_FLOW] [03][Fire.Blocked] reloading slot={slot.Value}");
                PhaseTestLog.Log(PhaseTestLogCategory.Weapon, "FireBlocked", $"reason=reloading slot={slot.Value}", this);
                return;
            }
            if (!_weaponCache.TryGetValue(slot.Value, out var inst))
            {
                Debug.LogWarning($"[FIRE_FLOW] TryFireOnce blocked: no weapon cache for slot={slot.Value}.");
                PhaseTestLog.Warning(PhaseTestLogCategory.Weapon, "FireBlocked", $"reason=weapon-cache-miss slot={slot.Value}", this);
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
                PhaseTestLog.Log(PhaseTestLogCategory.Weapon, "FireBlocked", $"reason=empty-mag slot={slot.Value} reserve={reserve:F0} weapon={inst.DefinitionID}", this);
                if (reserve > 0f) RequestReload();
                else              OnWeaponDepleted?.Invoke(slot.Value);
                return;
            }

            // Deduct ammo locally (server-auth ammo is handled separately if needed).
            int ammoCost = ResolveAmmoCostPerShot(inst, mag);
            inst.AdjustCurrentValue(ItemStatType.MagazineSize, -ammoCost);
            float magCap = inst.GetComputedStat(ItemStatType.MagazineSize);
            OnAmmoChanged?.Invoke(
                (int)inst.GetCurrentValue(ItemStatType.MagazineSize),
                (int)inst.GetCurrentValue(ItemStatType.MaxAmmo),
                (int)magCap);

            // Target acquisition and elevation.
            // 1. Fire origin. If the muzzle model still points behind the player on the
            // first frame of aiming, use a shoulder-level fallback in front of the root.
            Vector3 shotAimDir = ResolveFlatFireDirection(_aimDirection);
            Vector3 origin = ResolveFireOrigin(shotAimDir);

            // 2. Default: fire horizontally in the aim direction (as before).
            Vector3 finalFireDir    = shotAimDir;
            float   elevationAngle  = 0f;
            Vector3? projectileTargetPoint = null;

            // 3. Query the registry for the best target within the acquisition cone.
            //    _bulletTargetConfig == null means registry disabled, pure physics raycast fallback.
            if (_bulletTargetConfig != null && BulletTargetRegistry.Instance != null)
            {
                float maxRange = ResolveEffectiveMaxRange(weaponDef);

                var result = BulletTargetRegistry.FindBestTarget(
                    origin, shotAimDir, maxRange, _bulletTargetConfig);

                if (result.HasTarget)
                {
                    // Real 3D direction from muzzle to target centre, including Y.
                    finalFireDir = (result.HitPoint - origin).normalized;
                    projectileTargetPoint = result.HitPoint;

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

            Vector3 ballisticFireDir = ClampFireElevation(finalFireDir, shotAimDir, _maxFireElevationAngle);

            // Fire events and ballistics.
            // Raise local fire event (drives VFX + animation on owner).
            OnShotFired?.Invoke(slot.Value, shotAimDir);
            TriggerOwnerCameraShake(weaponDef);

            // Delegate ballistics to weapon prefab component.
            // finalFireDir has correct 3-D elevation; spread is applied inside WeaponBase.
            if (_currentWeaponBase != null)
            {
                var config = BuildWeaponConfigData(inst);
                config.ApplyDamage = false;
                Vector3 visualFireDir = ApplyClientRecoil(ballisticFireDir, config);
                _lastFireHitHittable = false;
                _lastFireHitNormal = -visualFireDir.normalized;
                _lastFireEndpoint = BuildFallbackProjectileEndpoint(origin, visualFireDir, config.MaxRange);
                if (projectileTargetPoint.HasValue)
                {
                    config.HasProjectileTargetPoint = true;
                    config.ProjectileTargetPoint = projectileTargetPoint.Value;
                }
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Weapon,
                    "FireLocal",
                    $"obj={ObjectId} owner={Owner?.ClientId} slot={slot.Value} weapon={inst.DefinitionID} ballistic={config.BallisticType} origin={origin:F2} aimFlat={shotAimDir:F2} dir={visualFireDir:F2} target={(projectileTargetPoint.HasValue ? projectileTargetPoint.Value.ToString("F2") : "none")} speed={config.ProjectileSpeed:F1} range={config.MaxRange:F1} mag={(int)inst.GetCurrentValue(ItemStatType.MagazineSize)} reserve={(int)inst.GetCurrentValue(ItemStatType.MaxAmmo)}",
                    this);
                LogProjectile($"TryFireOnce local shot slot={slot.Value} weaponId='{inst.DefinitionID}' origin={origin:F2} aimFlat={shotAimDir:F2} dir={visualFireDir:F2} intentDir={ballisticFireDir:F2} ammoCost={ammoCost} mag={(int)inst.GetCurrentValue(ItemStatType.MagazineSize)} reserve={(int)inst.GetCurrentValue(ItemStatType.MaxAmmo)} speed={config.ProjectileSpeed:F1} maxRange={config.MaxRange:F1} vision={config.VisionRangeClamp:F1} endpointBefore={_lastFireEndpoint:F2}");
                _currentWeaponBase.Fire(origin, visualFireDir, config, (int)ObjectId);

                if (IsServerInitialized)
                    ResolveAuthoritativeShotServer(
                        slot.Value,
                        ballisticFireDir,
                        consumeAmmo: false,
                        config.HasProjectileTargetPoint,
                        config.ProjectileTargetPoint);
                else if (IsOwner)
                    RequestAuthoritativeShotServerRpc(
                        slot.Value,
                        ballisticFireDir,
                        config.HasProjectileTargetPoint,
                        config.ProjectileTargetPoint,
                        TimeManager.GetPreciseTick(TickType.Tick));

                // Spawn projectile visual on remote clients using the clamped 3D direction.
                // For hitscan weapons, _lastFireEndpoint is the raycast hit point or max range.
                if (IsOwner && _currentWeaponBase.ProjectilePrefab != null)
                {
                    Vector3 broadcastEndpoint = SanitizeProjectileEndpoint(
                        origin,
                        visualFireDir,
                        _lastFireEndpoint,
                        config.MaxRange,
                        "ownerBroadcast");
                    _lastFireEndpoint = broadcastEndpoint;

                    LogProjectile($"BroadcastProjectileServerRpc origin={origin:F2} dir={visualFireDir:F2} endpoint={broadcastEndpoint:F2} weaponId='{config.WeaponId}'");
                    BroadcastProjectileServerRpc(origin, visualFireDir, config,
                        broadcastEndpoint, _lastFireHitHittable, _lastFireHitNormal);
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
                BroadcastShotFiredServerRpc(slot.Value, shotAimDir, elevationAngle);
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
            Vector3 fallback = root.position + Vector3.up * 1.25f + flatAim * 0.7f;

            bool invalidOrigin =
                !IsFinite(origin) ||
                origin.y < root.position.y - 1f ||
                origin.y > root.position.y + 4f ||
                flatToOrigin.sqrMagnitude > 16f;

            if (invalidOrigin)
            {
                if (TryRepairFireOrigin("ResolveFireOrigin"))
                {
                    origin = _fireOrigin != null ? _fireOrigin.position : root.position;
                    flatToOrigin = Vector3.ProjectOnPlane(origin - root.position, Vector3.up);
                    invalidOrigin =
                        !IsFinite(origin) ||
                        origin.y < root.position.y - 1f ||
                        origin.y > root.position.y + 4f ||
                        flatToOrigin.sqrMagnitude > 16f;

                    if (!invalidOrigin)
                    {
                        LogProjectile($"ResolveFireOrigin repaired muzzle={(_fireOrigin != null ? _fireOrigin.name : "null")} origin={origin:F2} root={root.position:F2}");
                    }
                }

                if (!invalidOrigin)
                {
                    bool repairedBehindRoot = Vector3.Dot(flatToOrigin, flatAim) < -0.05f;
                    if (!repairedBehindRoot)
                        return origin;
                }

                LogProjectile($"ResolveFireOrigin fallback: invalid muzzle. muzzle={(_fireOrigin != null ? _fireOrigin.name : "null")} raw={origin:F2} fallback={fallback:F2} root={root.position:F2} flatDistance={flatToOrigin.magnitude:F2}");
                return fallback;
            }

            bool muzzleBehindRoot = Vector3.Dot(flatToOrigin, flatAim) < -0.05f;
            if (!muzzleBehindRoot)
                return origin;

            LogProjectile($"ResolveFireOrigin fallback: muzzle was behind root. raw={origin:F2} fallback={fallback:F2} aim={flatAim:F2} root={root.position:F2}");
            return fallback;
        }

        private void CaptureFireOriginSnapshot(Transform muzzle)
        {
            if (muzzle == null)
            {
                _fireOriginParent = null;
                _fireOriginLocalPosition = Vector3.zero;
                _fireOriginLocalRotation = Quaternion.identity;
                _fireOriginLocalScale = Vector3.one;
                _hasFireOriginSnapshot = false;
                return;
            }

            _fireOriginParent = muzzle.parent;
            _fireOriginLocalPosition = muzzle.localPosition;
            _fireOriginLocalRotation = muzzle.localRotation;
            _fireOriginLocalScale = muzzle.localScale;
            _hasFireOriginSnapshot = true;
        }

        private bool TryRepairFireOrigin(string source)
        {
            bool repaired = false;

            if (_currentWeaponBase != null && _currentWeaponBase.FirePoint != null &&
                _fireOrigin != _currentWeaponBase.FirePoint)
            {
                _fireOrigin = _currentWeaponBase.FirePoint;
                CaptureFireOriginSnapshot(_fireOrigin);
                repaired = true;
            }

            if (_fireOrigin == null || !_hasFireOriginSnapshot)
                return repaired;

            if (_fireOriginParent != null && _fireOrigin.parent != _fireOriginParent)
            {
                _fireOrigin.SetParent(_fireOriginParent, false);
                repaired = true;
            }

            if (_fireOrigin.parent == _fireOriginParent)
            {
                if (_fireOrigin.localPosition != _fireOriginLocalPosition)
                {
                    _fireOrigin.localPosition = _fireOriginLocalPosition;
                    repaired = true;
                }

                if (_fireOrigin.localRotation != _fireOriginLocalRotation)
                {
                    _fireOrigin.localRotation = _fireOriginLocalRotation;
                    repaired = true;
                }

                if (_fireOrigin.localScale != _fireOriginLocalScale)
                {
                    _fireOrigin.localScale = _fireOriginLocalScale;
                    repaired = true;
                }
            }

            if (repaired)
            {
                LogProjectile(
                    $"FireOrigin repair source={source} muzzle={_fireOrigin.name} " +
                    $"parent={(_fireOrigin.parent != null ? _fireOrigin.parent.name : "null")} " +
                    $"local={_fireOrigin.localPosition:F2} world={_fireOrigin.position:F2}");
            }

            return repaired;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private int ResolveAmmoCostPerShot(ItemInstance inst, int currentMagazine)
        {
            return Mathf.Clamp(1, 1, Mathf.Max(1, currentMagazine));
        }

        private Vector3 BuildFallbackProjectileEndpoint(Vector3 origin, Vector3 direction, float maxRange)
        {
            Vector3 dir = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : ResolveFlatFireDirection(_aimDirection);
            float range = Mathf.Max(1f, maxRange);
            return origin + dir * range;
        }

        private Vector3 SanitizeProjectileEndpoint(Vector3 origin, Vector3 direction, Vector3 endpoint, float maxRange, string source)
        {
            float range = Mathf.Max(1f, maxRange);
            Vector3 toEndpoint = endpoint - origin;
            float distance = toEndpoint.magnitude;
            Vector3 flatDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            Vector3 flatToEndpoint = Vector3.ProjectOnPlane(toEndpoint, Vector3.up);
            bool hasForwardFlatEndpoint =
                flatDirection.sqrMagnitude > 0.0001f &&
                flatToEndpoint.sqrMagnitude > 0.0001f &&
                Vector3.Dot(flatDirection.normalized, flatToEndpoint.normalized) > 0.05f;

            bool invalid =
                !IsFinite(endpoint) ||
                distance < 0.05f ||
                distance > range + 1.5f ||
                endpoint.y < origin.y - 3f ||
                endpoint.y > origin.y + 4f ||
                !hasForwardFlatEndpoint;

            if (!invalid)
                return endpoint;

            Vector3 fallback = BuildFallbackProjectileEndpoint(origin, direction, range);
            fallback.y = Mathf.Clamp(fallback.y, origin.y - 1f, origin.y + 2f);
            LogProjectile($"SanitizeProjectileEndpoint fallback source={source} raw={endpoint:F2} origin={origin:F2} dir={direction:F2} fallback={fallback:F2} maxRange={range:F1}");
            return fallback;
        }

        private void FireMelee(WeaponSlotType slot)
        {
            // Melee has no magazine/projectile path. This event drives:
            // - CharacterAnimationController melee Attack trigger
            // - CharacterAnimationController melee fallback hit delay
            // - WeaponAudioController swing/attack sound through weapon profile
            PhaseTestLog.Log(
                PhaseTestLogCategory.Weapon,
                "MeleeFire",
                $"slot={slot} aim={_aimDirection:F2} owner={Owner?.ClientId} isOwner={IsOwner}",
                this);
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
            float rawMaxRange = _currentWeaponBase?.MaxRange ?? 150f;
            float visionRangeClamp = ResolveVisionRangeClamp();
            float effectiveMaxRange = ResolveEffectiveMaxRange(rawMaxRange, visionRangeClamp, weaponDef);

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
                MaxRange          = effectiveMaxRange,
                GravityScale      = _currentWeaponBase?.GravityScale    ?? 0f,
                // Clamp bullet visual travel to the owner's VisionRange so projectiles
                // never fly beyond the visible circle (same radius used by AimSystem).
                VisionRangeClamp  = visionRangeClamp,
                ApplyDamage       = true,
                SpreadBase      = inst.GetComputedStat(ItemStatType.SpreadBase),
                SpreadMoveMul   = inst.GetComputedStat(ItemStatType.SpreadPenalty),
                RecoilHorizontal = inst.GetComputedStat(ItemStatType.RecoilHorizontal),
                RecoilVertical   = inst.GetComputedStat(ItemStatType.RecoilVertical),
            };
        }

        private float ResolveEffectiveMaxRange()
        {
            float rawMaxRange = _currentWeaponBase?.MaxRange ?? 150f;
            return ResolveEffectiveMaxRange(rawMaxRange, ResolveVisionRangeClamp(), ResolveActiveWeaponDefinition());
        }

        private float ResolveEffectiveMaxRange(WeaponDefinition weaponDef)
        {
            float rawMaxRange = _currentWeaponBase?.MaxRange ?? 150f;
            return ResolveEffectiveMaxRange(rawMaxRange, ResolveVisionRangeClamp(), weaponDef);
        }

        private WeaponDefinition ResolveActiveWeaponDefinition()
        {
            var slot = _activeSlot.Value;
            if (slot.HasValue &&
                _weaponCache.TryGetValue(slot.Value, out var inst))
                return ItemDatabase.GetDefinition(inst.DefinitionID) as WeaponDefinition;

            return null;
        }

        private float ResolveEffectiveMaxRange(float rawMaxRange, float visionRangeClamp, WeaponDefinition weaponDef)
        {
            float range = Mathf.Max(1f, rawMaxRange);
            if (visionRangeClamp <= 0.1f)
                return range;

            if (_sniperCanExceedVisionRange &&
                weaponDef != null &&
                weaponDef.WeaponClass == WeaponClass.Sniper)
            {
                float sniperRange = visionRangeClamp * Mathf.Max(1f, _sniperVisionRangeMultiplier);
                return Mathf.Min(range, sniperRange);
            }

            return Mathf.Min(range, visionRangeClamp);
        }

        private float ResolveVisionRangeClamp()
        {
            float vision = _statSystem != null ? _statSystem.GetStat(PlayerStatType.VisionRange) : 0f;
            return vision > 0.1f ? vision : 0f;
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

        private void TriggerOwnerCameraShake(WeaponDefinition weaponDef)
        {
            if (!_enableOwnerCameraShake || !IsOwner)
                return;

            WeaponClass weaponClass = weaponDef != null ? weaponDef.WeaponClass : WeaponClass.Rifle;
            if (weaponClass == WeaponClass.Melee)
                return;

            ResolveCameraShakeTuning(weaponClass, out float amplitude, out float duration);
            if (amplitude <= 0f || duration <= 0f)
                return;

            WeaponCameraShakeDriver.Shake(amplitude, duration, _cameraShakeFrequency);
            PhaseTestLog.Log(
                PhaseTestLogCategory.Weapon,
                "CameraShake",
                $"weapon={weaponDef?.ItemID ?? "unknown"} class={weaponClass} amplitude={amplitude:F3} duration={duration:F3} frequency={_cameraShakeFrequency:F1}",
                this);
        }

        private void ResolveCameraShakeTuning(WeaponClass weaponClass, out float amplitude, out float duration)
        {
            duration = _cameraShakeShortDuration;
            amplitude = weaponClass switch
            {
                WeaponClass.Pistol     => _pistolCameraShakeAmplitude,
                WeaponClass.SMG        => _smgCameraShakeAmplitude,
                WeaponClass.MachineGun => _machineGunCameraShakeAmplitude,
                WeaponClass.Shotgun    => _shotgunCameraShakeAmplitude,
                WeaponClass.Sniper     => _sniperCameraShakeAmplitude,
                WeaponClass.Launcher   => _launcherCameraShakeAmplitude,
                _                      => _rifleCameraShakeAmplitude,
            };

            if (weaponClass == WeaponClass.Shotgun ||
                weaponClass == WeaponClass.Sniper ||
                weaponClass == WeaponClass.Launcher)
                duration = _cameraShakeHeavyDuration;
        }

        private void HandleWeaponFireResult(WeaponBase.WeaponFireResult result)
        {
            // Cache the endpoint so TryFireOnce can pass it to BroadcastProjectileServerRpc
            // AFTER Fire() returns.  HandleWeaponFireResult is called FROM inside Fire() via
            // the OnFireResult event, which means _lastFireEndpoint is fully populated before
            // BroadcastProjectileServerRpc is reached.
            Vector3 origin = result.Origin;
            Vector3 endpoint = result.Endpoint;
            _lastFireEndpoint = endpoint;
            _lastFireHitHittable = result.HitAnIHittable;
            _lastFireHitNormal = result.HitNormal;
            var slot = _activeSlot.Value;
            if (slot != null) OnHitscanResult?.Invoke(slot.Value, origin, endpoint);
        }
    }

    internal sealed class WeaponCameraShakeDriver : MonoBehaviour
    {
        private float _endTime;
        private float _duration;
        private float _amplitude;
        private float _frequency;
        private float _seed;
        private Vector3 _lastOffset;
        private Quaternion _lastRotationOffset = Quaternion.identity;

        public static void Shake(float amplitude, float duration, float frequency)
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null)
                return;

            var driver = camera.GetComponent<WeaponCameraShakeDriver>();
            if (driver == null)
                driver = camera.gameObject.AddComponent<WeaponCameraShakeDriver>();

            driver.AddShake(amplitude, duration, frequency);
        }

        private void AddShake(float amplitude, float duration, float frequency)
        {
            _amplitude = Mathf.Max(_amplitude * 0.65f, amplitude);
            _duration = Mathf.Max(0.01f, duration);
            _frequency = Mathf.Max(1f, frequency);
            _endTime = Time.unscaledTime + _duration;
            _seed = Time.unscaledTime * 37.13f;
            enabled = true;
        }

        private void OnPreCull()
        {
            RemoveLastOffset();

            float remaining = _endTime - Time.unscaledTime;
            if (remaining <= 0f)
            {
                enabled = false;
                _amplitude = 0f;
                return;
            }

            float strength = _amplitude * Mathf.Clamp01(remaining / Mathf.Max(0.01f, _duration));
            float time = Time.unscaledTime * _frequency;
            float x = Mathf.PerlinNoise(_seed, time) * 2f - 1f;
            float y = Mathf.PerlinNoise(_seed + 11.3f, time + 7.1f) * 2f - 1f;
            float z = Mathf.PerlinNoise(_seed + 23.7f, time + 3.9f) * 2f - 1f;

            _lastOffset = new Vector3(x, y, 0f) * strength;
            _lastRotationOffset = Quaternion.Euler(y * strength * 18f, -x * strength * 18f, z * strength * 10f);

            transform.localPosition += _lastOffset;
            transform.localRotation *= _lastRotationOffset;
        }

        private void OnPostRender()
        {
            RemoveLastOffset();
        }

        private void OnDisable()
        {
            RemoveLastOffset();
        }

        private void RemoveLastOffset()
        {
            if (_lastOffset.sqrMagnitude > 0f)
            {
                transform.localPosition -= _lastOffset;
                _lastOffset = Vector3.zero;
            }

            if (_lastRotationOffset != Quaternion.identity)
            {
                transform.localRotation *= Quaternion.Inverse(_lastRotationOffset);
                _lastRotationOffset = Quaternion.identity;
            }
        }
    }
}
