using TMPro;
using UnityEngine;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// A performance-optimized FPS Counter that calculates smoothed FPS and updates a TMP text field.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    [DisallowMultipleComponent]
    public class FPSCounter : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float updateInterval = 0.5f; // Update twice a second

        private TextMeshProUGUI fpsText;
        private float accum = 0f;
        private int frames = 0;
        private float timeLeft;

        private void Awake()
        {
            fpsText = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            timeLeft = updateInterval;
        }

        private void Update()
        {
            timeLeft -= Time.unscaledDeltaTime;
            accum += Time.unscaledDeltaTime;
            ++frames;

            // Interval ended - update text and reset values
            if (timeLeft <= 0.0f)
            {
                float fps = frames / accum;
                UpdateDisplay(fps);

                timeLeft = updateInterval;
                accum = 0.0f;
                frames = 0;
            }
        }

        private void UpdateDisplay(float fps)
        {
            if (fpsText == null) return;

            // Determine appropriate color based on target frame rates
            string color = "red";
            if (fps >= 58f)
            {
                color = "#00FF66"; // Sleek neon green
            }
            else if (fps >= 30f)
            {
                color = "#FFCC00"; // Sleek warm yellow
            }
            else
            {
                color = "#FF3333"; // Vibrant red
            }

            fpsText.text = $"FPS: <color={color}>{Mathf.RoundToInt(fps)}</color>";
        }

        private void OnEnable()
        {
            // Reset counter when turned on to prevent spike/lag representation
            timeLeft = updateInterval;
            accum = 0.0f;
            frames = 0;
        }
    }
}
