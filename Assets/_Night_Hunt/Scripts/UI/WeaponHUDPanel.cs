using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.UI;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.UI
{
    /// <summary>
    /// WeaponHUDPanel — displays weapon slot buttons in the combat HUD.
    ///
    /// EXTRACTED FROM: <see cref="NightHunt.GameplaySystems.UI.Combat.CombatHUDPanel"/>
    /// (weapon-slot concerns are separated; item-selection concerns moved to <see cref="ItemSelectionHUD"/>).
    ///
    /// RESPONSIBILITIES:
    ///   • Spawn one <see cref="NightHunt.GameplaySystems.UI.Combat.WeaponSlotButton"/> per slot
    ///     defined in <see cref="InventoryConfig.WeaponConfig"/> (Phase 1 — Awake, once).
    ///   • Bind all buttons to the live <see cref="IWeaponSystem"/> from <see cref="UIPlayerContext"/>
    ///     (Phase 2 — when GameHUDController calls <see cref="Bind"/>).
    ///   • Rebind cleanly on player/spectate switch.
    ///
    /// DOES NOT: handle item selection, reload progress animation, or mobile fire button
    ///           (those are in <see cref="ItemSelectionHUD"/> and <see cref="NightHunt.GameplaySystems.UI.Combat.CombatHUDPanel"/>).
    /// </summary>
    public sealed class WeaponHUDPanel : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Slot Config")]
        [Tooltip("Defines which weapon slot types to spawn (Primary, Secondary, Melee).")]
        [SerializeField] private InventoryConfig _inventoryConfig;

        [Tooltip("Prefab with a WeaponSlotButton component. One instance per slot.")]
        [SerializeField] private GameplaySystems.UI.Combat.WeaponSlotButton _slotPrefab;

        [Tooltip("HorizontalLayoutGroup transform that receives spawned slot buttons.")]
        [SerializeField] private Transform _slotsContainer;

        [Header("Optional Fixed Slots")]
        [SerializeField] private GameplaySystems.UI.Combat.WeaponSlotButton _primarySlotButton;
        [SerializeField] private GameplaySystems.UI.Combat.WeaponSlotButton _secondarySlotButton;
        [SerializeField] private GameplaySystems.UI.Combat.WeaponSlotButton _meleeSlotButton;

        // Injected at runtime by GameHUDController.BindProgress.
        private ActionProgressPresenter _progressPresenter;

        // ── Runtime ───────────────────────────────────────────────────────────

        private readonly List<GameplaySystems.UI.Combat.WeaponSlotButton> _spawnedButtons = new();
        private readonly List<WeaponSlotType> _spawnedTypes = new();
        private IWeaponSystem _weaponSystem;
        private UIPlayerContext _playerContext;
        private Coroutine _reloadProgressCoroutine;
        private bool _slotsSpawned;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            // Phase 1: spawn slot GameObjects immediately so layout is stable
            // even before a player is bound. Buttons show as empty placeholders.
            EnsureSlots();
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
            UnbindAllButtons();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Spawn weapon slot buttons from InventoryConfig. Safe to call from inactive GameObjects
        /// or before the player spawns — buttons start as empty placeholders.
        /// Calling more than once is a no-op.
        /// </summary>
        public void EnsureSlots()
        {
            if (_slotsSpawned) return;
            _slotsSpawned = true;
            SpawnSlots();
        }

        /// <summary>
        /// Bind all spawned buttons to the IWeaponSystem provided by the context.
        /// Safe to call multiple times (old bindings are released first).
        /// </summary>
        public void Bind(UIPlayerContext context)
        {
            EnsureSlots();
            _playerContext = context;
            var weaponSys = context?.Bridge?.Weapon;
            UnwireWeaponSystem();
            StopReloadProgress();
            _weaponSystem = weaponSys;
            RebindAllButtons(weaponSys);
            WireWeaponSystem();
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void SpawnSlots()
        {
            if (TryUseFixedSlots())
                return;

            if (TryUseSceneSlots())
                return;

            if (_slotPrefab == null || _slotsContainer == null)
            {
                Debug.LogWarning("[WeaponHUDPanel] _slotPrefab or _slotsContainer is not assigned.");
                return;
            }

            // Weapon layout is fixed: Primary, Secondary, Melee.
            // Using hardcoded types ensures the HUD never silently breaks when
            // the InventoryConfig WeaponConfig array is edited by designers.
            var fixedSlots = new[] { WeaponSlotType.Primary, WeaponSlotType.Secondary, WeaponSlotType.Melee };

            foreach (var slotType in fixedSlots)
            {
                var btn = Instantiate(_slotPrefab, _slotsContainer);
                btn.name = $"WeaponSlot_{slotType}";
                btn.Bind(slotType, null);   // null system → empty placeholder until Bind(context) is called
                btn.gameObject.SetActive(true);
                _spawnedButtons.Add(btn);
                _spawnedTypes.Add(slotType);
            }


        }

        private bool TryUseFixedSlots()
        {
            if (_primarySlotButton == null || _secondarySlotButton == null)
                return false;

            _spawnedButtons.Clear();
            _spawnedTypes.Clear();

            BindFixedSlot(_primarySlotButton, WeaponSlotType.Primary);
            BindFixedSlot(_secondarySlotButton, WeaponSlotType.Secondary);

            if (_meleeSlotButton != null)
                BindFixedSlot(_meleeSlotButton, WeaponSlotType.Melee);


            return true;
        }

        private bool TryUseSceneSlots()
        {
            if (_slotsContainer == null)
                return false;

            var discovered = transform.root.GetComponentsInChildren<GameplaySystems.UI.Combat.WeaponSlotButton>(true);
            GameplaySystems.UI.Combat.WeaponSlotButton primary = null;
            GameplaySystems.UI.Combat.WeaponSlotButton secondary = null;
            GameplaySystems.UI.Combat.WeaponSlotButton melee = null;

            foreach (var button in discovered)
            {
                if (button == null)
                    continue;

                string lowerName = button.name.ToLowerInvariant();
                if (secondary == null && lowerName.Contains("secondary"))
                {
                    secondary = button;
                    continue;
                }

                if (melee == null && lowerName.Contains("melee"))
                {
                    melee = button;
                    continue;
                }

                if (primary == null && (lowerName.Contains("primary") || lowerName == "btn_weapon" || lowerName.Contains("weapon")))
                    primary = button;
            }

            if (primary == null || secondary == null)
                return false;

            _primarySlotButton = primary;
            _secondarySlotButton = secondary;
            _meleeSlotButton = melee;

            ReparentSceneSlot(_primarySlotButton, 0);
            ReparentSceneSlot(_secondarySlotButton, 1);
            ReparentSceneSlot(_meleeSlotButton, 2);

            _spawnedButtons.Clear();
            _spawnedTypes.Clear();
            BindFixedSlot(_primarySlotButton, WeaponSlotType.Primary);
            BindFixedSlot(_secondarySlotButton, WeaponSlotType.Secondary);
            if (_meleeSlotButton != null)
                BindFixedSlot(_meleeSlotButton, WeaponSlotType.Melee);


            return true;
        }

        private void BindFixedSlot(GameplaySystems.UI.Combat.WeaponSlotButton button, WeaponSlotType slotType)
        {
            if (button == null)
                return;

            button.name = $"WeaponSlot_{slotType}";
            button.Bind(slotType, null);
            _spawnedButtons.Add(button);
            _spawnedTypes.Add(slotType);
        }

        private void ReparentSceneSlot(GameplaySystems.UI.Combat.WeaponSlotButton button, int siblingIndex)
        {
            if (button == null || _slotsContainer == null)
                return;

            button.transform.SetParent(_slotsContainer, false);
            button.transform.SetSiblingIndex(siblingIndex);
            button.gameObject.SetActive(true);
        }

        private void RebindAllButtons(IWeaponSystem weaponSys)
        {
            var combatHandler = InputManager.Instance?.CombatHandler;

            // Retrieve item systems from the bound UIPlayerContext so buttons can cancel armed items.
            IItemSelectionSystem itemSelectionSys = null;
            IItemUseSystem       itemUseSys       = null;
            if (_playerContext?.Bridge != null)
            {
                itemSelectionSys = _playerContext.Bridge.ItemSelection;
                itemUseSys       = _playerContext.Bridge.ItemUse;
            }

            for (int i = 0; i < _spawnedButtons.Count; i++)
            {
                if (_spawnedButtons[i] == null) continue;
                _spawnedButtons[i].Bind(_spawnedTypes[i], weaponSys);
                _spawnedButtons[i].BindCombatHandler(combatHandler);
                _spawnedButtons[i].BindItemSystems(itemSelectionSys, itemUseSys);
            }
        }

        private void UnbindAllButtons()
        {
            UnwireWeaponSystem();
            StopReloadProgress();
            foreach (var btn in _spawnedButtons)
                btn?.Unbind();
        }

        private void WireWeaponSystem()
        {
            if (_weaponSystem == null)
                return;

            _weaponSystem.OnReloadStateChanged += HandleReloadStateChanged;
            _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
        }

        private void UnwireWeaponSystem()
        {
            if (_weaponSystem == null)
                return;

            _weaponSystem.OnReloadStateChanged -= HandleReloadStateChanged;
            _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
            _weaponSystem = null;
        }

        private void HandleReloadStateChanged(bool isReloading)
        {
            if (isReloading)
                StartReloadProgress();
            else
                StopReloadProgress();
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            if (!newSlot.HasValue)
                StopReloadProgress();
        }

        private void StartReloadProgress()
        {
            StopReloadProgress();

            if (_progressPresenter == null)
                return;

            float duration = 2.5f;
            var activeWeapon = _weaponSystem?.GetActiveWeapon();
            if (activeWeapon != null)
            {
                float statDuration = activeWeapon.GetComputedStat(ItemStatType.ReloadSpeed);
                if (statDuration > 0f)
                    duration = statDuration;
            }

            _progressPresenter.Show(ActionProgressKind.Reload, "Reloading", false);
            _reloadProgressCoroutine = StartCoroutine(ReloadProgressRoutine(duration));
        }

        private void StopReloadProgress()
        {
            if (_reloadProgressCoroutine != null)
            {
                StopCoroutine(_reloadProgressCoroutine);
                _reloadProgressCoroutine = null;
            }

            _progressPresenter?.Hide(ActionProgressKind.Reload);
        }

        private System.Collections.IEnumerator ReloadProgressRoutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _progressPresenter?.SetProgress(ActionProgressKind.Reload, elapsed / duration);
                yield return null;
            }

            StopReloadProgress();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_slotsContainer == null)
                _slotsContainer = transform;
        }
#endif
    }
}
