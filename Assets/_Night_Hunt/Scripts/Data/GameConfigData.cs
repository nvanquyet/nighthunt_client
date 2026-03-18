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
        public BeaconConfigData BeaconConfig;
        public List<BossSpawnConfigData> BossSpawnConfig;
        public MatchEndConfigData MatchEndConfig;
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
        public string Phase;
        public int DurationMin;
        public int DurationMax;
        public bool RespawnEnabled;
        public bool BeaconEnabled;
        public string ZoneActive; // "No", "Medium", "Final"
        public string ObjectiveEvents; // "Low", "Yes", "High"
        public float ScoreMultiplier;
        public float SurvivalMultiplier;
        public string BuffForLosingTeam;
        public string NerfForWinningTeam;
        // Phase transition warning
        public float WarningTime;        // seconds before phase ends to show warning (default 30)
        // Phase 3 respawn
        public float Phase3RespawnDelay; // seconds delay before respawn in Phase 3 (default 10)
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
    // BEACON CONFIG
    // ─────────────────────────────────────────────────────────────
    [Serializable]
    public class BeaconConfigData
    {
        /// <summary>Max beacons a team can have active simultaneously</summary>
        public int MaxActivePerTeam;
        /// <summary>HP of a placed beacon</summary>
        public float BeaconHealth;
        /// <summary>Time (sec) to place a beacon</summary>
        public float PlaceTime;
        /// <summary>Relative weight of BeaconItem spawning in loot pool</summary>
        public float LootSpawnWeight;
    }

    // ─────────────────────────────────────────────────────────────
    // BOSS CONFIG
    // ─────────────────────────────────────────────────────────────
    [Serializable]
    public class BossSpawnConfigData
    {
        public string BossId;
        public string BossType;          // "Elite", "Champion", etc.
        public string SpawnPointTag;     // Tag used to find spawn Transform in scene
        public float MaxHP;
        public float MoveSpeed;
        public bool RespawnAfterKill;
        public float RespawnDelay;       // seconds before boss respawns (0 = no respawn)
        public List<BossDropEntryData> DropTable;

        // ── AI params ──────────────────────────────────────────────────────────
        [Tooltip("Radius within which boss detects players.")]
        public float AggroRadius = 20f;
        [Tooltip("Radius within which boss performs melee attack.")]
        public float AttackRadius = 3f;
        [Tooltip("Damage per hit.")]
        public float AttackDamage = 50f;
        [Tooltip("Seconds between attacks.")]
        public float AttackCooldown = 2f;
        [Tooltip("Seconds after death before the NetworkObject is despawned (lets death anim play).")]
        public float DespawnDelay = 3f;
    }

    [Serializable]
    public class BossDropEntryData
    {
        public string ItemId;
        public bool IsFixed;             // true = always drops, false = roll by weight
        public float Weight;             // 0-1, used when IsFixed = false
        public int MinQuantity;
        public int MaxQuantity;
    }

    // ─────────────────────────────────────────────────────────────
    // MATCH END CONFIG
    // ─────────────────────────────────────────────────────────────
    [Serializable]
    public class MatchEndConfigData
    {
        /// <summary>Seconds to show results screen before auto-navigate</summary>
        public float ResultsDisplayDuration;
        /// <summary>Countdown seconds after match end before results auto-dismiss</summary>
        public float PostMatchCountdown;
        /// <summary>Score per second holding a capture zone (base, before phase multiplier)</summary>
        public float CaptureZoneScorePerSecond;
        /// <summary>Minimum players needed inside zone to count as holding</summary>
        public int CaptureZoneMinPlayers;
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

