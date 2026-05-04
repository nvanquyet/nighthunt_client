using UnityEngine;
using FishNet.Object;
using System.Collections;
using NightHunt.Data;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.Boss;
using NightHunt.Gameplay.Deployables;
using NightHunt.Gameplay.Objective;
using NightHunt.Utilities;

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
    /// Damage is computed on the server. Local/remote projectiles are visual-only.
    /// </summary>
    public partial class WeaponSystem
    {
        private readonly RaycastHit[] _authoritativeRaycastHits = new RaycastHit[32];

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

            // Muzzle flash is now owned by the projectile — played inside ProjectileComponent.Initialize().
            // For hitscan weapons, ShowProjectileOnClientsRpc spawns the visual bullet which triggers it.
        }

        // ── 3. Reload state broadcast ──────────────────────────────────────────

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastReloadStateObserversRpc(bool isReloading)
        {
            _isReloading = isReloading;
            OnReloadStateChanged?.Invoke(isReloading); // → CharacterAnimationController
        }

        // hitscanEndpoint = world-space impact point for hitscan weapons (hit point or max-range).
        // Passing it explicitly lets remote clients fly the visual bullet to the exact hit
        // position while ignoring local collision.
        [ServerRpc(RequireOwnership = true)]
        internal void BroadcastProjectileServerRpc(Vector3 origin, Vector3 direction,
                                                   WeaponConfigData config, Vector3 hitscanEndpoint,
                                                   bool hitAnIHittable, Vector3 hitNormal)
            => ShowProjectileOnClientsRpc(origin, direction, config, hitscanEndpoint, hitAnIHittable, hitNormal);

        [ServerRpc(RequireOwnership = true)]
        private void RequestAuthoritativeShotServerRpc(WeaponSlotType slot, Vector3 direction)
        {
            ResolveAuthoritativeShotServer(slot, direction, consumeAmmo: true);
        }

        [Server]
        private void ResolveAuthoritativeShotServer(WeaponSlotType slot, Vector3 direction, bool consumeAmmo)
        {
            if (!IsUsableFireDirection(direction))
            {
                RejectAuthoritativeShot(slot, "invalid-direction", $"dir={direction}");
                return;
            }

            if (!_activeSlot.Value.HasValue || _activeSlot.Value.Value != slot)
            {
                RejectAuthoritativeShot(slot, "active-slot-mismatch", $"active={(_activeSlot.Value.HasValue ? _activeSlot.Value.Value.ToString() : "none")}");
                return;
            }

            if (!_weaponCache.TryGetValue(slot, out var inst) || inst == null)
            {
                RebuildWeaponCache();
                if (!_weaponCache.TryGetValue(slot, out inst) || inst == null)
                {
                    RejectAuthoritativeShot(slot, "weapon-cache-miss");
                    return;
                }
            }

            var weaponDef = ItemDatabase.GetDefinition(inst.DefinitionID) as WeaponDefinition;
            if (weaponDef == null || weaponDef.WeaponClass == WeaponClass.Melee)
            {
                RejectAuthoritativeShot(slot, "invalid-weapon-definition", $"def={inst.DefinitionID}");
                return;
            }

            Vector3 fireDir = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            if (!ValidateServerAimDelta(slot, fireDir))
                return;

            if (consumeAmmo)
            {
                int mag = (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
                if (mag <= 0)
                {
                    RejectAuthoritativeShot(slot, "empty-magazine", $"def={inst.DefinitionID}");
                    return;
                }

                int ammoCost = ResolveAmmoCostPerShot(inst, mag);
                inst.AdjustCurrentValue(ItemStatType.MagazineSize, -ammoCost);
            }

            var config = BuildWeaponConfigData(inst);
            config.ApplyDamage = true;

            float maxRange = Mathf.Max(1f, config.MaxRange);
            Vector3 origin = ResolveServerFireOrigin(fireDir);
            float serverSpread = ResolveServerSpread(config);
            Vector3 authoritativeFireDir = ApplyServerRecoil(fireDir, config, serverSpread);

            if (config.BallisticType == "Projectile")
            {
                StartCoroutine(ServerProjectileFlight(origin, ApplyServerSpread(authoritativeFireDir, config), config, (int)ObjectId));
            }
            else
            {
                ResolveAuthoritativeHitscan(origin, authoritativeFireDir, config, maxRange, (int)ObjectId);
            }

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableProjectileDebugLogs)
            {
                Debug.Log($"[SHOOT.SERVER] Authoritative shot slot={slot} weapon={config.WeaponId} origin={origin:F2} dir={authoritativeFireDir:F2} intentDir={fireDir:F2} spread={serverSpread:F2} maxRange={config.MaxRange:F1} vision={config.VisionRangeClamp:F1} endpoint={_lastFireEndpoint:F2} consumeAmmo={consumeAmmo}", this);
            }
        }

        private static bool IsUsableFireDirection(Vector3 direction)
        {
            return direction.sqrMagnitude > 0.0001f
                && !float.IsNaN(direction.x)
                && !float.IsNaN(direction.y)
                && !float.IsNaN(direction.z)
                && !float.IsInfinity(direction.x)
                && !float.IsInfinity(direction.y)
                && !float.IsInfinity(direction.z);
        }

        private void RejectAuthoritativeShot(WeaponSlotType slot, string reason, string details = null)
        {
            string suffix = string.IsNullOrEmpty(details) ? string.Empty : $" {details}";
            Debug.LogWarning(
                $"[SHOOT.SERVER.REJECT] owner={Owner?.ClientId} obj={ObjectId} slot={slot} reason={reason}{suffix}",
                this);
        }

        [Server]
        private bool ValidateServerAimDelta(WeaponSlotType slot, Vector3 requestedDirection)
        {
            Vector3 requestedFlat = Vector3.ProjectOnPlane(requestedDirection, Vector3.up);
            if (requestedFlat.sqrMagnitude <= 0.0001f)
            {
                RejectAuthoritativeShot(slot, "aim-flat-zero", $"dir={requestedDirection}");
                return false;
            }
            requestedFlat.Normalize();

            if (_lastServerShotDirection.sqrMagnitude <= 0.0001f || _lastServerShotTime < 0f)
            {
                CacheAcceptedServerAim(requestedFlat);
                return true;
            }

            Vector3 lastFlat = Vector3.ProjectOnPlane(_lastServerShotDirection, Vector3.up);
            if (lastFlat.sqrMagnitude <= 0.0001f)
            {
                CacheAcceptedServerAim(requestedFlat);
                return true;
            }
            lastFlat.Normalize();

            float now = Time.time;
            float dt = Mathf.Max(0.016f, now - _lastServerShotTime);
            float allowedDelta = _serverAimDeltaGraceDegrees + _maxServerAimDeltaDegPerSecond * dt;
            float actualDelta = Vector3.Angle(lastFlat, requestedFlat);
            if (actualDelta > allowedDelta)
            {
                RejectAuthoritativeShot(
                    slot,
                    "aim-delta-too-fast",
                    $"delta={actualDelta:F1} allowed={allowedDelta:F1} dt={dt:F3} last={lastFlat:F2} requested={requestedFlat:F2}");
                return false;
            }

            CacheAcceptedServerAim(requestedFlat);
            return true;
        }

        [Server]
        private void CacheAcceptedServerAim(Vector3 flatDirection)
        {
            _lastServerShotDirection = flatDirection;
            _lastServerShotTime = Time.time;
        }

        private Vector3 ResolveServerFireOrigin(Vector3 fireDir)
        {
            Transform root = transform.root != null ? transform.root : transform;
            Vector3 flatDir = new Vector3(fireDir.x, 0f, fireDir.z);
            if (flatDir.sqrMagnitude < 0.001f)
                flatDir = root.forward;
            flatDir.Normalize();

            Vector3 fallback = root.position + Vector3.up * 1.25f + flatDir * 0.6f;
            if (_fireOrigin == null)
                return fallback;

            Vector3 origin = _fireOrigin.position;
            Vector3 flatToOrigin = Vector3.ProjectOnPlane(origin - root.position, Vector3.up);
            bool invalidOrigin =
                !IsFinite(origin) ||
                origin.y < root.position.y - 1f ||
                origin.y > root.position.y + 4f ||
                flatToOrigin.sqrMagnitude > 16f;

            if (invalidOrigin && TryRepairFireOrigin("ResolveServerFireOrigin"))
            {
                origin = _fireOrigin != null ? _fireOrigin.position : fallback;
                flatToOrigin = Vector3.ProjectOnPlane(origin - root.position, Vector3.up);
                invalidOrigin =
                    !IsFinite(origin) ||
                    origin.y < root.position.y - 1f ||
                    origin.y > root.position.y + 4f ||
                    flatToOrigin.sqrMagnitude > 16f;

                if (!invalidOrigin)
                    LogProjectile($"ResolveServerFireOrigin repaired muzzle={(_fireOrigin != null ? _fireOrigin.name : "null")} origin={origin:F2} root={root.position:F2}");
            }

            if (!invalidOrigin)
                return origin;

            LogProjectile($"ResolveServerFireOrigin fallback: invalid muzzle raw={origin:F2} fallback={fallback:F2} root={root.position:F2} flatDistance={flatToOrigin.magnitude:F2}");
            return fallback;
        }

        [Server]
        private void ResolveAuthoritativeHitscan(Vector3 origin, Vector3 fireDir, WeaponConfigData config, float maxRange, int shooterNetObjId)
        {
            int pelletCount = 1;
            float pelletSpreadBonus = 0f;

            if (_currentWeaponBase is HitscanWeapon hitscanWeapon)
            {
                pelletCount = Mathf.Max(1, hitscanWeapon.PelletCount);
                pelletSpreadBonus = hitscanWeapon.PelletSpreadBonus;
            }

            Vector3 endpointSum = Vector3.zero;
            for (int i = 0; i < pelletCount; i++)
            {
                Vector3 pelletDir = ApplyServerSpread(fireDir, config);
                if (pelletCount > 1 && pelletSpreadBonus > 0.001f)
                    pelletDir = ClampFireElevation(
                        AddServerSpread(pelletDir, pelletSpreadBonus),
                        fireDir,
                        _maxFireElevationAngle);

                endpointSum += ResolveAuthoritativePellet(origin, pelletDir, config, maxRange, shooterNetObjId);
            }

            _lastFireEndpoint = endpointSum / pelletCount;
        }

        [Server]
        private Vector3 ResolveAuthoritativePellet(Vector3 origin, Vector3 direction, WeaponConfigData config, float maxRange, int shooterNetObjId)
        {
            Transform root = transform.root != null ? transform.root : transform;

            if (TryGetFirstAuthoritativeHit(origin, direction, maxRange, root, out RaycastHit hit))
            {
                ApplyAuthoritativeHit(hit, config, shooterNetObjId);
                return hit.point;
            }

            if (TryResolveRegisteredTarget(origin, direction, maxRange, config, shooterNetObjId, out Vector3 registeredHitPoint))
                return registeredHitPoint;

            return origin + direction * maxRange;
        }

        [Server]
        private bool TryResolveRegisteredTarget(
            Vector3 origin,
            Vector3 direction,
            float maxRange,
            WeaponConfigData config,
            int shooterNetObjId,
            out Vector3 hitPoint)
        {
            hitPoint = default;

            if (_bulletTargetConfig == null || BulletTargetRegistry.Instance == null)
                return false;

            var result = BulletTargetRegistry.FindBestTarget(origin, direction, maxRange, _bulletTargetConfig);
            if (!result.HasTarget || result.Target.HitTarget == null)
                return false;

            var ownerHealth = ResolveOwnerHealthSystem();
            if (ownerHealth != null && ReferenceEquals(result.Target.HitTarget, ownerHealth))
                return false;

            hitPoint = result.HitPoint;
            var info = new DamageInfo
            {
                Damage = config.DamageBody,
                IsHeadshot = false,
                HitPoint = result.HitPoint,
                HitNormal = -direction.normalized,
                ShooterNetworkObjectId = shooterNetObjId,
                WeaponId = config.WeaponId ?? string.Empty,
            };

            ApplyAuthoritativeTargetHit(result.Target.HitTarget, info);

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableProjectileDebugLogs)
            {
                Debug.Log($"[SHOOT.SERVER.TARGET] weapon={config.WeaponId} target={result.Target.TargetType} point={hitPoint:F2} angle={result.AngleDeg:F1} dist={result.Distance:F1}", this);
            }

            return true;
        }

        [Server]
        private void ApplyAuthoritativeTargetHit(IHittable target, DamageInfo info)
        {
            switch (target)
            {
                case PlayerHealthSystem healthSystem:
                    healthSystem.ApplyDamageServer(info);
                    break;
                case BaseDeployable deployable:
                    deployable.TakeDamage(Mathf.RoundToInt(info.Damage));
                    break;
                case BossController boss:
                    boss.TakeDamage(info.Damage, NetworkObject);
                    break;
                case EMPNodeObjective emp:
                    emp.TakeDamage(info.Damage);
                    break;
                default:
                    target.RequestDamage(info);
                    break;
            }
        }

        private PlayerHealthSystem ResolveOwnerHealthSystem()
        {
            Transform root = transform.root != null ? transform.root : transform;
            return root.GetComponent<PlayerHealthSystem>()
                   ?? root.GetComponentInChildren<PlayerHealthSystem>(includeInactive: true);
        }

        [Server]
        private bool TryGetFirstAuthoritativeHit(Vector3 origin, Vector3 direction, float range, Transform shooterRoot, out RaycastHit hit)
        {
            hit = default;

            int count = Physics.RaycastNonAlloc(origin, direction, _authoritativeRaycastHits, range, NightHunt.Core.NightHuntLayers.MaskHitscanBlock, QueryTriggerInteraction.Ignore);
            if (count <= 0)
                return false;

            System.Array.Sort(_authoritativeRaycastHits, 0, count, RaycastHitDistanceComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                if (_authoritativeRaycastHits[i].collider == null || IsSelfHit(_authoritativeRaycastHits[i].collider, shooterRoot))
                    continue;

                hit = _authoritativeRaycastHits[i];
                return true;
            }

            return false;
        }

        [Server]
        private IEnumerator ServerProjectileFlight(Vector3 origin, Vector3 direction, WeaponConfigData config, int shooterNetObjId)
        {
            Vector3 position = origin;
            Vector3 velocity = direction.normalized * Mathf.Max(1f, config.ProjectileSpeed);
            float maxRange = Mathf.Max(1f, config.MaxRange);
            float traveled = 0f;
            Transform root = transform.root != null ? transform.root : transform;

            while (traveled < maxRange)
            {
                float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : Time.deltaTime;
                dt = Mathf.Clamp(dt, 0.001f, 0.05f);

                if (config.GravityScale > 0f)
                    velocity += Vector3.down * (config.GravityScale * 9.81f * dt);

                Vector3 move = velocity * dt;
                float distance = move.magnitude;
                if (distance > 0.001f)
                {
                    Vector3 rayDir = move / distance;
                    if (Physics.SphereCast(position, 0.08f, rayDir, out RaycastHit hit, distance, NightHunt.Core.NightHuntLayers.MaskHitscanBlock, QueryTriggerInteraction.Ignore)
                        && !IsSelfHit(hit.collider, root))
                    {
                        ApplyAuthoritativeHit(hit, config, shooterNetObjId);
                        _lastFireEndpoint = hit.point;
                        yield break;
                    }

                    position += move;
                    traveled += distance;
                }

                yield return null;
            }

            _lastFireEndpoint = position;
        }

        [Server]
        private void ApplyAuthoritativeHit(RaycastHit hit, WeaponConfigData config, int shooterNetObjId)
        {
            var hitbox = ComponentResolver.Find<PlayerHitboxMarker>(hit.collider)
                .OnSelf()
                .InParent()
                .Resolve();

            if (hitbox != null && hitbox.HealthSystem != null)
            {
                float damage = config.DamageBody * (hitbox.IsHeadshot ? Mathf.Max(1f, config.DamageHeadMul) : 1f);
                hitbox.HealthSystem.ApplyDamageServer(new DamageInfo
                {
                    Damage = damage,
                    IsHeadshot = hitbox.IsHeadshot,
                    HitPoint = hit.point,
                    HitNormal = hit.normal,
                    ShooterNetworkObjectId = shooterNetObjId,
                    WeaponId = config.WeaponId ?? string.Empty,
                });
                return;
            }

            var damageInfo = new DamageInfo
            {
                Damage = config.DamageBody,
                IsHeadshot = false,
                HitPoint = hit.point,
                HitNormal = hit.normal,
                ShooterNetworkObjectId = shooterNetObjId,
                WeaponId = config.WeaponId ?? string.Empty,
            };

            if (hit.collider.GetComponentInParent<BaseDeployable>() is { } deployable)
            {
                deployable.TakeDamage(Mathf.RoundToInt(damageInfo.Damage));
                return;
            }

            if (hit.collider.GetComponentInParent<BossController>() is { } boss)
            {
                boss.TakeDamage(damageInfo.Damage, NetworkObject);
                return;
            }

            if (hit.collider.GetComponentInParent<EMPNodeObjective>() is { } emp)
            {
                emp.TakeDamage(damageInfo.Damage);
                return;
            }

            var hittable = hit.collider.GetComponentInParent<IHittable>();
            hittable?.RequestDamage(damageInfo);
        }

        private Vector3 ApplyServerSpread(Vector3 direction, WeaponConfigData config)
        {
            float spread = ResolveServerSpread(config);
            return ClampFireElevation(AddServerSpread(direction, spread), direction, _maxFireElevationAngle);
        }

        private Vector3 ApplyServerRecoil(Vector3 direction, WeaponConfigData config, float currentSpread)
        {
            Vector2 recoil = DamageCalculator.CalculateRecoil(config, currentSpread);
            if (recoil.sqrMagnitude <= 0.0001f)
                return direction.normalized;

            Vector3 recoiled = DamageCalculator.ApplyRecoil(direction.normalized, recoil);
            return ClampFireElevation(recoiled, direction, _maxFireElevationAngle);
        }

        private float ResolveServerSpread(WeaponConfigData config)
        {
            float spread = 0f;
            if (_currentWeaponBase != null)
                spread = Mathf.Max(_currentWeaponBase.CurrentSpread, _currentWeaponBase.SpreadBase);

            if (spread <= 0.001f)
                spread = config.SpreadBase;

            return spread;
        }

        private static Vector3 AddServerSpread(Vector3 direction, float halfAngleDeg)
        {
            if (halfAngleDeg <= 0.001f)
                return direction.normalized;

            float radius = Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
            Vector2 disc = Random.insideUnitCircle * radius;
            Vector3 right = Vector3.Cross(direction, Vector3.up);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(right, direction).normalized;
            return (direction + right * disc.x + up * disc.y).normalized;
        }

        private static bool IsSelfHit(Collider collider, Transform root)
        {
            return root != null &&
                   (collider.transform == root || collider.transform.IsChildOf(root));
        }

        private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new();

            public int Compare(RaycastHit x, RaycastHit y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }

        [ObserversRpc]
        private void ShowProjectileOnClientsRpc(Vector3 origin, Vector3 direction,
                                                WeaponConfigData config, Vector3 hitscanEndpoint,
                                                bool hitAnIHittable, Vector3 hitNormal)
        {
            // Owner already spawned the authoritative projectile locally.
            if (IsOwner)
            {
                LogProjectile($"ShowProjectileOnClientsRpc skipped owner origin={origin:F1} weaponId='{config.WeaponId}'");
                return;
            }

            var pool   = ProjectilePool.Instance;
            var prefab = _currentWeaponBase?.ProjectilePrefab;
            if (prefab == null)
                prefab = ResolveProjectilePrefabFromDefinition(config.WeaponId);

            if (pool == null)
                WarnProjectile("ShowProjectileOnClientsRpc: ProjectilePool.Instance is null " +
                                 "— instantiating one-shot remote bullet visual. " +
                                 "Add ProjectilePool to the scene for production pooling.");

            if (prefab == null)
            {
                // _currentWeaponBase is null → weapon model prefab missing HitscanWeapon/ProjectileWeapon,
                // or the model hasn't finished loading on this client yet.
                WarnProjectile("ShowProjectileOnClientsRpc: WeaponBase or ProjectilePrefab " +
                                 "is null on this client — remote bullet visual not spawned. " +
                                 "Ensure the weapon VisualPrefab has a HitscanWeapon / ProjectileWeapon " +
                                 $"component with 'Projectile Prefab' assigned. weaponId='{config.WeaponId}'  " +
                                 $"_currentWeaponBase={(object)_currentWeaponBase ?? "null"}");
                return;
            }

            var proj = pool != null
                ? pool.Get(prefab, origin, Quaternion.LookRotation(direction))
                : Instantiate(prefab, origin, Quaternion.LookRotation(direction)).GetComponent<ProjectileComponent>();
            if (proj == null)
            {
                WarnProjectile($"ShowProjectileOnClientsRpc: pool.Get returned null. Increase ProjectilePool capacity for '{prefab.name}'.");
                return;
            }

            proj.SetIgnoredRoot(transform.root);

            bool isHitscan = config.BallisticType == "Hitscan";

            // For hitscan: fly the visual bullet to the pre-computed hit point while ignoring
            // local collision, so impact timing is distance / speed instead of a hard delay.
            // For ballistic projectiles: hitscanEndpoint is ignored (pass null).
            Vector3? endpoint = isHitscan ? (Vector3?)hitscanEndpoint : null;

            LogProjectile($"ShowProjectileOnClientsRpc remote visual spawned origin={origin:F1} isHitscan={isHitscan} endpoint={endpoint?.ToString("F1") ?? "null (ballistic)"} proj='{proj.gameObject.name}' weaponId='{config.WeaponId}'");

            if (isHitscan)
                proj.SetHitscanHitType(hitAnIHittable, hitNormal);

            proj.Initialize(config, direction, useHitscan: isHitscan, hitscanEndpoint: endpoint);
        }

        private static GameObject ResolveProjectilePrefabFromDefinition(string weaponId)
        {
            var def = ItemDatabase.GetDefinition(weaponId);
            if (def == null)
                return null;

            var visualPrefab = ItemVisualResolver.ResolveVisualPrefab(def);
            if (visualPrefab == null)
                return null;

            var weaponBase = visualPrefab.GetComponent<WeaponBase>()
                          ?? visualPrefab.GetComponentInChildren<WeaponBase>(includeInactive: true);
            return weaponBase != null ? weaponBase.ProjectilePrefab : null;
        }

        private static bool ProjectileDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && cfg.EnableProjectileDebugLogs;
        }

        private static void LogProjectile(string message)
        {
            if (ProjectileDebugEnabled())
                Debug.Log($"[PROJ.RPC] {message}");
        }

        private static void WarnProjectile(string message)
        {
            Debug.LogWarning($"[PROJ.RPC] {message}");
        }
    }
}
