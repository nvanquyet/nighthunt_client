using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace _Night_Hunt.Scripts.InventorySystems.Core.Interfaces
{
    /// <summary>
    /// Public API for EquipmentSystem.
    /// </summary>
    public interface IEquipmentSystem
    {
        // === Query ===
        ItemInstance GetEquippedItem(EquipmentSlotType slotType);
        bool IsSlotEquipped(EquipmentSlotType slotType);
        bool CanEquip(ItemInstance item, EquipmentSlotType slotType);
        
        // === Equip/Unequip ===
        OperationResult EquipItem(ItemInstance item, EquipmentSlotType slotType);
        OperationResult UnequipItem(EquipmentSlotType slotType, out ItemInstance unequippedItem);
        OperationResult SwapEquipment(ItemInstance newItem, EquipmentSlotType slotType, out ItemInstance oldItem);
        
        // === Utility ===
        void UnequipAll();
        EquipmentSlotType[] GetAllSlotTypes();
    }
}