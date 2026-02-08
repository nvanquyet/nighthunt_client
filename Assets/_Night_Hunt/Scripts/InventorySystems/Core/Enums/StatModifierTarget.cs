namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines what the stat modifier targets (Character vs Weapon).
    /// Example: Flashlight on helmet → modifies Character.VisionRadius
    ///          Grip on weapon → modifies Weapon.Recoil
    /// </summary>
    public enum StatModifierTarget
    {
        /// <summary>Modifies character stats (HP, MoveSpeed, VisionRadius, etc.)</summary>
        Character,
        
        /// <summary>Modifies weapon stats (Damage, Recoil, Accuracy, etc.)</summary>
        Weapon
    }
}