using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Spectator;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.Zone;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.Scoring;
using NightHunt.GameplaySystems.ItemUse;
using NightHunt.Gameplay.Core.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightHunt.Gameplay.Core
{
    /// <summary>
    /// GameplayTestOrchestrator — DEBUG ONLY MonoBehaviour for TestScene_Full.
    ///
    /// Purpose:
    ///   • Single-object runtime debug overlay (ONGUI) showing system status
    ///   • Scene gizmos (Gizmos.Draw*) for all gameplay zones, boss, spawns, loot
    ///   • Keyboard shortcuts for triggering gameplay events during testing
    ///   • System connectivity check — quickly surfaces missing references
    ///
    /// Add to scene: Create empty GO "[ TestOrchestrator ]" → add this component.
    /// Toggle UI: F1 = full panel, F2 = compact, F3 = off
    ///
    /// KEYBOARD SHORTCUTS (Editor/Dev builds only):
    ///   G  — Give all test items to local player
    ///   K  — Kill local player
    ///   R  — Force respawn
    ///   T  — Toggle spectate on next player
    ///   B  — Spawn Boss immediately
    ///   Z  — Activate Lockdown Zone
    ///   P  — Advance to next Match Phase
    ///   [  — Decrease local player health by 25
    ///   ]  — Increase local player health by 25
    ///   1/2/3 — Switch weapon slot (simulates UI button press)
    ///   Q/E/F/X — Use quickslot 0/1/2/3
    ///   N  — Simulate boss spawn notification (debug)
    /// </summary>
    [DisallowMultipleComponent]
    public class GameplayTestOrchestrator : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("Display")]
        [SerializeField] private bool _showOnGUI      = false;
        [SerializeField] private bool _showGizmos     = true;
#pragma warning disable CS0414
        [SerializeField] private bool _compactMode    = false;
#pragma warning restore CS0414
        [SerializeField] private KeyCode _toggleKey   = KeyCode.F1;
        [SerializeField] private KeyCode _compactKey  = KeyCode.F2;

        [Header("References (auto-found if null)")]
        [SerializeField] private Gameplay.Zone.SafeZoneManager    _safeZoneManager;
        [SerializeField] private Gameplay.Scoring.ScoringSystem   _scoringSystem;
        [SerializeField] private Gameplay.Boss.BossController     _bossController;
        [SerializeField] private Gameplay.Boss.BossSpawnManager   _bossSpawnManager;

        // ── Internal state ────────────────────────────────────────────────────────
        private int    _displayMode   = 0; // 0=off, 1=full, 2=compact
        private float  _fps;
        private float  _fpsTimer;
        private int    _frameCount;
        private string _systemReport;
        private float  _reportTimer;

        private readonly GUIStyle _headerStyle   = new GUIStyle();
        private readonly GUIStyle _normalStyle   = new GUIStyle();
        private readonly GUIStyle _warnStyle     = new GUIStyle();
        private readonly GUIStyle _errorStyle    = new GUIStyle();
        private bool   _stylesInit;

        // Colors
        private static readonly Color GoodColor  = new Color(0.4f, 1f, 0.4f);
        private static readonly Color WarnColor  = new Color(1f, 0.85f, 0.3f);
        private static readonly Color ErrorColor = new Color(1f, 0.3f, 0.3f);
        private static readonly Color InfoColor  = new Color(0.7f, 0.9f, 1f);

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            AutoFindReferences();
        }

        private void AutoFindReferences()
        {
            if (_safeZoneManager   == null) _safeZoneManager   = Gameplay.Zone.SafeZoneManager.Instance;
            if (_scoringSystem   == null) _scoringSystem   = FindFirstObjectByType<Gameplay.Scoring.ScoringSystem>();
            if (_bossController  == null) _bossController  = FindFirstObjectByType<Gameplay.Boss.BossController>();
            if (_bossSpawnManager== null) _bossSpawnManager= FindFirstObjectByType<Gameplay.Boss.BossSpawnManager>();
        }

        private void Update()
        {
            // Toggle display mode
            if (UnityEngine.Input.GetKeyDown(_toggleKey))
                _displayMode = _displayMode == 1 ? 0 : 1;
            if (UnityEngine.Input.GetKeyDown(_compactKey))
                _displayMode = _displayMode == 2 ? 1 : 2;
            if (UnityEngine.Input.GetKeyDown(KeyCode.F3))
                _displayMode = 0;

            // FPS
            _frameCount++;
            _fpsTimer += Time.deltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer   = 0f;
            }

            // System report (refreshed every 5s)
            _reportTimer += Time.deltaTime;
            if (_reportTimer >= 5f || string.IsNullOrEmpty(_systemReport))
            {
                _reportTimer  = 0f;
                _systemReport = BuildSystemReport();
            }

            // Test keyboard shortcuts
            HandleTestShortcuts();
        }

        // ── ONGUI ─────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_displayMode == 0 || !_showOnGUI) return;

            InitStyles();

            if (_displayMode == 2)
            {
                DrawCompact();
                return;
            }

            DrawFull();
        }

        private void DrawFull()
        {
            float x = 10f, y = 10f, w = 390f;

            // ── Title bar ──────────────────────────────────────────────────────
            y = Box(x, y, w, 24f,
                $"<b>NightHunt Debug</b>  FPS: {_fps:F0}  [F1=toggle F2=compact F3=off]",
                _headerStyle);

            // ── Platform / Input mode ──────────────────────────────────────────
            bool isMobile = Application.isMobilePlatform || UnityEngine.Device.SystemInfo.deviceType == DeviceType.Handheld;
            string platform = isMobile ? "MOBILE (joystick active)" : "PC (WASD + mouse)";
            string mobileNote = isMobile
                ? "Joystick drives movement | FireButton syncs SimulateFire()"
                : "WASD via InputSystem | Joystick UI = decorative (zero on PC) | LMB/Space fires";
            y = Box(x, y, w, 36f,
                $"<b>Platform:</b> {platform}\n{mobileNote}",
                _normalStyle);

            // ── Input layer state ──────────────────────────────────────────────
            if (InputLayerManager.Instance != null)
            {
                string state = InputLayerManager.Instance.CurrentState.ToString();
                string layer = InputLayerManager.Instance.ActiveLayers.ToString();
                y = Box(x, y, w, 24f, $"<b>InputLayer</b>  State:{state}  Layers:{layer}", _normalStyle);
            }
            else
                y = Box(x, y, w, 24f, "<b>InputLayer</b>  ⚠ InputLayerManager NOT FOUND", _errorStyle);

            // ── Local player ───────────────────────────────────────────────────
            DrawLocalPlayerStats(x, ref y, w);

            // ── Combat / Weapon state ──────────────────────────────────────────
            DrawCombatState(x, ref y, w);

            // ── QuickSlot state ────────────────────────────────────────────────
            DrawQuickSlotState(x, ref y, w);

            // ── Match phase + Score ────────────────────────────────────────────
            DrawMatchState(x, ref y, w);

            // ── Boss state ────────────────────────────────────────────────────
            DrawBossState(x, ref y, w);

            // ── Zone system ───────────────────────────────────────────────────
            DrawZones(x, ref y, w);

            // ── System connectivity ───────────────────────────────────────────
            if (!string.IsNullOrEmpty(_systemReport))
            {
                int lineCount = _systemReport.Split('\n').Length + 1;
                y = Box(x, y, w, lineCount * 16f + 8f, _systemReport, _normalStyle);
            }

            // ── Shortcut guide ────────────────────────────────────────────────
            y = Box(x, y, w, 88f,
                "<b>Shortcuts:</b> G=GiveItems  K=KillMe  R=Respawn  T=Spectate\n" +
                "B=SpawnBoss  Z=ActivateZone  P=NextPhase  [=−25HP  ]=+25HP\n" +
                "1/2/3=WeaponSlot  Q/E/F/X=QuickSlot0/1/2/3  N=BossAlert",
                _normalStyle);
        }

        private void DrawCompact()
        {
            float x = 10f, y = 10f, w = 250f;
            Box(x, y, w, 24f,
                $"NightHunt  FPS:{_fps:F0}  Zone:{Gameplay.Zone.SafeZoneManager.Instance?.ZoneIndex}  [F1=full]",
                _normalStyle);
        }

        private void DrawLocalPlayerStats(float x, ref float y, float w)
        {
            var local = SpectateManager.Instance?.GetCurrentPlayer();
            if (local == null)
            {
                y = Box(x, y, w, 24f, "<b>Player</b>  ⚠ No local player found", _warnStyle);
                return;
            }

            var stats = local.GetComponentInChildren<IPlayerStatSystem>();
            if (stats == null)
            {
                y = Box(x, y, w, 24f, "<b>Player</b>  ⚠ IPlayerStatSystem missing", _errorStyle);
                return;
            }

            float hp    = stats.GetStat(PlayerStatType.Health);
            float maxHp = stats.GetStat(PlayerStatType.MaxHealth);
            float armor = stats.GetStat(PlayerStatType.Armor);
            float speed = stats.GetStat(PlayerStatType.MovementSpeed);
            float vis   = stats.GetStat(PlayerStatType.VisionRange);
            float wgt   = stats.GetCurrentWeight();
            float cap   = stats.GetWeightCapacity();

            string hpBar = ProgressBar(hp / Mathf.Max(1, maxHp), 20);

            y = Box(x, y, w, 80f,
                $"<b>Player: {local.DisplayName}</b>\n" +
                $"HP: {hp:F0}/{maxHp:F0} {hpBar}  Armor:{armor:F0}\n" +
                $"Speed:{speed:F1}  Vision:{vis:F1}  Weight:{wgt:F1}/{cap:F1}",
                hp < maxHp * 0.3f ? _warnStyle : _normalStyle);
        }

        private void DrawBossState(float x, ref float y, float w)
        {
            // Refresh if previously null (boss may have spawned after orchestrator init)
            if (_bossController == null)
                _bossController = FindFirstObjectByType<Gameplay.Boss.BossController>();

            if (_bossController == null)
            {
                y = Box(x, y, w, 24f, "<b>Boss</b>  Not spawned (B=spawn)", _normalStyle);
                return;
            }

            float hp    = _bossController.CurrentHp;
            float maxHp = _bossController.MaxHp;
            float hpPct = maxHp > 0f ? hp / maxHp : 0f;
            string hpBar = ProgressBar(hpPct, 20);
            string state = _bossController.IsDead ? "<color=#f66>DEAD</color>" : _bossController.CurrentHp > 0
                ? $"<color=#f90>ALIVE</color>"
                : "<color=#888>?</color>";

            // Aggro radius gizmo hint
            Vector3 bossPos = _bossController.transform.position;
            var local = SpectateManager.Instance?.GetCurrentPlayer();
            float distToBoss = local != null ? Vector3.Distance(local.transform.position, bossPos) : -1f;
            string distStr = distToBoss >= 0f ? $"  dist:{distToBoss:F1}m" : "";

            y = Box(x, y, w, 64f,
                $"<b>Boss [{_bossController.BossId}]</b>  {state}{distStr}\n" +
                $"HP: {hp:F0}/{maxHp:F0}  {hpBar}\n" +
                $"Pos: ({bossPos.x:F1}, {bossPos.z:F1})\n" +
                $"Ranges: aggro=<color=#ff0>{_bossController.AggroRadius:F0}m</color>  attack=<color=#f66>{_bossController.AttackRadius:F0}m</color>  dist={distToBoss:F1}m",
                hpPct < 0.25f ? _warnStyle : _normalStyle);
        }

        private void DrawZones(float x, ref float y, float w)
        {
            var safeZone = Gameplay.Zone.SafeZoneManager.Instance;
            if (safeZone == null)
            {
                y = Box(x, y, w, 24f, "<b>SafeZone</b>  ⚠ SafeZoneManager NOT FOUND", _errorStyle);
                return;
            }

            string state = safeZone.MatchActive
                ? $"Zone {safeZone.ZoneIndex + 1}{(safeZone.IsInFinalZone ? " <color=#f88>(FINAL)</color>" : "")}  r={safeZone.CurrentRadius:F0}m  {(safeZone.IsShrinking ? "<color=#f84>SHRINKING</color>" : "waiting")}"
                : "<color=#aaa>Waiting for match...</color>";

            y = Box(x, y, w, 24f, $"<b>SafeZone</b>  {state}", _normalStyle);
        }

        // ── Match state ───────────────────────────────────────────────────────────

        private void DrawMatchState(float x, ref float y, float w)
        {
            var mgr = _safeZoneManager ?? Gameplay.Zone.SafeZoneManager.Instance;
            if (mgr == null)
            {
                y = Box(x, y, w, 24f, "<b>Match</b>  ⚠ SafeZoneManager NOT FOUND", _errorStyle);
                return;
            }

            string ph  = mgr.MatchActive ? $"Zone {mgr.ZoneIndex + 1}{(mgr.IsInFinalZone ? " (FINAL)" : "")}" : "Waiting";
            float  rem = mgr.IsInFinalZone ? 0f : 0f; // countdown handled by HUDProxy

            string scoreStr = "";
            if (_scoringSystem != null)
            {
                int t0 = _scoringSystem.GetTeamScore(0);
                int t1 = _scoringSystem.GetTeamScore(1);
                scoreStr = $"  |  Team0: <b>{t0}</b>  Team1: <b>{t1}</b>";
            }

            y = Box(x, y, w, 40f,
                $"<b>Zone</b>: {ph}  Radius: {mgr.CurrentRadius:F0}m  Shrinking: {mgr.IsShrinking}\n" +
                $"Score{scoreStr}",
                _normalStyle);
        }

        // ── Combat / Weapon state ─────────────────────────────────────────────────

        private void DrawCombatState(float x, ref float y, float w)
        {
            var local = SpectateManager.Instance?.GetCurrentPlayer();
            if (local == null) return;

            var combat = local.GetComponentInChildren<CombatInputHandler>();
            if (combat == null)
            {
                y = Box(x, y, w, 24f, "<b>Combat</b>  ⚠ CombatInputHandler not found on player", _warnStyle);
                return;
            }

            string fireStr   = combat.IsFiring()    ? "<color=#ff4444>FIRING</color>"   : "idle";
            string aimStr    = combat.IsAiming()    ? "<color=#ffcc00>AIMING</color>"   : "hip";
            string reloadStr = combat.IsReloading() ? "<color=#88ccff>RELOAD</color>"  : "";
            int    slot      = combat.GetCurrentWeaponSlot();
            string slots     = $"Slot<b>{slot + 1}</b>";
            string enabledStr = combat.IsInputEnabled ? "" : " <color=#f66>[INPUT DISABLED]</color>";

            y = Box(x, y, w, 40f,
                $"<b>Combat</b>  {fireStr}  {aimStr}  {reloadStr}  {slots}{enabledStr}\n" +
                "<color=#aaa>1/2/3=switch slot  Fire: LMB/Space + FireButton (synced via SimulateFire)</color>",
                _normalStyle);
        }

        // ── QuickSlot / Item selection state ──────────────────────────────────────

        private void DrawQuickSlotState(float x, ref float y, float w)
        {
            var local = SpectateManager.Instance?.GetCurrentPlayer();
            if (local == null) return;

            var itemSel = local.GetComponentInChildren<IItemSelectionSystem>();
            var itemUse = local.GetComponentInChildren<ItemUseSystem>();

            if (itemSel == null)
            {
                y = Box(x, y, w, 24f, "<b>QuickSlot</b>  ⚠ IItemSelectionSystem not found", _warnStyle);
                return;
            }

            string selName  = itemSel.HasSelection ? $"<b>{itemSel.SelectedItem?.DefinitionID ?? "?"}" + "</b>" : "<color=#888>none</color>";
            bool   inUse    = itemUse != null && itemUse.IsUsingItem;
            string useState = inUse ? $"  <color=#88ccff>USING {itemUse.CurrentItem?.DefinitionID ?? "?"}" + "</color>" : "";

            y = Box(x, y, w, 40f,
                $"<b>QuickSlot</b>  Selected: {selName}{useState}\n" +
                "<color=#aaa>Q/E/F/X=use slot 0/1/2/3 (item selection is single-select, filter via HUD arrows)</color>",
                _normalStyle);
        }

        // ── System Report ─────────────────────────────────────────────────────────

        private string BuildSystemReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>System Connectivity Check</b>");

            Check(sb, "InputLayerManager", InputLayerManager.Instance != null);
            Check(sb, "SpectateManager",   SpectateManager.Instance   != null);
            Check(sb, "SafeZoneManager",   Gameplay.Zone.SafeZoneManager.Instance != null);
            Check(sb, "ScoringSystem",     _scoringSystem             != null);

            var registry = PlayerPublicRegistry.Instance;
            if (registry != null)
            {
                int playerCount = registry.GetAllPlayers()?.Length ?? 0;
                sb.AppendLine($"  Players online: {playerCount}");
            }
            else
            {
                sb.AppendLine("  ⚠ PlayerPublicRegistry: NOT FOUND");
            }

            return sb.ToString();
        }

        private static void Check(StringBuilder sb, string label, bool ok)
        {
            sb.AppendLine($"  {(ok ? "✓" : "✗")} {label}");
        }

        // ── Keyboard shortcuts ────────────────────────────────────────────────────

        private void HandleTestShortcuts()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;

            var local = SpectateManager.Instance?.GetCurrentPlayer();

            if (UnityEngine.Input.GetKeyDown(KeyCode.K) && local != null)
            {
                var health = local.GetComponentInChildren<Character.Combat.PlayerHealthSystem>();
                health?.ApplyDamageServer(new Character.Combat.DamageInfo
                {
                    Damage = 9999f, WeaponId = "debug_kill", ShooterNetworkObjectId = -1,
                    IsHeadshot = false, HitPoint = local.transform.position, HitNormal = Vector3.up
                });
                Debug.Log("[TestOrchestrator] K pressed: Kill local player");
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftBracket) && local != null)
            {
                var stats = local.GetComponentInChildren<IPlayerStatSystem>();
                if (stats != null)
                {
                    float current = stats.GetStat(PlayerStatType.Health);
                    stats.SetCurrentStat(PlayerStatType.Health, current - 25f);
                }
                Debug.Log("[TestOrchestrator] [ pressed: -25 HP");
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.RightBracket) && local != null)
            {
                var stats = local.GetComponentInChildren<IPlayerStatSystem>();
                if (stats != null)
                {
                    float current = stats.GetStat(PlayerStatType.Health);
                    stats.SetCurrentStat(PlayerStatType.Health, current + 25f);
                }
                Debug.Log("[TestOrchestrator] ] pressed: +25 HP");
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.P) && Gameplay.Zone.SafeZoneManager.Instance != null && Gameplay.Zone.SafeZoneManager.Instance.IsServerStarted)
            {
                // Advance zone manually for testing
                Debug.Log("[TestOrchestrator] P pressed: zone advance not directly available — use server console.");
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.B) && _bossSpawnManager != null)
            {
                Debug.Log("[TestOrchestrator] B pressed: SpawnBoss (call SpawnBoss method if exposed)");
                // BossSpawnManager.SpawnBoss() — call if the method is public
                var mi = typeof(Boss.BossSpawnManager).GetMethod("SpawnBoss",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                mi?.Invoke(_bossSpawnManager, null);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.T) && SpectateManager.Instance != null)
            {
                SpectateManager.Instance.SwitchSpectatedPlayer(true);
                Debug.Log("[TestOrchestrator] T pressed: switch spectate target");
            }

            // ── Weapon slot 1 / 2 / 3 ────────────────────────────────────────────
            if (local != null)
            {
                var combat = local.GetComponentInChildren<CombatInputHandler>();
                if (combat != null)
                {
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) { combat.SwitchToWeaponSlot(0); Debug.Log("[TestOrchestrator] 1 → weapon slot 0"); }
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) { combat.SwitchToWeaponSlot(1); Debug.Log("[TestOrchestrator] 2 → weapon slot 1"); }
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) { combat.SwitchToWeaponSlot(2); Debug.Log("[TestOrchestrator] 3 → weapon slot 2"); }
                }
            }

            // ── QuickSlot Q / E / F / X — trigger UseSelectedItem ────────────────
            // Item selection is single-select; Q/E/F/X trigger use on whatever
            // item is currently selected via the HUD item-filter arrows.
            if (local != null)
            {
                var itemSel = local.GetComponentInChildren<IItemSelectionSystem>();
                if (itemSel != null && itemSel.HasSelection)
                {
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Q)) { itemSel.RequestUseSelectedItem(); Debug.Log("[TestOrchestrator] Q → RequestUseSelectedItem"); }
                    if (UnityEngine.Input.GetKeyDown(KeyCode.E)) { itemSel.RequestUseSelectedItem(); Debug.Log("[TestOrchestrator] E → RequestUseSelectedItem"); }
                    if (UnityEngine.Input.GetKeyDown(KeyCode.F)) { itemSel.RequestUseSelectedItem(); Debug.Log("[TestOrchestrator] F → RequestUseSelectedItem"); }
                    if (UnityEngine.Input.GetKeyDown(KeyCode.X)) { itemSel.RequestCancelSelection(); Debug.Log("[TestOrchestrator] X → RequestCancelSelection"); }
                }
            }

            // ── N — simulate boss spawn notification ──────────────────────────────
            if (UnityEngine.Input.GetKeyDown(KeyCode.N))
            {
                Debug.Log("[TestOrchestrator] N pressed: publishing debug BossSpawnedEvent");
                GameplayEventBus.Instance?.Publish(new BossSpawnedEvent
                {
                    BossId   = "debug_boss",
                    Position = Vector3.zero
                });
            }
        }

        // ── Gizmos ────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;
            DrawZoneGizmos();
            DrawSpawnGizmos();
            DrawLootGizmos();
            DrawBossGizmos();
        }

        private void DrawZoneGizmos()
        {
            // SafeZone gizmo
            var safeZone = Gameplay.Zone.SafeZoneManager.Instance;
            if (safeZone != null && Application.isPlaying)
            {
                Vector3 center = safeZone.CurrentCenter;
                float   radius = safeZone.CurrentRadius;

                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
                Gizmos.DrawSphere(center, radius);
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
                Gizmos.DrawWireSphere(center, radius);
                Handles.color = Color.red;
                Handles.Label(center + Vector3.up * (radius + 1f), $"SafeZone z={safeZone.ZoneIndex} r={radius:F0}");
            }


        }

        private void DrawSpawnGizmos()
        {
            var spawns = FindObjectsByType<Spawn.SpawnPoint>(FindObjectsSortMode.None);
            foreach (var sp in spawns)
            {
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.8f);
                Gizmos.DrawWireSphere(sp.transform.position, 1f);
                Gizmos.DrawLine(sp.transform.position, sp.transform.position + Vector3.up * 2f);
                Handles.Label(sp.transform.position + Vector3.up * 2.2f, sp.name);
            }
        }

        private void DrawLootGizmos()
        {
            var lootPoints = FindObjectsByType<GameplaySystems.World.WorldItemSpawnPoint>(
                FindObjectsSortMode.None);
            foreach (var lp in lootPoints)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(lp.transform.position, Vector3.one * 0.6f);
                Handles.Label(lp.transform.position + Vector3.up * 0.8f, "LOOT");
            }
        }

        private void DrawBossGizmos()
        {
            var bossSpawnPt = FindFirstObjectByType<Boss.BossSpawnPoint>();
            if (bossSpawnPt != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                Gizmos.DrawWireSphere(bossSpawnPt.transform.position, 3f);
                Handles.Label(bossSpawnPt.transform.position + Vector3.up * 4f, "BOSS SPAWN");
            }

            if (_bossController != null)
            {
                // Aggro radius (yellow)
                Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
                Gizmos.DrawWireSphere(_bossController.transform.position, _bossController.AggroRadius);
                Handles.Label(_bossController.transform.position + Vector3.up * (_bossController.AggroRadius + 1f), $"Aggro {_bossController.AggroRadius:F0}m");
                // Attack radius (red)
                Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
                Gizmos.DrawWireSphere(_bossController.transform.position, _bossController.AttackRadius);
                Handles.Label(_bossController.transform.position + Vector3.up * (_bossController.AttackRadius + 1f), $"Attack {_bossController.AttackRadius:F0}m");
            }
        }
#endif

        // ── ONGUI helpers ─────────────────────────────────────────────────────────

        private void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _headerStyle.fontSize  = 12;
            _headerStyle.richText  = true;
            _headerStyle.normal.textColor = InfoColor;

            _normalStyle.fontSize  = 11;
            _normalStyle.richText  = true;
            _normalStyle.normal.textColor = Color.white;

            _warnStyle.fontSize    = 11;
            _warnStyle.richText    = true;
            _warnStyle.normal.textColor = WarnColor;

            _errorStyle.fontSize   = 11;
            _errorStyle.richText   = true;
            _errorStyle.normal.textColor = ErrorColor;
        }

        private float Box(float x, float y, float w, float h, string text, GUIStyle style)
        {
            var bgRect = new Rect(x - 2, y - 2, w + 4, h + 4);
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 4, y + 2, w - 6, h), text, style);
            return y + h + 4f;
        }

        private static string ProgressBar(float t, int width)
        {
            t = Mathf.Clamp01(t);
            int filled = Mathf.RoundToInt(t * width);
            return "[" + new string('█', filled) + new string('░', width - filled) + "]";
        }
    }
}
