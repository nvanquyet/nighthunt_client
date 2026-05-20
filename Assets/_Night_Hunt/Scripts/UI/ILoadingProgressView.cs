using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    public interface ILoadingProgressView
    {
        float CurrentProgress { get; }
        void SetProgress(float normalizedProgress);
        void SetMessage(string message);
    }

    [DisallowMultipleComponent]
    public sealed class LoadingProgressView : MonoBehaviour, ILoadingProgressView
    {
        [Header("Progress Sources")]
        [SerializeField] private Slider unitySlider;
        [SerializeField] private Michsky.UI.Shift.SliderManager shiftSlider;
        [SerializeField] private Michsky.MUIP.ProgressBar modernProgressBar;

        [Header("Labels")]
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private TMP_Text percentLabel;

        public float CurrentProgress
        {
            get
            {
                if (unitySlider != null)
                    return unitySlider.normalizedValue;
                if (modernProgressBar != null)
                    return Mathf.InverseLerp(modernProgressBar.minValue, modernProgressBar.maxValue, modernProgressBar.currentPercent);
                return 0f;
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public void SetProgress(float normalizedProgress)
        {
            float clamped = Mathf.Clamp01(normalizedProgress);

            if (unitySlider != null)
                unitySlider.normalizedValue = clamped;

            if (modernProgressBar != null)
                modernProgressBar.SetValue(Mathf.Lerp(modernProgressBar.minValue, modernProgressBar.maxValue, clamped));

            if (percentLabel != null)
                percentLabel.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
        }

        public void SetMessage(string message)
        {
            if (messageLabel != null)
                messageLabel.text = message;
        }

        private void ResolveReferences()
        {
            if (shiftSlider != null && unitySlider == null)
                unitySlider = ShiftUIBridge.ResolveUnitySlider(shiftSlider);

            if (unitySlider == null)
                unitySlider = GetComponent<Slider>();

            if (modernProgressBar == null)
                modernProgressBar = GetComponent<Michsky.MUIP.ProgressBar>();
        }
    }
}
