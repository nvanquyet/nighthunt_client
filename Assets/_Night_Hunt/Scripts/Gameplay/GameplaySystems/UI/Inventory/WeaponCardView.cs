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
    /// DESIGN:
    ///   Each weapon TYPE has its own prefab (AK47Card, PistolCard, SniperCard…).
    ///   The prefab contains pre-placed <see cref="ItemSlotView"/> children for EVERY
    ///   attachment slot that weapon type supports, positioned to match the weapon silhouette.
    ///
    ///   Example — AK47Card prefab layout:
    ///     ├── MainSlot         [ItemSlotView]  ← weapon icon, UISlotType.Weapon
    ///     ├── OpticSlot        [ItemSlotView]  ← above barrel,  SlotType=Optic
    ///     ├── BarrelSlot       [ItemSlotView]  ← front of gun,  SlotType=Barrel
    ///     ├── MagazineSlot     [ItemSlotView]  ← under receiver, SlotType=Magazine
    ///     └── GripSlot         [ItemSlotView]  ← handle,        SlotType=Grip
    ///
    ///   The designer sets _attachmentSlotViews[] and _attachmentSlotTypes[] manually
    ///   in the Inspector once per prefab. No runtime layout algorithm needed.
    ///
    /// USAGE:
    ///   InventoryScreen instantiates the correct WeaponCardView prefab when a weapon
    ///   is equipped, calls Show(), and registers all slots with DragDropController.
    ///   Calls Hide() / gameObject.SetActive(false) when the weapon is unequipped.
    ///
    /// ATTACHMENT REFRESH:
    ///   Subscribe to IGameplayBridge.OnAttachmentAttached / OnAttachmentDetached and
    ///   call RefreshAttachmentSlot(slotIndex) to update individual slots without rebuild.
    /// </summary>
    public class WeaponCardView : MonoBehaviour
    {
        // ── Inspector — set ONCE per prefab in the Editor ─────────────────────

        [Header("Main Weapon Slot")]
        [Tooltip("The primary item slot showing the weapon icon. Must already exist in the prefab.")]
        [SerializeField] private ItemSlotView _mainSlot;

        [Header("Attachment Slots — Pre-placed in prefab")]
        [Tooltip("Attachment slots in slot-index order (index 0, 1, 2…). " +
                 "Must match AttachmentSlots[] length in the equipped ItemDefinition.")]
        [SerializeField] private ItemSlotView[] _attachmentSlotViews;

        [Tooltip("Slot types parallel to _attachmentSlotViews. " +
                 "Used to show the correct type icon when a slot is empty.")]
        [SerializeField] private AttachmentSlotType[] _attachmentSlotTypes;

        [Header("Slot Name (Optional)")]
        [Tooltip("Text element to display the name of the slot (e.g. 'Primary'). " +
                 "Uses the DisplayName from InventoryConfig's WeaponConfig.")]
        [SerializeField] private TMPro.TextMeshProUGUI _slotNameText;

        [Header("Config")]
        [SerializeField] private UISlotLayoutConfig _uiConfig;

        // ── Runtime ───────────────────────────────────────────────────────────

        private ItemInstance      _weaponInstance;
        private WeaponSlotType    _weaponSlot;
        private IAttachmentSystem _attachmentSystem;

        /// <summary>All slots registered with DragDropController (main + attachment sub-slots).</summary>
        private readonly List<ItemSlotView> _registeredSlots = new List<ItemSlotView>();

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Bind this card to a weapon instance and populate all slots.
        /// Registers all slots with <see cref="DragDropController"/>.
        /// </summary>
        public void Show(
            WeaponSlotType     weaponSlot,
            ItemInstance       weapon,
            IAttachmentSystem  attachmentSystem,
            UISlotLayoutConfig uiConfig)
        {
            _weaponSlot       = weaponSlot;
            _weaponInstance   = weapon;
            _attachmentSystem = attachmentSystem;
            if (uiConfig != null) _uiConfig = uiConfig;

            UnregisterAllSlots();

            if (_slotNameText != null)
            {
                var cfg = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance?.GetWeaponSlot(weaponSlot);
                _slotNameText.text = cfg.HasValue ? cfg.Value.ResolvedDisplayName : weaponSlot.ToString();
            }

            // ── Main weapon slot ─────────────────────────────────────────────
            if (_mainSlot != null)
            {
                _mainSlot.Initialize(_uiConfig, UISlotId.Weapon(weaponSlot));

                if (weapon != null)
                {
                    var def = ItemDatabase.GetDefinition(weapon.DefinitionID);
                    _mainSlot.SetState(new UISlotState
                    {
                        Item            = weapon,
                        Icon            = def?.Icon,
                        BackgroundColor = Color.white,
                        StackCount      = 1,
                    });
                }
                else
                {
                    _mainSlot.SetEmptyState();
                }

                RegisterSlot(_mainSlot);
            }

            // ── Attachment sub-slots ─────────────────────────────────────────
            if (_attachmentSlotViews == null || weapon == null) { gameObject.SetActive(true); return; }

            for (int i = 0; i < _attachmentSlotViews.Length; i++)
            {
                var view = _attachmentSlotViews[i];
                if (view == null) continue;

                view.Initialize(_uiConfig, UISlotId.Attachment(weapon.InstanceID, i));

                // Show slot-type icon when empty, or the attached item when filled.
                var attached = attachmentSystem?.GetAttachment(weapon.InstanceID, i);
                if (attached != null)
                {
                    var attDef = ItemDatabase.GetDefinition(attached.DefinitionID);
                    view.SetState(new UISlotState
                    {
                        Item            = attached,
                        Icon            = attDef?.Icon,
                        BackgroundColor = Color.white,
                        StackCount      = 1,
                    });
                }
                else
                {
                    // Show the slot-type placeholder icon.
                    view.SetState(new UISlotState
                    {
                        Icon            = GetSlotTypeIcon(i),
                        BackgroundColor = new Color(1f, 1f, 1f, 0.25f),
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
            UnregisterAllSlots();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Refresh a single attachment slot without rebuilding the whole card.
        /// Subscribe to IGameplayBridge.OnAttachmentAttached / OnAttachmentDetached.
        /// </summary>
        public void RefreshAttachmentSlot(int slotIndex)
        {
            if (_weaponInstance == null) return;
            if (_attachmentSlotViews == null || slotIndex >= _attachmentSlotViews.Length) return;

            var view = _attachmentSlotViews[slotIndex];
            if (view == null) return;

            var attached = _attachmentSystem?.GetAttachment(_weaponInstance.InstanceID, slotIndex);
            if (attached != null)
            {
                var def = ItemDatabase.GetDefinition(attached.DefinitionID);
                view.SetState(new UISlotState { Item = attached, Icon = def?.Icon, BackgroundColor = Color.white });
            }
            else
            {
                view.SetState(new UISlotState { Icon = GetSlotTypeIcon(slotIndex), BackgroundColor = new Color(1f, 1f, 1f, 0.25f) });
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
            if (_uiConfig == null) return null;
            if (_attachmentSlotTypes == null || slotIndex >= _attachmentSlotTypes.Length) return null;
            return _uiConfig.GetAttachmentSlotIcon(_attachmentSlotTypes[slotIndex]);
        }

        private void OnDestroy()
        {
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
