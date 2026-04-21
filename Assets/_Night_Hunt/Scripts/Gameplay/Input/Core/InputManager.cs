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
    /// Facade providing quick references to concrete input handlers.
    /// <para><b>State / ActionMap enable-disable is managed by:</b>
    /// <see cref="InputLayerManager"/> — this is the single source of truth.</para>
    /// <para><b>Never</b> call <c>map.Enable()</c> / <c>map.Disable()</c> from here.</para>
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
            // Start in PlayerAlive context when the scene finishes loading.
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

        #region Convenience Methods (delegates to InputLayerManager)

        /// <summary>
        /// Enable all gameplay input (PlayerAlive context).
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
        /// Disable all input (Cinematic context).
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