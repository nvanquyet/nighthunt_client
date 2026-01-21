using FishNet.Object.Prediction;
using UnityEngine;

namespace NightHunt.Networking.Prediction.Modules.Attack
{
    public struct AttackReplicateData : IReplicateData
    {
        public int WeaponId;
        public Vector3 FireOrigin;
        public Vector3 FireDirection;

        private uint _tick;

        public AttackReplicateData(int weaponId, Vector3 fireOrigin, Vector3 fireDirection)
        {
            WeaponId = weaponId;
            FireOrigin = fireOrigin;
            FireDirection = fireDirection;
            _tick = 0;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}

