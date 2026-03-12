using UnityEngine;
using NightHunt.GameplaySystems.UI;
using NightHunt.GameplaySystems.UI.Inventory;
using NightHunt.GameplaySystems.UI.Combat;
using NightHunt.GameplaySystems.UI.Interaction;
using NightHunt.Networking;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// GameHUD — thin orchestrator for all in-game UI panels.
    ///
    /// Responsibilities:
    ///   • Holds serialized references to every HUD sub-panel.
    ///   • Calls Initialize(localPlayer) distributing the player to sub-systems.
    ///   • Provides Show/Hide helpers usable from GameplayBootstrap or similar.
    ///
    /// All actual stat display logic lives in the individual sub-panels
    /// (PlayerHUDPanel, CombatHUDPanel, etc.) — this class stays dumb on purpose.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        // ── Core HUD sub-panels ───────────────────────────────────────────────

        [Header("Core HUD Panels")]
        [Tooltip("Stats panel: HP / Stamina / Armor / Speed / Weight sliders")]
        [SerializeField] private PlayerHUDPanel playerHUDPanel;

        [Tooltip("Combat panel: weapon slots, quick slots, ammo label")]
        [SerializeField] private CombatHUDPanel combatHUDPanel;

        [Tooltip("UIRootController owns InventoryScreen + PlayerHUDPanel bridge")]
        [SerializeField] private UIRootController uiRootController;

        // ── Contextual / overlay panels ───────────────────────────────────────

        [Header("Match / Score")]
        [SerializeField] private MatchUI matchUI;
        [SerializeField] private KillFeedUI killFeedUI;

        [Header("Crosshair")]
        [SerializeField] private CrosshairUI crosshairUI;

        [Header("Interaction")]
        [SerializeField] private InteractionPromptUI interactionPromptUI;

        [Header("Minimap")]
        [SerializeField] private MinimapUI minimapUI;

        [Header("Death Screen")]
        [SerializeField] private DeathScreen deathScreen;

        [Header("World / Loot")]
        [SerializeField] private LootContainerUI lootContainerUI;

        [Header("Feedback")]
        [SerializeField] private Gameplay.Feedback.DamageFeedbackSystem damageFeedback;

        // ── Public accessors (for external systems that need a sub-panel ref) ─

        public PlayerHUDPanel     PlayerHUDPanel    => playerHUDPanel;
        public CombatHUDPanel     CombatHUDPanel    => combatHUDPanel;
        public KillFeedUI         KillFeed          => killFeedUI;
        public CrosshairUI        Crosshair         => crosshairUI;
        public InteractionPromptUI InteractionPrompt => interactionPromptUI;
        public MinimapUI          Minimap           => minimapUI;
        public DeathScreen        DeathScreen       => deathScreen;
        public LootContainerUI    LootContainerUI   => lootContainerUI;
        // NetworkObject ID of the local player; used to filter shooter-side damage numbers.
        private int _localNetObjId = -1;
        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // ── Panel activation strategy ─────────────────────────────────────────
            // ALL panels must be active here so Awake()/GetComponentInChildren()
            // run on every child MonoBehaviour and references are wired up.
            // We then selectively hide panels that should NOT show at game start.
            // Do NOT SetActive(false) on item prefabs inside panels — only the PANEL root.
            SetupPanelsInitialState();

            // ── Cursor: always visible in top-down gameplay — cursor IS the aim indicator.
            // Never lock/hide; mobile has no cursor so this is a no-op on device.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        // ── Panel initial state ───────────────────────────────────────────────────
        private void SetupPanelsInitialState()
        {
            // These panels are OVERLAY / on-demand → start hidden
            if (deathScreen      != null) deathScreen.Hide();
            if (lootContainerUI  != null) lootContainerUI.Hide();
            // Combat + Player HUD stay active — they show gameplay info at all times
            // MatchUI / KillFeed / Crosshair / InteractionPrompt / Minimap start active
            // (they self-manage visibility via their own logic)
        }

        // ── Cursor helpers ─────────────────────────────────────────────────────────
        /// <summary>
        /// Show or hide cursor for UI overlay contexts (e.g. pause / loading).
        /// In this top-down game the cursor should stay visible at all times
        /// because it is the aim indicator.  Only hide when a full-screen non-
        /// gameplay overlay needs an exclusively locked state (rare).
        /// On mobile this is a no-op because there is no system cursor.
        /// </summary>
        public static void SetCursorForUI(bool uiOverlayActive)
        {
            // Top-down game: cursor is ALWAYS visible during gameplay.
            // UI overlays can also leave cursor visible — no state change needed.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        /// <summary>Legacy alias kept so existing call-sites compile.</summary>
        public static void LockCursor(bool locked)
        {
            // Repurposed: locking is no longer used in this top-down game.
            // Cursor stays visible regardless of the 'locked' argument.
            SetCursorForUI(!locked);
        }

        /// <summary>
        /// Call once the local NetworkPlayer is ready.
        /// Distributes the player reference to all sub-panels that need it.
        /// </summary>
        public void Initialize(NetworkPlayer localPlayer)
        {
            if (localPlayer == null)
            {
                Debug.LogWarning("[GameHUD] Initialize called with null localPlayer.");
                return;
            }

            _localNetObjId = (int)localPlayer.ObjectId;

            if (minimapUI != null)        minimapUI.SetLocalPlayer(localPlayer);
            if (deathScreen != null)      deathScreen.RegisterPlayer(localPlayer);
            if (lootContainerUI != null)  lootContainerUI.SetLocalPlayer(localPlayer);

            // Subscribe global combat events so UI updates for all players, not only the local one.
            PlayerHealthSystem.OnAnyPlayerDied  += HandleAnyPlayerDied;
            PlayerHealthSystem.OnAnyHitReceived += HandleAnyHitReceived;

            Debug.Log($"[GameHUD] Initialized for player ObjectId={_localNetObjId}");
        }

        private void OnDestroy()
        {
            PlayerHealthSystem.OnAnyPlayerDied  -= HandleAnyPlayerDied;
            PlayerHealthSystem.OnAnyHitReceived -= HandleAnyHitReceived;
        }

        // ── Combat event routing ───────────────────────────────────────────

        // Fires on all clients when any player is killed — route to kill feed.
        private void HandleAnyPlayerDied(string victimName, string killerName, string weaponId)
        {
            killFeedUI?.AddKill(killerName, victimName, weaponId);
        }

        // Fires on all clients when any player takes a hit.
        // Only show damage numbers when the LOCAL player is the shooter.
        private void HandleAnyHitReceived(DamageInfo info)
        {
            if (_localNetObjId < 0 || info.ShooterNetworkObjectId != _localNetObjId)
                return;

            damageFeedback?.ShowDamageNumber(info.HitPoint, info.Damage, info.IsHeadshot);
        }

        // ── Visibility helpers ────────────────────────────────────────────────

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void ShowDeathScreen(string killerName = "")
        {
            if (deathScreen != null) deathScreen.Show(killerName);
        }

        public void HideDeathScreen()
        {
            if (deathScreen != null) deathScreen.Hide();
        }

        // ── Debug helper ──────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (playerHUDPanel == null)  playerHUDPanel  = ComponentResolver.Find<PlayerHUDPanel>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] PlayerHUDPanel not found")
        .Resolve();
            if (combatHUDPanel == null)  combatHUDPanel  = ComponentResolver.Find<CombatHUDPanel>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] CombatHUDPanel not found")
        .Resolve();
            if (uiRootController == null) uiRootController = ComponentResolver.Find<UIRootController>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] UIRootController not found")
        .Resolve();
            if (matchUI == null)         matchUI         = ComponentResolver.Find<MatchUI>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] MatchUI not found")
        .Resolve();
            if (killFeedUI == null)      killFeedUI      = ComponentResolver.Find<KillFeedUI>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] KillFeedUI not found")
        .Resolve();
            if (crosshairUI == null)     crosshairUI     = ComponentResolver.Find<CrosshairUI>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] CrosshairUI not found")
        .Resolve();
            if (interactionPromptUI == null) interactionPromptUI = ComponentResolver.Find<InteractionPromptUI>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] InteractionPromptUI not found")
        .Resolve();
            if (minimapUI == null)       minimapUI       = ComponentResolver.Find<MinimapUI>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] MinimapUI not found")
        .Resolve();
            if (deathScreen == null)     deathScreen     = ComponentResolver.Find<DeathScreen>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] DeathScreen not found")
        .Resolve();
            if (lootContainerUI == null) lootContainerUI = ComponentResolver.Find<LootContainerUI>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] LootContainerUI not found")
        .Resolve();
            if (damageFeedback == null)  damageFeedback  = ComponentResolver.Find<Gameplay.Feedback.DamageFeedbackSystem>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] Gameplay.Feedback.DamageFeedbackSystem not found")
        .Resolve();
        }
#endif
    }
}

//         [SerializeField] private TextMeshProUGUI healthText;
//         [SerializeField] private Image healthBarFill;
//
//         [Header("Stamina Bar")]
//         [SerializeField] private Slider staminaBar;
//         [SerializeField] private TextMeshProUGUI staminaText;
//
//         [Header("Ammo Display")]
//         [SerializeField] private TextMeshProUGUI ammoText;
//         [SerializeField] private TextMeshProUGUI reserveAmmoText;
//         [SerializeField] private GameObject reloadIndicator;
//
//         [Header("Weapon Display")]
//         [SerializeField] private TextMeshProUGUI weaponNameText;
//         [SerializeField] private Image weaponIcon;
//
//         [Header("Weight Display")]
//         [SerializeField] private Slider weightBar;
//         [SerializeField] private TextMeshProUGUI weightText;
//         [SerializeField] private Color normalWeightColor = Color.green;
//         [SerializeField] private Color heavyWeightColor = Color.red;
//
//         [Header("Minimap")]
//         [SerializeField] private RectTransform minimapContainer;
//         [SerializeField] private Camera minimapCamera;
//         [SerializeField] private RawImage minimapImage;
//
//         [Header("Crosshair")]
//         [SerializeField] private Image crosshair;
//         [SerializeField] private float crosshairSpread = 10f;
//
//         private NetworkPlayer localPlayer;
//         //private PlayerStats _playerStats;
//         private CharacterCombat characterCombat;
//         private CharacterPredictedMovement _characterPredictedMovement;
//         
//         // Cooldown to avoid FindObjectsOfType every frame
//         private float lastPlayerSearchTime = 0f;
//         private const float playerSearchCooldown = 1f; // Search every 1 second if not found
//
//         private void Start()
//         {
//             // Find local player
//             FindLocalPlayer();
//         }
//
//         private void Update()
//         {
//             if (localPlayer == null)
//             {
//                 // Only search if cooldown expired
//                 if (Time.time - lastPlayerSearchTime >= playerSearchCooldown)
//                 {
//                     FindLocalPlayer();
//                     lastPlayerSearchTime = Time.time;
//                 }
//                 return;
//             }
//
//             UpdateHealthBar();
//             UpdateStaminaBar();
//             UpdateAmmoDisplay();
//             UpdateWeightDisplay();
//             UpdateCrosshair();
//         }
//
//         /// <summary>
//         /// Find local player
//         /// </summary>
//         private void FindLocalPlayer()
//         {
//             NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
//             foreach (var player in players)
//             {
//                 if (player.IsLocalPlayer)
//                 {
//                     localPlayer = player;
//                     //_playerStats = player.GetComponent<PlayerStats>();
//                     characterCombat = player.GetComponent<CharacterCombat>();
//                     _characterPredictedMovement = player.GetComponent<CharacterPredictedMovement>();
//                     break;
//                 }
//             }
//         }
//
//         /// <summary>
//         /// Update health bar
//         /// </summary>
//         private void UpdateHealthBar()
//         {
//             if (_playerStats == null || healthBar == null) return;
//
//             float currentHP = _playerStats.GetCurrentHealth();
//             float maxHP = _playerStats.GetCurrentStamina();
//             float hpPercentage = maxHP > 0 ? currentHP / maxHP : 0f;
//
//             healthBar.value = hpPercentage;
//
//             if (healthText != null)
//             {
//                 healthText.text = $"{Mathf.CeilToInt(currentHP)}/{Mathf.CeilToInt(maxHP)}";
//             }
//
//             // Color based on health
//             if (healthBarFill != null)
//             {
//                 if (hpPercentage > 0.6f)
//                     healthBarFill.color = Color.green;
//                 else if (hpPercentage > 0.3f)
//                     healthBarFill.color = Color.yellow;
//                 else
//                     healthBarFill.color = Color.red;
//             }
//         }
//
//         /// <summary>
//         /// Update stamina bar
//         /// </summary>
//         private void UpdateStaminaBar()
//         {
//             if (_playerStats == null || staminaBar == null) return;
//
//             float currentStamina = _playerStats.GetCurrentStamina();
//             float maxStamina = _playerStats.GetMaxStamina();
//             float staminaPercentage = maxStamina > 0 ? currentStamina / maxStamina : 0f;
//
//             staminaBar.value = staminaPercentage;
//
//             if (staminaText != null)
//             {
//                 staminaText.text = $"{Mathf.CeilToInt(currentStamina)}/{Mathf.CeilToInt(maxStamina)}";
//             }
//         }
//
//         /// <summary>
//         /// Update ammo display
//         /// </summary>
//         private void UpdateAmmoDisplay()
//         {
//             if (characterCombat == null) return;
//
//             int currentAmmo = characterCombat.GetCurrentAmmo();
//             int reserveAmmo = characterCombat.GetReserveAmmo();
//             bool isReloading = characterCombat.IsReloading();
//
//             if (ammoText != null)
//             {
//                 ammoText.text = currentAmmo.ToString();
//             }
//
//             if (reserveAmmoText != null)
//             {
//                 reserveAmmoText.text = reserveAmmo.ToString();
//             }
//
//             if (reloadIndicator != null)
//             {
//                 reloadIndicator.SetActive(isReloading);
//             }
//
//             // Update weapon name
//             var weapon = characterCombat.GetCurrentWeapon();
//             if (weapon != null && weaponNameText != null)
//             {
//                 weaponNameText.text = weapon.DisplayName;
//             }
//         }
//
//         /// <summary>
//         /// Update weight display
//         /// </summary>
//         private void UpdateWeightDisplay()
//         {
//             // // if (inventorySystem == null || weightBar == null) return;
//             // //
//             // // float currentWeight = inventorySystem.GetCurrentWeight();
//             // // float maxWeight = inventorySystem.GetWeightCapacity();
//             // float weightPercentage = maxWeight > 0 ? currentWeight / maxWeight : 0f;
//             //
//             // weightBar.value = weightPercentage;
//             //
//             // if (weightText != null)
//             // {
//             //     weightText.text = $"{currentWeight:F1}/{maxWeight:F1} kg";
//             // }
//             //
//             // // Color based on weight
//             // if (weightBar != null)
//             // {
//             //     var fillImage = weightBar.fillRect?.GetComponent<Image>();
//             //     if (fillImage != null)
//             //     {
//             //         if (weightPercentage < 0.8f)
//             //             fillImage.color = normalWeightColor;
//             //         else
//             //             fillImage.color = Color.Lerp(normalWeightColor, heavyWeightColor, (weightPercentage - 0.8f) / 0.2f);
//             //     }
//             // }
//         }
//
//         /// <summary>
//         /// Update crosshair
//         /// </summary>
//         private void UpdateCrosshair()
//         {
//             if (crosshair == null) return;
//
//             // Update crosshair spread based on weapon spread
//             if (characterCombat != null)
//             {
//                 // Would need to get spread from combat system
//                 // For now, just show crosshair
//             }
//         }
//
//         /// <summary>
//         /// Show/Hide HUD
//         /// </summary>
//         public void SetVisible(bool visible)
//         {
//             gameObject.SetActive(visible);
//         }
//     }
// }
//
