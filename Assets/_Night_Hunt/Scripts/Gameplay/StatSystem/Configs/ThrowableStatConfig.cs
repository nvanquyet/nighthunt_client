using UnityEngine;

namespace NightHunt.Gameplay.StatSystem.Configs
{
    /// <summary>
    /// Throwable stat config - Stats only (typically Weight).
    /// No PlayerModifiers / ItemModifiers / Effects.
    /// </summary>
    [CreateAssetMenu(fileName = "ThrowableStatConfig", menuName = "NightHunt/StatSystem/Throwable Stat Config")]
    public class ThrowableStatConfig : ItemStatConfig { }
}
