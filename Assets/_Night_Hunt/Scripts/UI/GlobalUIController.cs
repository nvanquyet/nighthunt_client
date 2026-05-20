using NightHunt.Config;
using NightHunt.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Manages global UI states that persist across panels (UI Scale, Crosshair).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GlobalUIController : SingletonPersistent<GlobalUIController>
    {
        [Header("References")]
        [SerializeField] private CanvasScaler mainCanvasScaler;
        [SerializeField] private CrosshairController crosshairController;

        protected override void OnSingletonAwake()
        {
            ApplyAll();
            GameSettings.OnSettingsChanged += ApplyAll;
        }

        protected override void OnDestroy()
        {
            GameSettings.OnSettingsChanged -= ApplyAll;
            base.OnDestroy();
        }

        private void Start()
        {
            // Re-apply on start to ensure everything is synced
            ApplyAll();
        }

        public void ApplyAll()
        {
            var settings = GameSettings.Instance;
            if (settings == null) return;

            ApplyUIScale(settings.UIScale);
            ApplyCrosshair(settings.CrosshairType);
        }

        public void ApplyUIScale(float scale)
        {
            if (mainCanvasScaler == null)
                mainCanvasScaler = GameObject.Find("3. UI/Canvas")?.GetComponent<CanvasScaler>();

            if (mainCanvasScaler != null)
            {
                // We use ConstantPixelSize and adjust scaleFactor for precise control
                mainCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                mainCanvasScaler.scaleFactor = scale;
            }
        }

        public void ApplyCrosshair(int typeIndex)
        {
            if (crosshairController == null)
                crosshairController = FindFirstObjectByType<CrosshairController>(FindObjectsInactive.Include);

            if (crosshairController != null)
            {
                crosshairController.SetCrosshairType(typeIndex);
            }
        }

        private void OnValidate()
        {
            if (mainCanvasScaler == null)
                mainCanvasScaler = GameObject.Find("3. UI/Canvas")?.GetComponent<CanvasScaler>();
        }
    }
}
