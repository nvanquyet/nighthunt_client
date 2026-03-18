using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// One stat row in the PlayerHUDPanel.
    /// Spawned at runtime by PlayerHUDPanel for every stat that has ShowInUI = true
    /// in <see cref="NightHunt.Gameplay.StatSystem.Configs.PlayerStatUIConfig"/>.
    ///
    /// Prefab layout (suggested):
    ///   StatRow (StatRowEntry)
    ///     ├─ Lbl        (TextMeshProUGUI)  ← left label, e.g. "HP"
    ///     ├─ Slider     (Slider)           ← current / max ratio
    ///     └─ ValueText  (TextMeshProUGUI)  ← "100 / 100" or formatted value
    ///
    /// The asset is drag-and-dropped into PlayerHUDPanel._statRowPrefab in the Inspector.
    /// PlayerHUDPanel sets <see cref="StatType"/> after instantiation.
    /// </summary>
    public class StatRowEntry : MonoBehaviour
    {
        [Tooltip("Filled by PlayerHUDPanel after Instantiate – do NOT set in the prefab.")]
        public PlayerStatType StatType;

        [Tooltip("Optional slider showing current / max ratio (0–1).")]
        public Slider Slider;

        [Tooltip("Optional text label showing the formatted value string.")]
        public TextMeshProUGUI ValueText;

        [Tooltip("Optional accent image whose color is set from PlayerStatUIConfig.AccentColor.")]
        public UnityEngine.UI.Image AccentImage;
    }
}
