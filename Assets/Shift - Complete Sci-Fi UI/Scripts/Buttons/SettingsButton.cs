using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Michsky.UI.Shift
{
    public class SettingsButton : MonoBehaviour, IPointerEnterHandler
    {
        [Header("Resources")]
        public Image detailImage;
        public Image detailIcon;
        public Image detailBackground;
        public TextMeshProUGUI detailTitle;
        public TextMeshProUGUI detailDescription;
        public TextMeshProUGUI buttonTitleObj;

        [Header("Content")]
        public bool useCustomContent;
        public string buttonTitle;

        [Header("Preview")]
        public bool enableIconPreview;
        public string title;
        [TextArea] public string description;
        public Sprite imageSprite;
        public Sprite iconSprite;
        public Sprite iconBackground;

        void Start()
        {
            if (useCustomContent == false && buttonTitleObj != null) { buttonTitleObj.text = buttonTitle; }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (enableIconPreview == true)
            {
                if (detailImage != null) detailImage.gameObject.SetActive(false);
                if (detailIcon != null)
                {
                    detailIcon.gameObject.SetActive(true);
                    detailIcon.sprite = iconSprite;
                }
                if (detailBackground != null)
                {
                    detailBackground.gameObject.SetActive(true);
                    detailBackground.sprite = iconBackground;
                }
            }

            else
            {
                if (detailImage != null)
                {
                    detailImage.gameObject.SetActive(true);
                    detailImage.sprite = imageSprite;
                }
                if (detailIcon != null) detailIcon.gameObject.SetActive(false);
                if (detailBackground != null) detailBackground.gameObject.SetActive(false);
            }

            if (detailTitle != null) detailTitle.text = title;
            if (detailDescription != null) detailDescription.text = description;
        }
    }
}
