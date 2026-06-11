using UnityEngine;
using NightHunt.GameplaySystems.UI;
using NightHunt.GameplaySystems.UI.Inventory;
using NightHunt.GameplaySystems.UI.Combat;
using NightHunt.GameplaySystems.UI.Interaction;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Feedback;
using NightHunt.Gameplay.Deployables;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.Gameplay.Input.Handlers.UI;
using NightHunt.UI.Mobile;
using NightHunt.Gameplay.Spectator;
using NightHunt.Utilities;
using NightHunt.Audio;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.UI.Settings;

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

        [Tooltip("Gameplay settings / controls overlay. Auto-found in children if unassigned.")]
        [SerializeField] private SettingsView _settingsView;

        [Tooltip("Floating damage numbers shown when local player deals damage.")]
        [SerializeField] private Gameplay.Feedback.DamageFeedbackSystem _damageFeedback;

        // ── Mobile Input ─────────────────────────────────────────────────────

        [Header("Mobile Input")]
        [Tooltip("Central panel for all on-screen mobile controls. Auto-found in children if unassigned.")]
        [SerializeField] private MobileHUDPanel _mobileHUDPanel;

        [Tooltip("HUD indicator that shows current camera-lock (Strafe/Tank) state. " +
                 "Auto-found in children if unassigned. Call Bind/Unbind automatically managed.")]
        [SerializeField] private CameraLockIndicator _cameraLockIndicator;

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
        private InputManager _subscribedInputManager;
        private UIInputHandler _subscribedUIHandler;
        private SettingsView _subscribedSettingsView;
        private bool _combatEventsSubscribed;
        private bool _settingsOpen;

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
            GameActionBus.OnInventoryToggleRequested += HandleInventoryToggle;
            GameplayEventBus.Instance?.Subscribe<MatchEndedEvent>(OnMatchEnded);

            TrySubscribeInputHandlers();
            if (_initializedForPlayer)
                SubscribeCombatEvents();

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
            GameActionBus.OnInventoryToggleRequested -= HandleInventoryToggle;
            GameplayEventBus.Instance?.Unsubscribe<MatchEndedEvent>(OnMatchEnded);

            CloseSettingsOverlay(true);
            UnsubscribeInputHandlers();

            if (InputLayerManager.Instance != null)
                InputLayerManager.Instance.OnContextChanged -= HandleContextChanged;

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnCurrentPlayerChanged -= HandlePlayerChanged;

            UnsubscribeCombatEvents();
            UnsubscribeLifecycle();
        }

        private void Start()
        {
            GameplayEventBus.Instance?.Subscribe<MatchEndedEvent>(OnMatchEnded);
            TrySubscribeInputHandlers();

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
            UnsubscribeCombatEvents();
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
            TrySubscribeInputHandlers();

            if (localPlayer == null) return;

            _initializedForPlayer = true;
            _localPlayer          = localPlayer;
            _localNetObjId        = (int)localPlayer.ObjectId;

            InitializeCombatAudio(localPlayer);

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

            // ⑥ Global combat events (all players)
            SubscribeCombatEvents();

            // ⑦ Local player lifecycle — must come before WireInteractionPrompt because
            //    SubscribeLifecycle() calls UnsubscribeLifecycle() which resets _localInteractionSystem.
            SubscribeLifecycle(localPlayer);

            // Mobile input must bind after SubscribeLifecycle, which clears stale player refs.
            BindMobileInput(_mobileHUDPanel, localPlayer);

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

            if (_currentState != UIState.PostMatch)
                SetState(UIState.Combat);

            Debug.Log($"[GameHUDController] Initialized for player '{localPlayer.name}' (netId={_localNetObjId})");
        }

        // ── Player Switch (Spectate) ──────────────────────────────────────────

        private void HandlePlayerChanged(NetworkPlayer newPlayer)
        {
            if (newPlayer == null) return;
            if (_currentState == UIState.PostMatch) return;

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
            bool isSettings  = _currentState == UIState.Settings;
            bool isActivePly = (isCombat || isInventory) && !isSettings; // local player alive and in control

            // ── Combat layer ─────────────────────────────────────────────────
            SetActive(_weaponHUD,           isCombat && !isSettings);
            SetActive(_itemSelectionHUD,    isCombat && !isSettings);
            SetActive(_playerStatsHUD,      isActivePly);   // also visible in inventory
            SetActive(_interactionPromptUI,  isCombat && !isSettings);

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
            SetActive(_minimapUI, (isCombat || isSpectate) && !isSettings);

            // ── Inventory ────────────────────────────────────────────────────
            if (_inventoryRoot != null) _inventoryRoot.SetActive(isInventory && !isSettings);

            // ── Match layer ──────────────────────────────────────────────────
            bool matchVisible = !isPostMatch && !isSettings;
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
            SetActive(_spectatorHUD,  isSpectate && !isSettings);
            if (_resultsView != null)  _resultsView.gameObject.SetActive(isPostMatch && !isSettings);
            SetActive(_settingsView,   isSettings);
        }

        private static void SetActive(Component c, bool active)
        {
            if (c != null) c.gameObject.SetActive(active);
        }

        // ── Lifecycle event handlers ──────────────────────────────────────────

        private void OnLocalPlayerDied()
        {
            if (_currentState == UIState.PostMatch)
                return;

            CloseSettingsOverlay(false);
            SetState(UIState.Dead);
        }

        private void OnLocalPlayerRespawned()
        {
            if (_currentState == UIState.PostMatch)
                return;

            CloseSettingsOverlay(false);
            _deathScreen?.Hide();
            SetState(UIState.Combat);
        }

        private void OnMatchEnded(MatchEndedEvent evt)
        {
            CloseSettingsOverlay(false);
            _deathScreen?.Hide();
            SetState(UIState.PostMatch);
            SpectateManager.Instance?.StopSpectating();
        }

        private void OnSpectateStarted()
        {
            if (_currentState == UIState.PostMatch)
                return;

            CloseSettingsOverlay(false);
            // Hide any loot panel that the local player had open before dying.
            _lootContainerUI?.Hide();
            SetState(UIState.Spectating);
        }

        private void OnSpectateStopped()
        {
            if (_currentState == UIState.PostMatch)
                return;

            CloseSettingsOverlay(false);
            // HideSpectateView is a no-op if the panel is not in spectate mode,
            // so this is safe to call unconditionally.
            _lootContainerUI?.HideSpectateView();
            SetState(UIState.Combat);
        }

        // ── Inventory Toggle ──────────────────────────────────────────────────

        private void HandleOpenMenuPressed()
        {
            if (_currentState == UIState.PostMatch)
                return;

            if (_settingsOpen || _currentState == UIState.Settings)
                CloseSettingsOverlay(true);
            else
                OpenSettingsOverlay();
        }

        private void HandleSettingsCloseRequested()
        {
            CloseSettingsOverlay(true);
        }

        private void OpenSettingsOverlay()
        {
            if (_settingsView == null || _settingsOpen)
                return;

            TrySubscribeSettingsView();

            _settingsOpen = true;
            _lootContainerUI?.Hide();

            if (!_settingsView.gameObject.activeSelf)
                _settingsView.gameObject.SetActive(true);

            _ = _settingsView.OnShowAsync(new NavigationContext(PanelType.None, PanelType.Settings, false, true, "Gameplay"));
        }

        private void CloseSettingsOverlay(bool popContext)
        {
            if (_settingsView == null)
                return;

            if (!_settingsOpen && !_settingsView.gameObject.activeSelf)
                return;

            _settingsOpen = false;

            if (popContext)
                _ = _settingsView.OnHideAsync(new NavigationContext(PanelType.Settings, PanelType.None, false, true, "Gameplay"));

            if (_settingsView.gameObject.activeSelf)
                _settingsView.gameObject.SetActive(false);
        }

        private static UIState ResolveUiStateFromInputState(InputState state)
        {
            return state switch
            {
                InputState.InventoryOpen => UIState.Inventory,
                InputState.PlayerDead => UIState.Dead,
                InputState.Spectating => UIState.Spectating,
                _ => UIState.Combat
            };
        }

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

        private void TrySubscribeInputHandlers()
        {
            var input = InputManager.Instance;
            if (input == null)
                return;

            if (input.UIHandler != null && _subscribedUIHandler != input.UIHandler)
            {
                if (_subscribedUIHandler != null)
                    _subscribedUIHandler.OnOpenMenuPressed -= HandleOpenMenuPressed;

                _subscribedUIHandler = input.UIHandler;
                _subscribedUIHandler.OnOpenMenuPressed += HandleOpenMenuPressed;
            }

            TrySubscribeSettingsView();
            _subscribedInputManager = input;
        }

        private void UnsubscribeInputHandlers()
        {
            if (_subscribedUIHandler != null)
            {
                _subscribedUIHandler.OnOpenMenuPressed -= HandleOpenMenuPressed;
                _subscribedUIHandler = null;
            }

            if (_subscribedSettingsView != null)
            {
                _subscribedSettingsView.CloseRequested -= HandleSettingsCloseRequested;
                _subscribedSettingsView = null;
            }

            _subscribedInputManager = null;
        }

        private void HandleContextChanged(InputState oldState, InputState newState)
        {
            if (newState == InputState.Paused)
            {
                SetState(UIState.Settings);
                return;
            }

            if (oldState == InputState.Paused)
            {
                CloseSettingsOverlay(false);
                SetState(ResolveUiStateFromInputState(newState));

                if (newState == InputState.InventoryOpen)
                {
                    Debug.Log($"[INV] Restoring inventory after settings. _inventoryRoot={(_inventoryRoot != null ? _inventoryRoot.name : "NULL")} " +
                              $"_inventoryScreen={(_inventoryScreen != null ? "ok" : "NULL")} " +
                              $"_localInteractionSystem={(_localInteractionSystem != null ? "ok" : "NULL")}");
                    _localInteractionSystem?.HandleInventoryOpened();
                    StartInventoryProximityRefresh();
                }

                return;
            }

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

        private void TrySubscribeSettingsView()
        {
            if (_settingsView == null)
                return;

            if (_subscribedSettingsView == _settingsView)
                return;

            if (_subscribedSettingsView != null)
                _subscribedSettingsView.CloseRequested -= HandleSettingsCloseRequested;

            _subscribedSettingsView = _settingsView;
            _subscribedSettingsView.CloseRequested += HandleSettingsCloseRequested;
        }

        private void HandleAnyPlayerDied(string victimName, string killerName, string weaponId)
        {
            _killFeedUI?.AddKill(killerName, victimName, weaponId);
        }

        private void SubscribeCombatEvents()
        {
            if (_combatEventsSubscribed)
                return;

            PlayerHealthSystem.OnAnyPlayerDied  += HandleAnyPlayerDied;
            PlayerHealthSystem.OnAnyHitReceived += HandleAnyHitReceived;
            CombatFeedbackEvents.LocalHitConfirmed += HandleLocalHitConfirmed;
            BaseDeployable.OnAnyDeployableHit += HandleAnyDeployableHit;
            _combatEventsSubscribed = true;

            if (_damageFeedback == null)
                Debug.LogWarning("[DAMAGE][HUD] _damageFeedback is NULL — damage numbers, hit indicators, and hit effects will NOT show. Drag DamageFeedbackSystem into GameHUDController._damageFeedback in Inspector.");
            else
                Debug.Log($"[DAMAGE][HUD] Combat events subscribed. DamageFeedbackSystem='{_damageFeedback.name}' OK.");
        }

        private void UnsubscribeCombatEvents()
        {
            if (!_combatEventsSubscribed)
                return;

            PlayerHealthSystem.OnAnyPlayerDied  -= HandleAnyPlayerDied;
            PlayerHealthSystem.OnAnyHitReceived -= HandleAnyHitReceived;
            CombatFeedbackEvents.LocalHitConfirmed -= HandleLocalHitConfirmed;
            BaseDeployable.OnAnyDeployableHit -= HandleAnyDeployableHit;
            _combatEventsSubscribed = false;
        }

        private void HandleAnyHitReceived(DamageInfo info)
        {
            Debug.Log($"[DAMAGE][HUD] HitReceived: shooter={info.ShooterNetworkObjectId} localId={_localNetObjId} dmg={info.Damage:F1} weapon={info.WeaponId} feedbackNull={_damageFeedback == null}");
            if (_localNetObjId < 0 || _damageFeedback == null) return;

            // Don't show indicator for hits the local player caused (handled by HandleLocalHitConfirmed)
            if (info.ShooterNetworkObjectId == _localNetObjId)
                return;

            if (IsHitOnObservedPlayer(info))
                _damageFeedback.ShowHitIndicator(GetIncomingHitDirection(info));
        }

        /// <summary>
        /// Deployable hit broadcast from BaseDeployable.NotifyHitObserversRpc.
        /// Show hit indicator only when the observed player is nearby the hit deployable.
        /// Damage number / HitConfirm is already handled by HandleLocalHitConfirmed (TargetRpc).
        /// </summary>
        private void HandleAnyDeployableHit(DamageInfo info)
        {
            if (_localNetObjId < 0 || _damageFeedback == null) return;

            // Only show incoming-fire indicator if the local (or spectated) player is nearby.
            if (info.ShooterNetworkObjectId == _localNetObjId)
                return; // local shooter already gets feedback via HandleLocalHitConfirmed

            if (IsHitNearObservedPlayer(info.HitPoint, 36f)) // 6m radius
                _damageFeedback.ShowHitIndicator(GetIncomingHitDirection(info));
        }

        private void HandleLocalHitConfirmed(CombatHitFeedbackInfo feedback)
        {
            if (_damageFeedback == null) return;

            DamageInfo info = feedback.DamageInfo;
            _damageFeedback.ShowHitConfirm(info.IsHeadshot);
            _damageFeedback.ShowDamageNumber(info.HitPoint, info.Damage, info.IsHeadshot);
            _damageFeedback.ShowHitEffect(info.HitPoint, info.HitNormal);
        }

        private void InitializeCombatAudio(NetworkPlayer localPlayer)
        {
            CombatAudioController audioController = null;
            if (AudioManager.HasInstance)
                audioController = AudioManager.Instance.GetComponent<CombatAudioController>();

#if UNITY_2023_2_OR_NEWER
            audioController ??= FindFirstObjectByType<CombatAudioController>();
#else
            audioController ??= FindObjectOfType<CombatAudioController>();
#endif
            if (audioController == null)
                return;

            var statSystem = ComponentResolver
                .Find<IPlayerStatSystem>(localPlayer)
                .OnSelf()
                .InChildren()
                .Resolve();

            audioController.Initialize(statSystem, localPlayer.DisplayName, _localNetObjId);
        }

        private bool IsHitOnObservedPlayer(DamageInfo info)
        {
            var observed = SpectateManager.Instance?.GetCurrentPlayer() ?? _localPlayer;
            if (observed == null) return false;

            return Vector3.SqrMagnitude(observed.transform.position - info.HitPoint) <= 9f;
        }

        private bool IsHitNearObservedPlayer(Vector3 hitPoint, float sqrRadiusThreshold)
        {
            var observed = SpectateManager.Instance?.GetCurrentPlayer() ?? _localPlayer;
            if (observed == null) return false;
            return Vector3.SqrMagnitude(observed.transform.position - hitPoint) <= sqrRadiusThreshold;
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
                _mobileHUDPanel = panel;
                Debug.Log($"[GameHUDController] BindMobileInput panel={panel.name} player={localPlayer.name}");
                panel.Bind(InputManager.Instance);
                var statSys = ComponentResolver
                    .Find<NightHunt.Gameplay.StatSystem.Core.Interfaces.IPlayerStatSystem>(localPlayer)
                    .OnSelf().InChildren()
                    .Resolve();
                panel.BindPlayerContext(localPlayer.transform, statSys, localPlayer.GamePlaySystemBridge?.Weapon);
            }
            else
            {
                Debug.LogWarning("[GameHUDController] MobileHUDPanel not found under HUD; mobile action buttons cannot bind.");
            }

            // Wire CameraLockIndicator to the local player's MovementInputHandler.
            // GetComponentInChildren fallback ensures this works even if the field is not
            // assigned in the Inspector.
            if (_cameraLockIndicator == null)
                _cameraLockIndicator = GetComponentInChildren<CameraLockIndicator>(true);
            if (_cameraLockIndicator != null)
            {
                var movementHandler = InputManager.Instance?.MovementHandler;
                _cameraLockIndicator.Bind(movementHandler);
                Debug.Log($"[GameHUDController] CameraLockIndicator bound movement={(movementHandler != null ? "ok" : "null")}");
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
            if (_cameraLockIndicator == null)
                _cameraLockIndicator = GetComponentInChildren<CameraLockIndicator>(true);
            if (_settingsView == null)
                _settingsView = GetComponentInChildren<SettingsView>(true);
            if (_settingsView == null)
            {
                var settingsRoot = new GameObject("GameplaySettingsView", typeof(RectTransform));
                settingsRoot.transform.SetParent(transform, false);
                var rect = settingsRoot.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                _settingsView = settingsRoot.AddComponent<SettingsView>();
            }
            TrySubscribeSettingsView();
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
            _cameraLockIndicator?.Unbind();

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
            if (_settingsView != null)   _settingsView.gameObject.SetActive(false);
            _objectiveCaptureHUD?.SetHudVisible(false);
            _bossHealthHUD?.SetHudVisible(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_weaponHUD == null)              _weaponHUD              = GetComponentInChildren<WeaponHUDPanel>(true);
            if (_itemSelectionHUD == null)       _itemSelectionHUD       = GetComponentInChildren<ItemSelectionHUD>(true);
            if (_playerStatsHUD == null)         _playerStatsHUD         = GetComponentInChildren<PlayerStatsHUD>(true);
            if (_interactionPromptUI == null)    _interactionPromptUI    = GetComponentInChildren<InteractionPromptUI>(true);
            if (_minimapUI == null)              _minimapUI              = GetComponentInChildren<MinimapUI>(true);
            if (_inventoryScreen == null)        _inventoryScreen        = GetComponentInChildren<InventoryScreen>(true);
            if (_matchUI == null)                _matchUI                = GetComponentInChildren<MatchUI>(true);
            if (_killFeedUI == null)             _killFeedUI             = GetComponentInChildren<KillFeedUI>(true);
            if (_objectiveCaptureHUD == null)    _objectiveCaptureHUD    = GetComponentInChildren<ObjectiveCaptureHUD>(true);
            if (_bossHealthHUD == null)          _bossHealthHUD          = GetComponentInChildren<BossHealthHUD>(true);
            if (_deathScreen == null)            _deathScreen            = GetComponentInChildren<DeathScreen>(true);
            if (_spectatorHUD == null)           _spectatorHUD           = GetComponentInChildren<SpectatorHUD>(true);
            if (_resultsView == null)            _resultsView            = GetComponentInChildren<ResultsView>(true);
            if (_actionProgressPresenter == null) _actionProgressPresenter = GetComponentInChildren<ActionProgressPresenter>(true);
            if (_lootContainerUI == null)        _lootContainerUI        = GetComponentInChildren<LootContainerUI>(true);
            if (_mobileHUDPanel == null)         _mobileHUDPanel         = GetComponentInChildren<MobileHUDPanel>(true);
            if (_teamMemberPanel == null)        _teamMemberPanel        = GetComponentInChildren<TeamMemberPanel>(true);
            if (_cameraLockIndicator == null)    _cameraLockIndicator    = GetComponentInChildren<CameraLockIndicator>(true);
        }
#endif
    }
}
