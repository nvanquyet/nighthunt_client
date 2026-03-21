using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Michsky.MUIP;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Determines how quick-slot buttons are arranged in the HUD.
    /// </summary>
    public enum QuickSlotLayoutMode
    {
        /// <summary>Radial arc around the fire button (default MOBA style).</summary>
        Circle,
        /// <summary>Horizontal list — parent root should have a HorizontalLayoutGroup.</summary>
        HorizontalList,
    }

    /// <summary>
    /// Config-driven Combat HUD panel.
    ///
    /// Weapon slot buttons are spawned at runtime from <see cref="InventoryConfig"/>.
    /// Quick-slot buttons are spawned in a radial arc around the fire button (quadrant II).
    ///
    /// Inspector setup:
    ///   • _inventoryConfig      – assign InventoryConfig SO
    ///   • _weaponSlotPrefab     – prefab with WeaponSlotButton component
    ///   • _weaponSlotsContainer – HorizontalLayoutGroup parent for weapon slot buttons
    ///   • _quickSlotPrefab      – prefab with QuickSlotHUDButton component
    ///   • _quickSlotsRoot       – empty RectTransform placed at fire button center (origin of radial layout)
    ///   • _radialRadius         – distance from fire button to each quick slot button
    ///   • _arcCenterAngle       – center of the arc in degrees (135 = upper-left)
    ///   • _arcSpread            – total degrees spread across all buttons
    ///   • _audioSource / _depletedClip – plays when active weapon goes fully empty
    ///
    /// Call Initialize(IWeaponSystem, IQuickSlotSystem) when the local player spawns.
    /// </summary>
    public class CombatHUDPanel : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector — Config + Prefabs
        // ─────────────────────────────────────────────────────────────────────

        [Header("Config (drives slot count)")]
        [Tooltip("Same InventoryConfig SO used by gameplay systems.")]
        [SerializeField] private InventoryConfig _inventoryConfig;

        [Header("Weapon Slots (spawned from InventoryConfig.WeaponConfig)")]
        [Tooltip("Prefab with WeaponSlotButton component.")]
        [SerializeField] private WeaponSlotButton _weaponSlotPrefab;

        [Tooltip("Parent with HorizontalLayoutGroup — weapon slot buttons land here.")]
        [SerializeField] private Transform _weaponSlotsContainer;

        [Header("Quick Slots — Layout")]
        [Tooltip("Show or hide the entire quick-slot UI. Set false for game modes that don't need quick items.")]
        [SerializeField] private bool _showQuickSlotsUI = true;

        [Tooltip("Circle = radial arc around the fire button.  HorizontalList = plain left-to-right row.")]
        [SerializeField] private QuickSlotLayoutMode _quickSlotLayout = QuickSlotLayoutMode.Circle;

        [Header("Quick Slots — Radial Arc around Fire Button")]
        [Tooltip("Prefab with QuickSlotHUDButton component.")]
        [SerializeField] private QuickSlotHUDButton _quickSlotPrefab;

        [Tooltip("Empty RectTransform placed at the fire button center — origin of the radial layout. " +
                 "Used when layout = Circle.")]
        [SerializeField] private RectTransform _quickSlotsRoot;

        [Tooltip("Radius in pixels from fire button center to each quick-slot button.")]
        [SerializeField] private float _radialRadius = 130f;

        [Tooltip("Center angle of the arc in degrees (0=right, 90=up, 135=upper-left, 180=left).")]
        [SerializeField] private float _arcCenterAngle = 135f;

        [Tooltip("Total spread of the arc in degrees. E.g. 80 = buttons span 40° each side of center.")]
        [SerializeField] private float _arcSpread = 80f;

        [Header("Quick Slots — Horizontal List")]
        [Tooltip("Parent RectTransform with a HorizontalLayoutGroup. " +
                 "Used when layout = HorizontalList. Buttons are simply instantiated here; " +
                 "no manual anchored-position is applied — the layout group handles spacing.")]
        [SerializeField] private RectTransform _quickSlotsRootList;

        // ─────────────────────────────────────────────────────────────────────
        //  Inspector — Shared HUD Labels (fixed — not config-driven)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Reload Progress (center-screen)")]
        [Tooltip("MUIP ProgressBar shown while reloading. Assign the ProgressBar component placed at screen centre.")]
        [SerializeField] private ProgressBar _reloadProgressBar;

        [Header("Mobile Fire Button")]
        [Tooltip("Show or hide the on-screen fire button. Set false for PC-only builds or game modes that hide the button.")]
        [SerializeField] private bool _showFireButton = true;

        [Tooltip("On-screen fire button for mobile / controller. Auto-finds CombatInputHandler if not pre-assigned.")]
        [SerializeField] private FireButton _fireButton;

        [Header("Quickslot Aim Controller (optional)")]
        [Tooltip("Assign the scene QuickSlotAimController so throwable slots show range/aim UI.")]
        [SerializeField] private QuickSlotAimController _aimController;

        [Header("Cancel Item Use Button (optional)")]
        [Tooltip("Single button shared by all 4 quick slots. " +
                 "Shown during any active item-use to abort it (throwable hold or consumable channel)." +
                 "Calls CancelAim() on the AimController and RequestCancelUse() on the server.")]
        [SerializeField] private UnityEngine.UI.Button _cancelItemUseButton;

        [Header("Audio")]
        [Tooltip("AudioSource to play sound effects. Add AudioSource component on this GameObject.")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Clip played when the active weapon's magazine AND reserve ammo both reach zero.")]
        [SerializeField] private AudioClip _depletedClip;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────

        private IWeaponSystem    _weaponSystem;
        private IQuickSlotSystem _quickSlotSystem;
        private IItemUseSystem   _itemUseSystem;
        private bool             _slotsSpawned;     // true after Awake spawn pass
        private bool             _isInitialized;    // true after first data bind
        private Coroutine        _reloadProgressCoroutine;

        private readonly List<WeaponSlotButton>    _spawnedWeaponButtons    = new List<WeaponSlotButton>();
        private readonly List<WeaponSlotType>      _spawnedWeaponTypes      = new List<WeaponSlotType>();
        private readonly List<QuickSlotHUDButton>  _spawnedQuickSlotButtons = new List<QuickSlotHUDButton>();

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
            SpawnQuickSlots();
            ApplyFireButtonVisibility();
            _slotsSpawned = true;
        }

        private void OnDestroy()
        {
            UnwireSystemEvents();
            UnwireCancelButton();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// ── PHASE 2: Bind ─────────────────────────────────────────────────
        /// Called when the local player spawns (or switches).
        /// Slots must already exist (Awake spawned them); this method only
        /// assigns live system references — it never destroys / recreates GOs.
        /// </summary>
        public void Initialize(
            IWeaponSystem      weaponSystem,
            IQuickSlotSystem   quickSlotSystem,
            IPlayerStatSystem  statSystem           = null,
            Transform          playerTransform      = null,
            IAimSystem         aimSystem            = null,
            IItemUseSystem     itemUseSystem        = null,
            CombatInputHandler combatInputHandler   = null)
        {
            Debug.Log($"[CombatHUDPanel] Initialize: quickSlotSystem={(quickSlotSystem != null ? quickSlotSystem.ToString() : "null")} ({quickSlotSystem?.GetHashCode() ?? 0})");
            // If slots were never spawned (edge-case: Initialize called before Awake),
            // run the spawn pass now as a safety net.
            if (!_slotsSpawned)
            {
                SpawnWeaponSlots();
                SpawnQuickSlots();
                _slotsSpawned = true;
            }

            // Detach events from previous system before swapping references.
            UnwireSystemEvents();

            _weaponSystem    = weaponSystem;
            _quickSlotSystem = quickSlotSystem;

            // Rebind existing slot buttons with the new system references.
            RebindWeaponSlots();
            RebindQuickSlots();

            // Wire the optional aim controller with player systems so it can read VisionRange + aim.
            if (_aimController != null)
                _aimController.Initialize(statSystem, quickSlotSystem, playerTransform, aimSystem, itemUseSystem, combatInputHandler);

            // Store itemUseSystem reference for cancel-button subscription.
            UnwireCancelButton();
            _itemUseSystem = itemUseSystem;
            WireCancelButton();

            // Bind fire-button to the local player's combat handler and range indicator.
            ApplyFireButtonVisibility();
            if (_showFireButton && _fireButton != null)
            {
                _fireButton.Initialize(InputManager.Instance?.CombatHandler);
                if (playerTransform != null)
                {
                    float vr = statSystem != null ? statSystem.GetStat(PlayerStatType.VisionRange) : 0f;
                    if (vr <= 0f) vr = 15f;
                    _fireButton.BindPlayerContext(playerTransform, vr);
                }
            }

            WireSystemEvents();
            HideStatusMessages();
            _isInitialized = true;
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
            if (_inventoryConfig == null || _inventoryConfig.WeaponConfig == null)
            {
                Debug.LogWarning("[CombatHUDPanel] _inventoryConfig.WeaponConfig is null — weapon slots not spawned.");
                return;
            }

            foreach (var cfg in _inventoryConfig.WeaponConfig)
            {
                var btn = Instantiate(_weaponSlotPrefab, _weaponSlotsContainer);
                btn.name = $"WeaponSlot_{cfg.Type}";
                btn.Bind(cfg.Type, null);   // null system → renders as placeholder
                _spawnedWeaponButtons.Add(btn);
                _spawnedWeaponTypes.Add(cfg.Type);  // keep type so RebindWeaponSlots doesn't need config
                btn.gameObject.SetActive(true);
            }
        }

        private void SpawnQuickSlots()
        {
            // Destroy only previously TRACKED buttons (from a prior call to this method).
            foreach (var btn in _spawnedQuickSlotButtons)
                if (btn != null) Destroy(btn.gameObject);
            _spawnedQuickSlotButtons.Clear();

            // ── Apply show/hide to both roots ─────────────────────────────────
            // Roots are hidden/shown regardless of whether a prefab is assigned,
            // so the designer can control visibility from the Inspector bool alone.
            if (_quickSlotsRoot     != null) _quickSlotsRoot.gameObject.SetActive(_showQuickSlotsUI && _quickSlotLayout == QuickSlotLayoutMode.Circle);
            if (_quickSlotsRootList != null) _quickSlotsRootList.gameObject.SetActive(_showQuickSlotsUI && _quickSlotLayout == QuickSlotLayoutMode.HorizontalList);

            if (!_showQuickSlotsUI)
            {
                Debug.Log("[CombatHUDPanel] Quick slot UI hidden (_showQuickSlotsUI = false).");
                return;
            }

            // Determine active root based on selected layout.
            RectTransform activeRoot = _quickSlotLayout == QuickSlotLayoutMode.HorizontalList
                ? _quickSlotsRootList
                : _quickSlotsRoot;

            if (_quickSlotPrefab == null || activeRoot == null)
            {
                // Fallback: register any designer-placed QuickSlotHUDButton children in
                // the relevant root (pre-placed buttons stay owned by the designer).
                if (activeRoot != null)
                {
                    var prePlaced = activeRoot.GetComponentsInChildren<QuickSlotHUDButton>(true);
                    foreach (var p in prePlaced)
                        _spawnedQuickSlotButtons.Add(p);   // register for later rebind
                }
                else
                {
                    Debug.LogWarning($"[CombatHUDPanel] _quickSlotPrefab or active root for layout " +
                                     $"'{_quickSlotLayout}' is null — quick slots not spawned.");
                }
                return;
            }

            // Prefab spawner path: hide any un-tracked designer-placed children in the active
            // root so they don't stack on top of freshly instantiated buttons.
            foreach (var e in activeRoot.GetComponentsInChildren<QuickSlotHUDButton>(true))
                if (e != null) e.gameObject.SetActive(false);

            // Also ensure the inactive root's children are hidden.
            RectTransform inactiveRoot = _quickSlotLayout == QuickSlotLayoutMode.HorizontalList
                ? _quickSlotsRoot
                : _quickSlotsRootList;
            if (inactiveRoot != null)
                foreach (var e in inactiveRoot.GetComponentsInChildren<QuickSlotHUDButton>(true))
                    if (e != null) e.gameObject.SetActive(false);

            // Slot count is fixed in the config ScriptableObject — known at Awake time.
            int count = _inventoryConfig != null ? _inventoryConfig.QuickSlotConfig.SlotCount : 0;
            if (count <= 0)
            {
                Debug.LogWarning("[CombatHUDPanel] Quick slot count is 0 — assign _inventoryConfig.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var btn = Instantiate(_quickSlotPrefab, activeRoot);
                btn.name = $"QuickSlot_{i}";

                if (_quickSlotLayout == QuickSlotLayoutMode.Circle)
                {
                    // Radial position in quadrant II (upper-left from fire button).
                    float t        = count > 1 ? (float)i / (count - 1) : 0.5f;
                    float angleDeg = _arcCenterAngle - _arcSpread * 0.5f + t * _arcSpread;
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    var   rt       = btn.GetComponent<RectTransform>();
                    if (rt != null)
                        rt.anchoredPosition = new Vector2(
                            Mathf.Cos(angleRad) * _radialRadius,
                            Mathf.Sin(angleRad) * _radialRadius);
                }
                // HorizontalList: no manual positioning — HorizontalLayoutGroup handles it.

                btn.Bind(i, null);                          // null system → placeholder
                btn.SetAimController(_aimController);
                _spawnedQuickSlotButtons.Add(btn);
                btn.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Show or hide the entire quick-slot UI at runtime (e.g. game-mode specific HUD).
        /// Reflects the same logic as the <see cref="_showQuickSlotsUI"/> inspector field.
        /// </summary>
        public void SetQuickSlotUIVisible(bool visible)
        {
            _showQuickSlotsUI = visible;
            if (_quickSlotsRoot     != null) _quickSlotsRoot.gameObject.SetActive(visible && _quickSlotLayout == QuickSlotLayoutMode.Circle);
            if (_quickSlotsRootList != null) _quickSlotsRootList.gameObject.SetActive(visible && _quickSlotLayout == QuickSlotLayoutMode.HorizontalList);
        }

        /// <summary>
        /// Show or hide the on-screen fire button at runtime.
        /// When hiding, also stops any in-progress fire via <see cref="FireButton.Bind"/>(null).
        /// </summary>
        public void SetFireButtonVisible(bool visible)
        {
            _showFireButton = visible;
            ApplyFireButtonVisibility();
        }

        private void ApplyFireButtonVisibility()
        {
            if (_fireButton == null) return;
            _fireButton.gameObject.SetActive(_showFireButton);
            // If hiding while fire is in progress, safely stop the fire state.
            if (!_showFireButton)
                _fireButton.Bind(null);
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
            }
        }

        private void RebindQuickSlots()
        {
            for (int i = 0; i < _spawnedQuickSlotButtons.Count; i++)
            {
                var btn = _spawnedQuickSlotButtons[i];
                if (btn == null) continue;
                btn.Bind(i, _quickSlotSystem);
                btn.SetAimController(_aimController);
            }
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

            if (_reloadProgressBar == null) return;

            // Read reload duration from the active weapon's computed stat.
            float duration = 2.5f;
            var activeWeapon = _weaponSystem?.GetActiveWeapon();
            if (activeWeapon != null)
            {
                float d = activeWeapon.GetComputedStat(ItemStatType.ReloadSpeed);
                if (d > 0f) duration = d;
            }

            // Configure and start MUIP ProgressBar:
            //   - currentPercent starts at 0, counts up to 100 over `duration` seconds.
            //   - speed = 100 / duration → bar reaches 100 % in exactly `duration` seconds.
            //   - invert = false  → 0 % → 100 %
            //   - restart = false → stays at 100 % when done (StopReloadProgress hides it).
            _reloadProgressBar.currentPercent = 0f;
            _reloadProgressBar.speed          = Mathf.Max(1, Mathf.RoundToInt(100f / duration));
            _reloadProgressBar.invert         = false;
            _reloadProgressBar.restart        = false;
            _reloadProgressBar.isOn           = true;
            _reloadProgressBar.gameObject.SetActive(true);

            _reloadProgressCoroutine = StartCoroutine(WaitForReload(duration));
        }

        private void StopReloadProgress()
        {
            if (_reloadProgressCoroutine != null)
            {
                StopCoroutine(_reloadProgressCoroutine);
                _reloadProgressCoroutine = null;
            }
            if (_reloadProgressBar != null)
            {
                _reloadProgressBar.isOn = false;
                _reloadProgressBar.gameObject.SetActive(false);
            }
        }

        private IEnumerator WaitForReload(float duration)
        {
            yield return new UnityEngine.WaitForSeconds(duration);
            StopReloadProgress();
        }

        private void HandleWeaponDepleted(WeaponSlotType slot)
        {
            if (_weaponSystem == null) return;
            var active = _weaponSystem.GetActiveWeaponSlot();
            if (!active.HasValue || active.Value != slot) return;

            // Play "out of ammo" audio cue.
            if (_audioSource != null && _depletedClip != null)
                _audioSource.PlayOneShot(_depletedClip);
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
                _aimController.CancelAim();
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

    }
}


