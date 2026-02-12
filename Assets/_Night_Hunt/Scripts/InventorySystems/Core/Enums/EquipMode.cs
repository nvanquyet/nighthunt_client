namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines how items behave when equipped to QuickSlot/Equipment slots.
    /// Used to determine if entire stack equips or only single item.
    /// </summary>
    public enum EquipMode
    {
        /// <summary>
        /// Only equip 1 item, rest stays in inventory (Weapons, Armor, Helmets)
        /// </summary>
        Single,
        
        /// <summary>
        /// Equip entire stack to slot (Consumables like bandages, ammo)
        /// </summary>
        StackAll
    }
}