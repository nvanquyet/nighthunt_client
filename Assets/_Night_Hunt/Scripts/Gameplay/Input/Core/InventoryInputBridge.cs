using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Gameplay.Input.Core
{
    /// <summary>
    /// Bridge giữa InputAction (Inventory/OpenInventory) và InputLayerManager.
    /// Đây là cách nhất quán để toggle inventory: InputAction → Bridge → State → UI.
    /// </summary>
    public class InventoryInputBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputLayerManager inputLayerManager;
        [SerializeField] private NightHunt.GameplaySystems.UI.Inventory.UIRootController uiRootController;

        private InputActionMap inventoryActionMap;
        private InputAction openInventoryAction;
        private bool isInventoryOpen = false;

        #region Lifecycle

        private void Awake()
        {
            if (inputLayerManager == null)
                inputLayerManager = GetComponent<InputLayerManager>();

            if (uiRootController == null)
                uiRootController = FindFirstObjectByType<NightHunt.GameplaySystems.UI.Inventory.UIRootController>();
        }

        private void OnEnable()
        {
            InitializeActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        #endregion

        #region Initialization

        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[InventoryInputBridge] InputLayerManager.Instance is null!");
                return;
            }

            // Lấy Inventory action map từ InputLayerManager
            inventoryActionMap = InputLayerManager.Instance.InventoryMap;

            if (inventoryActionMap != null)
            {
                openInventoryAction = inventoryActionMap.FindAction("OpenInventory");

                if (openInventoryAction != null)
                {
                    openInventoryAction.performed += OnOpenInventoryPerformed;
                    Debug.Log("[InventoryInputBridge] Initialized with Inventory/OpenInventory action");
                }
                else
                {
                    Debug.LogWarning("[InventoryInputBridge] 'OpenInventory' action not found in Inventory map!");
                }
            }
            else
            {
                Debug.LogError("[InventoryInputBridge] Inventory action map not found!");
            }
        }

        private void DisableActions()
        {
            if (openInventoryAction != null)
            {
                openInventoryAction.performed -= OnOpenInventoryPerformed;
            }
        }

        #endregion

        #region Input Event Handlers

        private void OnOpenInventoryPerformed(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;

            ToggleInventory();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Toggle inventory UI và input state.
        /// <para>Dùng <see cref="InputLayerManager.PushContext"/> khi mở và
        /// <see cref="InputLayerManager.PopContext"/> khi đóng để hỗ trợ nested context
        /// (VD: mở map trong inventory → PopContext về inventory, PopContext về Gameplay).</para>
        /// </summary>
        public void ToggleInventory()
        {
            isInventoryOpen = !isInventoryOpen;

            var ilm = inputLayerManager ?? InputLayerManager.Instance;

            if (isInventoryOpen)
            {
                // Push InventoryOpen:
                //   ❌ Combat OFF  → click chuột trái / E / F KHÔNG fire
                //   ❌ Player OFF  → không di chuyển / interact
                //   ✅ UI ON, Inventory ON, Camera ON
                ilm?.PushContext(InputState.InventoryOpen);

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
            else
            {
                // Pop về context trước đó (thường là PlayerAlive)
                ilm?.PopContext();

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }

            // Show/Hide UI panel
            if (uiRootController != null)
                uiRootController.ToggleInventory();

            Debug.Log($"[InventoryInputBridge] Inventory {(isInventoryOpen ? "OPENED" : "CLOSED")}");
        }

        /// <summary>
        /// Force mở inventory (không toggle)
        /// </summary>
        public void OpenInventory()
        {
            if (isInventoryOpen) return;
            ToggleInventory();
        }

        /// <summary>
        /// Force đóng inventory (không toggle)
        /// </summary>
        public void CloseInventory()
        {
            if (!isInventoryOpen) return;
            ToggleInventory();
        }

        /// <summary>
        /// Check xem inventory đang mở hay không
        /// </summary>
        public bool IsInventoryOpen => isInventoryOpen;

        #endregion
    }
}
