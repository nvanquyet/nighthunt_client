using UnityEngine;
using NightHunt.Data;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Lightweight WeaponBase for melee held prefabs.
    /// WeaponSystem special-cases melee before ammo/projectile logic, so Fire is a no-op.
    /// Keeping this component on the prefab still exposes FirePoint/LeftHandIK to model,
    /// audio and rig systems.
    /// </summary>
    public sealed class MeleeWeapon : WeaponBase
    {
        public override void Fire(Vector3 origin, Vector3 direction, WeaponConfigData config, int shooterNetObjId)
        {
            RaiseFireResult(origin, origin + direction.normalized * Mathf.Min(MaxRange, 2f));
        }
    }
}
