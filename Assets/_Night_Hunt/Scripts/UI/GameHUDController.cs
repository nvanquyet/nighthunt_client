using UnityEngine;
using NightHunt.GameplaySystems.UI;
using NightHunt.GameplaySystems.UI.Inventory;
using NightHunt.GameplaySystems.UI.Combat;
using NightHunt.GameplaySystems.UI.Interaction;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;
using NightHunt.UI.Mobile;
using NightHunt.Gameplay.Spectator;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// GameHUDController — the single orchestrator for all in-game UI.
    ///
    /// REPLACES: <see cref="GameHUD"/> + <see cref="NightHunt.GameplaySystems.UI.Inventory.UIRootController"/> (both now obsolete).
    ///
    /// RESPONSIBILITIES:
    ///   ① Single entry point: <see cref="NetworkPlayer.OnOwnerReady"/> → <see cref="Initialize"/>
    ///   ② Owns <see cref="UIPlayerContext"/> — single event bridge to gameplay systems
    ///   ③ <see cref="UIState"/> state machine — drives ALL panel visibility from one place
    ///   ④ Player lifecycle: death → Dead, respawn → Combat
    ///   ⑤ Spectate lifecycle: spectate start → Spectating, stop → Combat
    ///   ⑥ Inventory toggle via InputLayerManager (Inventory state ↔ Combat state)
    ///   ⑦ Mobile input binding (joystick + pinch zoom)
    ///   ⑧ Combat event routing (kill feed, damage numbers)
    ///
    /// ARCHITECTURE RULE: This class contains ZERO display logic.
    /// All rendering is delegated to the sub-panels listed in the Inspector.
    ///
    /// SETUP (in scene):
    ///   • Place on the root HUD Canvas GameObject.
    ///   • Wire all sub-panel references in the Inspector.
    ///   • Remove the old GameHUD and UIRootController components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHUDController : MonoBehaviour
    {
        // ── Combat Layer ─────────────────────────────────────────────────────
        // Visible during UIState.Combat only (except PlayerStatsHUD which also shows in Inventory)

        [Header("Combat Layer")]
        [Tooltip("Weapon slot buttons + ammo display. Visible during Combat only.")]
        [SerializeField] private WeaponHUDPanel _weaponHUD;

        [Tooltip("Consumable/throwable filter panels + item-use progress bar. Visible during Combat only.")]
        [SerializeField] private ItemSelectionHUD _itemSelectionHUD;

        [Tooltip("Shared progress bar presenter used by WeaponHUD (reload) and ItemSelectionHUD (item-use). " +
                 "Place once on the HUD canvas and assign here — panels receive it via injection.")]
        [SerializeField] private ActionProgressPresenter _actionProgressPresenter;

        [Tooltip("Compact HP / Armor / Stamina bars. Visible during Combat and Inventory states.")]
        [SerializeField] private PlayerStatsHUD _playerStatsHUD;

        [Tooltip("'Press E to interact' prompt. Visible during Combat only.")]
        [SerializeField] private InteractionPromptUI _interactionPromptUI;

        [Tooltip("Minimap raw image. Visible during Combat and Spectating.")]
        [SerializeField] private MinimapUI _minimapUI;

        // ── Inventory Layer ──────────────────────────────────────────────────
        // Visible during UIState.Inventory only

        [Header("Inventory Layer")]
        [Tooltip("Root GameObject that contains the full inventory screen. Toggled by Tab key.")]
        [SerializeField] private GameObject _inventoryRoot;

        [Tooltip("InventoryScreen component inside _inventoryRoot.")]
        [SerializeField] private InventoryScreen _inventoryScreen;


        // ── Match Layer ──────────────────────────────────────────────────────
        // Visible during Combat and Spectating (hidden in PostMatch/Dead)

        [Header("Match Layer")]
        [Tooltip("Phase label, timer, team scores. Visible during active play.")]
        [SerializeField] private MatchUI _matchUI;

        [Tooltip("Kill feed entries. Visible during active play.")]
        [SerializeField] private KillFeedUI _killFeedUI;

        [Tooltip("Objective capture/activation progress. Auto-created at runtime if unassigned.")]
        [SerializeField] private ObjectiveCaptureHUD _objectiveCaptureHUD;

        [Tooltip("Boss HP bar. Auto-created at runtime if unassigned.")]
        [SerializeField] private BossHealthHUD _bossHealthHUD;

        // ── Overlay Layer ────────────────────────────────────────────────────
        // Each panel shows only in its specific UIState

        [Header("Overlay Layer")]
        [Tooltip("Death screen with respawn timer. Shown during UIState.Dead.")]
        [SerializeField] private DeathScreen _deathScreen;

        [Tooltip("Spectator HUD (observed player name, HP, weapon). Shown during UIState.Spectating.")]
        [SerializeField] private SpectatorHUD _spectatorHUD;

        [Tooltip("Match results scoreboard. Shown during UIState.PostMatch.")]
        [SerializeField] private ResultsView _resultsView;

        [Tooltip("Loot container UI. Manages its own visibility.")]
        [SerializeField] private LootContainerUI _lootContainerUI;

        [Tooltip("Floating damage numbers shown when local player deals damage.")]
        [SerializeField] private Gameplay.Feedback.DamageFeedbackSystem _damageFeedback;

        // ── Mobile Input ─────────────────────────────────────────────────────

        [Header("Mobile Input")]
        [Tooltip("Central panel for all on-screen mobile controls. Auto-found in children if unassigned.")]
        [SerializeField] private MobileHUDPanel _mobileHUDPanel;

        // ── Top-Left Team HUD ─────────────────────────────────────────────────

        [Header("Team HUD")]
        [Tooltip("Top-left panel showing owner stats and team member rows.")]
        [SerializeField] private TeamMemberPanel _teamMemberPanel;

        // ── Panels to disable while inventory is open ─────────────────────────

        [Header("Inventory Overlay Exclusions")]
        [Tooltip("GameObjects hidden while UIState.Inventory is active (e.g. combat action buttons). " +
                 "Restore automatically when returning to Combat state.")]
        [SerializeField] private GameObject[] _disableOnInventory;

        // ── Runtime state ─────────────────────────────────────────────────────

        private UIPlayerContext _playerContext;
        private UIState _currentState = UIState.Combat;
        private NetworkPlayer _localPlayer;
        private int _localNetObjId = -1;
        private CharacterLifecycleController _localLifecycle;
        private NightHunt.GameplaySystems.Interaction.PlayerInteractionSystem _localInteractionSystem;
        private bool _initializedForPlayer;
        private Coroutine _inventoryProximityRefreshCoroutine;

        // ── Public accessors ──────────────────────────────────────────────────

        /// <summary>The shared event hub. Pass to panels that need direct access.</summary>
        public UIPlayerContext PlayerContext => _playerContext;

        /// <summary>Current UI state. Read-only; change via <see cref="SetState"/>.</summary>
        public UIState CurrentState => _currentState;

        // Convenience accessors for external systems
        public KillFeedUI     KillFeed       => _killFeedUI;
        public MinimapUI      Minimap        => _minimapUI;
        public LootContainerUI LootContainerUI => _lootContainerUI;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _playerContext = new UIPlayerContext();
            EnsureRuntimeHudPanels();
            HideOverlays();

            // Cursor is always visible in this top-down game.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        private void OnEnable()
        {
            // Primary init trigger: fires when the local player is ready.
            NetworkPlayer.OnOwnerReady += Initialize;

            // Inventory toggle
            var input = InputManager.Instance;
            if (input?.InventoryHandler != null)
            {
                input.InventoryHandler.OpenInventoryPerformed += HandleInventoryToggle;
                input.InventoryHandler.QuickSlotPerformed += HandleInventoryQuickSlot;
            }

            // Context change from InputLayerManager drives Inventory ↔ Combat transition.
            if (InputLayerManager.Instance != null)
                InputLayerManager.Instance.OnContextChanged += HandleContextChanged;

            // Spectate: player switch rebuilds UIPlayerContext.
            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnCurrentPlayerChanged += HandlePlayerChanged;
        }

        private void OnDisable()
        {
            NetworkPlayer.OnOwnerReady -= Initialize;

            var input = InputManager.Instance;
            if (input?.InventoryHandler != null)
            {
                input.InventoryHandler.OpenInventoryPerformed -= HandleInventoryToggle;
                input.InventoryHandler.QuickSlotPerformed -= HandleInventoryQuickSlot;
            }

            if (InputLayerManager.Instance != null)
                InputLayerManager.Instance.OnContextChanged -= HandleContextChanged;

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnCurrentPlayerChanged -= HandlePlayerChanged;

            UnsubscribeLifecycle();
        }

        private void Start()
        {
            // Edge case: SpectateManager was null during OnEnable (script execution order).
            if (SpectateManager.Instance != null)
            {
                // Re-register (idempotent — -= then +=)
                SpectateManager.Instance.OnCurrentPlayerChanged -= HandlePlayerChanged;
                SpectateManager.Instance.OnCurrentPlayerChanged += HandlePlayerChanged;

                // Edge case: GameHUDController enabled AFTER local player spawned (additive scene load).
                if (!_initializedForPlayer)
                {
                    var existing = SpectateManager.Instance.GetCurrentPlayer();
                    if (existing != null) Initialize(existing);
                }
            }
        }

        private void OnDestroy()
        {
            PlayerHealthSystem.OnAnyPlayerDied  -= HandleAnyPlayerDied;
            PlayerHealthSystem.OnAnyHitReceived -= HandleAnyHitReceived;
            _playerContext?.Dispose();
        }

        // ── Primary Entry Point ───────────────────────────────────────────────

        /// <summary>
        /// Called once (via <see cref="NetworkPlayer.OnOwnerReady"/>) when the local player spawns.
        /// This is the SINGLE initialization entry point for the entire UI.
        ///
        /// All panels receive their dependencies here — no panel should initialize itself
        /// before this method fires.
        /// </summary>
        public void Initialize(NetworkPlayer localPlayer)
        {
            if (localPlayer == null) return;

            _initializedForPlayer = true;
            _localPlayer          = localPlayer;
            _localNetObjId        = (int)localPlayer.ObjectId;

            // ① Bind UIPlayerContext — all UI events flow through this.
            _playerContext.Bind(localPlayer);

            // ② Match layer (manage their own event subscriptions via GameplayEventBus)
            _matchUI?.Initialize(localPlayer);
            _minimapUI?.SetLocalPlayer(localPlayer);
            var minimapCamera = _minimapUI != null
                ? _minimapUI.GetComponentInChildren<GameplaySystems.UI.Minimap.MinimapCameraController>(true)
                : null;
            minimapCamera?.SetLocalPlayer(localPlayer);
            _deathScreen?.RegisterPlayer(localPlayer);
            _lootContainerUI?.SetLocalPlayer(localPlayer);

            // ③ Inventory layer
            _inventoryScreen?.Initialize(_playerContext);

            // ④ Combat layer
            _weaponHUD?.Bind(_playerContext);
            _weaponHUD?.BindProgress(_actionProgressPresenter);
            _itemSelectionHUD?.Bind(_playerContext);
            _itemSelectionHUD?.BindProgress(_actionProgressPresenter);
            _playerStatsHUD?.Bind(_playerContext);
            _teamMemberPanel?.Initialize(localPlayer, _playerContext);
            _teamMemberPanel?.SetObservedPlayer(null);

            // ⑤ Mobile input
            BindMobileInput(_mobileHUDPanel, localPlayer);

            // ⑥ Global combat events (all players)
            PlayerHealthSystem.OnAnyPlayerDied  += HandleAnyPlayerDied;
            PlayerHealthSystem.OnAnyHitReceived += HandleAnyHitReceived;

            // ⑦ Local player lifecycle — must come before WireInteractionPrompt because
            //    SubscribeLifecycle() calls UnsubscribeLifecycle() which resets _localInteractionSystem.
            SubscribeLifecycle(localPlayer);

            // ⑧ Interaction prompt — after SubscribeLifecycle so _localInteractionSystem is not cleared.
            WireInteractionPrompt(localPlayer);

            // ⑨ Spectate start/stop (different from player switch)
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnSpectateStarted -= OnSpectateStarted;
                SpectateManager.Instance.OnSpectateStarted += OnSpectateStarted;
                SpectateManager.Instance.OnSpectateStopped -= OnSpectateStopped;
                SpectateManager.Instance.OnSpectateStopped += OnSpectateStopped;
            }

            SetState(UIState.Combat);

            Debug.Log($"[GameHUDController] Initialized for player '{localPlayer.name}' (netId={_localNetObjId})");
        }

        // ── Player Switch (Spectate) ──────────────────────────────────────────

        private void HandlePlayerChanged(NetworkPlayer newPlayer)
        {
            if (newPlayer == null) return;

            // Reset drag-drop state when the observed player switches.
            DragDropController.Instance?.ResetAll();

            // Rebuild UIPlayerContext for the new player.
            _playerContext.Unbind();
            _playerContext.Bind(newPlayer);

            // Re-distribute context to panels that hold direct references.
            _inventoryScreen?.RefreshForNewPlayer(_playerContext);
            _weaponHUD?.Bind(_playerContext);
            _itemSelectionHUD?.Bind(_playerContext);
            _playerStatsHUD?.Bind(_playerContext);

            bool isSpectating = SpectateManager.Instance != null && SpectateManager.Instance.IsSpectating();
            _teamMemberPanel?.SetObservedPlayer(isSpectating ? newPlayer : null);

            // Wire the proximity scanner of the spectated player so LootContainerUI shows
            // their nearby open containers in read-only mode.
            // EndSpectate() is called when isSpectating is false (player switched back to self).
            if (isSpectating)
                _localInteractionSystem?.BeginSpectate(newPlayer);
            else
                _localInteractionSystem?.EndSpectate();
        }

        // ── State Machine ─────────────────────────────────────────────────────

        /// <summary>
        /// Transition to a new <see cref="UIState"/>. No-ops if already in that state.
        /// Immediately updates all panel visibility according to the state matrix.
        /// </summary>
        public void SetState(UIState newState)
        {
            if (_currentState == newState)
            {
                ApplyState();
                return;
            }
            _currentState = newState;
            ApplyState();
        }

        public void CloseInventory()
        {
            _lootContainerUI?.Hide();
            var ilm = InputLayerManager.Instance;
            if (ilm != null && ilm.CurrentState == InputState.InventoryOpen)
                ilm.PopContext();
            else
                SetState(UIState.Combat);
        }

        private void ApplyState()
        {
            bool isCombat    = _currentState == UIState.Combat;
            bool isInventory = _currentState == UIState.Inventory;
            bool isDead      = _currentState == UIState.Dead;
            bool isSpectate  = _currentState == UIState.Spectating;
            bool isPostMatch = _currentState == UIState.PostMatch;
            bool isActivePly = isCombat || isInventory; // local player alive and in control

            // ── Combat layer ─────────────────────────────────────────────────
            SetActive(_weaponHUD,           isCombat);
            SetActive(_itemSelectionHUD,    isCombat);
            SetActive(_playerStatsHUD,      isActivePly);   // also visible in inventory
            SetActive(_interactionPromptUI,  isCombat);

            // Hide designer-specified objects while inventory is open (e.g. action buttons).
            bool enteringInventory = _currentState == UIState.Inventory;
            if (_disableOnInventory != null)
                foreach (var go in _disableOnInventory)
                    if (go != null) go.SetActive(!enteringInventory);

            // Team HUD — visible in Combat and Inventory only (not dead/spectate/results).
            SetActive(_teamMemberPanel, isActivePly);

            // Mobile root follows same visibility as team HUD.
            if (_mobileHUDPanel != null)
                _mobileHUDPanel.SetMobileUIVisible(isActivePly);

            // ── Minimap ──────────────────────────────────────────────────────
            SetActive(_minimapUI, isCombat || isSpectate);

            // ── Inventory ────────────────────────────────────────────────────
            if (_inventoryRoot != null) _inventoryRoot.SetActive(isInventory);

            // ── Match layer ──────────────────────────────────────────────────
            bool matchVisible = !isPostMatch;
            SetActive(_matchUI,    matchVisible);
            SetActive(_killFeedUI, matchVisible);
            if (_objectiveCaptureHUD != null)
                _objectiveCaptureHUD.SetHudVisible(matchVisible && (isCombat || isSpectate));
            if (_bossHealthHUD != null)
                _bossHealthHUD.SetHudVisible(matchVisible && (isCombat || isSpectate));

            // ── Overlays ─────────────────────────────────────────────────────
            // Death screen — panel manages its own Show/Hide animation;
            // we only toggle the root GameObject as a broad on/off.
            if (_deathScreen != null) _deathScreen.gameObject.SetActive(isDead);
            SetActive(_spectatorHUD,  isSpectate);
            if (_resultsView != null)  _resultsView.gameObject.SetActive(isPostMatch);
        }

        private static void SetActive(Component c, bool active)
        {
            if (c != null) c.gameObject.SetActive(active);
        }

        // ── Lifecycle event handlers ──────────────────────────────────────────

        private void OnLocalPlayerDied()      => SetState(UIState.Dead);

        private void OnLocalPlayerRespawned()
        {
            _deathScreen?.Hide();
            SetState(UIState.Combat);
        }

        private void OnSpectateStarted()
        {
            // Hide any loot panel that the local player had open before dying.
            _lootContainerUI?.Hide();
            SetState(UIState.Spectating);
        }

        private void OnSpectateStopped()
        {
            // HideSpectateView is a no-op if the panel is not in spectate mode,
            // so this is safe to call unconditionally.
            _lootContainerUI?.HideSpectateView();
            SetState(UIState.Combat);
        }

        // ── Inventory Toggle ──────────────────────────────────────────────────

        private void HandleInventoryToggle()
        {
            var ilm = InputLayerManager.Instance;
            if (ilm == null) return;

            if (ilm.CurrentState == InputState.InventoryOpen)
            {
                ilm.PopContext();
            }
            else
            {
                ilm.PushContext(InputState.InventoryOpen);
            }
        }

        private void HandleInventoryQuickSlot(int oneBasedSlot)
        {
            if (_currentState == UIState.Dead || _currentState == UIState.PostMatch ||
                _currentState == UIState.Spectating)
            {
                Debug.Log($"[ITEM_FLOW] [00][QuickSlot.Ignored] slot={oneBasedSlot} state={_currentState}");
                return;
            }

            if (_itemSelectionHUD == null)
            {
                Debug.LogWarning($"[ITEM_FLOW] [00][QuickSlot.Ignored] slot={oneBasedSlot} reason=no-item-selection-hud");
                return;
            }

            _itemSelectionHUD.ActivateQuickSlot(oneBasedSlot);
        }

        private void HandleContextChanged(InputState oldState, InputState newState)
        {
            // Only respond to Inventory transitions when in control (not dead/spectating/results).
            if (_currentState == UIState.Dead || _currentState == UIState.PostMatch ||
                _currentState == UIState.Spectating)
                return;

            if (newState == InputState.InventoryOpen)
            {
                Debug.Log($"[INV] Opening inventory. _inventoryRoot={(_inventoryRoot != null ? _inventoryRoot.name : "NULL")} " +
                          $"_inventoryScreen={(_inventoryScreen != null ? "ok" : "NULL")} " +
                          $"_localInteractionSystem={(_localInteractionSystem != null ? "ok" : "NULL")}");
                SetState(UIState.Inventory);
                _localInteractionSystem?.HandleInventoryOpened();
                StartInventoryProximityRefresh();
            }
            else if (oldState == InputState.InventoryOpen)
            {
                Debug.Log("[INV] Closing inventory.");
                StopInventoryProximityRefresh();
                if (_localInteractionSystem == null || !_localInteractionSystem.HandleInventoryClosed())
                    _lootContainerUI?.Hide();
                SetState(UIState.Combat);
            }
        }

        private void StartInventoryProximityRefresh()
        {
            StopInventoryProximityRefresh();
            if (_localInteractionSystem != null)
                _inventoryProximityRefreshCoroutine = StartCoroutine(InventoryProximityRefreshCoroutine());
        }

        private void StopInventoryProximityRefresh()
        {
            if (_inventoryProximityRefreshCoroutine != null)
            {
                StopCoroutine(_inventoryProximityRefreshCoroutine);
                _inventoryProximityRefreshCoroutine = null;
            }
        }

        private System.Collections.IEnumerator InventoryProximityRefreshCoroutine()
        {
            // Re-scan for nearby loot items every 0.5 s while inventory is open.
            // This updates the loot panel as the player walks closer to or away from items.
            var delay = new WaitForSeconds(0.5f);
            while (_currentState == UIState.Inventory)
            {
                yield return delay;
                if (_currentState != UIState.Inventory) break;
                _localInteractionSystem?.RefreshNearbyWorldItemsForInventory();
            }
            _inventoryProximityRefreshCoroutine = null;
        }

        // ── Combat Event Routing ──────────────────────────────────────────────

        private void HandleAnyPlayerDied(string victimName, string killerName, string weaponId)
        {
            _killFeedUI?.AddKill(killerName, victimName, weaponId);
        }

        private void HandleAnyHitReceived(DamageInfo info)
        {
            if (_localNetObjId < 0 || _damageFeedback == null) return;

            if (info.ShooterNetworkObjectId == _localNetObjId)
            {
                _damageFeedback.ShowDamageNumber(info.HitPoint, info.Damage, info.IsHeadshot);
                _damageFeedback.ShowHitEffect(info.HitPoint, info.HitNormal);
                return;
            }

            if (IsHitOnObservedPlayer(info))
                _damageFeedback.ShowHitIndicator(GetIncomingHitDirection(info));
        }

        private bool IsHitOnObservedPlayer(DamageInfo info)
        {
            var observed = SpectateManager.Instance?.GetCurrentPlayer() ?? _localPlayer;
            if (observed == null) return false;

            return Vector3.SqrMagnitude(observed.transform.position - info.HitPoint) <= 9f;
        }

        private Vector3 GetIncomingHitDirection(DamageInfo info)
        {
            var observed = SpectateManager.Instance?.GetCurrentPlayer() ?? _localPlayer;
            if (observed == null) return -info.HitNormal;

            var registry = PlayerPublicRegistry.Instance;
            if (registry != null && info.ShooterNetworkObjectId > 0)
            {
                var players = registry.GetAllPlayers();
                if (players != null)
                {
                    foreach (var player in players)
                    {
                        if (player == null || (int)player.ObjectId != info.ShooterNetworkObjectId)
                            continue;

                        Vector3 fromShooter = observed.transform.position - player.transform.position;
                        if (fromShooter.sqrMagnitude > 0.001f)
                            return fromShooter.normalized;
                    }
                }
            }

            Vector3 fallback = observed.transform.position - info.HitPoint;
            return fallback.sqrMagnitude > 0.001f ? fallback.normalized : -info.HitNormal;
        }

        // ── Setup Helpers ─────────────────────────────────────────────────────

        private void WireInteractionPrompt(NetworkPlayer player)
        {
            _localInteractionSystem = ComponentResolver
                .Find<NightHunt.GameplaySystems.Interaction.PlayerInteractionSystem>(player)
                .OnSelf().InChildren()
                .Resolve();

            if (_interactionPromptUI == null) return;

            var detector = ComponentResolver
                .Find<Gameplay.Input.Handlers.Interaction.RaycastDetector>(player)
                .OnSelf().InChildren()
                .OrLogWarning("[GameHUDController] RaycastDetector not found — interaction prompt will stay hidden.")
                .Resolve();

            var interactionSys = ComponentResolver
                .Find<GameplaySystems.Interaction.PlayerInteractionSystem>(player)
                .OnSelf().InChildren()
                .OrLogWarning("[GameHUDController] PlayerInteractionSystem not found — hold-progress disabled.")
                .Resolve();

            _localInteractionSystem = interactionSys;
            _interactionPromptUI.Init(detector, interactionSys);
            _interactionPromptUI.BindProgress(_actionProgressPresenter);
        }

        private void BindMobileInput(MobileHUDPanel panel, NetworkPlayer localPlayer)
        {
            if (panel == null)
                panel = GetComponentInChildren<MobileHUDPanel>(true);
            if (panel != null)
            {
                panel.Bind(InputManager.Instance);
                var statSys = ComponentResolver
                    .Find<NightHunt.Gameplay.StatSystem.Core.Interfaces.IPlayerStatSystem>(localPlayer)
                    .OnSelf().InChildren()
                    .Resolve();
                panel.BindPlayerContext(localPlayer.transform, statSys);
            }
        }

        private void EnsureRuntimeHudPanels()
        {
            if (_objectiveCaptureHUD == null)
                _objectiveCaptureHUD = GetComponentInChildren<ObjectiveCaptureHUD>(true);
            if (_objectiveCaptureHUD == null)
                _objectiveCaptureHUD = gameObject.AddComponent<ObjectiveCaptureHUD>();

            if (_bossHealthHUD == null)
                _bossHealthHUD = GetComponentInChildren<BossHealthHUD>(true);
            if (_bossHealthHUD == null)
                _bossHealthHUD = gameObject.AddComponent<BossHealthHUD>();

            // Runtime fallbacks for panels that are only resolved via OnValidate (editor-only).
            if (_weaponHUD == null)
                _weaponHUD = GetComponentInChildren<WeaponHUDPanel>(true);
            if (_itemSelectionHUD == null)
                _itemSelectionHUD = GetComponentInChildren<ItemSelectionHUD>(true);
            if (_actionProgressPresenter == null)
                _actionProgressPresenter = ComponentResolver.Find<ActionProgressPresenter>(this)
                    .InChildren()
                    .Resolve();
            if (_playerStatsHUD == null)
                _playerStatsHUD = GetComponentInChildren<PlayerStatsHUD>(true);
            if (_mobileHUDPanel == null)
                _mobileHUDPanel = GetComponentInChildren<MobileHUDPanel>(true);
        }

        private void SubscribeLifecycle(NetworkPlayer player)
        {
            UnsubscribeLifecycle();
            _localLifecycle = ComponentResolver
                .Find<CharacterLifecycleController>(player)
                .OnSelf().InChildren()
                .OrLogWarning("[GameHUDController] CharacterLifecycleController not found — death/respawn transitions disabled.")
                .Resolve();

            if (_localLifecycle != null)
            {
                _localLifecycle.OnDied      += OnLocalPlayerDied;
                _localLifecycle.OnRespawned += OnLocalPlayerRespawned;
            }
        }

        private void UnsubscribeLifecycle()
        {
            _mobileHUDPanel?.Unbind();

            if (_localLifecycle != null)
            {
                _localLifecycle.OnDied      -= OnLocalPlayerDied;
                _localLifecycle.OnRespawned -= OnLocalPlayerRespawned;
                _localLifecycle = null;
            }
            _localInteractionSystem = null;

            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnSpectateStarted -= OnSpectateStarted;
                SpectateManager.Instance.OnSpectateStopped -= OnSpectateStopped;
            }
        }

        private void HideOverlays()
        {
            // All panels must start in-scene (Awake/GetComponent works on active GOs).
            // Then selectively hide overlay panels that should NOT show at game start.
            _deathScreen?.Hide();
            _lootContainerUI?.Hide();
            if (_inventoryRoot != null)  _inventoryRoot.SetActive(false);
            if (_resultsView != null)    _resultsView.gameObject.SetActive(false);
            if (_spectatorHUD != null)   _spectatorHUD.gameObject.SetActive(false);
            _objectiveCaptureHUD?.SetHudVisible(false);
            _bossHealthHUD?.SetHudVisible(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_weaponHUD == null)           _weaponHUD           = GetComponentInChildren<WeaponHUDPanel>(true);
            if (_itemSelectionHUD == null)    _itemSelectionHUD    = GetComponentInChildren<ItemSelectionHUD>(true);
            if (_playerStatsHUD == null)      _playerStatsHUD      = GetComponentInChildren<PlayerStatsHUD>(true);
            if (_interactionPromptUI == null) _interactionPromptUI = GetComponentInChildren<InteractionPromptUI>(true);
            if (_minimapUI == null)           _minimapUI           = GetComponentInChildren<MinimapUI>(true);
            if (_inventoryScreen == null)     _inventoryScreen     = GetComponentInChildren<InventoryScreen>(true);
            if (_matchUI == null)             _matchUI             = GetComponentInChildren<MatchUI>(true);
            if (_killFeedUI == null)          _killFeedUI          = GetComponentInChildren<KillFeedUI>(true);
            if (_objectiveCaptureHUD == null) _objectiveCaptureHUD = GetComponentInChildren<ObjectiveCaptureHUD>(true);
            if (_bossHealthHUD == null)       _bossHealthHUD       = GetComponentInChildren<BossHealthHUD>(true);
            if (_deathScreen == null)         _deathScreen         = GetComponentInChildren<DeathScreen>(true);
            if (_spectatorHUD == null)        _spectatorHUD        = GetComponentInChildren<SpectatorHUD>(true);
            if (_resultsView == null)         _resultsView         = GetComponentInChildren<ResultsView>(true);
            if (_actionProgressPresenter == null) _actionProgressPresenter = GetComponentInChildren<ActionProgressPresenter>(true);
            if (_lootContainerUI == null)   _lootContainerUI   = GetComponentInChildren<LootContainerUI>(true);
            if (_mobileHUDPanel == null)    _mobileHUDPanel    = GetComponentInChildren<MobileHUDPanel>(true);
            if (_teamMemberPanel == null)   _teamMemberPanel   = GetComponentInChildren<TeamMemberPanel>(true);
        }
#endif
    }
}
