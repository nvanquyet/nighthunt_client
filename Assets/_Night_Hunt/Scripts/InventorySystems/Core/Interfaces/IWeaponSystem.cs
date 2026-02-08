using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace _Night_Hunt.Scripts.InventorySystems.Core.Interfaces
{
    /// <summary>
    /// Public API for WeaponSystem.
    /// </summary>
    public interface IWeaponSystem
    {
        // === Query ===
        ItemInstance GetEquippedWeapon(WeaponSlotType slotType);
        ItemInstance GetActiveWeapon();
        WeaponSlotType GetActiveWeaponSlot();
        bool IsSlotEquipped(WeaponSlotType slotType);
        bool CanEquip(ItemInstance weapon, WeaponSlotType slotType);
        
        // === Equip/Unequip ===
        OperationResult EquipWeapon(ItemInstance weapon, WeaponSlotType slotType);
        OperationResult UnequipWeapon(WeaponSlotType slotType, out ItemInstance unequippedWeapon);
        OperationResult SwapWeapon(ItemInstance newWeapon, WeaponSlotType slotType, out ItemInstance oldWeapon);
        
        // === Weapon Switching ===
        OperationResult SwitchToWeapon(WeaponSlotType slotType);
        OperationResult SwitchToNextWeapon();
        OperationResult SwitchToPreviousWeapon();
        
        // === Ammo ===
        OperationResult Reload(int ammoAmount);
        int GetCurrentAmmo();
        int GetMaxAmmo();
    }
}