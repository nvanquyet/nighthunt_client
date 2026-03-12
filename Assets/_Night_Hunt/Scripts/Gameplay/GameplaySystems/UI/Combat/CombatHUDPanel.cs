using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Michsky.MUIP;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.StatSystem.Core.Interfaces;
using NightHunt.StatSystem.Core.Types;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Combat
{
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
        [SerializeField]
        private InventoryConfig _inventoryConfig;

        [Header("Weapon Slots (spawned from InventoryConfig.WeaponConfig)")]
        [Tooltip("Prefab with WeaponSlotButton component.")]
        [SerializeField]
        private WeaponSlotButton _weaponSlotPrefab;

        [Tooltip("Parent with HorizontalLayoutGroup — weapon slot buttons land here.")] [SerializeField]
        private Transform _weaponSlotsContainer;

        [Header("Quick Slots — Radial Arc around Fire Button")]
        [Tooltip("Prefab with QuickSlotHUDButton component.")]
        [SerializeField]
        private QuickSlotHUDButton _quickSlotPrefab;

        [Tooltip("Empty RectTransform placed at the fire button center — origin of the radial layout.")]
        [SerializeField]
        private RectTransform _quickSlotsRoot;

        [Tooltip("Radius in pixels from fire button center to each quick-slot button.")] [SerializeField]
        private float _radialRadius = 130f;

        [Tooltip("Center angle of the arc in degrees (0=right, 90=up, 135=upper-left, 180=left).")] [SerializeField]
        private float _arcCenterAngle = 135f;

        [Tooltip("Total spread of the arc in degrees. E.g. 80 = buttons span 40° each side of center.")]
        [SerializeField]
        private float _arcSpread = 80f;

        // ─────────────────────────────────────────────────────────────────────
        //  Inspector — Shared HUD Labels (fixed — not config-driven)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Reload Progress (center-screen)")]
        [Tooltip("MUIP ProgressBar shown while reloading. Assign the ProgressBar component placed at screen centre.")]
        [SerializeField]
        private ProgressBar _reloadProgressBar;

        [Header("Mobile Fire Button (optional)")]
        [Tooltip("On-screen fire button for mobile / controller. Auto-finds CombatInputHandler if not pre-assigned.")]
        [SerializeField]
        private FireButton _fireButton;

        [Header("Quickslot Aim Controller (optional)")]
        [Tooltip("Assign the scene QuickSlotAimController so throwable slots show range/aim UI.")]
        [SerializeField]
        private QuickSlotAimController _aimController;

        [Header("Audio")]
        [Tooltip("AudioSource to play sound effects. Add AudioSource component on this GameObject.")]
        [SerializeField]
        private AudioSource _audioSource;

        [Tooltip("Clip played when the active weapon's magazine AND reserve ammo both reach zero.")] [SerializeField]
        private AudioClip _depletedClip;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────

        private IWeaponSystem _weaponSystem;
        private IQuickSlotSystem _quickSlotSystem;
        private bool _slotsSpawned; // true after Awake spawn pass
        private bool _isInitialized; // true after first data bind
        private Coroutine _reloadProgressCoroutine;

        private readonly List<WeaponSlotButton> _spawnedWeaponButtons = new List<WeaponSlotButton>();
        private readonly List<WeaponSlotType> _spawnedWeaponTypes = new List<WeaponSlotType>();
        private readonly List<QuickSlotHUDButton> _spawnedQuickSlotButtons = new List<QuickSlotHUDButton>();

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
            _slotsSpawned = true;
        }

        private void OnDestroy()
        {
            UnwireSystemEvents();
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
            IWeaponSystem weaponSystem,
            IQuickSlotSystem quickSlotSystem,
            IPlayerStatSystem statSystem = null,
            Transform playerTransform = null)
        {
            Debug.Log(
                $"[CombatHUDPanel] Initialize: quickSlotSystem={(quickSlotSystem != null ? quickSlotSystem.ToString() : "null")} ({quickSlotSystem?.GetHashCode() ?? 0})");
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

            _weaponSystem = weaponSystem;
            _quickSlotSystem = quickSlotSystem;

            // Rebind existing slot buttons with the new system references.
            RebindWeaponSlots();
            RebindQuickSlots();

            // Wire the optional aim controller with player systems so it can read VisionRange.
            if (_aimController != null)
                _aimController.Initialize(statSystem, quickSlotSystem, playerTransform);

            // Bind fire-button range indicator to the local player so the ring is correctly
            // positioned and sized (VisionRange) from the first fire tap.
            if (_fireButton != null && playerTransform != null)
            {
                float vr = statSystem != null ? statSystem.GetStat(PlayerStatType.VisionRange) : 0f;
                if (vr <= 0f) vr = 15f;
                _fireButton.BindPlayerContext(playerTransform, vr);
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
                if (btn != null)
                    Destroy(btn.gameObject);
            _spawnedWeaponButtons.Clear();
            _spawnedWeaponTypes.Clear();

            if (_weaponSlotPrefab == null || _weaponSlotsContainer == null)
            {
                Debug.LogWarning(
                    "[CombatHUDPanel] _weaponSlotPrefab or _weaponSlotsContainer is null — weapon slots not spawned.");
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
                btn.Bind(cfg.Type, null); // null system → renders as placeholder
                _spawnedWeaponButtons.Add(btn);
                _spawnedWeaponTypes.Add(cfg.Type); // keep type so RebindWeaponSlots doesn't need config
                btn.gameObject.SetActive(true);
            }
        }

        private void SpawnQuickSlots()
        {
            // Destroy only previously TRACKED buttons (from a prior call to this method).
            // Pre-placed designer children are kept unless we have a prefab to replace them.
            foreach (var btn in _spawnedQuickSlotButtons)
                if (btn != null)
                    Destroy(btn.gameObject);
            _spawnedQuickSlotButtons.Clear();

            if (_quickSlotPrefab == null || _quickSlotsRoot == null)
            {
                // Fallback: register any designer-placed QuickSlotHUDButton children.
                // These were NOT destroyed above, so they are still present.
                if (_quickSlotsRoot != null)
                {
                    var prePlaced = _quickSlotsRoot.GetComponentsInChildren<QuickSlotHUDButton>(true);
                    foreach (var p in prePlaced)
                        _spawnedQuickSlotButtons.Add(p); // register for later rebind
                }
                else
                {
                    Debug.LogWarning(
                        "[CombatHUDPanel] _quickSlotPrefab or _quickSlotsRoot is null — quick slots not spawned.");
                }

                return;
            }

            // Prefab spawner path: remove any un-tracked designer-placed children that
            // would stack on top of the freshly instantiated buttons.
            foreach (var e in _quickSlotsRoot.GetComponentsInChildren<QuickSlotHUDButton>(true))
                if (e != null)
                    e.gameObject.SetActive(false);
            ;

            // Slot count is fixed in the config ScriptableObject — known at Awake time.
            int count = _inventoryConfig != null ? _inventoryConfig.QuickSlotConfig.SlotCount : 0;
            if (count <= 0)
            {
                Debug.LogWarning("[CombatHUDPanel] Quick slot count is 0 — assign _inventoryConfig.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var btn = Instantiate(_quickSlotPrefab, _quickSlotsRoot);
                btn.name = $"QuickSlot_{i}";

                // Radial position in quadrant II (upper-left from fire button).
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float angleDeg = _arcCenterAngle - _arcSpread * 0.5f + t * _arcSpread;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                var rt = ComponentResolver.Find<RectTransform>(btn)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] RectTransform not found")
                    .Resolve();
                if (rt != null)
                    rt.anchoredPosition = new Vector2(
                        Mathf.Cos(angleRad) * _radialRadius,
                        Mathf.Sin(angleRad) * _radialRadius);

                btn.Bind(i, null); // null system → placeholder
                btn.SetAimController(_aimController);
                _spawnedQuickSlotButtons.Add(btn);
                btn.gameObject.SetActive(true);
            }
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
            _weaponSystem.OnReloadStateChanged += HandleReloadStateChanged;
            _weaponSystem.OnWeaponDepleted += HandleWeaponDepleted;
            _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
        }

        private void UnwireSystemEvents()
        {
            if (_weaponSystem == null) return;
            _weaponSystem.OnReloadStateChanged -= HandleReloadStateChanged;
            _weaponSystem.OnWeaponDepleted -= HandleWeaponDepleted;
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
            _reloadProgressBar.speed = Mathf.Max(1, Mathf.RoundToInt(100f / duration));
            _reloadProgressBar.invert = false;
            _reloadProgressBar.restart = false;
            _reloadProgressBar.isOn = true;
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
    }
}