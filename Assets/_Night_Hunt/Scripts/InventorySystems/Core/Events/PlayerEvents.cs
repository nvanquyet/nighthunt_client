using System;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Events from player/combat system.
    /// </summary>
    public static class PlayerEvents
    {
        public static event Action<float> OnPlayerDamaged; // damage amount
        public static event Action<object> OnPlayerDied; // NetworkPlayer
        
        public static void InvokePlayerDamaged(float damage) => OnPlayerDamaged?.Invoke(damage);
        public static void InvokePlayerDied(object player) => OnPlayerDied?.Invoke(player);
    }
}