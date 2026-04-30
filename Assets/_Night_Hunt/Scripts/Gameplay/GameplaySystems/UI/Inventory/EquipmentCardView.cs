using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Displays an EQUIPMENT item with its attachment sub-slots spawned generically at runtime.
    ///
    /// FIXES applied:
    ///   - Duplicate registration guard: UnregisterAllSlots() now clears the list before
    ///     re-registering the pre-placed _mainSlot so it is never registered twice.
    ///   - Attachment container visibility is now explicit: hidden when no item OR no
    ///     attachment slots; shown (if expanded) only when item has valid slots.
    ///   - Show(slot, null) hides the entire card when no item is equipped.
    /// </summary>
    public class EquipmentCardView : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Main Equipment Slot")]
        [SerializeField] private ItemSlotView _mainSlot;

        [Header("Attachment Container")]
        [Tooltip("Layout group (Horizontal/Vertical/Grid) into which attachment slots are spawned.")]
        [SerializeField] private RectTransform _attachmentContainer;

        [Tooltip("Generic attachment slot prefab. Must have an ItemSlotView component.")]
        [SerializeField] private GameObject _attachmentSlotPrefab;

        // ── Runtime ───────────────────────────────────────────────────────────

        private ItemInstance      _equipInstance;
        private EquipmentSlotType _equipSlot;
        private IAttachmentSystem _attachmentSystem;

        private readonly List<ItemSlotView>          _spawnedAttachmentViews = new List<ItemSlotView>();
        private readonly List<AttachmentSlotType>    _spawnedAttachmentTypes = new List<AttachmentSlotType>();
        private readonly List<ItemSlotView>          _registeredSlots        = new List<ItemSlotView>();

        private ItemSlotInput _mainSlotInput;
        private bool          _isExpanded = false;

        /// <summary>True when the equipped item has at least one attachment slot defined.</summary>
        private bool _hasAttachmentSlots = false;

        public event System.Action<EquipmentCardView> OnCardClicked;
        public event System.Action<EquipmentSlotType> OnCardDoubleClicked;

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Bind this card to an equipment instance, spawn attachment slots, and populate all.
        /// Pass equipment = null to show the card as an empty placeholder (or hide it entirely).
        /// </summary>
        public void Show(
            EquipmentSlotType  equipSlot,
            ItemInstance       equipment,
            IAttachmentSystem  attachmentSystem)
        {
            _equipSlot        = equipSlot;
            _equipInstance    = equipment;
            _attachmentSystem = attachmentSystem;
            _hasAttachmentSlots = false;

            // FIX: Always unregister + destroy before re-populating to avoid duplicate entries
            // when Show() is called multiple times on the same card instance (e.g. player
            // unequips then re-equips an item into the same slot).
            UnregisterAllSlots();
            DestroySpawnedSlots();

            // ── Main equipment slot ──────────────────────────────────────────
            if (_mainSlot != null)
            {
                _mainSlot.Initialize(UISlotId.Equipment(equipSlot));

                if (equipment != null)
                {
                    var def = ItemDatabase.GetDefinition(equipment.DefinitionID);
                    _mainSlot.SetState(new UISlotState
                    {
                        Item       = equipment,
                        Icon       = def?.Icon,
                        Background = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance
                                         .GetRarityBackground(def?.Rarity ?? ItemRarity.Common),
                    });
                }
                else
                {
                    _mainSlot.SetEmptyState();
                }

                // FIX: Register AFTER UnregisterAllSlots() so there is no window where the
                // same view appears twice in DragDropController._allSlots.
                RegisterSlot(_mainSlot);
                HookMainSlotInput(true);
            }

            // ── No item: hide attachment container, show card as empty placeholder ──
            if (equipment == null)
            {
                if (_attachmentContainer != null)
                    _attachmentContainer.gameObject.SetActive(false);

                // Keep card visible so the slot still occupies space in the layout.
                gameObject.SetActive(true);
                return;
            }

            // ── Dynamic attachment slots ─────────────────────────────────────
            var itemDef = ItemDatabase.GetDefinition(equipment.DefinitionID);
            bool hasSlots = itemDef?.AttachmentSlots != null && itemDef.AttachmentSlots.Length > 0;
            _hasAttachmentSlots = hasSlots;

            if (!hasSlots)
            {
                // Item exists but has no attachment configuration → hide container.
                if (_attachmentContainer != null)
                    _attachmentContainer.gameObject.SetActive(false);

                gameObject.SetActive(true);
                return;
            }

            // Attachment container visibility respects current expand state.
            if (_attachmentContainer != null)
                _attachmentContainer.gameObject.SetActive(_isExpanded);

            for (int i = 0; i < itemDef.AttachmentSlots.Length; i++)
            {
                var slotType = itemDef.AttachmentSlots[i];
                var view     = SpawnAttachmentSlot(equipment.InstanceID, i, slotType);
                if (view == null) continue;

                _spawnedAttachmentViews.Add(view);
                _spawnedAttachmentTypes.Add(slotType);
                RegisterSlot(view);
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide card and destroy spawned attachment slots.
        /// </summary>
        public void Hide()
        {
            HookMainSlotInput(false);
            UnregisterAllSlots();
            DestroySpawnedSlots();
            gameObject.SetActive(false);
        }

        public void SetExpanded(bool expanded)
        {
            _isExpanded = expanded;
            if (_attachmentContainer != null)
            {
                // Only show if we actually have attachment slots to display.
                _attachmentContainer.gameObject.SetActive(expanded && _hasAttachmentSlots);
            }
        }

        public void ToggleExpanded()
        {
            SetExpanded(!_isExpanded);
        }

        private void HookMainSlotInput(bool subscribe)
        {
            if (_mainSlot == null) return;

            if (_mainSlotInput == null)
                _mainSlotInput = _mainSlot.GetComponent<ItemSlotInput>();

            if (_mainSlotInput == null) return;

            if (subscribe)
            {
                _mainSlotInput.OnSlotPressed       -= HandleMainSlotPressed;
                _mainSlotInput.OnSlotDoubleClicked -= HandleMainSlotDoubleClicked;
                _mainSlotInput.OnSlotPressed       += HandleMainSlotPressed;
                _mainSlotInput.OnSlotDoubleClicked += HandleMainSlotDoubleClicked;
            }
            else
            {
                _mainSlotInput.OnSlotPressed       -= HandleMainSlotPressed;
                _mainSlotInput.OnSlotDoubleClicked -= HandleMainSlotDoubleClicked;
            }
        }

        private void HandleMainSlotPressed(ItemSlotView slot)
        {
            OnCardClicked?.Invoke(this);
        }

        private void HandleMainSlotDoubleClicked(ItemSlotView slot)
        {
            OnCardDoubleClicked?.Invoke(_equipSlot);
        }

        /// <summary>
        /// Refresh a single attachment slot (e.g. after attach/detach event).
        /// </summary>
        public void RefreshAttachmentSlot(int slotIndex)
        {
            if (_equipInstance == null) return;
            if (slotIndex >= _spawnedAttachmentViews.Count) return;

            var view = _spawnedAttachmentViews[slotIndex];
            if (view == null) return;

            var attached = _attachmentSystem?.GetAttachment(_equipInstance.InstanceID, slotIndex);
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
                var config   = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance;
                var typeIcon = (slotIndex >= 0 && slotIndex < _spawnedAttachmentTypes.Count)
                    ? config?.GetAttachmentSlotIcon(_spawnedAttachmentTypes[slotIndex])
                    : null;
                view.SetState(new UISlotState
                {
                    Icon       = typeIcon,
                    Background = config != null ? config.DefaultSlotBackground : null,
                });
            }
        }

        /// <summary>Lock all slots for spectator mode.</summary>
        public void SetLockedVisual(bool locked)
        {
            if (_mainSlot != null) _mainSlot.SetLockedVisual(locked);
            foreach (var v in _spawnedAttachmentViews)
                if (v != null) v.SetLockedVisual(locked);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Spawn / Destroy

        private ItemSlotView SpawnAttachmentSlot(string parentId, int index, AttachmentSlotType slotType)
        {
            if (_attachmentSlotPrefab == null || _attachmentContainer == null)
            {
                Debug.LogWarning("[EquipmentCardView] _attachmentSlotPrefab or _attachmentContainer not assigned.");
                return null;
            }

            var go = Instantiate(_attachmentSlotPrefab, _attachmentContainer, false);
            var view = go.GetComponentInChildren<ItemSlotView>(true);
            if (view == null)
            {
                Debug.LogWarning("[EquipmentCardView] Spawned prefab has no ItemSlotView component.");
                Destroy(go);
                return null;
            }

            var slotId = UISlotId.Attachment(parentId, index);
            view.Initialize(slotId);

            var config   = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance;
            Sprite typeIcon = config?.GetAttachmentSlotIcon(slotType);
            view.SetState(new UISlotState
            {
                Icon       = typeIcon,
                Background = config != null ? config.DefaultSlotBackground : null,
            });

            // Populate if attachment already in slot.
            var attached = _attachmentSystem?.GetAttachment(parentId, index);
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

            return view;
        }

        private void DestroySpawnedSlots()
        {
            foreach (var v in _spawnedAttachmentViews)
                if (v != null) Destroy(v.gameObject);
            _spawnedAttachmentViews.Clear();
            _spawnedAttachmentTypes.Clear();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Slot Registration

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

        private void OnDestroy()
        {
            HookMainSlotInput(false);
            UnregisterAllSlots();
            DestroySpawnedSlots();
        }

        #endregion
    }
}