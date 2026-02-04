using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Domain.Equipment
{
    /// <summary>
    /// Result of an equip operation.
    /// </summary>
    public struct EquipResult
    {
        public bool IsSuccess;
        public string FailReason;
        public ItemInstance SwappedItem; // Null if no swap occurred

        public static EquipResult Success()
        {
            return new EquipResult { IsSuccess = true };
        }

        public static EquipResult Swapped(ItemInstance oldItem)
        {
            return new EquipResult
            {
                IsSuccess = true,
                SwappedItem = oldItem
            };
        }

        public static EquipResult Fail(string reason)
        {
            return new EquipResult
            {
                IsSuccess = false,
                FailReason = reason
            };
        }
    }
}