using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.GameplaySystems.UI
{
    /// <summary>
    /// Single row in the LootContainerUI panel.
    /// Attach this to the LootItemRow prefab so LootContainerUI can wire up
    /// fields via direct references instead of fragile string-based Find().
    /// Layout: [Icon] [Name] [Qty] [TakeButton]
    /// </summary>
    public class LootItemRow : MonoBehaviour
    {
        [SerializeField] private Image           _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _qtyText;
        [SerializeField] private Button          _takeButton;

        public Image           Icon       => _icon;
        public TextMeshProUGUI NameText   => _nameText;
        public TextMeshProUGUI QtyText    => _qtyText;
        public Button          TakeButton => _takeButton;
    }
}
