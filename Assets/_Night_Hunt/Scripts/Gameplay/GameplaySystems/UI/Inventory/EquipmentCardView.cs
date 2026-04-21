using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Displays an EQUIPMENT item (vest, helmet, plate carrier, backpack…) with its
    /// attachment sub-slots spawned GENERICALLY at runtime from the item's
    /// <see cref="ItemDefinition.AttachmentSlots"/> array.
    ///
    /// DESIGN:
    ///   Unlike weapons (which have type-specific prefabs with pre-placed slot positions),
    ///   equipment items use a single generic prefab. Attachment slots are spawned into
    ///   _attachmentContainer at runtime using _attachmentSlotPrefab.
    ///   The number and types of slots come from the equipped item's ItemDefinition.
    ///
    ///   Example — a plate carrier might have:
    ///     AttachmentSlots = [Pouch, Pouch, Plate, Light]
    ///   → 4 generic attachment slots spawned from left to right.
    ///
    /// USAGE:
    ///   InventoryScreen calls Show() when the item is equipped, Hide() when unequipped.
    ///   Registers all slots with DragDropController automatically.
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

        [Header("Config")]
        [SerializeField] private UISlotLayoutConfig _uiConfig;

        // ── Runtime ───────────────────────────────────────────────────────────

        private ItemInstance      _equipInstance;
        private EquipmentSlotType _equipSlot;
        private IAttachmentSystem _attachmentSystem;

        private readonly List<ItemSlotView> _spawnedAttachmentViews = new List<ItemSlotView>();
        private readonly List<ItemSlotView> _registeredSlots        = new List<ItemSlotView>();

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Bind this card to an equipment instance, spawn attachment slots, and populate all.
        /// </summary>
        public void Show(
            EquipmentSlotType  equipSlot,
            ItemInstance       equipment,
            IAttachmentSystem  attachmentSystem,
            UISlotLayoutConfig uiConfig)
        {
            _equipSlot        = equipSlot;
            _equipInstance    = equipment;
            _attachmentSystem = attachmentSystem;
            if (uiConfig != null) _uiConfig = uiConfig;

            UnregisterAllSlots();
            DestroySpawnedSlots();

            // ── Main equipment slot ──────────────────────────────────────────
            if (_mainSlot != null)
            {
                _mainSlot.Initialize(_uiConfig, UISlotId.Equipment(equipSlot));

                if (equipment != null)
                {
                    var def = ItemDatabase.GetDefinition(equipment.DefinitionID);
                    _mainSlot.SetState(new UISlotState
                    {
                        Item            = equipment,
                        Icon            = def?.Icon,
                        BackgroundColor = Color.white,
                    });
                }
                else
                {
                    _mainSlot.SetEmptyState();
                }

                RegisterSlot(_mainSlot);
            }

            // ── Dynamic attachment slots ─────────────────────────────────────
            if (equipment == null)
            {
                gameObject.SetActive(true);
                return;
            }

            var itemDef = ItemDatabase.GetDefinition(equipment.DefinitionID);
            if (itemDef?.AttachmentSlots == null || itemDef.AttachmentSlots.Length == 0)
            {
                // Item has no attachment slots — hide the container.
                if (_attachmentContainer != null)
                    _attachmentContainer.gameObject.SetActive(false);

                gameObject.SetActive(true);
                return;
            }

            if (_attachmentContainer != null)
                _attachmentContainer.gameObject.SetActive(true);

            for (int i = 0; i < itemDef.AttachmentSlots.Length; i++)
            {
                var slotType = itemDef.AttachmentSlots[i];
                var view     = SpawnAttachmentSlot(equipment.InstanceID, i, slotType);
                if (view == null) continue;

                _spawnedAttachmentViews.Add(view);
                RegisterSlot(view);
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide card and destroy spawned attachment slots.
        /// Called when the item is unequipped.
        /// </summary>
        public void Hide()
        {
            UnregisterAllSlots();
            DestroySpawnedSlots();
            gameObject.SetActive(false);
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
                view.SetState(new UISlotState { Item = attached, Icon = def?.Icon, BackgroundColor = Color.white });
            }
            else
            {
                view.SetEmptyState();
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
            view.Initialize(_uiConfig, slotId);

            // Show slot-type icon when empty.
            Sprite typeIcon = _uiConfig?.GetAttachmentSlotIcon(slotType);
            view.SetState(new UISlotState
            {
                Icon            = typeIcon,
                BackgroundColor = new Color(1f, 1f, 1f, 0.25f),
            });

            // Populate if attachment already in slot.
            var attached = _attachmentSystem?.GetAttachment(parentId, index);
            if (attached != null)
            {
                var def = ItemDatabase.GetDefinition(attached.DefinitionID);
                view.SetState(new UISlotState
                {
                    Item            = attached,
                    Icon            = def?.Icon,
                    BackgroundColor = Color.white,
                });
            }

            return view;
        }

        private void DestroySpawnedSlots()
        {
            foreach (var v in _spawnedAttachmentViews)
                if (v != null) Destroy(v.gameObject);
            _spawnedAttachmentViews.Clear();
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
            UnregisterAllSlots();
            DestroySpawnedSlots();
        }

        #endregion
    }
}
