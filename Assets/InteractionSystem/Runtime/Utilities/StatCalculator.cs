using System.Collections.Generic;
using System.Linq;
using NightHunt.InteractionSystem.Core;

namespace NightHunt.InteractionSystem.Utilities
{
    public class StatCalculator
{
    private const int MaxModifierPriority = 100;
    
    public StatModifier[] CalculateFinalStats(EquipmentDataBase equipment, AttachmentData[] attachments)
    {
        // Start with base stats
        List<StatModifier> allModifiers = new List<StatModifier>(equipment.baseModifiers);
        
        // Add attachment modifiers
        foreach (var attachment in attachments)
        {
            if (attachment != null && attachment.modifiers != null)
            {
                allModifiers.AddRange(attachment.modifiers);
            }
        }
        
        // Group by stat type
        var groupedStats = allModifiers.GroupBy(m => m.statType);
        
        List<StatModifier> finalStats = new List<StatModifier>();
        
        foreach (var group in groupedStats)
        {
            float finalValue = CalculateStat(group.ToList());
            
            finalStats.Add(new StatModifier
            {
                statType = group.Key,
                modifierType = ModifierType.Override,
                value = finalValue,
                priority = MaxModifierPriority,
                sourceId = "Final"
            });
        }
        
        return finalStats.ToArray();
    }
    
    private float CalculateStat(List<StatModifier> modifiers)
    {
        // Sort by priority (lower = apply first)
        modifiers.Sort((a, b) => a.priority.CompareTo(b.priority));
        
        float baseValue = 0f;
        float additiveSum = 0f;
        float multiplicativeFactor = 1f;
        
        foreach (var modifier in modifiers)
        {
            switch (modifier.modifierType)
            {
                case ModifierType.Additive:
                    additiveSum += modifier.value;
                    break;
                
                case ModifierType.Multiplicative:
                    multiplicativeFactor *= modifier.value;
                    break;
                
                case ModifierType.Override:
                    baseValue = modifier.value;
                    break;
            }
        }
        
        // Apply in order: Base → Additive → Multiplicative
        float result = baseValue + additiveSum;
        result *= multiplicativeFactor;
        
        return result;
    }
    
    public static string GetStatDescription(StatModifier modifier)
    {
        string sign = modifier.value >= 0 ? "+" : "";
        string valueStr = modifier.modifierType == ModifierType.Multiplicative
            ? $"x{modifier.value:F2}"
            : $"{sign}{modifier.value:F1}";
        
        return $"{modifier.statType}: {valueStr}";
    }
}
}