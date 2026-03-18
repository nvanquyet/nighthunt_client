using UnityEngine;
using NightHunt.Core;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.Input.Handlers.Camera;
using NightHunt.Gameplay.Input.Handlers.Inventory;
using NightHunt.Gameplay.Input.Handlers.UI;
using NightHunt.Gameplay.Input.Handlers.Interaction;

namespace NightHunt.Gameplay.Input.Core
{
    /// <summary>
    /// Facade cung cấp tham chiếu nhanh tới các handler cụ thể.
    /// <para><b>Quản lý state / enable-disable ActionMap:</b>
    /// Dùng <see cref="InputLayerManager"/> – đây là Single Source of Truth.</para>
    /// <para><b>Không bao giờ</b> gọi <c>map.Enable()</c> / <c>map.Disable()</c> từ đây.</para>
    /// </summary>
    public class InputManager : Singleton<InputManager>
    {

        [Header("Handler References")]
        [SerializeField] private MovementInputHandler movementHandler;
        [SerializeField] private CombatInputHandler combatHandler;
        [SerializeField] private CameraInputHandler cameraHandler;
        [SerializeField] private InventoryInputHandler inventoryHandler;
        [SerializeField] private UIInputHandler uiInputHandler;
        [SerializeField] private InteractionInputHandler interactionHandler;

        private bool isInitialized = false;

        #region Lifecycle

        protected override void OnSingletonAwake()
        {
            InitializeHandlers();
        }

        private void Start()
        {
            // Bắt đầu ở PlayerAlive context khi scene load xong
            InputLayerManager.Instance?.TransitionToState(InputState.PlayerAlive);
        }

#endregion

        #region Initialization

        private void InitializeHandlers()
        {
            if (movementHandler == null)
                movementHandler = gameObject.AddComponent<MovementInputHandler>();

            if (combatHandler == null)
                combatHandler = gameObject.AddComponent<CombatInputHandler>();

            if (cameraHandler == null)
                cameraHandler = gameObject.AddComponent<CameraInputHandler>();

            if (inventoryHandler == null)
                inventoryHandler = gameObject.AddComponent<InventoryInputHandler>();

            if (uiInputHandler == null)
                uiInputHandler = gameObject.AddComponent<UIInputHandler>();

            if (interactionHandler == null)
                interactionHandler = gameObject.AddComponent<InteractionInputHandler>();

            isInitialized = true;
            Debug.Log("[InputManager] Initialized");
        }

        #endregion

        #region Convenience Methods (delegate tới InputLayerManager)

        /// <summary>
        /// Bật toàn bộ gameplay input (PlayerAlive context).
        /// </summary>
        public void EnableAllInput()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[InputManager] Cannot enable: Not initialized!");
                return;
            }
            InputLayerManager.Instance?.TransitionToState(InputState.PlayerAlive);
            Debug.Log("[InputManager] All input enabled (PlayerAlive)");
        }

        /// <summary>
        /// Tắt toàn bộ input (Cinematic context).
        /// </summary>
        public void DisableAllInput()
        {
            InputLayerManager.Instance?.DisableAll();
            Debug.Log("[InputManager] All input disabled");
        }

        #endregion

        #region Public Accessors

        public MovementInputHandler MovementHandler => movementHandler;
        public CombatInputHandler CombatHandler => combatHandler;
        public CameraInputHandler CameraHandler => cameraHandler;
        public InventoryInputHandler InventoryHandler => inventoryHandler;
        public UIInputHandler UIHandler => uiInputHandler;
        public InteractionInputHandler InteractionHandler => interactionHandler;
        public bool IsInitialized => isInitialized;

        #endregion
    }
}