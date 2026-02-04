namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines input states for the game.
    /// Used to enable/disable appropriate action maps.
    /// </summary>
    public enum InputState
    {
        /// <summary>Player is alive and can perform all actions</summary>
        PlayerAlive,
        
        /// <summary>Inventory UI is open</summary>
        InventoryOpen,
        
        /// <summary>Menu is open</summary>
        MenuOpen,
        
        /// <summary>Game is paused</summary>
        Paused,
        
        /// <summary>Camera-only mode (scout mode)</summary>
        ScoutMode,
        
        /// <summary>Player is dead (spectate mode)</summary>
        PlayerDead
    }
}