
namespace NightHunt.Gameplay.Core.Events
{
    /// <summary>
    /// Base interface for gameplay events
    /// </summary>
    public interface IGameplayEvent
    {
        /// <summary>
        /// Event timestamp
        /// </summary>
        float Timestamp { get; }
    }
}

