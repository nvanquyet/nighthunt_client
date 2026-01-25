using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core
{
     public abstract class EquipmentDataBase : ItemDataBase
     {
          [Header("Equipment")]
          public EquipmentSlot equipmentSlot;
          public float durability = 100f;
    
          [Header("Universal Attachments")]
          public AttachmentSlotDefinition[] attachmentSlots;
    
          [Header("Base Stats")]
          public StatModifier[] baseModifiers;
    
          public virtual StatModifier[] GetFinalStats(List<AttachmentInstance> attachments)
          {
               // Will be implemented in StatCalculator
               return baseModifiers;
          }
     }

     public enum EquipmentSlot
     {
          PrimaryWeapon, SecondaryWeapon, Melee,
          Head, Chest, Backpack
     }
}