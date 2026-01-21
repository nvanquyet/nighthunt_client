// using NightHunt.Gameplay.Weapons.Core;
// using NightHunt.Gameplay.Weapons.VFX;
// using NightHunt.Utils;
// using UnityEngine;
//
// namespace NightHunt.Networking.Prediction
// {
//     /// <summary>
//     /// Predicted action cho 1 lần bắn súng trên client.
//     /// - StartPrediction: spawn projectile local từ pool + play VFX/SFX.
//     /// - Confirm: server chấp nhận → giữ nguyên, không cần làm gì.
//     /// - Rollback: server từ chối → trả projectile về pool / destroy.
//     /// 
//     /// Lưu ý:
//     /// - Logic ammo/cooldown server-authoritative, nên rollback tập trung vào visual/projectile.
//     /// </summary>
//     public class WeaponFirePredictionAction : IPredictedAction
//     {
//         private readonly BaseWeapon _weapon;
//         private readonly IWeaponVFX _vfxSystem;
//         private readonly Vector3 _aimDirection;
//
//         private GameObject _spawnedProjectile;
//
//         public WeaponFirePredictionAction(BaseWeapon weapon, IWeaponVFX vfxSystem, Vector3 aimDirection)
//         {
//             _weapon = weapon;
//             _vfxSystem = vfxSystem;
//             _aimDirection = aimDirection;
//         }
//
//         public void StartPrediction()
//         {
//             if (_weapon == null)
//             {
//                 GameLogger.LogWarning("[WeaponFirePredictionAction] StartPrediction: weapon is null.", "WeaponFirePredictionAction");
//                 return;
//             }
//
//             if (_vfxSystem == null)
//             {
//                 GameLogger.LogWarning("[WeaponFirePredictionAction] StartPrediction: vfxSystem is null.", "WeaponFirePredictionAction");
//                 return;
//             }
//
//             // Client đã check CanFire() trước khi tạo action, nhưng double-check cho an toàn.
//             if (!_weapon.CanFire())
//             {
//                 GameLogger.LogDebug("[WeaponFirePredictionAction] StartPrediction: weapon cannot fire (local validation).", "WeaponFirePredictionAction");
//                 return;
//             }
//
//             // Spawn projectile local (prediction) từ pool.
//             _spawnedProjectile = _vfxSystem.SpawnProjectile(_aimDirection, _weapon.HitLayers);
//             if (_spawnedProjectile != null)
//             {
//                 GameLogger.LogDebug($"[WeaponFirePredictionAction] Spawned predicted projectile: {_spawnedProjectile.name}", "WeaponFirePredictionAction");
//             }
//
//             // Play muzzle flash + sound local.
//             _vfxSystem.PlayMuzzleFlash();
//             _vfxSystem.PlayFireSound();
//         }
//
//         public void Confirm()
//         {
//             // Server accept → prediction đúng, projectile tiếp tục sống bình thường.
//             // Không cần làm gì thêm ngoài log.
//             if (_spawnedProjectile != null)
//             {
//                 GameLogger.LogDebug("[WeaponFirePredictionAction] Prediction confirmed by server.", "WeaponFirePredictionAction");
//             }
//         }
//
//         public void Rollback()
//         {
//             if (_spawnedProjectile == null)
//                 return;
//
//             // Rollback: reset projectile và trả về pool (nếu có).
//             var projectileComponent = _spawnedProjectile.GetComponent<ProjectileComponent>();
//             if (projectileComponent != null)
//             {
//                 projectileComponent.ResetState();
//             }
//
//             if (ProjectilePool.Instance != null)
//             {
//                 ProjectilePool.Instance.ReturnProjectile(_spawnedProjectile);
//                 GameLogger.LogDebug("[WeaponFirePredictionAction] Rollback: Returned projectile to pool (server denied).", "WeaponFirePredictionAction");
//             }
//             else
//             {
//                 Object.Destroy(_spawnedProjectile);
//                 GameLogger.LogDebug("[WeaponFirePredictionAction] Rollback: Destroyed projectile (no pool, server denied).", "WeaponFirePredictionAction");
//             }
//
//             _spawnedProjectile = null;
//         }
//     }
// }
//
//
