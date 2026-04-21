using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.UI.Mobile
{
    /// <summary>
    /// On-screen button that toggles the inventory open/closed on mobile.
    ///
    /// Wiring:
    ///   Attach to the inventory button Image/Panel in the HUD canvas.
    ///   No Inspector references required — uses InputManager singleton.
    ///
    /// Design:
    ///   Calls InventoryInputHandler.SimulateToggle() which fires the same
    ///   OpenInventoryPerformed event as pressing Tab/I on keyboard, ensuring
    ///   UIRootController correctly pushes/pops InputState.InventoryOpen.
    ///
    /// Owner semantics:
    ///   Spectators can open the inventory for viewing, matching the existing
    ///   keyboard behaviour. Drag-drop operations are separately owner-gated.
    /// </summary>
    public class MobileInventoryButton : MonoBehaviour, IPointerDownHandler
    {
        [Tooltip("Force this button visible even on desktop (for UI testing in the Editor).")]
        [SerializeField] private bool _forceMobile;

        private void Awake()
        {
            // This button is only needed on mobile; hide it on desktop platforms.
            if (!Application.isMobilePlatform && !PlatformManager.IsMobile && !_forceMobile)
                gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            InputManager.Instance?.InventoryHandler?.SimulateToggle();
        }
    }
}
