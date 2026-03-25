using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Gameplay.Input.Handlers.Combat;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Shared base for all "permanent" HUD slot buttons: <see cref="WeaponSlotButton"/>
    /// and <see cref="SelectableItemButton"/>.
    ///
    /// Adds on top of <see cref="ActionButton"/>:
    ///   • Double-click detection (configurable threshold).
    ///   • <see cref="CombatInputHandler"/> reference so pressing any slot button
    ///     prevents the concurrent Input System LMB-performed event from firing
    ///     <c>CombatInputHandler.BeginFire</c> for the same press.
    ///
    /// Sub-classes should override <see cref="OnPointerDown"/> and call
    /// <c>base.OnPointerDown(eventData)</c> first, then call
    /// <see cref="ConsumeDoubleClick"/> to detect double-clicks.
    /// </summary>
    public abstract class SlotHUDButton : ActionButton
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Slot Interaction")]
        [Tooltip("Max seconds between two taps that count as a double-click.")]
        [SerializeField] private float _doubleClickThreshold = 0.3f;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────

        protected CombatInputHandler _combatInputHandler;
        private   float              _lastClickTime = -999f;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Inject the CombatInputHandler.
        /// Call once after the local player spawns so pointer-down events block
        /// the concurrent Input System LMB fire event on the same frame.
        /// </summary>
        public void BindCombatHandler(CombatInputHandler handler)
        {
            _combatInputHandler = handler;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  IPointerDownHandler — inform combat handler + sub-class hook
        // ─────────────────────────────────────────────────────────────────────

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);  // DOTween scale press animation
            // Block the simultaneous Input System LMB event from triggering BeginFire.
            _combatInputHandler?.NotifyUIConsumedPress();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Double-click helper
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Records the current unscaled timestamp and returns <c>true</c> if
        /// this click arrived within <see cref="_doubleClickThreshold"/> seconds
        /// of the previous one — i.e. this is the SECOND tap of a double-click.
        ///
        /// Call this ONCE per <see cref="OnPointerDown"/> invocation (after checking
        /// base guards) to advance the internal timer for the next click.
        /// </summary>
        protected bool ConsumeDoubleClick()
        {
            float now    = Time.unscaledTime;
            bool  result = (now - _lastClickTime) <= _doubleClickThreshold;
            _lastClickTime = now;
            return result;
        }
    }
}
