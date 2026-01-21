namespace NightHunt.Gameplay.Combat
{
    /// <summary>
    /// Interface for objects that can deal damage (attackers)
    /// </summary>
    public interface IAttackSource
    {
        /// <summary>
        /// Get attacker network object ID
        /// </summary>
        uint GetAttackerId();

        /// <summary>
        /// Get attacker team ID
        /// </summary>
        int GetTeamId();

        /// <summary>
        /// Get weapon/item ID used for attack
        /// </summary>
        string GetWeaponId();

        /// <summary>
        /// Get damage multiplier
        /// </summary>
        float GetDamageMultiplier();
    }
}

