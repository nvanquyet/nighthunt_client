#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using FishNet.Managing;
using FishNet.Object;

// ── Core ──────────────────────────────────────────────────────────────────────
using NightHunt.Gameplay.Core.State;

// ── Gameplay ──────────────────────────────────────────────────────────────────
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Respawn;
using NightHunt.Gameplay.Beacon;
using NightHunt.Gameplay.Boss;
using NightHunt.Gameplay.Spawn;
using NightHunt.Gameplay.Team;
using NightHunt.Gameplay.Scoring;
using NightHunt.Gameplay.Objective;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Character.Movement;
using NightHunt.Gameplay.Feedback;
using NightHunt.Gameplay.Spectator;
using NightHunt.Gameplay.ClientEffects;
using NightHunt.Gameplay.Camera;
using NightHunt.Gameplay.Camera.Spectator;

// ── Input ─────────────────────────────────────────────────────────────────────
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.Input.Handlers.Camera;
using NightHunt.Gameplay.Input.Handlers.Inventory;
using NightHunt.Gameplay.Input.Handlers.UI;
using NightHunt.Gameplay.Input.Handlers.Interaction;
using NightHunt.Gameplay.Input.Handlers.Spectator;

// ── GameplaySystems ───────────────────────────────────────────────────────────
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Loot;
using NightHunt.GameplaySystems.UI;
using NightHunt.GameplaySystems.UI.Inventory;
using NightHunt.GameplaySystems.UI.Combat;
using NightHunt.GameplaySystems.UI.Interaction;
using NightHunt.GameplaySystems.World;

// ── Networking ────────────────────────────────────────────────────────────────
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Networking.ClientOnly;

// ── Player systems ───────────────────────────────────────────────────────────
using NightHunt.StatSystem.Systems;
using NightHunt.StatSystem.Configs;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Equipment;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.GameplaySystems.QuickSlot;
using NightHunt.GameplaySystems.Attachment;
using NightHunt.GameplaySystems.Interaction;
using NightHunt.GameplaySystems.Stat;
using NightHunt.GameplaySystems.Aim;
using NightHunt.Gameplay.Player;
using NightHunt.Gameplay.FogOfWar;

// ── UI ────────────────────────────────────────────────────────────────────────
using NightHunt.UI;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// NightHunt — Gameplay Scene Setup Tool
    /// Menu: NightHunt ▶ Scene Setup Tool
    ///
    /// ① ScriptableObject Configs  — pre-filled from JSON
    /// ② Scene GameObjects         — server + client managers, all refs wired
    /// ③ UI Canvas Hierarchy       — full GameHUD hierarchy, all panels wired
    /// ④ Prefabs                   — stub NetworkObject prefabs
    /// ⑤ Player Prefab Systems     — all player components wired on a target prefab
    ///
    /// Generated assets → Assets/_NightHunt_Generated/  (safe to delete)
    /// Scene objects are NOT deleted by the cleanup button.
    ///
    /// NOTE: Console log lines starting with "→ assign" need a real
    ///       prefab / component / SO assigned in the Inspector manually.
    /// </summary>
    public class GameplaySceneSetupTool : EditorWindow
    {
        // ── Paths ─────────────────────────────────────────────────────────────

        private const string k_Root    = "Assets/_NightHunt_Generated";
        private const string k_Configs = "Assets/_NightHunt_Generated/Configs";
        private const string k_Prefabs = "Assets/_NightHunt_Generated/Prefabs";
        private const string k_JsonRes = "Data/NightHunt_Full_GameDesign_Config_v3";

        // ── Window state ──────────────────────────────────────────────────────

        // ── Player movement mode ───────────────────────────────────────────
        private enum PlayerMovementMode { CharacterController, Rigidbody }

        private Vector2           _scroll;
        private bool              _foldCfg    = true;
        private bool              _foldScene  = true;
        private bool              _foldUI     = true;
        private bool              _foldPfb    = true;
        private bool              _foldPlayer = true;
        private GUIStyle          _headerStyle;
        private GUIStyle          _warnStyle;
        private bool              _stylesInit;

        // ── Player section state ───────────────────────────────────────────
        private GameObject        _playerTarget;
        private PlayerMovementMode _playerMovementMode = PlayerMovementMode.CharacterController;
        private bool              _playerDebugLogs    = false;

        [MenuItem("NightHunt/Scene Setup Tool")]
        public static void Open() =>
            GetWindow<GameplaySceneSetupTool>("NH Scene Setup").minSize = new Vector2(460, 620);

        // ── GUI ───────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            InitStyles();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("NightHunt — Gameplay Scene Setup", _headerStyle);
            EditorGUILayout.LabelField($"Output: {k_Root}", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);

            Section(ref _foldCfg,
                "① ScriptableObject Configs",
                "MovementSettings · GameplayConfig · InventoryConfig\n" +
                "LootableConfig · InteractableConfig\n" +
                "Pre-filled from NightHunt_Full_GameDesign_Config_v3.json.",
                "Create Configs", RunConfigs);

            Section(ref _foldScene,
                "② Scene GameObjects",
                "[NetworkCore]  NetworkGameManager · RegistryService\n" +
                "[ServerManager]  ServerGameManager (NetworkObject)\n" +
                "[GameManagers]  SpawnSystem+TeamAssign · MatchPhase/End · Scoring\n" +
                "                ObjectiveSystem · RespawnSystem · BeaconManager\n" +
                "                BossSpawnManager · WorldSpawnManager (NetworkObject)\n" +
                "[SpawnPoints]  4 points (T0×2, T1×2)\n" +
                "[CaptureZones]  ZONE_A · ZONE_B  (SphereCollider isTrigger)\n" +
                "[WorldSpawns]  4 WorldItemSpawnPoints\n" +
                "[ClientSystems]  RenderDisabler · SpectateManager · ClientEffectManager\n" +
                "[Input]  InputLayerManager · InputManager + 7 handlers\n" +
                "[Cameras]  CameraStateManager · GameCameraController",
                "Setup Scene", RunScene);

            Section(ref _foldUI,
                "③ UI Canvas Hierarchy",
                "Canvas_Game ▶ GameHUD\n" +
                "  Panel_PlayerHUD   — HP/STA/SPD/ARM/WEIGHT sliders\n" +
                "  Panel_CombatHUD   — weapon slots, quickslots, ammo, FireButton (mobile)\n" +
                "[AimSystem]  QuickSlotAimController · RangeIndicator · AimCursor (world-space)\n" +
                "  Panel_Crosshair   — 4 lines + center dot\n" +
                "  Panel_MatchUI     — phase/timer/score + warning overlay\n" +
                "  Panel_KillFeed    — scroll list\n" +
                "  Panel_Interaction — prompt + hold-progress slider\n" +
                "  Panel_Minimap     — RawImage + teammate dots\n" +
                "  Panel_DeathScreen — killed-by, respawn timer, buttons\n" +
                "  Panel_Results     — scoreboard, elo, continue\n" +
                "  Panel_LootContainer — name, slots grid, take-all/close\n" +
                "  DamageFeedbackSystem · UIRootController",
                "Setup UI", RunUI);

            Section(ref _foldPfb,
                "④ Prefabs  (scene-ready stubs)",
                "WorldItemPrefab · WorldContainerPrefab\n" +
                "RespawnBeaconPrefab · KillFeedItemPrefab · ResultRowPrefab\n" +
                "All saved to Assets/_NightHunt_Generated/Prefabs.\n" +
                "PlayerPrefab: drag your own NetworkObject prefab into ⑤ to wire systems.",
                "Create Prefabs", RunPrefabs);

            DrawPlayerSection();

            EditorGUILayout.Space(8);

            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button("▶  FULL SETUP  (①②③④)", GUILayout.Height(38)))
                EditorApplication.delayCall += () => { RunConfigs(); RunPrefabs(); RunScene(); RunUI(); Finalize("Full setup"); };
            GUI.backgroundColor = new Color(0.3f, 0.65f, 1f);
            if (GUILayout.Button("▶  FULL SETUP + PLAYER  (①②③④⑤)", GUILayout.Height(38)))
            {
                var tgt  = _playerTarget;
                var mode = _playerMovementMode;
                var dbg  = _playerDebugLogs;
                EditorApplication.delayCall += () =>
                {
                    RunConfigs(); RunPrefabs(); RunScene(); RunUI();
                    if (tgt != null) RunPlayer(tgt, mode, dbg);
                    Finalize("Full setup + Player");
                };
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(1f, 0.38f, 0.38f);
            if (GUILayout.Button("🗑  Delete _NightHunt_Generated folder", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Delete Generated Assets",
                    $"Delete {k_Root} and all contents?\nScene objects are NOT removed.",
                    "Delete", "Cancel"))
                {
                    AssetDatabase.DeleteAsset(k_Root);
                    AssetDatabase.Refresh();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(
                "Fields marked '→ assign manually' in the Console need a real\n" +
                "prefab / texture / SO / Cinemachine component.",
                _warnStyle);

            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────────────────
        // ⑤ Player section — drawn inline so it can host object-field + enum
        // ─────────────────────────────────────────────────────────────────────

        private void DrawPlayerSection()
        {
            _foldPlayer = EditorGUILayout.BeginFoldoutHeaderGroup(_foldPlayer,
                "⑤ Player Prefab Systems");
            if (_foldPlayer)
            {
                EditorGUILayout.HelpBox(
                    "Wires ALL player systems onto an existing NetworkObject prefab.\n\n" +
                    "[Move]   CharacterController → CharacterControllerPredictedMovement\n" +
                    "         Rigidbody           → Rigidbody + CapsuleCollider\n" +
                    "         (CharacterNormalMovement is in development — manual assign)\n\n" +
                    "[State]  CharacterStateMachine · CharacterLifecycleController\n" +
                    "         CharacterInputLifecycle\n" +
                    "[Net]    NetworkPlayer\n" +
                    "[Combat] WeaponSystem · WeaponVFXController · WeaponModelController\n" +
                    "[Stat]   PlayerStatSystem · ItemStatSystem · StatApplyOrchestrator\n" +
                    "[Inv]    InventorySystem · EquipmentSystem · QuickSlotSystem\n" +
                    "         AttachmentSystem\n" +
                    "[Items]  ItemUseSystem · ConsumableHandler · ThrowableHandler\n" +
                    "[Inter]  PlayerInteractionSystem · RaycastDetector\n" +
                    "         ProximityInteractScanner\n" +
                    "[Vision] PlayerVisionSystem · FogVisionBinder\n" +
                    "[Cam]    CameraRoot/CinemachineCamera child GO — add component manually",
                    MessageType.Info);

                _playerTarget = EditorGUILayout.ObjectField(
                    "Scene Object", _playerTarget,
                    typeof(GameObject), true) as GameObject;

                _playerMovementMode = (PlayerMovementMode)EditorGUILayout.EnumPopup(
                    "Movement Mode", _playerMovementMode);

                _playerDebugLogs = EditorGUILayout.Toggle(
                    "Enable Debug Logs", _playerDebugLogs);

                if (_playerTarget == null)
                {
                    EditorGUILayout.HelpBox(
                        "Drag a scene GameObject with a NetworkObject component here.",
                        MessageType.Warning);
                }
                else
                {
                    bool hasNOB = _playerTarget.GetComponent<NetworkObject>() != null;
                    EditorGUILayout.HelpBox(
                        hasNOB
                            ? "✓ Valid scene object — ready to setup."
                            : "⚠ No NetworkObject found. Add one first.",
                        hasNOB ? MessageType.Info : MessageType.Error);
                }

                GUI.enabled = _playerTarget != null;
                GUI.backgroundColor = new Color(0.5f, 0.82f, 1f);
                if (GUILayout.Button("⑤  Setup Player Systems", GUILayout.Height(32)))
                {
                    var tgt  = _playerTarget;
                    var mode = _playerMovementMode;
                    var dbg  = _playerDebugLogs;
                    EditorApplication.delayCall += () =>
                    {
                        RunPlayer(tgt, mode, dbg);
                        Finalize("Player Systems");
                    };
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4);
        }

        private void Section(ref bool fold, string title, string help, string btnLabel, Action action)
        {
            fold = EditorGUILayout.BeginFoldoutHeaderGroup(fold, title);
            if (fold)
            {
                EditorGUILayout.HelpBox(help, MessageType.Info);
                if (GUILayout.Button(btnLabel))
                {
                    var capturedAction = action;
                    var capturedLabel  = btnLabel;
                    EditorApplication.delayCall += () => { capturedAction(); Finalize(capturedLabel); };
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4);
        }

        private static void Finalize(string step)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GameplaySetupTool] ✓ {step} complete.");
        }

        // =====================================================================
        // ① CONFIGS
        // =====================================================================

        private static void RunConfigs()
        {
            EnsureDir(k_Root);
            EnsureDir(k_Configs);

            GameConfigJSON j = null;
            var ta = Resources.Load<TextAsset>(k_JsonRes);
            if (ta != null) { j = JsonUtility.FromJson<GameConfigJSON>(ta.text); Resources.UnloadAsset(ta); }
            else             Debug.LogWarning("[GameplaySetupTool] JSON not found — using code defaults.");

            MakeMovementSettings(j);
            MakeGameplayConfig(j);
            MakeInventoryConfig(j);
            MakeLootableConfig();
            MakeInteractableConfig();
            LogRespawnValues(j);
        }

        private static void MakeMovementSettings(GameConfigJSON j)
        {
            var a = SOLoad<MovementSettings>($"{k_Configs}/MovementSettings.asset");
            float b = (j?.CharacterConfig?.BaseMoveSpeed ?? 1f) * 5.5f;
            Sp(a, "baseSpeed",                  b);
            Sp(a, "sprintSpeedMultiplier",       1.6f);
            Sp(a, "crouchMultiplier",            0.45f);
            Sp(a, "acceleration",                25f);
            Sp(a, "deceleration",                30f);
            Sp(a, "gravity",                     20f);
            Sp(a, "jumpHeight",                  1.2f);
            Sp(a, "groundedStickDownVelocity",   2f);
            Sp(a, "maxStamina",                  j?.CharacterConfig?.BaseStamina ?? 100f);
            Sp(a, "staminaDrainRate",            20f);
            Sp(a, "staminaRegenRate",            15f);
            Sp(a, "minStaminaToSprint",          10f);
            Sp(a, "rotationSpeed",               12f);
            EditorUtility.SetDirty(a);
        }

        private static void MakeGameplayConfig(GameConfigJSON j)
        {
            var a   = SOLoad<GameplayConfig>($"{k_Configs}/GameplayConfig.asset");
            var inv = j?.InventoryConfig?.Length > 0 ? j.InventoryConfig[0] : null;
            Sp(a, "BaseWeightCapacity",      inv?.BaseWeightCapacity ?? 20f);
            Sp(a, "WeightWarningThreshold",  0.9f);
            Sp(a, "MaxOverweightPercent",    1.5f);
            Sp(a, "MinMovementSpeedPercent", 0.1f);
            Sp(a, "DamageInterruptsUsage",   true);
            Sp(a, "MovementInterruptsUsage", false);
            Sp(a, "AllowManualCancel",       true);
            EditorUtility.SetDirty(a);
        }

        private static void MakeInventoryConfig(GameConfigJSON j)
        {
            var a   = SOLoad<InventoryConfig>($"{k_Configs}/InventoryConfig.asset");
            var inv = j?.InventoryConfig?.Length > 0 ? j.InventoryConfig[0] : null;
            Sp(a, "DefaultExtraEmptySlots", inv?.BackpackSlots ?? 12);
            Sp(a, "DropDistance",           2f);
            Sp(a, "DropForce",              5f);
            Sp(a, "AutoStackOnAdd",         true);
            Sp(a, "AutoMergeOnMove",        true);
            Sp(a, "BatchWeightUpdates",     true);
            Sp(a, "CompressNetworkData",    true);
            Sp(a, "MaxCachedInstances",     100);
            EditorUtility.SetDirty(a);
        }

        private static void MakeLootableConfig()
        {
            var a = SOLoad<LootableConfig>($"{k_Configs}/LootableConfig.asset");
            a.InteractionMode     = LootInteractionMode.Instant;
            a.AllowAutoLoot       = true;
            a.ShowPrompt          = true;
            a.HoldDuration        = 1f;
            a.MaxInteractDistance = 3f;
            EditorUtility.SetDirty(a);
        }

        private static void MakeInteractableConfig()
        {
            var a = SOLoad<InteractableConfig>($"{k_Configs}/InteractableConfig.asset");
            a.InteractionType     = InteractionType.Generic;
            a.InteractionMode     = LootInteractionMode.Hold;
            a.HoldDuration        = 1.5f;
            a.MaxInteractDistance = 3f;
            a.OneTimeUse          = false;
            a.ShowPrompt          = true;
            a.PromptDefault       = "[E] Interact";
            a.PromptLocked        = "[E] Locked";
            a.PromptHolding       = "[Hold E] ...";
            EditorUtility.SetDirty(a);
        }

        private static void LogRespawnValues(GameConfigJSON j)
        {
            var r = j?.RespawnConfig?.Length > 0 ? j.RespawnConfig[0] : null;
            Debug.Log("[GameplaySetupTool] RespawnBeacon JSON values (assign to prefab):\n" +
                $"  BeaconHP={r?.BeaconHP ?? 500}  PlaceTime={r?.PlaceTime ?? 5}" +
                $"  RespawnDelay={r?.RespawnDelay ?? 15}  MinDistance={r?.MinDistance ?? 30}" +
                $"  RedeployCooldown={r?.RedeployCooldown ?? 60}");
        }

        // =====================================================================
        // ② SCENE
        // =====================================================================

        private static void RunScene()
        {
            // ── FishNet check ─────────────────────────────────────────────────
            if (UnityEngine.Object.FindFirstObjectByType<NetworkManager>() == null)
                Debug.LogWarning("[GameplaySetupTool] FishNet NetworkManager missing! " +
                    "Add via the FishNet menu before entering Play Mode.");

            // ── [NetworkCore] ─────────────────────────────────────────────────
            var netCore = GetOrMakeGO("[NetworkCore]", null);
            var ngm = Add<NetworkGameManager>(netCore);
            Sp(ngm, "port",                 (ushort)7777);
            Sp(ngm, "defaultServerAddress", "localhost");
            Add<RegistryService>(netCore);
            // → assign 'networkManager' (FishNet NetworkManager ref) manually
            Debug.Log("[GameplaySetupTool] [NetworkCore] NetworkGameManager → assign 'networkManager' ref manually.");
            EditorUtility.SetDirty(netCore);

            // ── [ServerManager]  (NetworkBehaviour → needs NetworkObject) ─────
            var srvRoot = GetOrMakeGO("[ServerManager]", null);
            if (srvRoot.GetComponent<NetworkObject>() == null)
                srvRoot.AddComponent<NetworkObject>();
            var sgm = Add<ServerGameManager>(srvRoot);
            Sp(sgm, "_expectedPlayerCount", 2);
            // playerPrefab, clientNetworkHandlerPrefab → assign after RunPrefabs
            Debug.Log("[GameplaySetupTool] [ServerManager] ServerGameManager → assign 'playerPrefab' and 'clientNetworkHandlerPrefab' manually.");
            EditorUtility.SetDirty(srvRoot);

            // ── [GameManagers] ────────────────────────────────────────────────
            var mgrs = GetOrMakeGO("[GameManagers]", null);

            // SpawnSystem + TeamAssignmentSystem (same GO)
            var spawnGO  = GetOrMakeGO("SpawnSystem", mgrs.transform);
            var spawnSys = Add<SpawnSystem>(spawnGO);
            var teamAss  = Add<TeamAssignmentSystem>(spawnGO);
            Sp(teamAss, "_maxTeams", 2);
            Sp(spawnSys, "_teamAssignmentSystem", teamAss);
            EditorUtility.SetDirty(spawnGO);

            // MatchPhaseManager
            var mpmGO = GetOrMakeGO("MatchPhaseManager", mgrs.transform);
            var mpm   = Add<MatchPhaseManager>(mpmGO);
            Sp(mpm, "phaseStartTime", 0f);
            Sp(mpm, "phaseDuration",  120f);
            EditorUtility.SetDirty(mpmGO);

            // MatchEndManager
            var memGO = GetOrMakeGO("MatchEndManager", mgrs.transform);
            var mem   = Add<MatchEndManager>(memGO);
            Sp(mem, "_phaseManager", mpm);
            EditorUtility.SetDirty(memGO);

            // ScoringSystem
            Add<ScoringSystem>(GetOrMakeGO("ScoringSystem", mgrs.transform));

            // ObjectiveSystem
            Add<ObjectiveSystem>(GetOrMakeGO("ObjectiveSystem", mgrs.transform));

            // RespawnSystem
            var rsGO = GetOrMakeGO("RespawnSystem", mgrs.transform);
            var rs   = Add<RespawnSystem>(rsGO);
            Sp(rs, "respawnDelay",       5f);
            Sp(rs, "phase3RespawnDelay", 10f);
            Sp(rs, "_spawnSystem",       spawnSys);
            Sp(rs, "_matchEndManager",   mem);
            EditorUtility.SetDirty(rsGO);

            // BeaconManager
            var bmGO = GetOrMakeGO("BeaconManager", mgrs.transform);
            var bm   = Add<BeaconManager>(bmGO);
            Sp(bm, "_matchEndManager",         mem);
            Sp(bm, "_maxActivePerTeamFallback", 2);
            // → assign '_beaconPrefabFallback' (RespawnBeaconPrefab) manually
            Debug.Log("[GameplaySetupTool] BeaconManager → assign '_beaconPrefabFallback' (RespawnBeaconPrefab) manually.");
            EditorUtility.SetDirty(bmGO);

            // BossSpawnManager
            var bsmGO = GetOrMakeGO("BossSpawnManager", mgrs.transform);
            var bsm   = Add<BossSpawnManager>(bsmGO);
            Sp(bsm, "_phaseManager", mpm);
            // → populate '_bossPrefabs' list manually
            Debug.Log("[GameplaySetupTool] BossSpawnManager → add entries to '_bossPrefabs' list manually.");
            EditorUtility.SetDirty(bsmGO);

            // WorldSpawnManager  (NetworkBehaviour → needs NetworkObject)
            var wsmGO = GetOrMakeGO("WorldSpawnManager", mgrs.transform);
            if (wsmGO.GetComponent<NetworkObject>() == null)
                wsmGO.AddComponent<NetworkObject>();
            Add<WorldSpawnManager>(wsmGO);
            // → assign 'worldItemPrefab' and 'worldContainerPrefab' manually
            Debug.Log("[GameplaySetupTool] WorldSpawnManager → assign 'worldItemPrefab' and 'worldContainerPrefab' manually.");
            EditorUtility.SetDirty(wsmGO);

            // Wire ServerGameManager now that spawnSys + mpm are ready
            Sp(sgm, "_spawnSystem",      spawnSys);
            Sp(sgm, "_matchPhaseManager", mpm);
            EditorUtility.SetDirty(srvRoot);
            EditorUtility.SetDirty(mgrs);

            // ── [SpawnPoints] ─────────────────────────────────────────────────
            var spRoot = GetOrMakeGO("[SpawnPoints]", null);
            var spList = new System.Collections.Generic.List<SpawnPoint>();
            (string n, int t, Vector3 p, Color c)[] spData =
            {
                ("SpawnPoint_T0_0", 0, new Vector3(-12f,0,-15f), Color.blue),
                ("SpawnPoint_T0_1", 0, new Vector3( -6f,0,-15f), Color.blue),
                ("SpawnPoint_T1_0", 1, new Vector3(  6f,0, 15f), Color.red),
                ("SpawnPoint_T1_1", 1, new Vector3( 12f,0, 15f), Color.red),
            };
            foreach (var d in spData)
            {
                var spGO = GetOrMakeGO(d.n, spRoot.transform);
                spGO.transform.position = d.p;
                var sp = Add<SpawnPoint>(spGO);
                Sp(sp, "_teamId",            d.t);
                Sp(sp, "_spawnRadius",        1.5f);
                Sp(sp, "_randomizeRotation",  true);
                Sp(sp, "_gizmoColor",         d.c);
                spList.Add(sp);
                EditorUtility.SetDirty(spGO);
            }
            // Wire SpawnSystem._spawnPoints[]
            var soSS  = new SerializedObject(spawnSys);
            var spArr = soSS.FindProperty("_spawnPoints");
            spArr.ClearArray();
            for (int i = 0; i < spList.Count; i++)
            {
                spArr.InsertArrayElementAtIndex(i);
                spArr.GetArrayElementAtIndex(i).objectReferenceValue = spList[i];
            }
            soSS.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spRoot);

            // ── [CaptureZones] ────────────────────────────────────────────────
            var czRoot = GetOrMakeGO("[CaptureZones]", null);
            (string id, Vector3 pos)[] czData =
            {
                ("CAPTURE_ZONE_A", new Vector3(-8f,0,0)),
                ("CAPTURE_ZONE_B", new Vector3( 8f,0,0)),
            };
            foreach (var (id, pos) in czData)
            {
                var go = GetOrMakeGO(id, czRoot.transform);
                go.transform.position = pos;
                var cz  = Add<CaptureZoneObjective>(go);
                Sp(cz, "objectiveId",     id);
                Sp(cz, "objectiveName",   $"Capture Zone {id[^1]}");
                Sp(cz, "captureRadius",   10f);
                Sp(cz, "captureTime",     10f);
                Sp(cz, "requiredPlayers", 1);
                Sp(cz, "_scorePerSecond", 1f);
                var col = go.GetComponent<SphereCollider>() ?? go.AddComponent<SphereCollider>();
                col.isTrigger = true; col.radius = 10f;
                EditorUtility.SetDirty(go);
            }
            EditorUtility.SetDirty(czRoot);

            // ── [WorldSpawns] ─────────────────────────────────────────────────
            var wsRoot = GetOrMakeGO("[WorldSpawns]", null);
            for (int i = 0; i < 4; i++)
            {
                var go = GetOrMakeGO($"WorldSpawn_{i:00}", wsRoot.transform);
                go.transform.position = new Vector3((i - 1.5f) * 8f, 0f, 5f);
                Add<WorldItemSpawnPoint>(go);
                // → assign 'spawnConfig' (WorldSpawnConfig SO) manually per point
                EditorUtility.SetDirty(go);
            }
            Debug.Log("[GameplaySetupTool] [WorldSpawns] → assign 'spawnConfig' (WorldSpawnConfig SO) to each WorldItemSpawnPoint.");
            EditorUtility.SetDirty(wsRoot);

            // ── [ClientSystems]  (disabled by RenderDisabler on DS builds) ────
            var cliSys = GetOrMakeGO("[ClientSystems]", null);
            Add<RenderDisabler>(cliSys);   // strips cameras/renderers in UNITY_SERVER builds

            var smGO = GetOrMakeGO("SpectateManager", cliSys.transform);
            var sm   = Add<SpectateManager>(smGO);
            Sp(sm, "enableDebugLogs", false);
            EditorUtility.SetDirty(smGO);

            var cemGO = GetOrMakeGO("ClientEffectManager", cliSys.transform);
            Add<ClientEffectManager>(cemGO);
            // → assign 4 VFX prefabs: damageEffectPrefab, healEffectPrefab, projectileTrailPrefab, muzzleFlashPrefab
            Debug.Log("[GameplaySetupTool] ClientEffectManager → assign 4 VFX prefab refs manually.");
            EditorUtility.SetDirty(cliSys);

            // ── [Input] ───────────────────────────────────────────────────────
            var inputGO = GetOrMakeGO("[Input]", null);
            Add<InputLayerManager>(inputGO);
            // → assign 'inputConfig' (InputConfig SO) manually
            Debug.Log("[GameplaySetupTool] InputLayerManager → assign 'inputConfig' (InputConfig SO) manually.");
            var im = Add<InputManager>(inputGO);
            // Pre-add all handlers so they appear in Inspector; InputManager will
            // use them directly rather than AddComponent at runtime.
            Add<MovementInputHandler>(inputGO);
            Add<CombatInputHandler>(inputGO);
            Add<CameraInputHandler>(inputGO);
            Add<InventoryInputHandler>(inputGO);
            Add<UIInputHandler>(inputGO);
            Add<InteractionInputHandler>(inputGO);
            Add<SpectatorInputHandler>(inputGO);
            Sp(im, "movementHandler",    inputGO.GetComponent<MovementInputHandler>());
            Sp(im, "combatHandler",      inputGO.GetComponent<CombatInputHandler>());
            Sp(im, "cameraHandler",      inputGO.GetComponent<CameraInputHandler>());
            Sp(im, "inventoryHandler",   inputGO.GetComponent<InventoryInputHandler>());
            Sp(im, "uiInputHandler",     inputGO.GetComponent<UIInputHandler>());
            Sp(im, "interactionHandler", inputGO.GetComponent<InteractionInputHandler>());
            EditorUtility.SetDirty(inputGO);

            // ── [Cameras] ─────────────────────────────────────────────────────
            var camRoot   = GetOrMakeGO("[Cameras]", null);
            var mainCamGO = GetOrMakeGO("MainCamera", camRoot.transform);
            var mainCam   = mainCamGO.GetComponent<UnityEngine.Camera>()
                            ?? mainCamGO.AddComponent<UnityEngine.Camera>();
            mainCam.tag = "MainCamera";
            Add<CameraStateManager>(mainCamGO);
            // → assign '_virtualCamera', '_inputAxisController', '_movementInput', '_weaponSystemMB' manually
            Debug.Log("[GameplaySetupTool] CameraStateManager → assign '_virtualCamera', '_inputAxisController', " +
                      "'_movementInput', '_weaponSystemMB' manually.");

            var cmCamGO = GetOrMakeGO("CinemachineCamera", camRoot.transform);
            var gcc = Add<GameCameraController>(cmCamGO);
            Sp(gcc, "_autoSpectateOnDeath", true);
            // → assign '_virtualCamera' (CinemachineCamera component), '_localLifecycle', '_spectatorInput' manually
            Debug.Log("[GameplaySetupTool] GameCameraController → assign '_virtualCamera', '_localLifecycle', '_spectatorInput' manually.");
            EditorUtility.SetDirty(camRoot);

            // ── EventSystem ───────────────────────────────────────────────────
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }
        }

        // =====================================================================
        // ③ UI
        // =====================================================================

        private static void RunUI()
        {
            // ── Load configs needed for UI wiring ─────────────────────────────
            var inventoryCfg = AssetDatabase.LoadAssetAtPath<InventoryConfig>($"{k_Configs}/InventoryConfig.asset");
            if (inventoryCfg == null)
                Debug.LogWarning("[GameplaySetupTool] InventoryConfig not found — CombatHUDPanel slot config not assigned. Run ① first.");

            // ── Canvas ────────────────────────────────────────────────────────
            var uiRoot = GetOrMakeGO("[UI]", null);
            var cvGO = GetOrMakeGO("Canvas_Game", uiRoot.transform);
            Canvas cv = cvGO.GetComponent<Canvas>();
            if (cv == null) cv = cvGO.AddComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 0;
            CanvasScaler cs = cvGO.GetComponent<CanvasScaler>();
            if (cs == null) cs = cvGO.AddComponent<CanvasScaler>();
            cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight  = 0.5f;
            if (cvGO.GetComponent<GraphicRaycaster>() == null)
                cvGO.AddComponent<GraphicRaycaster>();

            // ── GameHUD root (fills canvas) ───────────────────────────────────
            var hudGO = GetOrMakeUIGO("GameHUD", cvGO.transform);
            SetFull(hudGO);
            var hud = Add<GameHUD>(hudGO);

            // ==================================================================
            // Panel_PlayerHUD  — Bottom-Left  310×160
            // ==================================================================
            var pHudGO = Panel(hudGO, "Panel_PlayerHUD", Anc.BL, new Vector2(310, 160), new Vector2(16, 16));
            var pHud   = Add<PlayerHUDPanel>(pHudGO);

            // Stat rows spawned at runtime from PlayerStatUIConfig — container only
            var statRowContainer = EmptyChild(pHudGO, "StatRows", typeof(VerticalLayoutGroup));
            Sp(pHud, "_rowContainer", statRowContainer.transform);
            Debug.Log("[GameplaySetupTool] PlayerHUDPanel →\n" +
                      "  assign '_statRowPrefab' (StatRowEntry prefab with Slider + TMP)\n" +
                      "  assign '_statUIConfig'  (PlayerStatUIConfig SO) manually.");
            Sp(hud, "playerHUDPanel", pHud);

            // ==================================================================
            // Panel_CombatHUD  — Bottom-Right  300×170
            // ==================================================================
            var cHudGO = Panel(hudGO, "Panel_CombatHUD", Anc.BR, new Vector2(300, 170), new Vector2(-16, 16));
            var cHud   = Add<CombatHUDPanel>(cHudGO);

            // Weapon slots spawned at runtime from InventoryConfig
            var weaponSlotsGO = EmptyChild(cHudGO, "WeaponSlots", typeof(HorizontalLayoutGroup));
            Sp(cHud, "_weaponSlotsContainer", weaponSlotsGO.transform);
            // QuickSlots radial root — positioned at fire button center (origin for radial layout)
            // anchoredPosition matches Btn_Fire below so buttons arc from that pivot
            var quickSlotsRootGO = EmptyChild(cHudGO, "QuickSlotsRoot");
            var quickSlotsRT     = quickSlotsRootGO.GetComponent<RectTransform>();
            quickSlotsRT.anchorMin = quickSlotsRT.anchorMax = new Vector2(0.5f, 0.5f);
            quickSlotsRT.pivot     = new Vector2(0.5f, 0.5f);
            quickSlotsRT.anchoredPosition = new Vector2(-130f, 10f); // same as Btn_Fire
            quickSlotsRT.sizeDelta        = Vector2.zero;
            Sp(cHud, "_quickSlotsRoot", quickSlotsRT);
            Sp(cHud, "_inventoryConfig", inventoryCfg);
            // Fixed HUD elements
            Sp(cHud, "_reloadingIndicator", EmptyChild(cHudGO, "Ind_Reload"));

            // FireButton — large circular button, mobile-only fire trigger
            var fireBtnGO = GetOrMakeUIGO("Btn_Fire", cHudGO.transform);
            var fireBtnRT = fireBtnGO.GetComponent<RectTransform>();
            fireBtnRT.anchorMin = fireBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
            fireBtnRT.pivot     = new Vector2(0.5f, 0.5f);
            fireBtnRT.sizeDelta          = new Vector2(90f, 90f);
            fireBtnRT.anchoredPosition   = new Vector2(-130f, 10f);
            var fireBtnImg = fireBtnGO.GetComponent<Image>() ?? fireBtnGO.AddComponent<Image>();
            fireBtnImg.color = new Color(0.85f, 0.2f, 0.2f, 0.65f);
            if (fireBtnGO.GetComponent<CanvasGroup>() == null) fireBtnGO.AddComponent<CanvasGroup>();
            var fireBtn = Add<FireButton>(fireBtnGO);
            TMP(fireBtnGO, "Lbl_Fire", Vector2.zero, "FIRE", 16).alignment = TextAlignmentOptions.Center;
            Sp(cHud, "_fireButton", fireBtn);

            // _aimController will be wired after [AimSystem] is created below.
            Debug.Log("[GameplaySetupTool] CombatHUDPanel →\n" +
                      "  assign '_weaponSlotPrefab' (WeaponSlotButton prefab)\n" +
                      "  assign '_quickSlotPrefab'  (QuickSlotHUDButton prefab) manually.");
            Sp(hud, "combatHUDPanel", cHud);

            // ==================================================================
            // Panel_Crosshair  — Center  120×120  (transparent bg)
            // ==================================================================
            var xhGO = Panel(hudGO, "Panel_Crosshair", Anc.C, new Vector2(120, 120), Vector2.zero);
            xhGO.GetComponent<Image>().color = Color.clear;
            var xh = Add<CrosshairUI>(xhGO);
            Sp(xh, "_topLine",             LineImg(xhGO, "Line_Top"));
            Sp(xh, "_bottomLine",          LineImg(xhGO, "Line_Bottom"));
            Sp(xh, "_leftLine",            LineImg(xhGO, "Line_Left"));
            Sp(xh, "_rightLine",           LineImg(xhGO, "Line_Right"));
            Sp(xh, "_centerDot",           EmptyChild(xhGO, "Dot_Center"));
            Sp(xh, "_minSpread",           12f);
            Sp(xh, "_maxAdditionalSpread", 80f);
            Sp(xh, "_relaxSpeed",          6f);
            Sp(xh, "_lerpSpeed",           12f);
            Sp(xh, "_spreadPerShot",       20f);
            Sp(xh, "_normalColor",         Color.white);
            Sp(xh, "_reloadColor",         new Color(1f, 0.6f, 0f));
            Sp(xh, "_noAmmoColor",         Color.red);
            Sp(hud, "crosshairUI", xh);

            // ==================================================================
            // Panel_MatchUI  — Top-Center  500×82
            // ==================================================================
            var mUIGO = Panel(hudGO, "Panel_MatchUI", Anc.TC, new Vector2(500, 82), new Vector2(0, -12));
            var mUI   = Add<MatchUI>(mUIGO);
            Sp(mUI, "phaseText",            TMP(mUIGO, "Lbl_Phase",      new Vector2(-195, 28), "Phase 1", 16));
            Sp(mUI, "phaseDescriptionText", TMP(mUIGO, "Lbl_PhaseDesc",  new Vector2(-195,  8), "Preparation", 13));
            Sp(mUI, "timerText",            TMP(mUIGO, "Lbl_Timer",      new Vector2(0,    42), "00:00", 24));
            Sp(mUI, "phaseTimerText",       TMP(mUIGO, "Lbl_PhaseTimer", new Vector2(0,    18), "0:00", 16));
            Sp(mUI, "teamScoreText",        TMP(mUIGO, "Lbl_TeamScore",  new Vector2(185,  30), "0 – 0", 18));
            Sp(mUI, "personalScoreText",    TMP(mUIGO, "Lbl_PersScore",  new Vector2(185,  10), "Score: 0", 13));
            var teamListGO = EmptyChild(mUIGO, "TeamList", typeof(HorizontalLayoutGroup));
            Sp(mUI, "teamListParent", teamListGO.transform);
            // → assign 'teamMemberPrefab' manually
            Debug.Log("[GameplaySetupTool] MatchUI → assign 'teamMemberPrefab' manually.");
            // Phase warning overlay (full-screen, starts hidden)
            var warnGO = Panel(hudGO, "Panel_PhaseWarning", Anc.FS, Vector2.zero, Vector2.zero);
            var warnImg = warnGO.GetComponent<Image>(); warnImg.color = new Color(0.8f, 0.2f, 0.2f, 0.65f);
            Sp(mUI, "warningPanel",       warnGO);
            Sp(mUI, "warningText",        TMP(warnGO, "Lbl_Warning", Vector2.zero, "PHASE ENDING SOON", 28));
            Sp(mUI, "warningBackground",  warnImg);
            Sp(mUI, "warningHoldDuration",  3f);
            Sp(mUI, "warningFadeDuration",  0.4f);
            warnGO.SetActive(false); // hide AFTER children built
            Sp(hud, "matchUI", mUI);

            // ==================================================================
            // Panel_KillFeed  — Top-Right  300×200
            // ==================================================================
            var kfGO = Panel(hudGO, "Panel_KillFeed", Anc.TR, new Vector2(300, 200), new Vector2(-14, -14));
            var kf   = Add<KillFeedUI>(kfGO);
            var kfParent = EmptyChild(kfGO, "KillFeedParent", typeof(VerticalLayoutGroup));
            Sp(kf, "killFeedParent", kfParent.transform);
            Sp(kf, "maxItems",       5);
            Sp(kf, "itemLifetime",   5f);
            Sp(kf, "killColor",      Color.red);
            Sp(kf, "assistColor",    Color.yellow);
            Sp(kf, "deathColor",     Color.gray);
            // → assign 'killFeedItemPrefab' (KillFeedItemPrefab) after RunPrefabs
            Debug.Log("[GameplaySetupTool] KillFeedUI → assign 'killFeedItemPrefab' manually.");
            Sp(hud, "killFeedUI", kf);

            // ==================================================================
            // Panel_Interaction  — Bottom-Center  440×72
            // ==================================================================
            var ipGO = Panel(hudGO, "Panel_Interaction", Anc.BC, new Vector2(440, 72), new Vector2(0, 155));
            var ipUI = Add<InteractionPromptUI>(ipGO);
            ipGO.GetComponent<Image>().color = Color.clear;
            var ipInner = EmptyChild(ipGO, "PromptPanel");
            SetFull(ipInner);
            if (ipInner.GetComponent<Image>() == null) ipInner.AddComponent<Image>().color = new Color(0,0,0,0.4f);
            Sp(ipUI, "_promptPanel",        ipInner);
            Sp(ipUI, "_keyText",            TMP(ipInner, "Lbl_Key",    new Vector2(-100,0), "[E]",          21));
            Sp(ipUI, "_actionText",         TMP(ipInner, "Lbl_Action", new Vector2(  30,0), "Pick up item", 17));
            // Hold progress (hidden by default, shown for Hold-type interactions)
            var holdRoot = EmptyChild(ipInner, "HoldProgressRoot");
            holdRoot.SetActive(false);
            var holdSlider = holdRoot.GetComponent<Slider>() ?? holdRoot.AddComponent<Slider>();
            holdSlider.minValue = 0f; holdSlider.maxValue = 1f; holdSlider.value = 0f;
            holdSlider.direction = Slider.Direction.LeftToRight;
            Sp(ipUI, "_holdProgressRoot",   holdRoot);
            Sp(ipUI, "_holdProgressSlider", holdSlider);
            Sp(hud, "interactionPromptUI", ipUI);

            // ==================================================================
            // Panel_Minimap  — Top-Left  200×200
            // ==================================================================
            var mmGO  = Panel(hudGO, "Panel_Minimap", Anc.TL, new Vector2(200, 200), new Vector2(14, -14));
            var mm    = Add<MinimapUI>(mmGO);
            var mmRaw = mmGO.GetComponent<RawImage>() ?? mmGO.AddComponent<RawImage>();
            Sp(mm, "_minimapRawImage",    mmRaw);
            Sp(mm, "_cameraHeight",       50f);
            Sp(mm, "_orthoSize",          60f);
            Sp(mm, "_minimapPanelRect",   mmGO.GetComponent<RectTransform>());
            var dotParent  = EmptyChild(mmGO, "TeammateDots");
            Sp(mm, "_teammateDotParent",  dotParent.transform);
            Sp(mm, "_teammateColor",      Color.cyan);
            var playerInd    = EmptyChild(mmGO, "PlayerIndicator");
            var playerIndRT  = playerInd.GetComponent<RectTransform>() ?? playerInd.AddComponent<RectTransform>();
            Sp(mm, "_playerIndicator",    playerIndRT);
            var zoneCircle   = EmptyChild(mmGO, "ZoneCircle");
            var zoneCircleRT = zoneCircle.GetComponent<RectTransform>() ?? zoneCircle.AddComponent<RectTransform>();
            Sp(mm, "_zoneCircleRect",     zoneCircleRT);
            // → assign '_minimapCamera' (Camera with RenderTexture), '_minimapTexture'
            //         '_teammateDotPrefab'  manually
            Debug.Log("[GameplaySetupTool] MinimapUI → assign '_minimapCamera', '_minimapTexture', '_teammateDotPrefab' manually.");
            Sp(hud, "minimapUI", mm);

            // ==================================================================
            // Panel_DeathScreen  — Full-Screen  (hidden)
            // ==================================================================
            var dsGO = Panel(hudGO, "Panel_DeathScreen", Anc.FS, Vector2.zero, Vector2.zero);
            dsGO.GetComponent<Image>().color = new Color(0f,0f,0f,0.76f);
            var ds = Add<DeathScreen>(dsGO);
            Sp(ds, "_deathPanel",       dsGO);
            Sp(ds, "_killedByText",     TMP(dsGO, "Lbl_KilledBy",    new Vector2(0,  90), "Killed by: ",      22));
            Sp(ds, "_respawnTimerText", TMP(dsGO, "Lbl_RespawnTimer",new Vector2(0,  50), "Respawn in: 5s",   18));
            Sp(ds, "_spectateButton",   BtnComp(dsGO, "Btn_Spectate", new Vector2(-100, -75), "Spectate"));
            Sp(ds, "_respawnButton",    BtnComp(dsGO, "Btn_Respawn",  new Vector2( 100, -75), "Respawn"));
            Sp(ds, "_respawnDelay",     5f);
            Sp(hud, "deathScreen", ds);
            dsGO.SetActive(false); // hide AFTER children built

            // ==================================================================
            // Panel_Results  — Full-Screen  (hidden)
            // ==================================================================
            var rvGO = Panel(hudGO, "Panel_Results", Anc.FS, Vector2.zero, Vector2.zero);
            rvGO.GetComponent<Image>().color = new Color(0f,0f,0f,0.88f);
            var rv = Add<ResultsView>(rvGO);
            Sp(rv, "_panel",              rvGO);
            Sp(rv, "_resultHeaderText",   TMP(rvGO, "Lbl_Header",    new Vector2(0,  220), "VICTORY",             42));
            Sp(rv, "_reasonText",         TMP(rvGO, "Lbl_Reason",    new Vector2(0,  170), "Team eliminated",     18));
            Sp(rv, "_countdownText",      TMP(rvGO, "Lbl_Countdown", new Vector2(0, -215), "Returning in 10s...", 16));
            Sp(rv, "_eloChangeText",      TMP(rvGO, "Lbl_EloChange", new Vector2(0, -256), "+15 RP",              24));
            Sp(rv, "_eloPanel",           EmptyChild(rvGO, "EloPanel"));
            var sbCont = EmptyChild(rvGO, "ScoreboardContainer", typeof(VerticalLayoutGroup));
            Sp(rv, "_scoreboardContainer", sbCont.transform);
            Sp(rv, "_continueButton",     BtnComp(rvGO, "Btn_Continue", new Vector2(0, -305), "Continue"));
            Sp(rv, "_displayDuration",    10f);
            Sp(rv, "_postMatchCountdown", 15f);
            // → assign '_resultRowPrefab' (ResultRowPrefab) after RunPrefabs
            Debug.Log("[GameplaySetupTool] ResultsView → assign '_resultRowPrefab' manually.");
            rvGO.SetActive(false); // hide AFTER children built

            // ==================================================================
            // Panel_LootContainer  — Bottom-Right above CombatHUD  (hidden)
            // ==================================================================
            var lcGO = Panel(hudGO, "Panel_LootContainer", Anc.BR, new Vector2(380, 420), new Vector2(-16, 200));
            var lc = Add<LootContainerUI>(lcGO);

            // Inner panel with CanvasGroup for fade animations
            var lcInner = EmptyChild(lcGO, "ContainerPanel");
            SetFull(lcInner);
            var lcImg = lcInner.GetComponent<Image>() ?? lcInner.AddComponent<Image>();
            lcImg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            var lcCG = lcInner.GetComponent<CanvasGroup>() ?? lcInner.AddComponent<CanvasGroup>();
            Sp(lc, "_containerPanel",    lcInner);
            Sp(lc, "_canvasGroup",       lcCG);
            Sp(lc, "_containerNameText", TMP(lcInner, "Lbl_ContainerName", new Vector2(0,185), "Container", 20));

            var slotsParent = EmptyChild(lcInner, "SlotsParent", typeof(GridLayoutGroup));
            Sp(lc, "_slotsParent", slotsParent.transform);

            Sp(lc, "_takeAllButton", BtnComp(lcInner, "Btn_TakeAll", new Vector2(-70, -177), "Take All"));
            Sp(lc, "_closeButton",   BtnComp(lcInner, "Btn_Close",   new Vector2( 70, -177), "Close"));
            // → assign '_itemSlotPrefab' manually (optional, or leave null if using static slots)
            Debug.Log("[GameplaySetupTool] LootContainerUI → assign '_itemRowPrefab' (LootItemRow prefab) manually.");
            Sp(hud, "lootContainerUI", lc);

            // Hide AFTER all children are built (SetActive before child creation can cause NRE in Editor)
            lcGO.SetActive(false);

            // ==================================================================
            // DamageFeedbackSystem  (child of GameHUD)
            // ==================================================================
            var fbGO = EmptyChild(hudGO, "DamageFeedbackSystem");
            var fb   = Add<DamageFeedbackSystem>(fbGO);
            Sp(fb, "numberLifetime",      2f);
            Sp(fb, "numberSpeed",         2f);
            Sp(fb, "normalDamageColor",   Color.white);
            Sp(fb, "criticalDamageColor", Color.yellow);
            Sp(fb, "headshotColor",       Color.red);
            Sp(fb, "indicatorLifetime",   0.5f);
            // → assign 'damageNumberPrefab' and 'hitIndicatorPrefab' manually
            Debug.Log("[GameplaySetupTool] DamageFeedbackSystem → assign 'damageNumberPrefab' and 'hitIndicatorPrefab' manually.");
            Sp(hud, "damageFeedback", fb);

            // ==================================================================
            // UIRootController  (inventory bridge — child of GameHUD)
            // ==================================================================
            var urlGO = EmptyChild(hudGO, "UIRootController");
            var url   = Add<UIRootController>(urlGO);
            Sp(url, "_playerHudPanel", pHud);
            Sp(url, "_combatHudPanel", cHud);
            // ==================================================================
            // Panel_InventoryScreen  (full-screen, hidden by default)
            // Slot prefabs CANNOT be auto-created — assign UISlotLayoutConfig SO manually.
            // ==================================================================
            var invGO = Panel(hudGO, "Panel_InventoryScreen", Anc.FS, Vector2.zero, Vector2.zero);
            invGO.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            var invScreen = Add<InventoryScreen>(invGO);

            // ── Inventory Grid (GridLayoutGroup — left column) ────────────────
            var invGridGO   = EmptyChild(invGO, "InventoryGrid", typeof(GridLayoutGroup));
            var invGridRect = invGridGO.GetComponent<RectTransform>();
            invGridRect.anchorMin     = new Vector2(0.02f, 0.08f);
            invGridRect.anchorMax     = new Vector2(0.58f, 0.95f);
            invGridRect.offsetMin     = invGridRect.offsetMax = Vector2.zero;
            Sp(invScreen, "_inventoryGridRoot", invGridRect);

            // ── Equipment Root (right-top) ────────────────────────────────────
            var equipRootGO   = EmptyChild(invGO, "EquipmentRoot", typeof(VerticalLayoutGroup));
            var equipRootRect = equipRootGO.GetComponent<RectTransform>();
            equipRootRect.anchorMin   = new Vector2(0.60f, 0.52f);
            equipRootRect.anchorMax   = new Vector2(0.86f, 0.94f);
            equipRootRect.offsetMin   = equipRootRect.offsetMax = Vector2.zero;
            Sp(invScreen, "_equipmentRoot", equipRootRect);

            // ── Weapon Root (right-mid) ───────────────────────────────────────
            var weapRootGO   = EmptyChild(invGO, "WeaponRoot", typeof(HorizontalLayoutGroup));
            var weapRootRect = weapRootGO.GetComponent<RectTransform>();
            weapRootRect.anchorMin    = new Vector2(0.60f, 0.34f);
            weapRootRect.anchorMax    = new Vector2(0.86f, 0.52f);
            weapRootRect.offsetMin    = weapRootRect.offsetMax = Vector2.zero;
            Sp(invScreen, "_weaponRoot", weapRootRect);

            // ── QuickSlot Root (right-lower) ──────────────────────────────────
            var qsRootGO   = EmptyChild(invGO, "QuickSlotRoot", typeof(HorizontalLayoutGroup));
            var qsRootRect = qsRootGO.GetComponent<RectTransform>();
            qsRootRect.anchorMin      = new Vector2(0.60f, 0.18f);
            qsRootRect.anchorMax      = new Vector2(0.86f, 0.34f);
            qsRootRect.offsetMin      = qsRootRect.offsetMax = Vector2.zero;
            Sp(invScreen, "_quickSlotRoot", qsRootRect);

            // ── Trash Slot Root (bottom-right corner) ─────────────────────────
            var trashRootGO   = EmptyChild(invGO, "TrashSlotRoot");
            var trashRootRect = trashRootGO.GetComponent<RectTransform>();
            trashRootRect.anchorMin   = new Vector2(0.88f, 0.04f);
            trashRootRect.anchorMax   = new Vector2(0.98f, 0.14f);
            trashRootRect.offsetMin   = trashRootRect.offsetMax = Vector2.zero;
            Sp(invScreen, "_trashSlotRoot", trashRootRect);
            // → assign '_trashSlotPrefab' manually (TrashSlotView prefab)
            Debug.Log("[GameplaySetupTool] InventoryScreen → assign '_trashSlotPrefab' manually.");

            // ── Sort Button (top-right of inventory grid) ─────────────────────
            var sortBtn = BtnComp(invGO, "Btn_Sort", new Vector2(100f, -30f), "Sort");
            Sp(invScreen, "_sortButton", sortBtn);

            // ── PlayerStatUIPanel (far-right column) ──────────────────────────
            var pStatGO   = EmptyChild(invGO, "Panel_PlayerStats", typeof(VerticalLayoutGroup));
            var pStatRect = pStatGO.GetComponent<RectTransform>();
            pStatRect.anchorMin       = new Vector2(0.87f, 0.18f);
            pStatRect.anchorMax       = new Vector2(0.99f, 0.94f);
            pStatRect.offsetMin       = pStatRect.offsetMax = Vector2.zero;
            var pStatPanel = Add<PlayerStatUIPanel>(pStatGO);
            var pStatContainerGO = EmptyChild(pStatGO, "StatContainer", typeof(VerticalLayoutGroup));
            Sp(pStatPanel, "_statContainer", pStatContainerGO.GetComponent<RectTransform>());
            // → assign '_statRowPrefab' and '_statUIConfig' manually
            Debug.Log("[GameplaySetupTool] PlayerStatUIPanel → assign '_statRowPrefab' (PlayerStatUIView prefab) and '_statUIConfig' manually.");
            Sp(invScreen, "playerStatUIPanel", pStatPanel);

            // ── AttachmentPanel (float panel — starts hidden) ─────────────────
            var attGO  = EmptyChild(invGO, "Panel_Attachments");
            var attImg = attGO.GetComponent<Image>() ?? attGO.AddComponent<Image>();
            attImg.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
            var attRect = attGO.GetComponent<RectTransform>();
            attRect.anchorMin         = new Vector2(0.60f, 0.04f);
            attRect.anchorMax         = new Vector2(0.80f, 0.18f);
            attRect.offsetMin         = attRect.offsetMax = Vector2.zero;
            var attPanel  = Add<AttachmentPanel>(attGO);
            var attSlots  = EmptyChild(attGO, "AttachmentSlotsRoot");
            Sp(attPanel, "_slotsRoot",  attSlots.GetComponent<RectTransform>());
            var attPRoot  = EmptyChild(attGO, "PanelRoot");
            Sp(attPanel, "_panelRoot",  attPRoot);
            attGO.SetActive(false); // hidden until triggered
            Sp(invScreen, "_attachmentPanel", attPanel);

            // ── ItemTooltip (overlay, always-on-top child) ────────────────────
            var ttGO    = EmptyChild(invGO, "Tooltip_Item");
            var ttPanel = Add<ItemTooltip>(ttGO);
            var ttRoot  = EmptyChild(ttGO, "TooltipRoot");
            SetFull(ttRoot);
            var ttRootImg = ttRoot.GetComponent<Image>() ?? ttRoot.AddComponent<Image>();
            ttRootImg.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            Sp(ttPanel, "_tooltipRoot",              ttRoot);
            Sp(ttPanel, "_itemNameText",             TMP(ttRoot, "Lbl_ItemName",        new Vector2(0f,  80f), "Item Name",   20));
            Sp(ttPanel, "_itemDescriptionText",      TMP(ttRoot, "Lbl_ItemDescription", new Vector2(0f,  50f), "Description", 14));
            var itemStatsSect       = EmptyChild(ttRoot, "ItemStatsSection");
            var itemStatsContGO     = EmptyChild(itemStatsSect, "ItemStatsContainer", typeof(VerticalLayoutGroup));
            Sp(ttPanel, "_itemStatsSection",         itemStatsSect);
            Sp(ttPanel, "_itemStatsContainer",       itemStatsContGO.GetComponent<RectTransform>());
            var modSect             = EmptyChild(ttRoot, "PlayerModifiersSection");
            var modContGO           = EmptyChild(modSect, "PlayerModifiersContainer", typeof(VerticalLayoutGroup));
            Sp(ttPanel, "_playerModifiersSection",   modSect);
            Sp(ttPanel, "_playerModifiersContainer", modContGO.GetComponent<RectTransform>());
            Sp(ttPanel, "_canvas",                   cvGO.GetComponent<Canvas>());
            // → assign '_itemStatConfig' and '_statRowPrefab' (TooltipStatRow prefab) manually
            Debug.Log("[GameplaySetupTool] ItemTooltip → assign '_itemStatConfig' and '_statRowPrefab' (TooltipStatRow prefab) manually.");
            Sp(invScreen, "_itemTooltip", ttPanel);

            // ── DropQuantityDialog (modal overlay — starts hidden) ─────────────
            var dqdGO = Panel(hudGO, "Dialog_DropQuantity", Anc.C, new Vector2(420, 300), Vector2.zero);
            dqdGO.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.07f, 0.97f);
            var dqd     = Add<DropQuantityDialog>(dqdGO);
            var dqdRoot = EmptyChild(dqdGO, "Root");
            SetFull(dqdRoot);
            Sp(dqd, "_root",          dqdRoot);
            Sp(dqd, "_titleText",     TMP(dqdRoot, "Lbl_Title",  new Vector2(0f,  110f), "Drop Item",  20));
            Sp(dqd, "_hintText",      TMP(dqdRoot, "Lbl_Hint",   new Vector2(0f,   75f), "Select qty", 14));
            // Slider container
            var dqdSliderContGO = EmptyChild(dqdRoot, "SliderContainer");
            var dqdSCRect = dqdSliderContGO.GetComponent<RectTransform>();
            dqdSCRect.anchorMin = new Vector2(0.05f, 0.45f); dqdSCRect.anchorMax = new Vector2(0.95f, 0.68f);
            dqdSCRect.offsetMin = dqdSCRect.offsetMax = Vector2.zero;
            Sp(dqd, "_sliderContainer", dqdSliderContGO);
            var dqdSliderGO = GetOrMakeUIGO("Slider_Quantity", dqdSliderContGO.transform);
            var dqdSlider   = dqdSliderGO.GetComponent<Slider>() ?? dqdSliderGO.AddComponent<Slider>();
            var dqdSliderRect = dqdSliderGO.GetComponent<RectTransform>();
            dqdSliderRect.anchorMin = Vector2.zero; dqdSliderRect.anchorMax = Vector2.one;
            dqdSliderRect.offsetMin = new Vector2(4, 4); dqdSliderRect.offsetMax = new Vector2(-84, -4);
            Sp(dqd, "_quantitySlider", dqdSlider);
            var dqdInputGO   = GetOrMakeUIGO("Input_Quantity", dqdSliderContGO.transform);
            var dqdInputRect = dqdInputGO.GetComponent<RectTransform>();
            dqdInputRect.anchorMin = new Vector2(1, 0); dqdInputRect.anchorMax = new Vector2(1, 1);
            dqdInputRect.pivot = new Vector2(1, 0.5f);
            dqdInputRect.offsetMin = new Vector2(-80, 2); dqdInputRect.offsetMax = new Vector2(0, -2);
            var dqdBg     = dqdInputGO.GetComponent<Image>() ?? dqdInputGO.AddComponent<Image>();
            dqdBg.color   = new Color(0.15f, 0.15f, 0.15f, 1f);
            var dqdInput  = dqdInputGO.GetComponent<TMP_InputField>() ?? dqdInputGO.AddComponent<TMP_InputField>();
            Sp(dqd, "_quantityInput", dqdInput);
            Sp(dqd, "_cancelButton",  BtnComp(dqdRoot, "Btn_Cancel",  new Vector2(-148f, -110f), "Cancel"));
            Sp(dqd, "_dropOneButton", BtnComp(dqdRoot, "Btn_DropOne", new Vector2(  -48f, -110f), "Drop 1"));
            Sp(dqd, "_dropAllButton", BtnComp(dqdRoot, "Btn_DropAll", new Vector2(  52f, -110f), "Drop All"));
            Sp(dqd, "_dropButton",    BtnComp(dqdRoot, "Btn_Drop",    new Vector2( 152f, -110f), "Drop"));
            dqdGO.SetActive(false); // hidden until triggered
            Sp(invScreen, "_dropQuantityDialog", dqd);

            // ── DragDropController (scene singleton — sibling of InventoryScreen root) ──
            var ddcGO = EmptyChild(hudGO, "DragDropController");
            var ddc   = Add<DragDropController>(ddcGO);
            Sp(ddc, "_dragCanvas",         cvGO.GetComponent<Canvas>());
            Sp(ddc, "_dropQuantityDialog", dqd);
            Sp(ddc, "_raycaster",          cvGO.GetComponent<GraphicRaycaster>());
            // → assign '_ghostPrefab' manually (DragDropGhost prefab)
            Debug.Log("[GameplaySetupTool] DragDropController → assign '_ghostPrefab' (DragDropGhost prefab) manually.");

            // ── UISlotLayoutConfig note ───────────────────────────────────────
            // UISlotLayoutConfig is a ScriptableObject — cannot be auto-created here.
            // Assign it to InventoryScreen._uiConfig in the Inspector after running ③.
            Debug.Log("[GameplaySetupTool] InventoryScreen → assign '_uiConfig' (UISlotLayoutConfig SO) in the Inspector.");

            // Hide inventory screen AFTER all children are built
            invGO.SetActive(false);

            // ── Wire UIRootController ─────────────────────────────────────────
            Sp(url, "_inventoryScreen",     invScreen);
            Sp(url, "_inventoryRootObject", invGO);
            Sp(hud, "uiRootController", url);

            EditorUtility.SetDirty(hudGO);
            EditorUtility.SetDirty(cvGO);

            // ==================================================================
            // [AimSystem]  — world-space objects (NOT inside Canvas)
            // QuickSlotAimController + RangeIndicator + AimCursor
            // ==================================================================
            var aimRootGO = GetOrMakeGO("[AimSystem]", null);

            // RangeIndicator — visual assigned by designer (any child GO works: mesh/decal/VFX)
            var rangeIndGO = GetOrMakeGO("RangeIndicator", aimRootGO.transform);
            var rangeInd = Add<RangeIndicator>(rangeIndGO);
            EditorUtility.SetDirty(rangeIndGO);

            // AimCursor — simple empty marker; swap out for a visible mesh at polish time
            var aimCursorGO = GetOrMakeGO("AimCursor", aimRootGO.transform);
            aimCursorGO.SetActive(false);
            EditorUtility.SetDirty(aimCursorGO);

            // QuickSlotAimController on the root
            var aimCtrl = Add<QuickSlotAimController>(aimRootGO);
            Sp(aimCtrl, "_rangeIndicator", rangeInd);
            Sp(aimCtrl, "_aimCursor",      aimCursorGO.transform);

            // Wire aim controller back into CombatHUDPanel
            Sp(cHud, "_aimController", aimCtrl);

            EditorUtility.SetDirty(aimRootGO);
            Debug.Log("[GameplaySetupTool] [AimSystem] created.\n" +
                      "  QuickSlotAimController → _rangeIndicator / _aimCursor wired.\n" +
                      "  CombatHUDPanel._aimController wired.\n" +
                      "  • Enable '_forceMobileMode' on QuickSlotAimController for editor testing.\n" +
                      "  • Replace AimCursor with a visible mesh/sprite as needed.");

            // EventSystem guard
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }
        }

        // =====================================================================
        // ④ PREFABS
        // =====================================================================

        private static void RunPrefabs()
        {
            EnsureDir(k_Root);
            EnsureDir(k_Prefabs);
            MakePlayerPrefab();
            MakeWorldItemPrefab();
            MakeWorldContainerPrefab();
            MakeRespawnBeaconPrefab();
            MakeKillFeedItemPrefab();
            MakeResultRowPrefab();
        }

        // ── PlayerPrefab  (minimal stub) ─────────────────────────────────────
        // Full component wiring is done by RunPlayer() in section ⑤.

        private static void MakePlayerPrefab()
        {
            const string path = k_Prefabs + "/PlayerPrefab.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var root = new GameObject("PlayerPrefab");
            root.AddComponent<NetworkObject>();

            // Placeholder capsule so the prefab is selectable in Project window
            var col     = root.AddComponent<CharacterController>();
            col.radius  = 0.4f; col.height = 1.8f;
            col.center  = new Vector3(0f, 0.9f, 0f);

            // Basic child hierarchy
            var wHolder = new GameObject("WeaponHolder");
            wHolder.transform.SetParent(root.transform, false);
            wHolder.transform.localPosition = new Vector3(0.3f, 1.5f, 0.5f);
            var fp = new GameObject("FirePoint");
            fp.transform.SetParent(wHolder.transform, false);
            fp.transform.localPosition = Vector3.forward * 0.5f;
            var camRoot = new GameObject("CameraRoot");
            camRoot.transform.SetParent(root.transform, false);
            camRoot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            new GameObject("CinemachineCamera").transform.SetParent(camRoot.transform, false);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[GameplaySetupTool] Created stub {path}\n" +
                      "  → Drag it into section ⑤ and click 'Setup Player Systems' to wire all components.");
        }

        // ── WorldItem prefab ——————————————————————————————————————————————────

        private static void MakeWorldItemPrefab()
        {
            const string path = k_Prefabs + "/WorldItemPrefab.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var root = new GameObject("WorldItem");
            root.AddComponent<NetworkObject>();
            root.AddComponent<WorldItem>();
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.3f; col.isTrigger = true;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[GameplaySetupTool] Created {path}  (assign to WorldSpawnManager.worldItemPrefab)");
        }

        // ── WorldContainer prefab ─────────────────────────────────────────────

        private static void MakeWorldContainerPrefab()
        {
            const string path = k_Prefabs + "/WorldContainerPrefab.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var root = new GameObject("WorldContainer");
            root.AddComponent<NetworkObject>();
            root.AddComponent<WorldContainer>();
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.8f, 0.6f, 0.6f); col.isTrigger = true;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[GameplaySetupTool] Created {path}  (assign to WorldSpawnManager.worldContainerPrefab)");
        }

        // ── RespawnBeacon prefab ──────────────────────────────────────────────

        private static void MakeRespawnBeaconPrefab()
        {
            const string path = k_Prefabs + "/RespawnBeaconPrefab.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var root   = new GameObject("RespawnBeaconPrefab");
            root.AddComponent<NetworkObject>();
            var beacon = root.AddComponent<RespawnBeacon>();
            Sp(beacon, "placeTime",                   5f);
            Sp(beacon, "respawnDelay",                15f);
            Sp(beacon, "minDistanceFromOtherBeacons", 30f);

            var model = new GameObject("BeaconModel");
            model.transform.SetParent(root.transform, false);
            var indic = new GameObject("PlacementIndicator");
            indic.transform.SetParent(root.transform, false);
            Sp(beacon, "beaconModel",        model);
            Sp(beacon, "placementIndicator", indic);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[GameplaySetupTool] Created {path}  (assign to BeaconManager._beaconPrefabFallback)");
        }

        // ── KillFeedItem prefab ───────────────────────────────────────────────

        private static void MakeKillFeedItemPrefab()
        {
            const string path = k_Prefabs + "/KillFeedItemPrefab.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var root = new GameObject("KillFeedItem");
            var rt   = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(280, 28);
            var item = root.AddComponent<KillFeedItem>();
            Sp(item, "text", TMP(root, "Text", Vector2.zero, "Player killed Player", 15));

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[GameplaySetupTool] Created {path}  (assign to KillFeedUI.killFeedItemPrefab)");
        }

        // ── ResultRow prefab ──────────────────────────────────────────────────

        private static void MakeResultRowPrefab()
        {
            const string path = k_Prefabs + "/ResultRowPrefab.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var root = new GameObject("ResultRow");
            var rt   = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(860, 36);
            var row = root.AddComponent<ResultRowView>();
            Sp(row, "nameText",   TMP(root, "Lbl_Name",   new Vector2(-300, 0), "PlayerName",  16));
            Sp(row, "teamText",   TMP(root, "Lbl_Team",   new Vector2(-130, 0), "Team 0",      16));
            Sp(row, "killsText",  TMP(root, "Lbl_Kills",  new Vector2(   0, 0), "0",           16));
            Sp(row, "deathsText", TMP(root, "Lbl_Deaths", new Vector2(  80, 0), "0",           16));
            Sp(row, "scoreText",  TMP(root, "Lbl_Score",  new Vector2( 180, 0), "0",           16));
            Sp(row, "eloText",    TMP(root, "Lbl_Elo",    new Vector2( 280, 0), "+0",          16));

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[GameplaySetupTool] Created {path}  (assign to ResultsView._resultRowPrefab)");
        }

        // =====================================================================
        // ⑤ PLAYER SYSTEMS
        // =====================================================================

        private static void RunPlayer(GameObject root, PlayerMovementMode movMode, bool debugLogs)
        {
            if (root == null)
            { Debug.LogError("[GameplaySetupTool] RunPlayer: no target object."); return; }
            if (root.GetComponent<NetworkObject>() == null)
            { Debug.LogError($"[GameplaySetupTool] '{root.name}' missing NetworkObject. Aborting."); return; }

            // ── Load configs ──────────────────────────────────────────────────
            var gameplayCfg  = AssetDatabase.LoadAssetAtPath<GameplayConfig>($"{k_Configs}/GameplayConfig.asset");
            var inventoryCfg = AssetDatabase.LoadAssetAtPath<InventoryConfig>($"{k_Configs}/InventoryConfig.asset");
            var movementCfg  = AssetDatabase.LoadAssetAtPath<MovementSettings>($"{k_Configs}/MovementSettings.asset");
            var statCfg      = AssetDatabase.LoadAssetAtPath<PlayerStatConfig>($"{k_Configs}/PlayerStatConfig.asset")
                               ?? Resources.Load<PlayerStatConfig>("Configs/PlayerStatConfig");
            if (gameplayCfg == null || inventoryCfg == null || movementCfg == null)
                Debug.LogWarning("[GameplaySetupTool] Some configs are null — run ① first.");
            if (statCfg == null)
                Debug.LogWarning("[GameplaySetupTool] PlayerStatConfig not found — assign _statConfig manually.");

            Undo.RecordObject(root, "NH Setup Player Systems");
            ApplyPlayerComponents(root, movMode, debugLogs, gameplayCfg, inventoryCfg, movementCfg, statCfg);
            EditorUtility.SetDirty(root);
        }

        private static void ApplyPlayerComponents(
            GameObject root,
            PlayerMovementMode movMode, bool debugLogs,
            GameplayConfig gameplayCfg, InventoryConfig inventoryCfg,
            MovementSettings movementCfg, PlayerStatConfig statCfg)
        {
            // ── Child hierarchy ───────────────────────────────────────────────
            var wHolder = GetOrMakeChildGO(root, "WeaponHolder");
            wHolder.transform.localPosition = new Vector3(0.3f, 1.5f, 0.5f);
            var firePoint = GetOrMakeChildGO(wHolder, "FirePoint");
            firePoint.transform.localPosition = Vector3.forward * 0.5f;
            var cameraRoot = GetOrMakeChildGO(root, "CameraRoot");
            cameraRoot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            GetOrMakeChildGO(cameraRoot, "CinemachineCamera");   // add CinemachineCamera component manually

            // ── Physics & Movement ────────────────────────────────────────────
            if (movMode == PlayerMovementMode.CharacterController)
            {
                CharacterController cc = root.GetComponent<CharacterController>();
                if (cc == null) cc = root.AddComponent<CharacterController>();
                cc.radius    = 0.4f;  cc.height     = 1.8f;
                cc.center    = new Vector3(0f, 0.9f, 0f);
                cc.skinWidth = 0.08f; cc.slopeLimit = 45f; cc.stepOffset = 0.3f;

                var mov = Add<CharacterControllerPredictedMovement>(root);
                Sp(mov, "movementSettings",      movementCfg);
                Sp(mov, "tankTurnSpeed",         10f);
                Sp(mov, "lockTurnSpeed",         18f);
                Sp(mov, "staminaRecoveryDelay",  1.5f);
                Sp(mov, "allowCameraLockToggle", true);
                Sp(mov, "startWithCameraLock",   false);
                Sp(mov, "slowMovementThreshold", 1f);
                Sp(mov, "enableDebugLogs",       debugLogs);
                Debug.Log("[GameplaySetupTool] Movement: CharacterControllerPredictedMovement ✓");
            }
            else
            {
                Rigidbody rb = root.GetComponent<Rigidbody>();
                if (rb == null) rb = root.AddComponent<Rigidbody>();
                rb.freezeRotation         = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation          = RigidbodyInterpolation.Interpolate;
                CapsuleCollider cap = root.GetComponent<CapsuleCollider>();
                if (cap == null) cap = root.AddComponent<CapsuleCollider>();
                cap.radius = 0.4f; cap.height = 1.8f; cap.center = new Vector3(0f, 0.9f, 0f);

                var mov = Add<RigidbodyPredictedMovement>(root);
                Sp(mov, "movementSettings",      movementCfg);
                Sp(mov, "tankTurnSpeed",         10f);
                Sp(mov, "lockTurnSpeed",         18f);
                Sp(mov, "staminaRecoveryDelay",  1.5f);
                Sp(mov, "allowCameraLockToggle", true);
                Sp(mov, "startWithCameraLock",   false);
                Sp(mov, "slowMovementThreshold", 1f);
                Sp(mov, "enableDebugLogs",       debugLogs);
                Debug.Log("[GameplaySetupTool] Movement: RigidbodyPredictedMovement ✓");
            }

            // ── State machine & Lifecycle ─────────────────────────────────────
            var fsm = Add<CharacterStateMachine>(root);
            Sp(fsm, "initialState", 0); // CharacterLifecycleState.Alive = 0

            var llc = Add<CharacterLifecycleController>(root);
            Sp(llc, "_stateMachine",              fsm);
            Sp(llc, "_autoRequestRespawnOnDeath", true);
            // _statSystemSource and _networkPlayer wired after those are added below

            Add<CharacterInputLifecycle>(root);

            // ── Networking ────────────────────────────────────────────────────
            var netPlayer = Add<NetworkPlayer>(root);
            // → assign '_playerCamera' (CinemachineCamera component) manually
            Debug.Log("[GameplaySetupTool] NetworkPlayer._playerCamera\n" +
                      "  → Add CinemachineCamera component to CameraRoot/CinemachineCamera child,\n" +
                      "  → then assign that CinemachineCamera to:\n" +
                      "     • NetworkPlayer._playerCamera\n" +
                      "     • CameraStateManager._virtualCamera\n" +
                      "     • GameCameraController._virtualCamera\n" +
                      "     • CameraStateManager._inputAxisController (CinemachineInputAxisController)");

            // ── Weapon model + VFX (client-side) ─────────────────────────────
            var wmc  = Add<WeaponModelController>(root);
            Sp(wmc, "_weaponSocket", wHolder.transform);

            var wvfx = Add<WeaponVFXController>(root);
            // _weaponSystemSource / _vfxController wired below after WeaponSystem is added.

            // ── Stat systems ──────────────────────────────────────────────────
            var pss = Add<PlayerStatSystem>(root);
            Sp(pss, "_statConfig",    statCfg);
            Sp(pss, "_gameplayConfig", gameplayCfg);
            Sp(pss, "_showDebugUI",   debugLogs);
            // Note: ItemStatSystem is a static utility class — no component needed.

            var aim = Add<AimSystem>(root);
            Sp(aim, "_playerStatSystemMB", pss);

            var sao = Add<StatApplyOrchestrator>(root);
            Sp(sao, "_playerStatSystemMB", pss);
            // _equipmentSystemMB, _weaponSystemMB, _attachmentSystemMB wired after those added

            // ── Inventory stack ───────────────────────────────────────────────
            var inv = Add<InventorySystem>(root);
            Sp(inv, "_gameplayConfig",          gameplayCfg);
            Sp(inv, "_inventoryConfig",         inventoryCfg);
            Sp(inv, "_statSystemComponent",     pss);
            Sp(inv, "_batchWeightUpdates",      true);
            Sp(inv, "_autoCleanupInvalidItems", true);
            Sp(inv, "_enableDebugLogs",         debugLogs);

            var eq = Add<EquipmentSystem>(root);
            Sp(eq, "_inventoryConfig",          inventoryCfg);
            Sp(eq, "_statSystemComponent",      pss);
            Sp(eq, "_inventorySystemComponent", inv);
            Sp(eq, "_enableDebugLogs",          debugLogs);

            var wepSys = Add<WeaponSystem>(root);
            Sp(wepSys, "_inventoryConfig",          inventoryCfg);
            Sp(wepSys, "_statSystemComponent",      pss);
            Sp(wepSys, "_inventorySystemComponent", inv);
            Sp(wepSys, "_enableDebugLogs",          debugLogs);

            // Wire weapon model + VFX controllers now that WeaponSystem exists.
            Sp(wmc,  "_weaponSystemSource", wepSys);
            Sp(wmc,  "_vfxController",      wvfx);
            Sp(wvfx, "_weaponSystemSource", wepSys);

            var qs = Add<QuickSlotSystem>(root);
            Sp(qs, "_inventoryConfig",          inventoryCfg);
            Sp(qs, "_inventorySystemComponent", inv);
            Sp(qs, "_enableDebugLogs",          debugLogs);

            var att = Add<AttachmentSystem>(root);
            Sp(att, "_inventorySystem",  inv);
            Sp(att, "_inventoryConfig",  inventoryCfg);
            Sp(att, "_autoRecoverAttachments", true);
            Sp(att, "_enableDebugLogs",  debugLogs);

            // ── Item use / consumable / throwable ─────────────────────────────
            var conH = Add<ConsumableHandler>(root);
            var thrH = Add<ThrowableHandler>(root);
            var ius  = Add<ItemUseSystem>(root);
            Sp(ius, "_weaponSystemComponent",    wepSys);
            Sp(ius, "_statSystemComponent",      pss);
            Sp(ius, "_inventorySystemComponent", inv);
            Sp(ius, "_consumableHandler",        conH);
            Sp(ius, "_throwableHandler",         thrH);
            Sp(ius, "_defaultUseTime",           3.5f);

            // QuickSlotSystem needs ItemUseSystem — wire now
            Sp(qs, "_itemUseSystemComponent", ius);

            // ── Interaction ───────────────────────────────────────────────────
            var ray = Add<RaycastDetector>(root);
            Sp(ray, "interactableLayerMask", (LayerMask)(-1));
            Sp(ray, "maxDistance",           5f);
            Sp(ray, "ignorePlayerLayer",     true);
            Sp(ray, "showDebugRay",          debugLogs);
            Sp(ray, "logTargetChanges",      debugLogs);
            // → assign 'playerCamera' (Camera component) manually
            Debug.Log("[GameplaySetupTool] RaycastDetector.playerCamera → assign Camera component manually.");

            var prox = Add<ProximityInteractScanner>(root);
            Sp(prox, "scanRadius",            4f);
            Sp(prox, "scanInterval",          0.15f);
            Sp(prox, "interactableLayerMask", (LayerMask)(-1));
            Sp(prox, "maxResults",            16);
            Sp(prox, "drawGizmos",            true);
            Sp(prox, "showDebugUI",           debugLogs);

            var piSys = Add<PlayerInteractionSystem>(root);
            Sp(piSys, "raycastDetector",  ray);
            Sp(piSys, "proximityScanner", prox);
            Sp(piSys, "debugInputLogs",   debugLogs);

            // ── Vision / Fog-of-War ───────────────────────────────────────────
            var vis = Add<PlayerVisionSystem>(root);
            Sp(vis, "networkPlayer", netPlayer);
            // → assign 'fogOfWarRevealer3D' (FogOfWarRevealer3D) manually
            Debug.Log("[GameplaySetupTool] PlayerVisionSystem.fogOfWarRevealer3D → assign manually.");

            var fogBind = Add<FogVisionBinder>(root);
            Sp(fogBind, "defaultViewRadius", 15f);

            // ── StatApplyOrchestrator — wire remaining refs ───────────────────
            Sp(sao, "_equipmentSystemMB",  eq);
            Sp(sao, "_weaponSystemMB",     wepSys);
            Sp(sao, "_attachmentSystemMB", att);

            // ── CharacterLifecycleController — wire stat source ───────────────
            Sp(llc, "_statSystemSource", pss);
            Sp(llc, "_networkPlayer",    netPlayer);
            // → assign '_respawnSystem' (RespawnSystem from scene) manually
            Debug.Log("[GameplaySetupTool] CharacterLifecycleController._respawnSystem\n" +
                      "  → assign the scene RespawnSystem component manually.");

            Debug.Log($"[GameplaySetupTool] ✓ Player systems fully wired on '{root.name}'.\n" +
                      "  Remaining manual assignments:\n" +
                      "  • CinemachineCamera component → NetworkPlayer + CameraStateManager + GameCameraController\n" +
                      "  • CinemachineInputAxisController → CameraStateManager._inputAxisController\n" +
                      "  • Camera (runtime) → CameraStateManager._movementInput, RaycastDetector.playerCamera\n" +
                      "  • WeaponSystemMB → CameraStateManager._weaponSystemMB\n" +
                      "  • CharacterLifecycleController._localLifecycle → GameCameraController\n" +
                      "  • RespawnSystem → CharacterLifecycleController._respawnSystem\n" +
                      "  • FogOfWarRevealer3D → PlayerVisionSystem\n" +
                      "  • ProjectilePrefab / MuzzleFlash / HitEffect → WeaponDefinition (per weapon SO)\n" +
                      "  • PlayerStatConfig SO → PlayerStatSystem._statConfig (if not found automatically)");
        }

        /// <summary>Gets or creates a direct child GameObject by name.</summary>
        private static GameObject GetOrMakeChildGO(GameObject parent, string name)
        {
            var existing = parent.transform.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        // =====================================================================
        // UTILITIES
        // =====================================================================

        // ── Asset ─────────────────────────────────────────────────────────────

        private static void EnsureDir(string p)
        {
            if (AssetDatabase.IsValidFolder(p)) return;
            string parent = Path.GetDirectoryName(p).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(p));
        }

        private static T SOLoad<T>(string path) where T : ScriptableObject
        {
            var ex = AssetDatabase.LoadAssetAtPath<T>(path);
            if (ex != null) return ex;
            var a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // ── GameObject ───────────────────────────────────────────────────────

        private static GameObject GetOrMakeGO(string name, Transform parent)
        {
            if (parent == null)
            {
                var found = GameObject.Find(name);
                if (found) return found;
            }
            else
            {
                var found = parent.Find(name);
                if (found) return found.gameObject;
            }
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            return go;
        }

        private static T Add<T>(GameObject go) where T : Component
            => go.GetComponent<T>() ?? go.AddComponent<T>();

        // ── SerializedObject field setter ─────────────────────────────────────

        private static void Sp(UnityEngine.Object target, string field, object value)
        {
            var so   = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[GameplaySetupTool] Field '{field}' not found on {target.GetType().Name}");
                return;
            }
            switch (value)
            {
                case bool      v: prop.boolValue              = v;  break;
                case int       v: prop.intValue               = v;  break;
                case ushort    v: prop.intValue               = v;  break;
                case float     v: prop.floatValue             = v;  break;
                case string    v: prop.stringValue            = v;  break;
                case Color     v: prop.colorValue             = v;  break;
                case Vector2   v: prop.vector2Value           = v;  break;
                case Vector3   v: prop.vector3Value           = v;  break;
                case LayerMask v: prop.intValue               = v.value; break;
                case Transform v: prop.objectReferenceValue   = v;  break;
                case UnityEngine.Object v: prop.objectReferenceValue = v; break;
                default:
                    Debug.LogWarning($"[GameplaySetupTool] Unhandled type {value?.GetType()} for '{field}'");
                    return;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── UI layout helpers ─────────────────────────────────────────────────

        private enum Anc { BL, BR, TL, TR, TC, BC, C, FS }

        /// <summary>Gets or creates a UI child GO with RectTransform (required for all Canvas children).</summary>
        private static GameObject GetOrMakeUIGO(string name, Transform parent)
        {
            var found = parent.Find(name);
            if (found != null) return found.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetFull(GameObject go)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static GameObject Panel(GameObject parent, string name,
                                        Anc anchor, Vector2 size, Vector2 pos)
        {
            var go  = GetOrMakeUIGO(name, parent.transform);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            Image img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.35f);

            if (anchor == Anc.FS)
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                return go;
            }
            Vector2 a, piv;
            switch (anchor)
            {
                case Anc.BL: a = new Vector2(0,   0);    piv = new Vector2(0,   0);    break;
                case Anc.BR: a = new Vector2(1,   0);    piv = new Vector2(1,   0);    break;
                case Anc.TL: a = new Vector2(0,   1);    piv = new Vector2(0,   1);    break;
                case Anc.TR: a = new Vector2(1,   1);    piv = new Vector2(1,   1);    break;
                case Anc.TC: a = new Vector2(.5f, 1);    piv = new Vector2(.5f, 1);    break;
                case Anc.BC: a = new Vector2(.5f, 0);    piv = new Vector2(.5f, 0);    break;
                default:     a = new Vector2(.5f, .5f);  piv = new Vector2(.5f, .5f);  break;
            }
            rt.anchorMin = rt.anchorMax = a; rt.pivot = piv;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            return go;
        }

        private static TextMeshProUGUI TMP(GameObject parent, string name,
                                           Vector2 pos, string def, int fontSize = 18)
        {
            var go = GetOrMakeUIGO(name, parent.transform);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta        = new Vector2(240, fontSize + 8);
            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            if (t == null) t = go.AddComponent<TextMeshProUGUI>();
            if (string.IsNullOrEmpty(t.text)) t.text = def;
            t.fontSize = fontSize; t.color = Color.white;
            return t;
        }

        private static Slider MkSlider(GameObject parent, string name, Vector2 pos,
                                       float width, Color fillColor)
        {
            var go = GetOrMakeUIGO(name, parent.transform);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(width, 16);
            Slider s = go.GetComponent<Slider>();
            if (s == null) s = go.AddComponent<Slider>();
            s.minValue = 0f; s.maxValue = 1f; s.value = 1f;
            s.direction = Slider.Direction.LeftToRight;
            // Background
            var bg = GetOrMakeUIGO("BG", go.transform);
            Image bgImg = bg.GetComponent<Image>();
            if (bgImg == null) bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            // Fill
            var fill = GetOrMakeUIGO("Fill", go.transform);
            RectTransform fillRT = fill.GetComponent<RectTransform>();
            if (fillRT == null) fillRT = fill.AddComponent<RectTransform>();
            Image fillImg = fill.GetComponent<Image>();
            if (fillImg == null) fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;
            s.fillRect = fillRT;
            return s;
        }

        private static RectTransform LineImg(GameObject parent, string name)
        {
            var go = GetOrMakeUIGO(name, parent.transform);
            Image img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.color = Color.white;
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            return rt;
        }

        private static GameObject EmptyChild(GameObject parent, string name,
                                             params Type[] extra)
        {
            var go = GetOrMakeUIGO(name, parent.transform);
            foreach (var t in extra)
                if (go.GetComponent(t) == null) go.AddComponent(t);
            return go;
        }

        private static GameObject BtnChild(GameObject parent, string name,
                                           Vector2 pos, Vector2 size)
        {
            var go = GetOrMakeUIGO(name, parent.transform);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);
            if (go.GetComponent<Button>() == null) go.AddComponent<Button>();
            return go;
        }

        private static Button BtnComp(GameObject parent, string name,
                                      Vector2 pos, string label)
        {
            var go = BtnChild(parent, name, pos, new Vector2(160, 44));
            TMP(go, "Lbl", Vector2.zero, label, 16).alignment = TextAlignmentOptions.Center;
            return go.GetComponent<Button>();
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            _warnStyle   = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            _warnStyle.normal.textColor = new Color(1f, 0.7f, 0.2f);
            _stylesInit = true;
        }

        // =====================================================================
        // JSON HELPER CLASSES
        // =====================================================================

        [Serializable] private class GameConfigJSON
        {
            public CharacterEntry   CharacterConfig;
            public InventoryEntry[] InventoryConfig;
            public RespawnEntry[]   RespawnConfig;
        }

        [Serializable] private class CharacterEntry
        {
            public float BaseHP;
            public float BaseStamina;
            public float BaseMoveSpeed;
            public float BaseWeightCapacity;
        }

        [Serializable] private class InventoryEntry
        {
            public int   BackpackSlots;
            public float BaseWeightCapacity;
            public int   QuickSlotCount;
        }

        [Serializable] private class RespawnEntry
        {
            public float BeaconHP;
            public float PlaceTime;
            public float RespawnDelay;
            public float RedeployCooldown;
            public float MinDistance;
        }
    }
}
#endif
