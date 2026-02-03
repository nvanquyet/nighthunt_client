using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Progress bar UI for consumable usage and hold interactions.
    /// </summary>
    public class ProgressBarUI : MonoBehaviour
    {
        [SerializeField] private GameObject progressBarRoot;
        [SerializeField] private Image progressBarFill;
        
        public void Show()
        {
            progressBarRoot.SetActive(true);
            progressBarFill.fillAmount = 0f;
        }
        
        public void UpdateProgress(float progress)
        {
            progressBarFill.fillAmount = Mathf.Clamp01(progress);
        }
        
        public void Hide()
        {
            progressBarRoot.SetActive(false);
        }
    }
}
