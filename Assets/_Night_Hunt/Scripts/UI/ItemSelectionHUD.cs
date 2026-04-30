using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.UI;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Combat;

namespace NightHunt.UI
{
    // ── Serializable entry pairing an item type to its filter panel ──────────

    /// <summary>Maps one <see cref="ItemType"/> to its <see cref="NightHunt.GameplaySystems.UI.Combat.ItemFilterPanel"/> scene instance.</summary>
    [Serializable]
    public struct ItemFilterEntry
    {
        public ItemType FilterType;
        public GameplaySystems.UI.Combat.ItemFilterPanel Panel;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ItemSelectionHUD — manages consumable/throwable filter panels and item-use progress bar.
    ///
    /// EXTRACTED FROM: <see cref="NightHunt.GameplaySystems.UI.Combat.CombatHUDPanel"/>
    /// (item-selection concerns separated from weapon-slot concerns).
    ///
    /// RESPONSIBILITIES:
    ///   • Initialise each <see cref="GameplaySystems.UI.Combat.ItemFilterPanel"/> with the
    ///     correct systems when <see cref="Bind"/> is called.
    ///   • Show/hide the item-use progress bar as the player holds down a consumable.
    ///   • Show/hide the cancel button while an item use is in progress.
    ///   • Toggle expand/collapse of individual filter panels on keyboard shortcuts.
    ///
    /// FLOW:
    ///   [Player presses C] → TogglePanel(Consumable)
    ///   [Click item]       → bridge.SelectItem() → bridge.OnItemUseStarted → ProgressBar visible
    ///   [Progress 0→1]     → bridge.OnItemUseProgress → fill bar
    ///   [Complete]         → bridge.OnItemUseCompleted → hide bar
    ///   [Cancel]           → CancelButton / bridge.CancelItemUse() → hide bar
    /// </summary>
    public sealed class ItemSelectionHUD : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Filter Panels")]
        [Tooltip("One entry per selectable item type (Consumable, Throwable, etc.).")]
        [SerializeField] private ItemFilterEntry[] _filterPanels;

        // Injected at runtime by GameHUDController.BindProgress.
        private ActionProgressPresenter _progressPresenter;

        [Header("Cancel Button")]
        [Tooltip("Cancel button shown while item use is in progress.")]
        [SerializeField] private Button _cancelButton;

        // ── Runtime ───────────────────────────────────────────────────────────

        private UIPlayerContext _context;
        private CombatInputHandler _combatHandler;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            HideProgress();
            if (_cancelButton != null)
            {
                _cancelButton.onClick.AddListener(OnCancelClicked);
                _cancelButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Called by GameHUDController after the HUD is initialized.
        /// Injects the shared progress bar if it was not assigned in the Inspector.
        /// </summary>
        public void BindProgress(ActionProgressPresenter presenter)
        {
            _progressPresenter ??= presenter;
        }

        private void OnDestroy()
        {
            UnsubscribeContext();
            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Bind this HUD to a new UIPlayerContext.
        /// Reinitialises all filter panels and hooks item-use events.
        /// Safe to call multiple times (re-bind on player/spectate switch).
        /// </summary>
        public void Bind(UIPlayerContext context)
        {
            UnsubscribeContext();

            _context = context;
            if (context == null || !context.IsReady) return;

            InitialisePanels(context);
            SubscribeContext(context);
            HideProgress();
        }

        /// <summary>Toggle the expand/collapse state of the filter panel for a given item type.</summary>
        public void TogglePanel(ItemType type)
        {
            foreach (var entry in _filterPanels)
            {
                if (entry.FilterType == type && entry.Panel != null)
                {
                    if (!entry.Panel.gameObject.activeSelf)
                        entry.Panel.gameObject.SetActive(true);

                    if (entry.Panel.IsExpanded)
                        entry.Panel.CollapseList();
                    else
                        entry.Panel.ExpandList();
                    return;
                }
            }
        }

        /// <summary>
        /// Activate one configured item filter entry by keyboard slot number.
        /// Slot numbering is one-based to match QuickSlot1-4 InputSystem actions.
        /// </summary>
        public bool ActivateQuickSlot(int oneBasedSlot)
        {
            if (_filterPanels == null || oneBasedSlot <= 0 || oneBasedSlot > _filterPanels.Length)
            {
                Debug.LogWarning($"[ITEM_FLOW] [00][QuickSlot.Ignored] slot={oneBasedSlot} reason=no-filter-entry");
                return false;
            }

            var entry = _filterPanels[oneBasedSlot - 1];
            if (entry.Panel == null)
            {
                Debug.LogWarning($"[ITEM_FLOW] [00][QuickSlot.Ignored] slot={oneBasedSlot} type={entry.FilterType} reason=null-panel");
                return false;
            }

            Debug.Log($"[ITEM_FLOW] [00][QuickSlot] slot={oneBasedSlot} type={entry.FilterType} action=ActivateShortcut");
            entry.Panel.ActivateShortcut(entry.FilterType);
            return true;
        }

        /// <summary>Show or hide all filter panels at once (e.g. when player dies).</summary>
        public void SetPanelsVisible(bool visible)
        {
            foreach (var entry in _filterPanels)
                if (entry.Panel != null) entry.Panel.gameObject.SetActive(visible);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void InitialisePanels(UIPlayerContext context)
        {
            if (_filterPanels == null) return;

            var bridge        = context.Bridge;
            var selectionSys  = bridge?.ItemSelection;
            var inventorySys  = bridge?.Inventory;
            var itemUseSys    = bridge?.ItemUse;
            var combatHandler = InputManager.Instance?.CombatHandler;
            combatHandler?.BindItemSelectionSystem(selectionSys);
            BindCombatShortcuts(combatHandler);

            var initializedPanels = new List<GameplaySystems.UI.Combat.ItemFilterPanel>();
            foreach (var entry in _filterPanels)
            {
                if (entry.Panel == null) continue;
                if (initializedPanels.Contains(entry.Panel)) continue;

                var filterTypes = CollectFilterTypesForPanel(entry.Panel);
                entry.Panel.Initialize(filterTypes, selectionSys, inventorySys, itemUseSys, combatHandler);
                initializedPanels.Add(entry.Panel);
            }
        }

        private IReadOnlyList<ItemType> CollectFilterTypesForPanel(GameplaySystems.UI.Combat.ItemFilterPanel panel)
        {
            var types = new List<ItemType>();
            if (_filterPanels == null || panel == null) return types;

            foreach (var entry in _filterPanels)
            {
                if (entry.Panel != panel) continue;
                if (!types.Contains(entry.FilterType))
                    types.Add(entry.FilterType);
            }

            return types;
        }

        private void SubscribeContext(UIPlayerContext context)
        {
            context.OnItemUseStarted   += HandleItemUseStarted;
            context.OnItemUseProgress  += HandleItemUseProgress;
            context.OnItemUseCompleted += HandleItemUseCompleted;
            context.OnItemUseCancelled += HandleItemUseCancelled;
        }

        private void UnsubscribeContext()
        {
            if (_context == null) return;
            _context.OnItemUseStarted   -= HandleItemUseStarted;
            _context.OnItemUseProgress  -= HandleItemUseProgress;
            _context.OnItemUseCompleted -= HandleItemUseCompleted;
            _context.OnItemUseCancelled -= HandleItemUseCancelled;
            BindCombatShortcuts(null);
            _context = null;
        }

        private void BindCombatShortcuts(CombatInputHandler combatHandler)
        {
            if (_combatHandler != null)
            {
                _combatHandler.OnThrowGrenade -= HandleThrowGrenadeShortcut;
                _combatHandler.OnConsumablePanel -= HandleConsumablePanelShortcut;
            }

            _combatHandler = combatHandler;

            if (_combatHandler != null)
            {
                _combatHandler.OnThrowGrenade += HandleThrowGrenadeShortcut;
                _combatHandler.OnConsumablePanel += HandleConsumablePanelShortcut;
            }
        }

        private void HandleThrowGrenadeShortcut()
        {
            var panel = FindPanel(ItemType.Throwable);
            if (panel == null) { TogglePanel(ItemType.Throwable); return; }

            panel.ActivateShortcut();
        }

        private void HandleConsumablePanelShortcut()
        {
            var panel = FindPanel(ItemType.Consumable);
            if (panel == null) { TogglePanel(ItemType.Consumable); return; }

            panel.ActivateShortcut();
        }

        private GameplaySystems.UI.Combat.ItemFilterPanel FindPanel(ItemType type)
        {
            if (_filterPanels == null) return null;
            foreach (var entry in _filterPanels)
                if (entry.FilterType == type && entry.Panel != null)
                    return entry.Panel;
            return null;
        }

        // ── Item Use Event Handlers ───────────────────────────────────────────

        private void HandleItemUseStarted(ItemInstance item)
        {
            var def = item != null ? GameplaySystems.Inventory.ItemDatabase.GetDefinition(item.DefinitionID) : null;
            if (def != null && def.Type == ItemType.Throwable)
            {
                HideProgress();
                return;
            }

            if (def != null && def.Type != ItemType.Consumable)
            {
                HideProgress();
                return;
            }

            ShowProgress(item);
        }

        private void HandleItemUseProgress(ItemInstance item, float progress)
        {
            var def = item != null ? GameplaySystems.Inventory.ItemDatabase.GetDefinition(item.DefinitionID) : null;
            if (def == null || def.Type != ItemType.Consumable) return;

            _progressPresenter?.SetProgress(ActionProgressKind.ItemUse, progress);
        }

        private void HandleItemUseCompleted(ItemInstance item)
        {
            HideProgress();
        }

        private void HandleItemUseCancelled(ItemInstance item)
        {
            HideProgress();
        }

        // ── UI Helpers ────────────────────────────────────────────────────────

        private void ShowProgress(ItemInstance item)
        {
            string label = string.Empty;
            if (item != null)
            {
                var def = GameplaySystems.Inventory.ItemDatabase.GetDefinition(item.DefinitionID);
                label = def != null ? def.DisplayName : string.Empty;
            }

            _progressPresenter?.Show(ActionProgressKind.ItemUse, label, true, OnCancelClicked);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(false);
        }

        private void HideProgress()
        {
            _progressPresenter?.Hide(ActionProgressKind.ItemUse);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(false);
        }

        private void OnCancelClicked()
        {
            _context?.Bridge?.CancelItemUse();
        }
    }
}
