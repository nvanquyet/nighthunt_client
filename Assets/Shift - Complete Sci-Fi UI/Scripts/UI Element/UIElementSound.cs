using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Michsky.UI.Shift
{
    [ExecuteInEditMode]
    public class UIElementSound : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        [Header("Resources")]
        public UIManager UIManagerAsset;
        public AudioSource audioObject;

        [Header("Custom SFX")]
        public AudioClip hoverSFX;
        public AudioClip clickSFX;

        [Header("Settings")]
        public bool enableHoverSound = true;
        public bool enableClickSound = true;
        public bool checkForInteraction = true;

        private Button sourceButton;

        void OnEnable()
        {
            if (UIManagerAsset == null)
            {
                try { UIManagerAsset = Resources.Load<UIManager>("Shift UI Manager"); }
                catch { Debug.Log("<b>[UI Element Sound]</b> No UI Manager found.", this); this.enabled = false; }
            }

            if (Application.isPlaying == true && audioObject == null)
            {
                GameObject uiAudio = GameObject.Find("UI Audio");
                if (uiAudio != null)
                    audioObject = uiAudio.GetComponent<AudioSource>();
            }

            if (checkForInteraction == true) { sourceButton = gameObject.GetComponent<Button>(); }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (checkForInteraction == true && sourceButton != null && sourceButton.interactable == false)
                return;

            if (enableHoverSound == true)
            {
                AudioClip clip = hoverSFX != null ? hoverSFX : (UIManagerAsset != null ? UIManagerAsset.hoverSound : null);
                PlaySound(clip);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (checkForInteraction == true && sourceButton != null && sourceButton.interactable == false)
                return;

            if (enableClickSound == true)
            {
                AudioClip clip = clickSFX != null ? clickSFX : (UIManagerAsset != null ? UIManagerAsset.clickSound : null);
                PlaySound(clip);
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioObject == null || clip == null)
                return;

            audioObject.PlayOneShot(clip);
        }
    }
}
