using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.Audio;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.UI;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Config-driven Combat HUD panel.
    ///
    /// Weapon slot buttons are spawned at runtime from <see cref="InventoryConfig"/>.
    /// Left panel shows consumables, right panel shows throwables.
    ///
    /// Inspector setup:
    ///   • _inventoryConfig      – assign InventoryConfig SO
    ///   • _weaponSlotPrefab     – prefab with WeaponSlotButton component
    ///   • _weaponSlotsContainer – HorizontalLayoutGroup parent for weapon slot buttons
    ///   • _consumablePanel      – left panel, filtered to consumables
    ///   • _throwablePanel       – right panel, filtered to throwables
    ///   Audio: depleted-weapon sound routes through AudioManager (no AudioSource needed on this GO)
    ///
    /// Call Initialize(IWeaponSystem, IItemSelectionSystem) when the local player spawns.
    /// </summary>
    public class CombatHUDPanel : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector — Config + Prefabs
        // ─────────────────────────────────────────────────────────────────────

        [Header("Weapon Slots")]
        [Tooltip("Prefab with WeaponSlotButton component.")]
        [SerializeField] private WeaponSlotButton _weaponSlotPrefab;

        [Tooltip("Parent with HorizontalLayoutGroup — weapon slot buttons land here.")]
        [SerializeField] private Transform _weaponSlotsContainer;

        [Header("Item Selection Panels")]
        [Tooltip("One entry per item type panel (e.g. Consumable, Throwable). Assign ItemFilterPanel prefab instances in scene.")]
        [SerializeField] private bool _showItemPanelsUI = true;
        [SerializeField] private FilterPanelEntry[] _filterPanels;

        /// <summary>Each entry pairs an ItemType with its ItemFilterPanel scene instance.</summary>
        [System.Serializable]
        public struct FilterPanelEntry
        {
            public ItemType      FilterType;
            public ItemFilterPanel Panel;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Inspector — Shared HUD Labels (fixed — not config-driven)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Reload Progress")]
        [SerializeField] private ActionProgressPresenter _actionProgressPresenter;



        [Header("Throwable Aim Controller (optional)")]
        [Tooltip("Assign the scene ThrowableAimController so throwable slots show range/aim UI.")]
        [SerializeField] private ThrowableAimController _aimController;

        [Header("Cancel Item Use Button (optional)")]
        [Tooltip("Single button shared by all 4 quick slots. " +
                 "Shown during any active item-use to abort it (throwable hold or consumable channel)." +
                 "Calls CancelAim() on the AimController and RequestCancelUse() on the server.")]
        [SerializeField] private UnityEngine.UI.Button _cancelItemUseButton;


        // ─────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────

        private IWeaponSystem        _weaponSystem;
        private IItemSelectionSystem _itemSelectionSystem;
        private IItemUseSystem       _itemUseSystem;
        private IInventorySystem     _inventorySystem;
        private CombatInputHandler   _combatInputHandler;
        private bool             _slotsSpawned;
        private Coroutine        _reloadProgressCoroutine;

        private readonly List<WeaponSlotButton> _spawnedWeaponButtons = new List<WeaponSlotButton>();
        private readonly List<WeaponSlotType>   _spawnedWeaponTypes = new List<WeaponSlotType>();

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureSlots();
        }

        /// <summary>
        /// Spawn all slot GOs from config immediately — safe to call even when this
        /// GameObject is inactive (called explicitly from UIRootController.Awake).
        /// Idempotent: does nothing if slots were already spawned.
        /// </summary>
        public void EnsureSlots()
        {
            if (_slotsSpawned) return;
            SpawnWeaponSlots();
            SetItemPanelsVisible(_showItemPanelsUI);
            _slotsSpawned = true;
        }

        private void OnDestroy()
        {
            UnwireSystemEvents();
            UnwireCancelButton();
            UnwireShortcutKeys();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// ── PHASE 2: Bind ─────────────────────────────────────────────────
        /// Called when the local player spawns (or switches).
        /// Slots must already exist (Awake spawned them); this method only
        /// assigns live system references — it never destroys / recreates GOs.
        ///
        /// Delegates to <see cref="BindWeapon"/>, <see cref="BindItemSelection"/>,
        /// and <see cref="BindPlayerContext"/>.
        /// </summary>
        public void Initialize(
            IWeaponSystem        weaponSystem,
            IItemSelectionSystem itemSelectionSystem,
            IPlayerStatSystem    statSystem           = null,
            Transform            playerTransform      = null,
            IAimSystem           aimSystem            = null,
            IItemUseSystem       itemUseSystem        = null,
            CombatInputHandler   combatInputHandler   = null,
            IInventorySystem     inventorySystem      = null)
        {
            UnwireSystemEvents();
            UnwireShortcutKeys();

            BindWeapon(weaponSystem, inventorySystem);
            BindItemSelection(itemSelectionSystem, itemUseSystem, combatInputHandler);
            BindPlayerContext(statSystem, playerTransform, aimSystem);

            WireSystemEvents();
            WireShortcutKeys();
            HideStatusMessages();
        }

        /// <summary>
        /// Bind weapon and inventory systems, then rebind weapon slot buttons.
        /// Safe to call independently when only the weapon or inventory system changes.
        /// Call <see cref="WireSystemEvents"/> afterwards when used outside <see cref="Initialize"/>.
        /// </summary>
        public void BindWeapon(IWeaponSystem weaponSystem, IInventorySystem inventorySystem = null)
        {
            // Safety: ensure slots exist (edge-case when called before Awake).
            if (!_slotsSpawned)
            {
                SpawnWeaponSlots();
                SetItemPanelsVisible(_showItemPanelsUI);
                _slotsSpawned = true;
            }

            _weaponSystem    = weaponSystem;
            _inventorySystem = inventorySystem;
            RebindWeaponSlots();
        }

        /// <summary>
        /// Bind item-selection, item-use, and the combat input handler.
        /// Also re-initialises all item filter panels and rewires the cancel button.
        /// </summary>
        public void BindItemSelection(
            IItemSelectionSystem itemSelectionSystem,
            IItemUseSystem       itemUseSystem      = null,
            CombatInputHandler   combatInputHandler = null)
        {
            _itemSelectionSystem = itemSelectionSystem;
            _combatInputHandler  = combatInputHandler;

            // Let CombatInputHandler call UseSelectedItem on fire when a throwable is armed.
            combatInputHandler?.BindItemSelectionSystem(itemSelectionSystem);

            RefreshItemPanels();

            UnwireCancelButton();
            _itemUseSystem = itemUseSystem;
            WireCancelButton();
        }

        /// <summary>
        /// Bind player-context references (stat system, transform, aim system) to the
        /// aim controller and fire button.
        /// </summary>
        public void BindPlayerContext(
            IPlayerStatSystem statSystem      = null,
            Transform         playerTransform = null,
            IAimSystem        aimSystem       = null)
        {
            if (_aimController != null)
                _aimController.Initialize(statSystem, _itemSelectionSystem, playerTransform, aimSystem, _itemUseSystem, _combatInputHandler, _inventorySystem);
        }

        /// <summary>
        /// Toggle (expand/collapse) the item-selection panel for a given item type.
        /// Used by keyboard shortcuts (e.g. key 3 = Consumable panel, key 4 = Throwable panel).
        /// </summary>
        public void TogglePanel(ItemType filterType)
        {
            if (_filterPanels == null) return;
            foreach (var entry in _filterPanels)
            {
                if (entry.Panel == null || entry.FilterType != filterType) continue;
                if (entry.Panel.IsExpanded)
                    entry.Panel.CollapseList();
                else
                    entry.Panel.ExpandList();
                return;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 1 — Spawn (Awake, once)
        // ─────────────────────────────────────────────────────────────────────

        private void SpawnWeaponSlots()
        {
            // Safety: clear any pre-existing buttons (shouldn't be called twice).
            foreach (var btn in _spawnedWeaponButtons)
                if (btn != null) Destroy(btn.gameObject);
            _spawnedWeaponButtons.Clear();
            _spawnedWeaponTypes.Clear();

            if (_weaponSlotPrefab == null || _weaponSlotsContainer == null)
            {
                Debug.LogWarning("[CombatHUDPanel] _weaponSlotPrefab or _weaponSlotsContainer is null — weapon slots not spawned.");
                return;
            }

            var fixedSlots = new[] { WeaponSlotType.Primary, WeaponSlotType.Secondary, WeaponSlotType.Melee };

            foreach (var type in fixedSlots)
            {
                var btn = Instantiate(_weaponSlotPrefab, _weaponSlotsContainer);
                btn.name = $"WeaponSlot_{type}";
                btn.Bind(type, null);   // null system → renders as placeholder
                _spawnedWeaponButtons.Add(btn);
                _spawnedWeaponTypes.Add(type);  // keep type so RebindWeaponSlots doesn't need config
                btn.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Show or hide the item-selection panels at runtime (e.g. game-mode specific HUD).
        /// Reflects the same logic as the <see cref="_showItemPanelsUI"/> inspector field.
        /// </summary>
        public void SetItemPanelsVisible(bool visible)
        {
            _showItemPanelsUI = visible;
            if (_filterPanels == null) return;
            foreach (var entry in _filterPanels)
                if (entry.Panel != null) entry.Panel.gameObject.SetActive(visible);
        }



        // ─────────────────────────────────────────────────────────────────────
        //  Phase 2 — Rebind (per player, no GO creation)
        // ─────────────────────────────────────────────────────────────────────

        private void RebindWeaponSlots()
        {
            for (int i = 0; i < _spawnedWeaponButtons.Count; i++)
            {
                var btn = _spawnedWeaponButtons[i];
                if (btn == null) continue;
                var type = i < _spawnedWeaponTypes.Count ? _spawnedWeaponTypes[i] : default;
                btn.Bind(type, _weaponSystem);
                btn.BindCombatHandler(_combatInputHandler);
            }
        }

        private void RefreshItemPanels()
        {
            if (_filterPanels == null) return;
            var initializedPanels = new List<ItemFilterPanel>();
            foreach (var entry in _filterPanels)
            {
                if (entry.Panel == null) continue;
                if (initializedPanels.Contains(entry.Panel)) continue;

                var filterTypes = CollectFilterTypesForPanel(entry.Panel);
                entry.Panel.Initialize(filterTypes, _itemSelectionSystem, _inventorySystem, _itemUseSystem, _combatInputHandler, _aimController);
                initializedPanels.Add(entry.Panel);
            }
        }

        private IReadOnlyList<ItemType> CollectFilterTypesForPanel(ItemFilterPanel panel)
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

        // ─────────────────────────────────────────────────────────────────────
        //  Shortcut key wiring for item panel activation.
        // ─────────────────────────────────────────────────────────────────────

        private void WireShortcutKeys()
        {
            if (_combatInputHandler == null) return;
            _combatInputHandler.OnThrowGrenade    += HandleThrowGrenadeShortcut;
            _combatInputHandler.OnConsumablePanel += HandleConsumablePanelShortcut;
        }

        private void UnwireShortcutKeys()
        {
            if (_combatInputHandler == null) return;
            _combatInputHandler.OnThrowGrenade    -= HandleThrowGrenadeShortcut;
            _combatInputHandler.OnConsumablePanel -= HandleConsumablePanelShortcut;
        }

        private void HandleThrowGrenadeShortcut()    => ActivatePanelShortcut(ItemType.Throwable);
        private void HandleConsumablePanelShortcut() => ActivatePanelShortcut(ItemType.Consumable);

        private void ActivatePanelShortcut(ItemType filterType)
        {
            if (_filterPanels == null) return;
            foreach (var entry in _filterPanels)
            {
                if (entry.Panel == null || entry.FilterType != filterType) continue;
                Debug.Log($"[CombatHUDPanel] Shortcut {filterType} -> ItemFilterPanel.ActivateShortcut");
                entry.Panel.ActivateShortcut();
                return;
            }

            Debug.LogWarning($"[CombatHUDPanel] Shortcut {filterType} ignored: panel not found.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  System Event Wiring
        // ─────────────────────────────────────────────────────────────────────

        private void WireSystemEvents()
        {
            if (_weaponSystem == null) return;
            _weaponSystem.OnReloadStateChanged  += HandleReloadStateChanged;
            _weaponSystem.OnWeaponDepleted      += HandleWeaponDepleted;
            _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
        }

        private void UnwireSystemEvents()
        {
            if (_weaponSystem == null) return;
            _weaponSystem.OnReloadStateChanged  -= HandleReloadStateChanged;
            _weaponSystem.OnWeaponDepleted      -= HandleWeaponDepleted;
            _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event Handlers — Weapon System
        // ─────────────────────────────────────────────────────────────────────

        private void HandleReloadStateChanged(bool isReloading)
        {
            if (isReloading)
                StartReloadProgress();
            else
                StopReloadProgress();
        }

        private void StartReloadProgress()
        {
            StopReloadProgress();
            if (_actionProgressPresenter == null) return;

            float duration = 2.5f;
            var activeWeapon = _weaponSystem?.GetActiveWeapon();
            if (activeWeapon != null)
            {
                float d = activeWeapon.GetComputedStat(ItemStatType.ReloadSpeed);
                if (d > 0f) duration = d;
            }

            _actionProgressPresenter.Show(ActionProgressKind.Reload, "Reloading", false);
            _reloadProgressCoroutine = StartCoroutine(ReloadProgressRoutine(duration));
        }

        private void StopReloadProgress()
        {
            if (_reloadProgressCoroutine != null)
            {
                StopCoroutine(_reloadProgressCoroutine);
                _reloadProgressCoroutine = null;
            }
            _actionProgressPresenter?.Hide(ActionProgressKind.Reload);
        }

        private IEnumerator ReloadProgressRoutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _actionProgressPresenter?.SetProgress(ActionProgressKind.Reload, elapsed / duration);
                yield return null;
            }

            StopReloadProgress();
        }

        private void HandleWeaponDepleted(WeaponSlotType slot)
        {
            if (_weaponSystem == null) return;
            var active = _weaponSystem.GetActiveWeaponSlot();
            if (!active.HasValue || active.Value != slot) return;

            // Play "weapon fully depleted" audio cue via centralized AudioManager.
            if (AudioManager.HasInstance)
                AudioManager.Instance.PlayUI(AudioManager.Instance.Library?.weaponDepleted);
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            HideStatusMessages();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void HideStatusMessages()
        {
            StopReloadProgress();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Cancel Item Use Button
        // ─────────────────────────────────────────────────────────────────

        private void WireCancelButton()
        {
            if (_itemUseSystem != null)
            {
                _itemUseSystem.OnItemUseStarted   += HandleItemUseStartedForCancel;
                _itemUseSystem.OnItemUseCompleted += HandleItemUseCompletedForCancel;
                _itemUseSystem.OnItemUseCancelled += HandleItemUseCancelledForCancel;
            }

            if (_cancelItemUseButton == null) return;
            // Remove before re-adding to prevent double-subscribe on re-init.
            _cancelItemUseButton.onClick.RemoveListener(OnCancelItemUsePressed);
            _cancelItemUseButton.onClick.AddListener(OnCancelItemUsePressed);
            // Only show the button while an item use is in progress.
            UpdateCancelButtonVisibility();
        }

        private void UnwireCancelButton()
        {
            if (_itemUseSystem != null)
            {
                _itemUseSystem.OnItemUseStarted   -= HandleItemUseStartedForCancel;
                _itemUseSystem.OnItemUseCompleted -= HandleItemUseCompletedForCancel;
                _itemUseSystem.OnItemUseCancelled -= HandleItemUseCancelledForCancel;
            }

            if (_cancelItemUseButton != null)
                _cancelItemUseButton.onClick.RemoveListener(OnCancelItemUsePressed);
        }

        private void OnCancelItemUsePressed()
        {
            Debug.Log("[CombatHUDPanel] Cancel item-use button pressed.");
            // Cancel the aim-mode visuals (hides ring, cursor, resets mobile joystick).
            if (_aimController != null)
            {
                if (_aimController.IsInDeployMode)
                    _aimController.CancelDeploy();
                else
                    _aimController.CancelAim();
            }
            else
                // No aim controller (e.g. consumable with no throwable aim mode)
                // — still send the server cancel.
                _itemUseSystem?.RequestCancelUse();
        }

        private void UpdateCancelButtonVisibility()
        {
            if (_cancelItemUseButton == null) return;
            bool active = _itemUseSystem != null && _itemUseSystem.IsUsingItem;
            _cancelItemUseButton.gameObject.SetActive(active);
        }

        private void HandleItemUseStartedForCancel(ItemInstance _)
        {
            UpdateCancelButtonVisibility();
        }

        private void HandleItemUseCompletedForCancel(ItemInstance _)
        {
            UpdateCancelButtonVisibility();
        }

        private void HandleItemUseCancelledForCancel(ItemInstance _)
        {
            UpdateCancelButtonVisibility();
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────────
        //  Editor — Context Menu: Create WeaponSlotButton Template Prefab
        // ─────────────────────────────────────────────────────────────────────

        [ContextMenu("NightHunt/Create WeaponSlotButton Template Prefab")]
        private void Editor_CreateWeaponSlotButtonPrefab()
        {
            const string parent = "Assets/_Night_Hunt/Prefabs";
            const string dir    = parent + "/UI";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder(parent, "UI");

            const string path = dir + "/WeaponSlotButton_Template.prefab";

            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[CombatHUDPanel] WeaponSlotButton_Template already exists at {path}");
                return;
            }

            var go = new GameObject("WeaponSlotButton_Template");
            var rt = go.AddComponent<UnityEngine.RectTransform>();
            rt.sizeDelta = new UnityEngine.Vector2(80f, 80f);
            go.AddComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0.1f, 0.1f, 0.1f, 0.85f);
            go.AddComponent<UnityEngine.UI.Button>();

            // Icon
            var iconGo = new GameObject("WeaponIcon", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<UnityEngine.RectTransform>();
            iconRt.anchorMin = new UnityEngine.Vector2(0.1f, 0.25f);
            iconRt.anchorMax = new UnityEngine.Vector2(0.9f, 0.9f);
            iconRt.offsetMin = iconRt.offsetMax = UnityEngine.Vector2.zero;

            // Ammo text
            var ammoGo  = new GameObject("AmmoText", typeof(UnityEngine.RectTransform), typeof(TMPro.TextMeshProUGUI));
            ammoGo.transform.SetParent(go.transform, false);
            var ammoRt  = ammoGo.GetComponent<UnityEngine.RectTransform>();
            ammoRt.anchorMin = new UnityEngine.Vector2(0f, 0f);
            ammoRt.anchorMax = new UnityEngine.Vector2(1f, 0.28f);
            ammoRt.offsetMin = ammoRt.offsetMax = UnityEngine.Vector2.zero;
            var ammoTmp = ammoGo.GetComponent<TMPro.TextMeshProUGUI>();
            ammoTmp.text = "30/300"; ammoTmp.fontSize = 10f; ammoTmp.alignment = TMPro.TextAlignmentOptions.Center;

            // Slot index badge
            var badgeGo  = new GameObject("SlotBadge", typeof(UnityEngine.RectTransform), typeof(TMPro.TextMeshProUGUI));
            badgeGo.transform.SetParent(go.transform, false);
            var badgeRt  = badgeGo.GetComponent<UnityEngine.RectTransform>();
            badgeRt.anchorMin = new UnityEngine.Vector2(0f, 0.8f);
            badgeRt.anchorMax = new UnityEngine.Vector2(0.35f, 1f);
            badgeRt.offsetMin = badgeRt.offsetMax = UnityEngine.Vector2.zero;
            var badgeTmp = badgeGo.GetComponent<TMPro.TextMeshProUGUI>();
            badgeTmp.text = "1"; badgeTmp.fontSize = 10f;

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);

            if (_weaponSlotPrefab == null)
            {
                _weaponSlotPrefab = saved.GetComponent<WeaponSlotButton>();
                UnityEditor.EditorUtility.SetDirty(this);
            }
            Debug.Log($"[CombatHUDPanel] Created WeaponSlotButton_Template at {path}. " +
                      "Add WeaponSlotButton component and wire icon/ammo/badge fields.");
        }
#endif
    }
}


