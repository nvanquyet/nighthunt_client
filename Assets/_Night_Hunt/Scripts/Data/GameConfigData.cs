using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Data
{
    /// <summary>
    /// Root container for all game configuration data loaded from JSON
    /// </summary>
    [Serializable]
    public class GameConfigData
    {
        public List<WeaponConfigData> WeaponConfig;
        public List<ItemConfigData> ItemConfig;
        public List<VisionModifierData> VisionModifiers;
        public List<RespawnConfigData> RespawnConfig;
        public List<MatchPhaseConfigData> MatchPhaseConfig;
        public List<ZoneConfigData> ZoneConfig;
        public List<StaminaWeightConfigData> StaminaWeightConfig;
        public List<InventoryConfigData> InventoryConfig;
        public List<ScoreSystemData> ScoreSystem;
        public List<RankingSystemData> RankingSystem;
        public List<CharacterConfigData> CharacterConfig;
        public List<StatusEffectConfigData> StatusEffectConfig;
        // New systems
        public List<RankTierConfigData> RankTierConfig;
        public RankMatchmakingConfigData RankMatchmakingConfig;
    }

    [Serializable]
    public class WeaponConfigData
    {
        public string WeaponId;
        public string DisplayName;
        public string Category;
        public string BallisticType; // "Hitscan" or "Projectile"
        public int DamageBody;
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
        public int AllowedPhaseMask; // Bitmask: 1=Phase1, 2=Phase2, 4=Phase3
        public string Tags;
    }

    [Serializable]
    public class ItemConfigData
    {
        public string ItemId;
        public string DisplayName;
        public string Category;
        public string UseType; // "Instant", "Channel", "PlaceOnGround", "Throw"
        public string TargetType; // "Self", "Ground", "Ally"
        public string EffectType;
        public float EffectValue;
        public float EffectDuration;
        public float CastTime;
        public float Cooldown;
        public int MaxStack;
        public float Weight;
        public bool CanDrop;
        public bool IsConsumable;
        public string Rarity;
        public int AllowedPhaseMask;
        public string ExtraParamsJson; // For additional parameters
    }

    [Serializable]
    public class VisionModifierData
    {
        public string ModifierId;
        public string SourceType; // "Item", "Zone", "Status"
        public string Target; // "Radius"
        public string OpType; // "Add", "Multiply"
        public float Value;
        public float Duration;
        public bool Stackable;
    }

    [Serializable]
    public class RespawnConfigData
    {
        public string ConfigName;
        public int BeaconHP;
        public float PlaceTime;
        public float RespawnDelay;
        public float RedeployCooldown;
        public float MinDistance;
        public string DisableInPhase;
        public int DestroyScoreReward;
    }

    [Serializable]
    public class MatchPhaseConfigData
    {
        [Tooltip("Enum định danh Phase. Dùng để lookup và so sánh trong code. Không cần đổi.")]
        public NightHunt.Gameplay.Match.MatchPhaseState PhaseType;

        [Tooltip("Tên hiển thị trên UI/Log. Tùy biến được, chỉ dùng để đọc, không dùng compare.")]
        public string DisplayName;

        [Tooltip("Thời lượng tối thiểu của Phase, tính bằng phút.\n" +
                 "Default: Preparation=3, Hunt=5, Lockdown=3")]
        public int DurationMin;

        [Tooltip("Thời lượng tối đa của Phase, tính bằng phút.\n" +
                 "Duration thực sẽ được Random.Range(DurationMin, DurationMax) * 60.\n" +
                 "Default: Preparation=4, Hunt=8, Lockdown=3")]
        public int DurationMax;

        [Header("Respawn")]
        [Tooltip("Cho phép hồi sinh trong Phase này không?\n" +
                 "Default: true cho cả 3 Phase.")]
        public bool RespawnEnabled;

        [Tooltip("Thời gian chờ hồi sinh (giây).\n" +
                 "Default: Preparation=5s, Hunt=7s, Lockdown=10s")]
        public float RespawnDelay;

        [Header("Multipliers")]
        [Tooltip("Nhân điểm số cho mọi action trong Phase này.\n" +
                 "Default: Preparation=1.0×, Hunt=2.0×, Lockdown=3.0×")]
        public float ScoreMultiplier;

        [Tooltip("Nhân điểm Survival mỗi phút sống sót trong Phase này.\n" +
                 "Default: Preparation=1.0×, Hunt=1.5×, Lockdown=2.0×")]
        public float SurvivalMultiplier;

        [Header("UI")]
        [Tooltip("Số giây trước khi Phase kết thúc để bắn PhaseWarningEvent.\n" +
                 "Default: 30s cho tất cả Phase.")]
        public float WarningTime;
    }

    [Serializable]
    public class ZoneConfigData
    {
        public string ZoneId;
        public string ZoneType; // "Safe", "Heal", "Toxic", "Gravity", "Speed", "Scanner"
        public float Radius;
        public float Duration;
        public float DamagePerSec;
        public float VisionMultiplier;
        public float SpeedMultiplier;
        public float StaminaRegen;
        public bool PingReveal;
        public string Description;
    }

    [Serializable]
    public class StaminaWeightConfigData
    {
        public string WeightThreshold; // "<=20kg", "20-30kg", etc.
        public float MoveSpeedPenalty;
        public float StaminaDrainMultiplier;
        public bool SprintAllowed;
    }

    [Serializable]
    public class InventoryConfigData
    {
        public int BackpackSlots;
        public float BaseWeightCapacity;
        public int QuickSlotCount;
        public float OverWeightSpeedPenalty;
        public float OverWeightStaminaPenalty;
    }

    [Serializable]
    public class ScoreSystemData
    {
        public string Action; // "Kill", "Assist", "BossKill", etc.
        public int BaseScore;
        public float PhaseMultiplier;
        public string Notes;
    }

    [Serializable]
    public class RankingSystemData
    {
        public string Event;
        public int RPValue;
        public string Condition;
    }

    [Serializable]
    public class CharacterConfigData
    {
        public string CharacterId;
        public string DisplayName;
        public int BaseHP;
        public int BaseStamina;
        public float BaseMoveSpeed;
        public float BaseWeightCapacity;
        public float BaseVisionRadius;
        public float BaseNoiseLevel;
        public float BaseReviveTime;
        public int BaseRevivedHP;
        public float BaseBleedoutTime;
        public float BaseADS_Slow;
    }

    [Serializable]
    public class StatusEffectConfigData
    {
        public string StatusId;
        public string StatusType; // "Buff" or "Debuff"
        public string TargetStat; // "HP", "Stamina", "MoveSpeed", "Vision", "Noise"
        public string Operation; // "Add", "Multiply", "DamageOverTime"
        public float Value;
        public float Duration;
        public bool Stackable;
        public string Description;
    }

    // ─────────────────────────────────────────────────────────────
    // RANK + ELO CONFIG
    // ─────────────────────────────────────────────────────────────
    [Serializable]
    public class RankTierConfigData
    {
        public string Tier;              // "Bronze", "Silver", "Gold", "Platinum", "Diamond"
        public int MinElo;
        public int MaxElo;
        public int PointsPerWin;
        public int PointsPerLoss;
        public int PointsPerDraw;
        public string IconKey;           // Resource key for rank badge sprite
    }

    [Serializable]
    public class RankMatchmakingConfigData
    {
        /// <summary>Initial ELO delta allowed when searching</summary>
        public int InitialEloDelta;
        /// <summary>How much to expand delta per expand interval</summary>
        public int EloDeltaExpandAmount;
        /// <summary>Seconds between each expansion step</summary>
        public float EloDeltaExpandInterval;
        /// <summary>Max ELO delta allowed (upper cap)</summary>
        public int MaxEloDelta;
        /// <summary>Seconds before auto-cancel matchmaking queue</summary>
        public float QueueTimeout;
    }
}

