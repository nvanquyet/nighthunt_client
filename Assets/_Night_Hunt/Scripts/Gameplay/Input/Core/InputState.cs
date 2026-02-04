namespace NightHunt.Gameplay.Input
{
    /// <summary>
    /// Input states for state machine
    /// </summary>
    public enum InputState
    {
        None,
        /// <summary>
        /// Player is alive and can control character
        /// </summary>
        PlayerAlive,

        /// <summary>
        /// Player is dead or downed
        /// </summary>
        PlayerDead,

        /// <summary>
        /// Player is spectating
        /// </summary>
        Spectating,

        /// <summary>
        /// UI menu is open
        /// </summary>
        MenuOpen,

        /// <summary>
        /// Game is paused
        /// </summary>
        Paused,

        /// <summary>
        /// Scout mode is active
        /// </summary>
        ScoutMode,

        /// <summary>
        /// Camera controls (rotation, zoom)
        /// </summary>
        Camera,

        /// <summary>
        /// Inventory UI is open
        /// </summary>
        InventoryOpen,
        
        /// <summary>
        ///  In dialogue (only UI choices)
        /// </summary>
        InDialogue
    }
}

