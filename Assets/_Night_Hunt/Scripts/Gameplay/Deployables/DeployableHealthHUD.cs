using UnityEngine;
using UnityEngine.UI;
using NightHunt.Gameplay.Deployables;
using NightHunt.Gameplay.Spectator;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Attach to a world canvas child of a Deployable item.
    /// Shows the HP bar when the deployable takes damage, and hides it after a delay.
    /// Billboard effect uses the currently spectated player's camera to always face the screen.
    /// </summary>
    public class DeployableHealthHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BaseDeployable _deployable;
        [SerializeField] private Slider _hpSlider;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Settings")]
        [Tooltip("How long (in seconds) the HP bar stays visible after taking damage")]
        [SerializeField] private float _visibleDuration = 3f;

        private float _hideTimer;
        private UnityEngine.Camera _mainCamera;

        private void Awake()
        {
            if (_deployable == null)
                _deployable = GetComponentInParent<BaseDeployable>();
                
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            if (_deployable != null)
                _deployable.OnHealthChangedClient += HandleHealthChanged;
        }

        private void OnDisable()
        {
            if (_deployable != null)
                _deployable.OnHealthChangedClient -= HandleHealthChanged;
        }

        private void Start()
        {
            _mainCamera = UnityEngine.Camera.main;
            if (_hpSlider != null && _deployable != null)
            {
                _hpSlider.maxValue = _deployable.MaxHP > 0 ? _deployable.MaxHP : 100;
                _hpSlider.value = _deployable.CurrentHP;
            }
        }

        private void Update()
        {
            if (_canvasGroup != null && _canvasGroup.alpha > 0f)
            {
                _hideTimer -= Time.deltaTime;
                if (_hideTimer <= 0f)
                {
                    _canvasGroup.alpha = 0f;
                }

                BillboardToCamera();
            }
        }

        private void HandleHealthChanged(int oldHP, int newHP)
        {
            if (newHP < oldHP)
            {
                ShowHUD();
            }

            if (_hpSlider != null)
            {
                if (_deployable != null && _deployable.MaxHP > 0)
                    _hpSlider.maxValue = _deployable.MaxHP;
                    
                _hpSlider.value = newHP;
            }
        }

        private void ShowHUD()
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;

            _hideTimer = _visibleDuration;
            BillboardToCamera();
        }

        private void BillboardToCamera()
        {
            UnityEngine.Camera targetCam = _mainCamera != null ? _mainCamera : UnityEngine.Camera.main;

            if (targetCam != null)
            {
                transform.rotation = targetCam.transform.rotation;
            }
        }
    }
}
