
namespace NightHunt.Gameplay.Core.Config
{
    /// <summary>
    /// Interface for configurable objects
    /// </summary>
    /// <typeparam name="T">Type of configuration</typeparam>
    public interface IConfigurable<T>
    {
        /// <summary>
        /// Load configuration
        /// </summary>
        void LoadConfig(T config);

        /// <summary>
        /// Validate configuration
        /// </summary>
        bool ValidateConfig(T config);

        /// <summary>
        /// Apply configuration
        /// </summary>
        void ApplyConfig(T config);
    }
}

