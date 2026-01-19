namespace NightHunt.Gameplay.Core.Networking
{
    /// <summary>
    /// Interface for objects that need network synchronization
    /// </summary>
    /// <typeparam name="T">Type of data to sync</typeparam>
    public interface ISyncable<T>
    {
        /// <summary>
        /// Get current state for synchronization
        /// </summary>
        T GetSyncState();

        /// <summary>
        /// Apply synchronized state
        /// </summary>
        void ApplySyncState(T state);

        /// <summary>
        /// Check if state needs synchronization
        /// </summary>
        bool NeedsSync(T lastSyncedState);
    }
}

