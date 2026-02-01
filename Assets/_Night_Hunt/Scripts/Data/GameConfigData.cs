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

        // New fields for enhanced item system
        [Header("Item Type & Usage")]
        public NightHunt.Gameplay.Inventory.ItemType itemType = NightHunt.Gameplay.Inventory.ItemType.Misc;
        public float useDuration = 0f; // Thời gian sử dụng (cho consumable) - 0 = instant
        public bool canCancelUse = true; // Có thể hủy khi đang sử dụng

        [Header("Equipment Slots")]
        public List<ItemEquipmentSlotConfig> equipmentSlots = new List<ItemEquipmentSlotConfig>(); // Nested equipment slots

        [Header("Shop")]
        public int price = 0; // Giá tiền mua (cho shop)
        public bool isSellable = false; // Có thể bán
        public int sellPrice = 0; // Giá bán

        [System.Serializable]
        public class ItemEquipmentSlotConfig
        {
            public string slotId; // "Grip", "Scope", "Magazine", etc.
            public string displayName;
            public string allowedItemCategory; // Category of items that can be attached
        }
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
}

