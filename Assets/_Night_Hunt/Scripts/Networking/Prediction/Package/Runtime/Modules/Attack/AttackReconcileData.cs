using FishNet.Object.Prediction;
using UnityEngine;

namespace NightHunt.Networking.Prediction.Modules.Attack
{
    public struct AttackReconcileData : IReconcileData
    {
        public int WeaponId;
        public bool Success;
        public int HitTargetId;
        public float Damage;
        public Vector3 ImpactPoint;

        private uint _tick;

        public AttackReconcileData(int weaponId, bool success, int hitTargetId, float damage, Vector3 impactPoint)
        {
            WeaponId = weaponId;
            Success = success;
            HitTargetId = hitTargetId;
            Damage = damage;
            ImpactPoint = impactPoint;
            _tick = 0;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}

