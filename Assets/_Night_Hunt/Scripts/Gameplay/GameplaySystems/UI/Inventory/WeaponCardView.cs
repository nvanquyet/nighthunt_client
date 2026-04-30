using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Displays a WEAPON item inside the weapon-panel with its pre-placed attachment sub-slots.
    ///
    /// FIXES applied:
    ///   - Card stays visible as an empty placeholder when weapon == null so it remains a drop target.
    ///   - All attachment slot views are explicitly reset to empty state when weapon is cleared.
    ///   - Dual-same-type slot mapping bug fixed: each logical index may now map to a unique view
    ///     even when the prefab has multiple slots with the same AttachmentSlotType.
    /// </summary>
    public class WeaponCardView : MonoBehaviour
    {
        // ── Inspector — set ONCE per prefab in the Editor ─────────────────────

        [Header("Main Weapon Slot")]
        [Tooltip("The primary item slot showing the weapon icon. Must already exist in the prefab.")]
        [SerializeField] private ItemSlotView _mainSlot;

        [Header("Attachment Slots — Pre-placed in prefab")]
        [Tooltip("Optional: The parent container for all attachment slots. " +
                 "Will be completely hidden if the weapon has 0 attachments (like Melee).")]
        [SerializeField] private GameObject _attachmentsContainer;

        [Tooltip("Attachment slots in slot-index order (index 0, 1, 2…). " +
                 "Must match AttachmentSlots[] length in the equipped ItemDefinition.")]
        [SerializeField] private ItemSlotView[] _attachmentSlotViews;

        [Tooltip("Slot types parallel to _attachmentSlotViews. " +
                 "Used to show the correct type icon when a slot is empty.")]
        [SerializeField] private AttachmentSlotType[] _attachmentSlotTypes;

        [Header("Slot Name (Optional)")]
        [Tooltip("Text element to display the name of the slot (e.g. 'Primary').")]
        [SerializeField] private TMPro.TextMeshProUGUI _slotNameText;

        // ── Runtime ───────────────────────────────────────────────────────────

        private ItemInstance      _weaponInstance;
        private WeaponSlotType    _weaponSlot;
        private IAttachmentSystem _attachmentSystem;

        /// <summary>All slots registered with DragDropController (main + attachment sub-slots).</summary>
        private readonly List<ItemSlotView> _registeredSlots = new List<ItemSlotView>();

        /// <summary>Mapping from logical attachment index -> UI view index. -1 = unmapped.</summary>
        private int[] _logicalToViewIndex;
        private ItemSlotInput _mainSlotInput;

        public event System.Action<WeaponSlotType> OnCardDoubleClicked;

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Bind this card to a weapon instance and populate all slots.
        /// Pass weapon = null to reset the card to an empty/hidden state.
        /// </summary>
        public void Show(
            WeaponSlotType     weaponSlot,
            ItemInstance       weapon,
            IAttachmentSystem  attachmentSystem)
        {
            _weaponSlot       = weaponSlot;
            _weaponInstance   = weapon;
            _attachmentSystem = attachmentSystem;

            UnregisterAllSlots();

            // ── Slot name label ──────────────────────────────────────────────
            if (_slotNameText != null)
                _slotNameText.text = weaponSlot.ToString();

            // ── Empty weapon slot: keep the main slot alive as a drop target ──
            if (weapon == null)
            {
                // Reset main slot so stale data doesn't linger
                if (_mainSlot != null)
                {
                    _mainSlot.Initialize(UISlotId.Weapon(weaponSlot));
                    _mainSlot.SetEmptyState();
                    RegisterSlot(_mainSlot);
                    HookMainSlotInput(true);
                }

                // FIX: Reset ALL attachment views so they don't show data from a previously
                // equipped weapon when the card is reused for a different weapon type.
                ResetAllAttachmentViews();

                if (_attachmentsContainer != null)
                    _attachmentsContainer.SetActive(false);

                gameObject.SetActive(true);
                return;
            }

            // ── Main weapon slot ─────────────────────────────────────────────
            if (_mainSlot != null)
            {
                _mainSlot.Initialize(UISlotId.Weapon(weaponSlot));

                var def = ItemDatabase.GetDefinition(weapon.DefinitionID);
                _mainSlot.SetState(new UISlotState
                {
                    Item       = weapon,
                    Icon       = def?.Icon,
                    Background = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance
                                     .GetRarityBackground(def?.Rarity ?? ItemRarity.Common),
                    StackCount = 1,
                });

                RegisterSlot(_mainSlot);
                HookMainSlotInput(true);
            }

            // ── Attachment sub-slots ─────────────────────────────────────────
            if (_attachmentSlotViews == null || _attachmentSlotViews.Length == 0)
            {
                if (_attachmentsContainer != null)
                    _attachmentsContainer.SetActive(false);
                gameObject.SetActive(true);
                return;
            }

            var itemDef    = ItemDatabase.GetDefinition(weapon.DefinitionID);
            var weaponSlots = itemDef?.AttachmentSlots;
            int validAttachmentCount = weaponSlots != null ? weaponSlots.Length : 0;

            // Hide container entirely for weapons with no attachments (e.g. Melee).
            if (_attachmentsContainer != null)
                _attachmentsContainer.SetActive(validAttachmentCount > 0);

            // FIX: Build logical→view mapping that correctly handles DUPLICATE slot types.
            // Previous code would map both logical indices with the same type to the FIRST
            // matching view index, leaving the second view unpopulated.
            //
            // New approach: track which view indices have already been claimed so each
            // logical index gets a unique view index, even when types repeat.
            _logicalToViewIndex = new int[validAttachmentCount];
            for (int i = 0; i < _logicalToViewIndex.Length; i++) _logicalToViewIndex[i] = -1;

            // viewClaimed[viewIdx] = true once a logical index has claimed that view.
            var viewClaimed = new bool[_attachmentSlotViews.Length];

            if (weaponSlots != null)
            {
                for (int logicalIdx = 0; logicalIdx < weaponSlots.Length; logicalIdx++)
                {
                    var neededType = weaponSlots[logicalIdx];
                    for (int viewIdx = 0; viewIdx < _attachmentSlotViews.Length; viewIdx++)
                    {
                        if (!viewClaimed[viewIdx] &&
                            _attachmentSlotTypes != null &&
                            viewIdx < _attachmentSlotTypes.Length &&
                            _attachmentSlotTypes[viewIdx] == neededType)
                        {
                            viewClaimed[viewIdx]           = true;
                            _logicalToViewIndex[logicalIdx] = viewIdx;
                            break;
                        }
                    }
                }
            }

            // Populate views; hide any that weren't claimed by a logical index.
            for (int viewIdx = 0; viewIdx < _attachmentSlotViews.Length; viewIdx++)
            {
                var view = _attachmentSlotViews[viewIdx];
                if (view == null) continue;

                if (!viewClaimed[viewIdx])
                {
                    // This view slot type is not part of the currently equipped weapon definition.
                    view.gameObject.SetActive(false);
                    continue;
                }

                // Find which logical index owns this view.
                int logicalIdx = -1;
                for (int li = 0; li < _logicalToViewIndex.Length; li++)
                {
                    if (_logicalToViewIndex[li] == viewIdx) { logicalIdx = li; break; }
                }
                if (logicalIdx < 0) { view.gameObject.SetActive(false); continue; }

                view.gameObject.SetActive(true);
                view.Initialize(UISlotId.Attachment(weapon.InstanceID, logicalIdx));

                var attached = attachmentSystem?.GetAttachment(weapon.InstanceID, logicalIdx);
                if (attached != null)
                {
                    var attDef = ItemDatabase.GetDefinition(attached.DefinitionID);
                    view.SetState(new UISlotState
                    {
                        Item       = attached,
                        Icon       = attDef?.Icon,
                        Background = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance
                                         .GetRarityBackground(attDef?.Rarity ?? ItemRarity.Common),
                        StackCount = 1,
                    });
                }
                else
                {
                    view.SetState(new UISlotState
                    {
                        Icon       = GetSlotTypeIcon(viewIdx),
                        Background = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance
                                         .GetRarityBackground(ItemRarity.Common),
                    });
                }

                RegisterSlot(view);
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide this card and unregister all slots.
        /// Called when the weapon is unequipped.
        /// </summary>
        public void Hide()
        {
            HookMainSlotInput(false);
            UnregisterAllSlots();
            ResetAllAttachmentViews();
            gameObject.SetActive(false);
        }

        private void HookMainSlotInput(bool subscribe)
        {
            if (_mainSlot == null) return;

            if (_mainSlotInput == null)
                _mainSlotInput = _mainSlot.GetComponent<ItemSlotInput>();

            if (_mainSlotInput == null) return;

            if (subscribe)
            {
                _mainSlotInput.OnSlotDoubleClicked -= HandleMainSlotDoubleClicked;
                _mainSlotInput.OnSlotDoubleClicked += HandleMainSlotDoubleClicked;
            }
            else
            {
                _mainSlotInput.OnSlotDoubleClicked -= HandleMainSlotDoubleClicked;
            }
        }

        private void HandleMainSlotDoubleClicked(ItemSlotView slot)
        {
            OnCardDoubleClicked?.Invoke(_weaponSlot);
        }

        /// <summary>
        /// Refresh a single attachment slot without rebuilding the whole card.
        /// </summary>
        public void RefreshAttachmentSlot(int logicalIndex)
        {
            if (_weaponInstance == null) return;
            if (_attachmentSlotViews == null || _logicalToViewIndex == null) return;
            if (logicalIndex < 0 || logicalIndex >= _logicalToViewIndex.Length) return;

            int viewIdx = _logicalToViewIndex[logicalIndex];
            if (viewIdx < 0 || viewIdx >= _attachmentSlotViews.Length) return;

            var view = _attachmentSlotViews[viewIdx];
            if (view == null) return;

            var attached = _attachmentSystem?.GetAttachment(_weaponInstance.InstanceID, logicalIndex);
            if (attached != null)
            {
                var def = ItemDatabase.GetDefinition(attached.DefinitionID);
                view.SetState(new UISlotState
                {
                    Item       = attached,
                    Icon       = def?.Icon,
                    Background = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance
                                     .GetRarityBackground(def?.Rarity ?? ItemRarity.Common),
                });
            }
            else
            {
                view.SetState(new UISlotState
                {
                    Icon       = GetSlotTypeIcon(viewIdx),
                    Background = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance
                                     .GetRarityBackground(ItemRarity.Common),
                });
            }
        }

        /// <summary>Lock all slots for spectator mode.</summary>
        public void SetLockedVisual(bool locked)
        {
            if (_mainSlot != null) _mainSlot.SetLockedVisual(locked);
            if (_attachmentSlotViews == null) return;
            foreach (var view in _attachmentSlotViews)
                if (view != null) view.SetLockedVisual(locked);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Helpers

        /// <summary>
        /// Reset all attachment slot views to their empty/type-icon placeholder state.
        /// Called when weapon is null to prevent stale data from previous equip.
        /// </summary>
        private void ResetAllAttachmentViews()
        {
            if (_attachmentSlotViews == null) return;
            for (int i = 0; i < _attachmentSlotViews.Length; i++)
            {
                var view = _attachmentSlotViews[i];
                if (view == null) continue;
                // Deactivate — the slot has no meaningful identity without a weapon.
                view.gameObject.SetActive(false);
                view.SetEmptyState();
            }
        }

        private void RegisterSlot(ItemSlotView view)
        {
            DragDropController.Instance?.RegisterSlotView(view);
            _registeredSlots.Add(view);
        }

        private void UnregisterAllSlots()
        {
            foreach (var v in _registeredSlots)
                if (v != null) DragDropController.Instance?.UnregisterSlotView(v);
            _registeredSlots.Clear();
        }

        private Sprite GetSlotTypeIcon(int slotIndex)
        {
            var config = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance;
            if (config == null) return null;
            if (_attachmentSlotTypes == null || slotIndex >= _attachmentSlotTypes.Length) return null;
            return config.GetAttachmentSlotIcon(_attachmentSlotTypes[slotIndex]);
        }

        private void OnDestroy()
        {
            HookMainSlotInput(false);
            UnregisterAllSlots();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Editor Validator

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_attachmentSlotViews != null && _attachmentSlotTypes != null &&
                _attachmentSlotViews.Length != _attachmentSlotTypes.Length)
            {
                Debug.LogWarning(
                    $"[WeaponCardView] {name}: _attachmentSlotViews ({_attachmentSlotViews.Length}) " +
                    $"and _attachmentSlotTypes ({_attachmentSlotTypes.Length}) must have the same length!",
                    this);
            }
        }
#endif

        #endregion
    }
}
