using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Result of an equip/unequip operation.
    /// </summary>
    public struct EquipResult
    {
        public bool IsSuccess;
        public string FailReason;
        public ItemInstance SwappedItem;
        
        public static EquipResult Success() => new EquipResult { IsSuccess = true };
        public static EquipResult Swapped(ItemInstance old) => new EquipResult { IsSuccess = true, SwappedItem = old };
        public static EquipResult Fail(string reason) => new EquipResult { IsSuccess = false, FailReason = reason };
    }
}
