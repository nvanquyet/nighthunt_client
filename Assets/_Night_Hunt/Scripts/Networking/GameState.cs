namespace NightHunt.Networking
{
    /// <summary>
    /// Server game state machine
    /// Controls match flow and player spawning eligibility
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// Server starting, systems not ready
        /// </summary>
        INITIALIZING,
        
        /// <summary>
        /// Bootstrap completed, services initialized
        /// </summary>
        SERVICES_READY,
        
        /// <summary>
        /// Waiting for minimum players (optional phase)
        /// </summary>
        WAITING_FOR_PLAYERS,
        
        /// <summary>
        /// Ready to spawn players, accepting connections
        /// </summary>
        READY_FOR_SPAWN,
        
        /// <summary>
        /// Match in progress, late join based on config
        /// </summary>
        GAME_IN_PROGRESS,
        
        /// <summary>
        /// Match ended, preparing for cleanup
        /// </summary>
        MATCH_ENDED
    }
}