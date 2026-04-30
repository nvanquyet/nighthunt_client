using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// HUD button representing a single weapon slot.
    ///
    /// Single click selects the slot, or reloads when the slot is already active.
    /// Double click holsters the current weapon. Slot presses also cancel any armed
    /// consumable, throwable, or deployable before switching weapons.
    /// </summary>
    public class WeaponSlotButton : SlotHUDButton
    {
        private const string LogPrefix = "[WeaponSlotButton]";

        [Header("Weapon Slot UI")]
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Image _selectedBorder;
        [SerializeField] private TextMeshProUGUI _weaponNameText;
        [SerializeField] private TextMeshProUGUI _magAmmoText;
        [SerializeField] private TextMeshProUGUI _reserveAmmoText;
        [SerializeField] private Slider _ammoSlider;

        [Header("Slot Config")]
        [SerializeField] private WeaponSlotType _slotType;

        private IWeaponSystem _weaponSystem;
        private IItemSelectionSystem _itemSelectionSystem;
        private IItemUseSystem _itemUseSystem;
        private bool _isBound;

        protected override void Awake()
        {
            base.Awake();

            if (_iconImage == null)
            {
                var iconTransform = transform.Find("WeaponIcon") ?? transform.Find("Icon");
                if (iconTransform != null)
                    _iconImage = iconTransform.GetComponent<Image>();

                if (_iconImage == null)
                    Debug.LogWarning($"{LogPrefix} Icon image is not wired on '{name}'. HUD icon cannot update.");
            }
        }

        public void BindItemSystems(IItemSelectionSystem itemSelectionSystem, IItemUseSystem itemUseSystem)
        {
            _itemSelectionSystem = itemSelectionSystem;
            _itemUseSystem = itemUseSystem;
        }

        public void Bind(WeaponSlotType slotType, IWeaponSystem weaponSystem)
        {
            if (_isBound)
                Unbind();

            _slotType = slotType;
            _weaponSystem = weaponSystem;

            if (_weaponSystem == null)
            {
                _isBound = true;
                RefreshEmpty();
                return;
            }

            _weaponSystem.OnWeaponEquipped += HandleWeaponEquipped;
            _weaponSystem.OnWeaponUnequipped += HandleWeaponUnequipped;
            _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
            _weaponSystem.OnAmmoChanged += HandleAmmoChanged;
            _weaponSystem.OnReloadStateChanged += HandleReloadStateChanged;

            _isBound = true;
            RefreshAll();
        }

        public void Unbind()
        {
            if (!_isBound)
                return;

            if (_weaponSystem != null)
            {
                _weaponSystem.OnWeaponEquipped -= HandleWeaponEquipped;
                _weaponSystem.OnWeaponUnequipped -= HandleWeaponUnequipped;
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
                _weaponSystem.OnAmmoChanged -= HandleAmmoChanged;
                _weaponSystem.OnReloadStateChanged -= HandleReloadStateChanged;
            }

            _weaponSystem = null;
            _isBound = false;
        }

        protected override void OnDestroy()
        {
            Unbind();
            base.OnDestroy();
        }

        public override void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!IsInteractable)
                return;

            base.OnPointerDown(eventData);

            if (_weaponSystem == null)
                return;

            if (ConsumeDoubleClick())
            {
                _weaponSystem.HolsterWeapon();
                return;
            }

            if (_itemUseSystem != null && _itemUseSystem.IsUsingItem)
                _itemSelectionSystem?.RequestCancelSelection();

            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            if (activeSlot.HasValue && activeSlot.Value == _slotType)
                _weaponSystem.RequestReload();
            else
                _weaponSystem.SelectWeapon(_slotType);
        }

        private void HandleWeaponEquipped(WeaponSlotType slot, ItemInstance weapon)
        {
            if (slot != _slotType)
                return;

            RefreshAll();
        }

        private void HandleWeaponUnequipped(WeaponSlotType slot, ItemInstance weapon)
        {
            if (slot != _slotType)
                return;

            RefreshEmpty();
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            RefreshAll();
            SetSelectedBorder(newSlot.HasValue && newSlot.Value == _slotType);
        }

        private void HandleAmmoChanged(int currentMag, int totalLeft, int capacity)
        {
            if (_weaponSystem == null)
                return;

            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            if (!activeSlot.HasValue || activeSlot.Value != _slotType)
                return;

            RefreshAmmoDisplay(currentMag, totalLeft, capacity);
        }

        private void HandleReloadStateChanged(bool isReloading)
        {
            // Reload state is reflected through ammo count updates.
        }

        private void RefreshAll()
        {
            if (_weaponSystem == null)
            {
                RefreshEmpty();
                return;
            }

            var weapon = _weaponSystem.GetWeapon(_slotType);
            if (weapon == null)
            {
                RefreshEmpty();
                return;
            }

            SetSlotVisible(true);
            if (_visualRoot != null)
                _visualRoot.SetActive(true);

            RefreshIcon(weapon);

            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            SetSelectedBorder(activeSlot.HasValue && activeSlot.Value == _slotType);

            int mag = _weaponSystem.GetCurrentMagazine(_slotType);
            int total = Mathf.RoundToInt(_weaponSystem.GetTotalAmmo(_slotType));
            int cap = GetMagazineCapacity(weapon);
            RefreshAmmoDisplay(mag, total, cap);
        }

        private void RefreshEmpty()
        {
            bool keepVisible = _slotType != WeaponSlotType.Melee;
            SetSlotVisible(keepVisible);

            if (_visualRoot != null)
                _visualRoot.SetActive(false);

            SetIcon(null);
            SetSelectedBorder(false);

            if (_weaponNameText != null)
                _weaponNameText.text = keepVisible ? $"{_slotType} (Empty)" : string.Empty;

            if (_magAmmoText != null)
                _magAmmoText.text = string.Empty;

            if (_reserveAmmoText != null)
                _reserveAmmoText.text = string.Empty;

            if (_ammoSlider != null)
                _ammoSlider.value = 0f;
        }

        private void RefreshIcon(ItemInstance weapon)
        {
            if (weapon == null)
            {
                RefreshEmpty();
                return;
            }

            var def = ItemDatabase.GetDefinition(weapon.DefinitionID);
            SetIcon(def?.Icon);

            if (_weaponNameText != null)
                _weaponNameText.text = def?.DisplayName ?? string.Empty;
        }

        private void RefreshAmmoDisplay(int currentMag, int reserveAmmo, int magCapacity)
        {
            if (magCapacity <= 0)
            {
                if (_magAmmoText != null)
                    _magAmmoText.text = string.Empty;

                if (_reserveAmmoText != null)
                    _reserveAmmoText.text = string.Empty;

                if (_ammoSlider != null)
                    _ammoSlider.value = 0f;

                return;
            }

            if (_magAmmoText != null)
                _magAmmoText.text = $"<color=#FFD700><size=150%>{currentMag}</size></color> / {magCapacity}";

            if (_reserveAmmoText != null)
                _reserveAmmoText.text = reserveAmmo.ToString();

            if (_ammoSlider != null)
                _ammoSlider.value = magCapacity > 0 ? (float)currentMag / magCapacity : 0f;
        }

        private void SetSelectedBorder(bool selected)
        {
            if (_selectedBorder != null)
                _selectedBorder.enabled = selected;
        }

        private void SetSlotVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        private static int GetMagazineCapacity(ItemInstance inst)
        {
            return inst != null
                ? Mathf.RoundToInt(inst.GetComputedStat(ItemStatType.MagazineSize, 0f))
                : 0;
        }
    }
}
