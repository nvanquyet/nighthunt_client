using UnityEngine;
using TMPro;

namespace NightHunt.UI
{
    /// <summary>
    /// Kill feed item component
    /// </summary>
    public class KillFeedItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;

        public void Initialize(string actorName, string targetName, string weaponName, KillFeedType type, Color color)
        {
            if (_text == null)
                _text = GetComponentInChildren<TextMeshProUGUI>();

            if (_text != null)
            {
                string message = FormatMessage(actorName, targetName, weaponName, type);
                _text.text = message;
                _text.color = color;
            }
        }

        private string FormatMessage(string actor, string target, string weapon, KillFeedType type)
        {
            switch (type)
            {
                case KillFeedType.Kill:
                    if (string.IsNullOrEmpty(weapon))
                    {
                        return $"{actor} killed {target}";
                    }
                    return $"{actor} killed {target} with {weapon}";

                case KillFeedType.Assist:
                    return $"{actor} assisted in killing {target}";

                case KillFeedType.Death:
                    if (string.IsNullOrEmpty(actor))
                    {
                        return $"{target} died";
                    }
                    return $"{target} was killed by {actor}";

                default:
                    return "";
            }
        }
    }
}
