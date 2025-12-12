using System;

namespace NightHunt.Data.Configs
{
    [Serializable]
    public class WeaponConfig
    {
        public string WeaponId;
        public string DisplayName;
        public string Category;
        public string BallisticType;
        public float DamageBody;
        public float DamageHeadMul;
        public int MagazineSize;
        public int ReserveAmmo;
        public float FireRate;
        public int BurstCount;
        public float ReloadTime;
        public float EffectiveRange;
        public float MaxRange;
        public float ProjectileSpeed;
        public float GravityScale;
        public float SpreadBase;
        public float SpreadMoveMul;
        public float MoveSpeedMul;
        public float Weight;
        public string Rarity;
        public int AllowedPhaseMask;
        public string Tags;
    }
}