using UnityEngine;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Data;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.Stats
{
    /// <summary>
    /// Manages weapon stats with modifier tracking.
    /// Separate from CharacterStats - weapons have their own stats modified by attachments.
    /// Example: Grip attachment → reduces Recoil stat on THIS weapon, not character.
    /// Formula: FinalStat = (BaseStat × (1 + Σ Percentage)) + Σ Flat
    /// </summary>
    public class WeaponStats : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ItemInstance weaponInstance;
        
        [Header("Base Weapon Stats")]
        [SerializeField] private float baseDamage = 30f;
        [SerializeField] private float baseFireRate = 600f; // Rounds per minute
        [SerializeField] private float baseRecoil = 1f;
        [SerializeField] private float baseRange = 100f;
        [SerializeField] private float baseAccuracy = 0.95f;
        [SerializeField] private int baseMagazineSize = 30;
        [SerializeField] private float baseReloadSpeed = 1f; // Multiplier
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Modifier storage: sourceId → List of modifiers from that source
        private Dictionary<string, List<WeaponStatModifier>> modifiersBySource = new Dictionary<string, List<WeaponStatModifier>>();
        
        // Cached final stats (recalculated when modifiers change)
        private Dictionary<WeaponStatType, float> cachedFinalStats = new Dictionary<WeaponStatType, float>();
        
        // === Lifecycle ===
        
        void Awake()
        {
            InitializeStats();
        }
        
        void OnEnable()
        {
            AttachmentEvents.OnAttachmentAttached += HandleAttachmentAttached;
            AttachmentEvents.OnAttachmentDetached += HandleAttachmentDetached;
        }
        
        void OnDisable()
        {
            AttachmentEvents.OnAttachmentAttached -= HandleAttachmentAttached;
            AttachmentEvents.OnAttachmentDetached -= HandleAttachmentDetached;
        }
        
        // === Initialization ===
        
        void InitializeStats()
        {
            cachedFinalStats[WeaponStatType.Damage] = baseDamage;
            cachedFinalStats[WeaponStatType.FireRate] = baseFireRate;
            cachedFinalStats[WeaponStatType.Recoil] = baseRecoil;
            cachedFinalStats[WeaponStatType.Range] = baseRange;
            cachedFinalStats[WeaponStatType.Accuracy] = baseAccuracy;
            cachedFinalStats[WeaponStatType.MagazineSize] = baseMagazineSize;
            cachedFinalStats[WeaponStatType.ReloadSpeed] = baseReloadSpeed;
        }
        
        // === Public API - Setup ===
        
        /// <summary>
        /// Initialize weapon stats from item instance.
        /// Call this when weapon is equipped.
        /// </summary>
        public void Initialize(ItemInstance weapon)
        {
            weaponInstance = weapon;
            
            // Load base stats from weapon definition (if available)
            // TODO: Add weapon stat definitions to ItemDefinition
            
            // Apply attachment modifiers
            if (weapon.AttachedItems != null && weapon.AttachedItems.Count > 0)
            {
                foreach (var attachment in weapon.AttachedItems)
                {
                    ApplyAttachmentModifiers(attachment);
                }
            }
            
            RecalculateAllStats();
        }
        
        // === Event Handlers ===
        
        void HandleAttachmentAttached(ItemInstance parent, ItemInstance attachment, AttachmentSlotType slotType)
        {
            // Only process if this is our weapon
            if (parent != weaponInstance)
                return;
            
            ApplyAttachmentModifiers(attachment);
            RecalculateAllStats();
        }
        
        void HandleAttachmentDetached(ItemInstance parent, ItemInstance attachment, AttachmentSlotType slotType)
        {
            // Only process if this is our weapon
            if (parent != weaponInstance)
                return;
            
            RemoveAttachmentModifiers(attachment);
            RecalculateAllStats();
        }
        
        // === Modifier Management ===
        
        void ApplyAttachmentModifiers(ItemInstance attachment)
        {
            if (attachment == null || attachment.Definition == null)
                return;
            
            string sourceId = attachment.GetModifierSourceId();
            
            var modifiers = attachment.GetStatModifiers()
                .Where(m => m.Target == StatModifierTarget.Weapon)
                .ToList();
            
            if (modifiers.Count == 0)
                return;
            
            if (!modifiersBySource.ContainsKey(sourceId))
            {
                modifiersBySource[sourceId] = new List<WeaponStatModifier>();
            }
            
            foreach (var modData in modifiers)
            {
                var modifier = new WeaponStatModifier
                {
                    StatType = modData.WeaponStat,
                    CalculationType = modData.CalculationType,
                    Value = modData.Value,
                    SourceId = sourceId
                };
                
                modifiersBySource[sourceId].Add(modifier);
                
                Log($"Applied modifier: {modData.WeaponStat} {modData.CalculationType} {modData.Value:F2} from {sourceId}");
            }
        }
        
        void RemoveAttachmentModifiers(ItemInstance attachment)
        {
            if (attachment == null)
                return;
            
            string sourceId = attachment.GetModifierSourceId();
            
            if (!modifiersBySource.ContainsKey(sourceId))
                return;
            
            modifiersBySource.Remove(sourceId);
            Log($"Removed modifiers from: {sourceId}");
        }
        
        // === Stat Calculation ===
        
        void RecalculateStat(WeaponStatType statType)
        {
            float baseValue = GetBaseStat(statType);
            
            // Collect all modifiers for this stat
            var allModifiers = modifiersBySource.Values
                .SelectMany(list => list)
                .Where(m => m.StatType == statType)
                .ToList();
            
            // Separate by type
            var percentageModifiers = allModifiers.Where(m => m.CalculationType == ModifierCalculationType.Percentage).ToList();
            var flatModifiers = allModifiers.Where(m => m.CalculationType == ModifierCalculationType.Flat).ToList();
            
            // Calculate: (Base × (1 + Σ Percentage)) + Σ Flat
            float percentageSum = percentageModifiers.Sum(m => m.Value);
            float flatSum = flatModifiers.Sum(m => m.Value);
            
            float finalValue = (baseValue * (1f + percentageSum)) + flatSum;
            
            // Clamp to reasonable values
            finalValue = Mathf.Max(0f, finalValue);
            
            // Special clamping for accuracy (0-1 range)
            if (statType == WeaponStatType.Accuracy)
            {
                finalValue = Mathf.Clamp01(finalValue);
            }
            
            cachedFinalStats[statType] = finalValue;
            
            Log($"Recalculated {statType}: Base={baseValue:F2}, %={percentageSum:F2}, Flat={flatSum:F2}, Final={finalValue:F2}");
        }
        
        void RecalculateAllStats()
        {
            RecalculateStat(WeaponStatType.Damage);
            RecalculateStat(WeaponStatType.FireRate);
            RecalculateStat(WeaponStatType.Recoil);
            RecalculateStat(WeaponStatType.Range);
            RecalculateStat(WeaponStatType.Accuracy);
            RecalculateStat(WeaponStatType.MagazineSize);
            RecalculateStat(WeaponStatType.ReloadSpeed);
            
            Log("Recalculated all weapon stats");
        }
        
        // === Public API - Get Stats ===
        
        public float GetFinalStat(WeaponStatType statType)
        {
            return cachedFinalStats.ContainsKey(statType) ? cachedFinalStats[statType] : 0f;
        }
        
        public float GetBaseStat(WeaponStatType statType)
        {
            switch (statType)
            {
                case WeaponStatType.Damage: return baseDamage;
                case WeaponStatType.FireRate: return baseFireRate;
                case WeaponStatType.Recoil: return baseRecoil;
                case WeaponStatType.Range: return baseRange;
                case WeaponStatType.Accuracy: return baseAccuracy;
                case WeaponStatType.MagazineSize: return baseMagazineSize;
                case WeaponStatType.ReloadSpeed: return baseReloadSpeed;
                default: return 0f;
            }
        }
        
        // Convenience getters
        public float GetDamage() => GetFinalStat(WeaponStatType.Damage);
        public float GetFireRate() => GetFinalStat(WeaponStatType.FireRate);
        public float GetRecoil() => GetFinalStat(WeaponStatType.Recoil);
        public float GetRange() => GetFinalStat(WeaponStatType.Range);
        public float GetAccuracy() => GetFinalStat(WeaponStatType.Accuracy);
        public int GetMagazineSize() => Mathf.RoundToInt(GetFinalStat(WeaponStatType.MagazineSize));
        public float GetReloadSpeed() => GetFinalStat(WeaponStatType.ReloadSpeed);
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[WeaponStats] {message}");
        }
        
        public Dictionary<string, List<WeaponStatModifier>> GetAllModifiers()
        {
            return new Dictionary<string, List<WeaponStatModifier>>(modifiersBySource);
        }
    }
    
    /// <summary>
    /// Weapon stat modifier data structure.
    /// </summary>
    [System.Serializable]
    public struct WeaponStatModifier
    {
        public WeaponStatType StatType;
        public ModifierCalculationType CalculationType;
        public float Value;
        public string SourceId;
    }
}