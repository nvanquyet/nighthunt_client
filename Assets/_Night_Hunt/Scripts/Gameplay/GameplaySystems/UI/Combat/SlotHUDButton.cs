using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Gameplay.Input.Handlers.Combat;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Shared base for permanent HUD slot buttons such as weapon slots and
    /// selectable item slots.
    ///
    /// Adds double-click detection and notifies CombatInputHandler so a UI press
    /// does not also trigger the gameplay fire input on the same frame.
    /// </summary>
    public abstract class SlotHUDButton : ActionButton
    {
        [Header("Slot Interaction")]
        [Tooltip("Max seconds between two taps that count as a double-click.")]
        [SerializeField] private float _doubleClickThreshold = 0.45f;

        protected CombatInputHandler _combatInputHandler;
        private float _lastClickTime = -999f;

        public void BindCombatHandler(CombatInputHandler handler)
        {
            _combatInputHandler = handler;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable)
                return;

            base.OnPointerDown(eventData);
            _combatInputHandler?.NotifyUIConsumedPress();
        }

        protected bool ConsumeDoubleClick()
        {
            float now = Time.unscaledTime;
            bool result = now - _lastClickTime <= _doubleClickThreshold;
            _lastClickTime = now;
            return result;
        }
    }
}
