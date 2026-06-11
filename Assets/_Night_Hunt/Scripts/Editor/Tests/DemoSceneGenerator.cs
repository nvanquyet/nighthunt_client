using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NightHunt.Editor.Tests
{
    /// <summary>
    /// Menu: NightHunt / Tests / Generate Demo Scene
    ///
    /// Builds a FULLY-WIRED NightHunt test scene â€” ZERO null inspector refs
    /// (except items that require prefab assets assigned by hand afterward).
    ///
    ///   â‘  Physics layers + collision matrix
    ///   â‘¡ Environment (ground, walls, covers, boss platform)
    ///   â‘¢ Spawn  â†’ SpawnSystem + 4 SpawnPoints wired
    ///   â‘£ Loot   â†’ WorldSpawnManager + 8 WorldItemSpawnPoints
    ///   â‘¤ Doors  â†’ 2Ã— WorldDoor + WorldSwitch
    ///   â‘¥ Boss   â†’ BossSpawnManager + BossSpawnPoint + Arena
    ///   â‘¦ Zones  â†’ ZoneSystem + LockdownZone + 2 ZoneBuff areas
    ///   â‘§ Managers (20+ components â€” all cross-wired)
    ///       â€¢ MatchPhaseManager
    ///       â€¢ MatchEndManager        (wires _phaseManager + _scoringSystem)
    ///       â€¢ ScoringSystem
    ///       â€¢ ScoreSync
    ///       â€¢ RespawnSystem          (wires _spawnSystem + _matchEndManager + _respawnConfig)
    ///       â€¢ TeamAssignmentSystem
    ///       â€¢ ObjectiveSystem
    ///       â€¢ AntiCampingSystem
    ///       â€¢ SpectateManager
    ///       â€¢ ClientEffectManager
    ///       â€¢ BulletTargetRegistry
    ///       â€¢ FogTeamDebugController
    ///       â€¢ PlayerPublicRegistry
    ///       â€¢ RegistryService
    ///       â€¢ NetworkGameManager     (auto-finds NetworkManager at runtime)
    ///       â€¢ ServerGameManager      (wires playerPrefab + spawnSystem + _matchPhaseManager + debugConfig)
    ///       â€¢ DamageFeedbackSystem   (manager instance â€” separate from HUD instance)
    ///       â€¢ Objectives: CaptureZone, RadarStation, EMPNode
    ///       â€¢ GameplayTestOrchestrator (wires _phaseManager, _scoringSystem, _bossSpawnManager, _lockdownZone)
    ///   â‘¨ Minimap camera + RenderTexture asset (512Ã—512)
    ///   â‘© UI â€” fully wired:
    ///       GameHUD_Canvas (sortOrder=100)
    ///         GameHUD.cs + UIRootController (both on root)
    ///         PlayerHUDPanel
    ///         CombatHUDPanel + WeaponSlotsContainer + FireButton(script)+AimJoystick
    ///         MatchUI       (all text + list + warning wired)
    ///         KillFeedUI    (killFeedParent wired â€” assign killFeedItemPrefab manually)
    ///         InteractionPromptUI (hidden â€” key/action/holdSlider wired)
    ///         MinimapUI     (rawImage/playerIndicator/zoneCircle/dotParent wired)
    ///         DeathScreen   (hidden â€” deathPanel/killedBy/timer/buttons wired)
    ///         LootContainerUI (hidden â€” panel/slots/buttons wired)
    ///         DamageFeedbackSystem
    ///         [MoveJoystick] FixedJoystick + MobileMovementBridge
    ///       InventoryScreen_Canvas (sortOrder=1, hidden)
    ///         InventoryScreen (GridRoot/EquipmentRoot/WeaponRoot/TrashSlot/SortButton wired)
    ///       PersistentUICanvas (sortOrder=9999)
    ///       EventSystem
    ///   â‘ª Input  â†’ InputLayerManager + InputManager
    ///   â‘« Camera â†’ Main Camera + CameraStateManager
    ///   â‘¬ Lighting â†’ Directional sun + trilight ambient
    ///
    /// MANUAL STEPS AFTER GENERATION (cannot be automated â€” require prefab assets):
    ///   1. Add FishNet NetworkManager prefab (Tugboat transport) to scene
    ///   2. Register PlayerPrefab in NetworkManager.SpawnablePrefabs
    ///   3. Wire MinimapUI._minimapCamera + _minimapTexture in Inspector
    ///   4. Assign killFeedItemPrefab on KillFeedUI
    ///   5. Attach PersistentUICanvas.cs + DontDestroyOnLoad on PersistentUICanvas GO
    ///   6. Assign MovementSettings SO on player prefab
    ///   7. Assign ItemDatabase SO on WorldSpawnManager
    ///   8. Wire [MoveJoystick].FixedJoystick → MobileHUDPanel._joystick in Inspector
    /// </summary>
    public static class DemoSceneGenerator
    {
        // â”€â”€ Asset paths â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private const string ScenePath       = "Assets/_Night_Hunt/Scenes/TestScene_Full.unity";
        private const string PrefabRoot      = "Assets/_Night_Hunt/Prefabs";
        private const string GeneratedDir    = "Assets/_Night_Hunt/Textures/Generated";
        private const string ConfigRoot      = "Assets/_Night_Hunt/Data/Configs";

        // â”€â”€ Physics layer indices â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Slot indices MUST match NightHuntLayerSetupTool canonical table.
        // Run  NightHunt ▸ Tools ▸ Layer & Tag Setup  to write these to TagManager.
        private const int L_Player          =  6;
        private const int L_PlayerHitBox    =  7;
        private const int L_Projectile      =  8;
        private const int L_Interactable    = 10;
        private const int L_Zone            = 11;
        private const int L_Throwable       = 12;
        private const int L_DeadCharacter   = 13;
        private const int L_MapObstacle     = 15;
        private const int L_Items           = 20;
        private const int L_Minimap         = 21;   // Minimap at slot 21 (was wrongly 12)
        private const int L_MapStatic       = 22;
        private const int L_Wall            = 23;
        private const int L_Ground          = 24;   // Ground at slot 24 (was wrongly 9)
        private const int L_SeeThrough      = 25;

        // â”€â”€ Shared state (valid only within a single generation run) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static Gameplay.Spawn.SpawnSystem          s_SpawnSystem;
        private static Gameplay.Zone.SafeZoneManager      s_SafeZoneManager;
        private static Gameplay.Match.MatchEndManager      s_MatchEndManager;
        private static Gameplay.Scoring.ScoringSystem      s_ScoringSystem;
        private static Gameplay.Respawn.RespawnSystem      s_RespawnSystem;
        private static Gameplay.Boss.BossSpawnManager      s_BossSpawnManager;
        private static RenderTexture                       s_MinimapRT;
        private static Camera                              s_MinimapCamera;
        private static Gameplay.Team.TeamAssignmentSystem s_TeamAssignmentSystem;

        // â”€â”€ Menu items â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [MenuItem("NightHunt/Tests/Generate Demo Scene", priority = 1)]
        public static void GenerateDemoScene()
        {
            if (!EditorUtility.DisplayDialog(
                    "Generate Demo Scene",
                    $"Create / overwrite scene at:\n{ScenePath}",
                    "Generate", "Cancel"))
                return;

            ResetSharedState();
            EnsureDir(ScenePath);
            EnsureDir(GeneratedDir + "/.keep");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var log = new List<string> { "=== NightHunt Demo Scene ===" };

            BuildPhysicsLayers(log);   // â‘  must be first â€” others use LayerMask.NameToLayer

            var root      = CreateEmpty("[ SceneRoot ]");
            var envRoot   = CreateEmpty("[ Environment ]",  root);
            var spawnRoot = CreateEmpty("[ Spawn ]",        root);
            var lootRoot  = CreateEmpty("[ Loot ]",         root);
            var doorsRoot = CreateEmpty("[ Doors ]",        root);
            var bossRoot  = CreateEmpty("[ Boss ]",         root);
            var zoneRoot  = CreateEmpty("[ Zones ]",        root);
            var mgrsRoot  = CreateEmpty("[ Managers ]",     root);
            var uiRoot    = CreateEmpty("[ UI ]",           root);
            var inputRoot = CreateEmpty("[ Input ]",        root);
            var camRoot   = CreateEmpty("[ Camera ]",       root);

            BuildEnvironment(envRoot,    log);
            BuildSpawnPoints(spawnRoot,  log);   // â†’ s_SpawnSystem
            BuildLootSpawnPoints(lootRoot, log);
            BuildDoorsAndSwitches(doorsRoot, log);
            BuildBossZone(bossRoot,      log);   // â†’ s_BossSpawnManager
            BuildZones(zoneRoot,         log);   // â†’ s_LockdownZone
            BuildManagers(mgrsRoot,      log);   // â†’ all manager refs + cross-wiring
            BuildMinimapCamera(camRoot,  log);   // â†’ s_MinimapRT
            BuildUI(uiRoot,              log);   // uses s_MinimapRT
            BuildInput(inputRoot,        log);
            BuildCamera(camRoot,         log);
            BuildLighting(root,          log);

            EditorSceneManager.SaveScene(
                SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.Refresh();
            Debug.Log(string.Join("\n", log));

            EditorUtility.DisplayDialog("Demo Scene Generated",
                $"Saved: {ScenePath}\n\nSee Console for full log.\n\n" +
                "MANUAL STEPS:\n" +
                "1. Add FishNet NetworkManager prefab (Tugboat)\n" +
                "2. Register PlayerPrefab in NetworkManager.SpawnablePrefabs\n" +
                "3. Wire MinimapUI._minimapCamera + _minimapTexture\n" +
                "4. Assign killFeedItemPrefab on KillFeedUI\n" +
                "5. Attach PersistentUICanvas.cs + DDOL on PersistentUICanvas GO\n" +
                "6. Call MobileHUDPanel.Bind(inputManager) on player spawn",
                "7. Wire [MoveJoystick].FixedJoystick → MobileHUDPanel._joystick in Inspector",
                "OK");
        }

        [MenuItem("NightHunt/Tests/Add Managers to Active Scene", priority = 2)]
        public static void AddToActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!EditorUtility.DisplayDialog("Add Managers",
                    $"Add NightHunt managers to '{scene.name}'?", "Add", "Cancel")) return;

            ResetSharedState();
            var root = CreateEmpty("[NightHunt Managers]");
            var log  = new List<string>();
            BuildManagers(root, log);
            BuildInput(root, log);
            Debug.Log(string.Join("\n", log));
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ResetSharedState()
        {
            s_SpawnSystem     = null;
            s_SafeZoneManager = null;
            s_MatchEndManager = null;
            s_ScoringSystem   = null;
            s_RespawnSystem   = null;
            s_BossSpawnManager= null;
            s_MinimapRT             = null;
            s_MinimapCamera         = null;
            s_TeamAssignmentSystem  = null;
        }

        // â”€â”€ â‘  Physics Layers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildPhysicsLayers(List<string> log)
        {
            log.Add("\n[Physics Layers]");

            // NOTE: The full canonical layer + tag + collision-matrix setup is performed by
            //       NightHuntLayerSetupTool (NightHunt ▸ Tools ▸ Layer & Tag Setup).
            //       This demo-scene generator only VERIFIES layers are present;
            //       it does NOT overwrite conflicting slots (that is the tool's job).

            var tm     = new SerializedObject(
                AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
            var layers = tm.FindProperty("layers");

            // Verify (add if empty, warn if occupied by wrong name — do NOT force-overwrite).
            DefineLayer(layers, L_Player,          "Player",          log);
            DefineLayer(layers, L_PlayerHitBox,    "PlayerHitBox",    log);
            DefineLayer(layers, L_Projectile,      "Projectile",      log);
            DefineLayer(layers, L_Interactable,    "Interactable",    log);
            DefineLayer(layers, L_Zone,            "Zone",            log);
            DefineLayer(layers, L_Throwable,       "Throwable",       log);
            DefineLayer(layers, L_DeadCharacter,   "DeadCharacter",   log);
            DefineLayer(layers, L_MapObstacle,     "MapObstacle",     log);
            DefineLayer(layers, L_Items,           "Items",           log);
            DefineLayer(layers, L_Minimap,         "Minimap",         log);
            DefineLayer(layers, L_MapStatic,       "MapStatic",       log);
            DefineLayer(layers, L_Wall,            "Wall",            log);
            DefineLayer(layers, L_Ground,          "Ground",          log);
            DefineLayer(layers, L_SeeThrough,      "SeeThrough",      log);
            tm.ApplyModifiedProperties();

            // Collision matrix — resolved by NAME so it is immune to future slot changes.
            // The full matrix is applied by NightHuntLayerSetupTool; here we only apply
            // the minimal rules needed for the demo scene to be playable.
            int lPlayer       = LayerMask.NameToLayer("Player");
            int lPlayerHitBox = LayerMask.NameToLayer("PlayerHitBox");
            int lProjectile   = LayerMask.NameToLayer("Projectile");
            int lZone         = LayerMask.NameToLayer("Zone");
            int lMinimap      = LayerMask.NameToLayer("Minimap");
            int lGround       = LayerMask.NameToLayer("Ground");
            int lInteractable = LayerMask.NameToLayer("Interactable");

            if (lPlayer       >= 0) Physics.IgnoreLayerCollision(lPlayer,       lPlayer,       true);
            if (lPlayerHitBox >= 0 && lPlayer >= 0)
                                    Physics.IgnoreLayerCollision(lPlayerHitBox, lPlayer,       true);
            if (lPlayerHitBox >= 0) Physics.IgnoreLayerCollision(lPlayerHitBox, lPlayerHitBox, true);
            if (lProjectile   >= 0) Physics.IgnoreLayerCollision(lProjectile,   lProjectile,   true);
            if (lZone         >= 0 && lProjectile >= 0)
                                    Physics.IgnoreLayerCollision(lZone,         lProjectile,   true);
            if (lZone         >= 0 && lGround >= 0)
                                    Physics.IgnoreLayerCollision(lZone,         lGround,       true);
            if (lZone         >= 0 && lInteractable >= 0)
                                    Physics.IgnoreLayerCollision(lZone,         lInteractable, true);
            if (lZone         >= 0) Physics.IgnoreLayerCollision(lZone,         lZone,         true);
            if (lMinimap      >= 0)
                for (int i = 0; i < 32; i++)
                    Physics.IgnoreLayerCollision(lMinimap, i, true);

            log.Add("  Collision matrix applied (name-based — survives slot moves).");
            log.Add("  Run NightHunt ▸ Tools ▸ Layer & Tag Setup for the FULL matrix.");
        }

        private static void DefineLayer(SerializedProperty p, int idx, string name, List<string> log)
        {
            if (idx >= p.arraySize) { log.Add($"  âš ï¸ idx {idx} out of range"); return; }
            var e = p.GetArrayElementAtIndex(idx);
            if (string.IsNullOrEmpty(e.stringValue))
            { e.stringValue = name; log.Add($"  {idx:D2}: \"{name}\" created"); }
            else
                log.Add(e.stringValue == name
                    ? $"  {idx:D2}: \"{name}\" âœ“"
                    : $"  {idx:D2}: âš ï¸ occupied by \"{e.stringValue}\" (expected \"{name}\")");
        }

        // â”€â”€ â‘¡ Environment â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildEnvironment(GameObject parent, List<string> log)
        {
            log.Add("\n[Environment]");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground"; ground.transform.SetParent(parent.transform);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            SetLayer(ground, "Ground"); SetStaticFlags(ground);

            CreateWall("Wall_N", parent, new Vector3(  0, 2.5f, 100), new Vector3(200, 5, 1));
            CreateWall("Wall_S", parent, new Vector3(  0, 2.5f,-100), new Vector3(200, 5, 1));
            CreateWall("Wall_E", parent, new Vector3( 100, 2.5f,  0), new Vector3(1, 5, 200));
            CreateWall("Wall_W", parent, new Vector3(-100, 2.5f,  0), new Vector3(1, 5, 200));

            var covers = new[]
            {
                (new Vector3( 30, 1.5f, 30), new Vector3(6, 3, 1.5f)),
                (new Vector3( 33, 1.5f, 27), new Vector3(1.5f, 3, 6)),
                (new Vector3(-30, 1.5f, 30), new Vector3(6, 3, 1.5f)),
                (new Vector3(-33, 1.5f, 27), new Vector3(1.5f, 3, 6)),
                (new Vector3(  0, 1.5f,  5), new Vector3(12, 3, 1.5f)),
                (new Vector3(  0, 1.5f, -5), new Vector3(12, 3, 1.5f)),
                (new Vector3(  5, 1.5f,  0), new Vector3(1.5f, 3, 12)),
                (new Vector3( -5, 1.5f,  0), new Vector3(1.5f, 3, 12)),
                (new Vector3( 20, 1.5f,-30), new Vector3(1.5f, 3, 20)),
                (new Vector3(-20, 1.5f,-30), new Vector3(1.5f, 3, 20)),
            };
            int ci = 1;
            foreach (var (pos, size) in covers) CreateWall($"Cover_{ci++}", parent, pos, size);

            var plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plat.name = "BossPlatform"; plat.transform.SetParent(parent.transform);
            plat.transform.position = new Vector3(0, 0.25f, 50);
            plat.transform.localScale = new Vector3(30, 0.5f, 30);
            SetLayer(plat, "Ground"); SetStaticFlags(plat);
            log.Add("  Ground + 4 walls + 10 covers + BossPlatform");
        }

        private static void CreateWall(string name, GameObject parent, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent.transform);
            go.transform.position = pos; go.transform.localScale = scale;
            SetLayer(go, "Ground"); SetStaticFlags(go);
        }

        // â”€â”€ â‘¢ Spawn Points â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildSpawnPoints(GameObject parent, List<string> log)
        {
            log.Add("\n[Spawn]");
            var sysGo = new GameObject("SpawnSystem"); sysGo.transform.SetParent(parent.transform);
            s_SpawnSystem = sysGo.AddComponent<Gameplay.Spawn.SpawnSystem>();

            var cfg = new[]
            {
                ("SpawnCluster_Team1_A", new Vector3( 80, 0.5f,  80), 0),
                ("SpawnCluster_Team1_B", new Vector3( 75, 0.5f,  75), 0),
                ("SpawnCluster_Team2_A", new Vector3(-80, 0.5f, -80), 1),
                ("SpawnCluster_Team2_B", new Vector3(-75, 0.5f, -75), 1),
            };

            var pts = new List<Gameplay.Spawn.SpawnPoint>();
            foreach (var (n, pos, team) in cfg)
            {
                var sp = new GameObject(n); sp.transform.SetParent(parent.transform);
                sp.transform.position = pos;
                var comp = sp.AddComponent<Gameplay.Spawn.SpawnPoint>();
                var so = new SerializedObject(comp);
                var p  = so.FindProperty("_teamId");
                if (p != null) { p.intValue = team; so.ApplyModifiedProperties(); }
                pts.Add(comp);
            }

            var sysSO = new SerializedObject(s_SpawnSystem);
            var list  = sysSO.FindProperty("_spawnPoints");
            if (list != null)
            {
                list.ClearArray();
                for (int i = 0; i < pts.Count; i++)
                {
                    list.InsertArrayElementAtIndex(i);
                    list.GetArrayElementAtIndex(i).objectReferenceValue = pts[i];
                }
                sysSO.ApplyModifiedProperties();
                log.Add($"  âœ… SpawnSystem + {pts.Count} SpawnPoints wired");
            }
            else log.Add("  SpawnSystem + 4 SpawnPoints â€” âš ï¸ _spawnPoints property not found");
        }

        // â”€â”€ â‘£ Loot â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildLootSpawnPoints(GameObject parent, List<string> log)
        {
            log.Add("\n[Loot]");
            var wsm = new GameObject("WorldSpawnManager"); wsm.transform.SetParent(parent.transform);
            wsm.AddComponent<GameplaySystems.Loot.WorldSpawnManager>();
            log.Add("  WorldSpawnManager â€” âš ï¸ assign ItemDatabase SO");

            var positions = new[]
            {
                new Vector3( 15, 0.5f,  15), new Vector3(-15, 0.5f,  15),
                new Vector3( 15, 0.5f, -15), new Vector3(-15, 0.5f, -15),
                new Vector3(  0, 0.5f,  40), new Vector3(-40, 0.5f,   0),
                new Vector3( 40, 0.5f,   0), new Vector3(  0, 0.5f, -40),
            };
            for (int i = 0; i < positions.Length; i++)
            {
                var sp = new GameObject($"LootSpawn_{i + 1}"); sp.transform.SetParent(parent.transform);
                sp.transform.position = positions[i];
                sp.AddComponent<GameplaySystems.World.WorldItemSpawnPoint>();
                var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                m.name = "Gizmo"; m.transform.SetParent(sp.transform);
                m.transform.localPosition = Vector3.zero; m.transform.localScale = Vector3.one * 0.3f;
                Object.DestroyImmediate(m.GetComponent<Collider>());
                if (m.TryGetComponent<Renderer>(out var r)) r.sharedMaterial = MakeMat(Color.yellow);
            }
            log.Add($"  {positions.Length} WorldItemSpawnPoints");
        }

        // â”€â”€ â‘¤ Doors & Switches â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildDoorsAndSwitches(GameObject parent, List<string> log)
        {
            log.Add("\n[Doors]");
            var pairs = new[]
            {
                (new Vector3(  0, 1.5f, 20), new Vector3( 5, 1.5f, 20)),
                (new Vector3(-40, 1.5f,  0), new Vector3(-45, 1.5f,  0)),
            };
            int idx = 1;
            foreach (var (dp, sp) in pairs)
            {
                var dgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dgo.name = $"WorldDoor_{idx}"; dgo.transform.SetParent(parent.transform);
                dgo.transform.position = dp; dgo.transform.localScale = new Vector3(2, 3, 0.2f);
                SetLayer(dgo, "Interactable");
                dgo.AddComponent<GameplaySystems.World.WorldDoor>();
                if (dgo.TryGetComponent<Renderer>(out var dr))
                    dr.sharedMaterial = MakeMat(new Color(0.5f, 0.3f, 0.1f));

                var sgo = new GameObject($"WorldSwitch_{idx}"); sgo.transform.SetParent(parent.transform);
                sgo.transform.position = sp; SetLayer(sgo, "Interactable");
                sgo.AddComponent<GameplaySystems.World.WorldSwitch>();
                var sc = sgo.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 1.5f;
                idx++;
            }
            log.Add($"  {pairs.Length} Door+Switch pairs");
        }

        // â”€â”€ â‘¥ Boss Zone â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildBossZone(GameObject parent, List<string> log)
        {
            log.Add("\n[Boss]");

            var spGo   = new GameObject("BossSpawnPoint"); spGo.transform.SetParent(parent.transform);
            spGo.transform.position = new Vector3(0, 0.5f, 50);
            var spComp = spGo.AddComponent<Gameplay.Boss.BossSpawnPoint>();

            var bsmGo   = new GameObject("BossSpawnManager"); bsmGo.transform.SetParent(parent.transform);
            s_BossSpawnManager = bsmGo.AddComponent<Gameplay.Boss.BossSpawnManager>();

            const string bossPrefabPath = PrefabRoot + "/Boss/Boss Prefab.prefab";
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bossPrefabPath);
            var bsmSO      = new SerializedObject(s_BossSpawnManager);
            var spawnsProp = bsmSO.FindProperty("_bossSpawns");
            if (spawnsProp != null)
            {
                spawnsProp.arraySize = 1;
                var entry = spawnsProp.GetArrayElementAtIndex(0);
                var pf    = entry.FindPropertyRelative("BossPrefab");
                if (pf != null && bossPrefab != null) pf.objectReferenceValue = bossPrefab;
                var pts   = entry.FindPropertyRelative("SpawnPoints");
                if (pts != null) { pts.arraySize = 1; pts.GetArrayElementAtIndex(0).objectReferenceValue = spComp; }
                bsmSO.ApplyModifiedProperties();
            }
            log.Add($"  BossSpawnManager âœ…  prefab:{(bossPrefab != null ? "âœ…" : "âš ï¸ assign")}  spawnPt:âœ…");

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "ArenaRing"; ring.transform.SetParent(parent.transform);
            ring.transform.position = new Vector3(0, 0.1f, 50);
            ring.transform.localScale = new Vector3(30, 0.05f, 30);
            if (ring.TryGetComponent<Renderer>(out var rr)) rr.sharedMaterial = MakeMat(new Color(1, 0, 0, 0.25f));
            if (ring.TryGetComponent<Collider>(out var rc)) Object.DestroyImmediate(rc);

            var aggro = new GameObject("BossAggroZone"); aggro.transform.SetParent(parent.transform);
            aggro.transform.position = new Vector3(0, 0.5f, 50); SetLayer(aggro, "Zone");
            var ac = aggro.AddComponent<SphereCollider>(); ac.isTrigger = true; ac.radius = 20f;
        }

        // â”€â”€ â‘¦ Zones â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildZones(GameObject parent, List<string> log)
        {
            log.Add("\n[Zones]");
            var ldGo = new GameObject("SafeZoneManager"); ldGo.transform.SetParent(parent.transform);
            s_SafeZoneManager = ldGo.AddComponent<Gameplay.Zone.SafeZoneManager>();
            log.Add("  SafeZoneManager added");

            CreateZoneBuff(parent, "SpeedBuffZone",  new Vector3( 0, 0.5f, -10), 8f,
                Gameplay.StatSystem.Core.Types.PlayerStatType.MovementSpeed,
                Gameplay.StatSystem.Core.Types.ModifierType.Percentage, 0.25f,
                new Color(0f, 1f, 0.5f, 0.2f), log);
            CreateZoneBuff(parent, "ArmorBuffZone",  new Vector3(-25, 0.5f, 40), 8f,
                Gameplay.StatSystem.Core.Types.PlayerStatType.Armor,
                Gameplay.StatSystem.Core.Types.ModifierType.Flat, 30f,
                new Color(0f, 0.5f, 1f, 0.2f), log);
        }

        private static void CreateZoneBuff(
            GameObject parent, string name, Vector3 pos, float radius,
            Gameplay.StatSystem.Core.Types.PlayerStatType stat,
            Gameplay.StatSystem.Core.Types.ModifierType modType, float value,
            Color color, List<string> log)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform);
            go.transform.position = pos; SetLayer(go, "Zone");
            var col = go.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = radius;
            var buff = go.AddComponent<Gameplay.Zone.ZoneBuff>();
            var so   = new SerializedObject(buff);
            SetSOStr(so, "_zoneId", name); SetSOFloat(so, "_radius", radius);
            SetSOInt(so, "_statType", (int)stat); SetSOInt(so, "_modType", (int)modType);
            SetSOFloat(so, "_value", value); so.ApplyModifiedProperties();
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vis.name = "Visual"; vis.transform.SetParent(go.transform);
            vis.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            if (vis.TryGetComponent<Renderer>(out var r))  r.sharedMaterial = MakeMat(color);
            if (vis.TryGetComponent<Collider>(out var vc)) Object.DestroyImmediate(vc);
            log.Add($"  {name}  {stat} {modType}={value}  r={radius}");
        }

        // â”€â”€ â‘§ Managers + cross-wiring â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildManagers(GameObject parent, List<string> log)
        {
            log.Add("\n[Managers]");

            // â”€â”€ Core event bus â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            AddMgr<Gameplay.Core.Events.GameplayEventBus>(parent, "GameplayEventBus", log);

            // â”€â”€ Match â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // SafeZoneManager is self-managed -- add to scene, no serialized wiring needed
            var szGo = AddMgr<Gameplay.Zone.SafeZoneManager>(parent, "SafeZoneManager", log);
            s_SafeZoneManager = szGo.GetComponent<Gameplay.Zone.SafeZoneManager>();
            s_ScoringSystem = AddMgr<Gameplay.Scoring.ScoringSystem>(parent, "ScoringSystem", log)
                                  .GetComponent<Gameplay.Scoring.ScoringSystem>();
            // ScoreSync must live on the SAME GO as ScoringSystem so ComponentResolver.OnSelf() finds it
            s_ScoringSystem.gameObject.AddComponent<Gameplay.Scoring.ScoreSync>();
            log.Add("  ScoreSync (added to ScoringSystem GO)");

            // MatchEndManager â€” wire _phaseManager + _scoringSystem immediately
            var memGo = AddMgr<Gameplay.Match.MatchEndManager>(parent, "MatchEndManager", log);
            s_MatchEndManager = memGo.GetComponent<Gameplay.Match.MatchEndManager>();
            {
                var so = new SerializedObject(s_MatchEndManager);
                // no _phaseManager needed -- SafeZoneManager handles this
                SetSOObj(so, "_scoringSystem", s_ScoringSystem);
                so.ApplyModifiedProperties();
                log.Add("    MatchEndManager ✅ _scoringSystem wired");
            }

            // â”€â”€ Respawn â€” wire _spawnSystem + _matchEndManager + RespawnConfig SO â”€
            var rsGo = AddMgr<Gameplay.Respawn.RespawnSystem>(parent, "RespawnSystem", log);
            s_RespawnSystem = rsGo.GetComponent<Gameplay.Respawn.RespawnSystem>();
            {
                const string cfgPath = ConfigRoot + "/Gameplay Config/RespawnConfig.asset";
                var respawnCfg = AssetDatabase.LoadAssetAtPath<ScriptableObject>(cfgPath);
                var so = new SerializedObject(s_RespawnSystem);
                SetSOObj(so, "_spawnSystem",    s_SpawnSystem);
                SetSOObj(so, "_matchEndManager",s_MatchEndManager);
                if (respawnCfg != null) SetSOObj(so, "_respawnConfig", respawnCfg);
                so.ApplyModifiedProperties();
                log.Add($"    RespawnSystem âœ… spawnSystem + matchEndManager wired | " +
                        $"respawnConfig:{(respawnCfg != null ? "âœ…" : "âš ï¸ assign")}");
            }

            // â”€â”€ Team / Objective â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var tasGo = AddMgr<Gameplay.Team.TeamAssignmentSystem>(parent, "TeamAssignmentSystem", log);
            s_TeamAssignmentSystem = tasGo.GetComponent<Gameplay.Team.TeamAssignmentSystem>();
            if (s_SpawnSystem != null && s_TeamAssignmentSystem != null)
            {
                var spawnSO = new SerializedObject(s_SpawnSystem);
                SetSOObj(spawnSO, "_teamAssignmentSystem", s_TeamAssignmentSystem);
                spawnSO.ApplyModifiedProperties();
                log.Add("    SpawnSystem._teamAssignmentSystem wired");
            }
            AddMgr<Gameplay.Objective.ObjectiveSystem>(parent,  "ObjectiveSystem",     log);

            // â”€â”€ Other gameplay systems â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            AddMgr<Gameplay.AntiCamping.AntiCampingSystem>(parent,   "AntiCampingSystem",   log);
            AddMgr<Gameplay.Spectator.SpectateManager>(parent,       "SpectateManager",     log);
            AddMgr<Gameplay.ClientEffects.SimpleEffectPool>(parent,         "SimpleEffectPool",    log);

            // â”€â”€ Player registry â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            AddMgr<Networking.Player.PlayerPublicRegistry>(parent, "PlayerPublicRegistry", log);
            AddMgr<Networking.RegistryService>(parent,             "RegistryService",      log);

            // â”€â”€ Networking â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            AddMgr<Networking.NetworkGameManager>(parent, "NetworkGameManager", log);
            // (NetworkGameManager auto-finds NetworkManager at runtime via FindFirstObjectByType)

            BuildServerGameManager(parent, log);

            // â”€â”€ Technical â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            // -- Beacon -- wire _matchEndManager + _phaseManager ---------------
            var beaconGo = AddMgr<Gameplay.Beacon.BeaconManager>(parent, "BeaconManager", log);
            {
                var bmnSO = new SerializedObject(beaconGo.GetComponent<Gameplay.Beacon.BeaconManager>());
                SetSOObj(bmnSO, "_matchEndManager", s_MatchEndManager);
                // no _phaseManager needed
                bmnSO.ApplyModifiedProperties();
                log.Add("    BeaconManager ✅ _matchEndManager wired");
            }

            // -- ProjectilePool -- scene-level object pool for projectiles -----
            AddMgr<Gameplay.Character.Combat.Weapons.ProjectilePool>(parent, "ProjectilePool", log);
            AddMgr<Gameplay.Character.Combat.BulletTargetRegistry>(parent, "BulletTargetRegistry",  log);
            AddMgr<Gameplay.FogOfWar.FogTeamDebugController>(parent,       "FogTeamDebugController",log);
            BuildDamageFeedbackMgr(parent, log);

            // â”€â”€ Objectives â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            BuildObjectives(parent, log);

            // â”€â”€ Test orchestrator â€” wire all known refs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            BuildTestOrchestrator(parent, log);
        }

        private static void BuildServerGameManager(GameObject parent, List<string> log)
        {
            var go   = new GameObject("ServerGameManager"); go.transform.SetParent(parent.transform);
            var comp = go.AddComponent<Networking.ServerGameManager>();
            var so   = new SerializedObject(comp);

            // Prefab assets
            const string ppPath  = PrefabRoot + "/Network_Player Rigidbody Predict.prefab";
            const string chhPath = PrefabRoot + "/Networking/ClientNetworkHandlerPrefab.prefab";
            var pp   = AssetDatabase.LoadAssetAtPath<GameObject>(ppPath);
            var chh  = AssetDatabase.LoadAssetAtPath<GameObject>(chhPath);

            // DebugConfig SO
            const string dbgPath = ConfigRoot + "/NightHuntDebugConfig.asset";
            var dbgCfg = AssetDatabase.LoadAssetAtPath<ScriptableObject>(dbgPath);

            if (pp  != null) SetSOObj(so, "playerPrefab", pp);
            if (s_SpawnSystem   != null) SetSOObj(so, "_spawnSystem",      s_SpawnSystem);
            if (dbgCfg          != null) SetSOObj(so, "_debugConfig",      dbgCfg);
            if (chh != null)
            {
                var cnhComp = chh.GetComponent<Networking.ClientNetworkHandler>();
                if (cnhComp != null) SetSOObj(so, "clientNetworkHandlerPrefab", cnhComp);
            }
            so.ApplyModifiedProperties();

            log.Add($"  ServerGameManager âœ… " +
                    $"playerPrefab:{(pp     != null ? "âœ…" : "âš ï¸")} " +
                    $"spawnSystem:{( s_SpawnSystem  != null ? "âœ…" : "âš ï¸")} " +
                    $"phaseManager: N/A (SafeZoneManager) " +
                    $"debugConfig:{( dbgCfg         != null ? "âœ…" : "âš ï¸")} " +
                    $"clientHandler:{(chh           != null ? "âœ…" : "âš ï¸")}");
        }

        private static void BuildDamageFeedbackMgr(GameObject parent, List<string> log)
        {
            var go = new GameObject("DamageFeedbackSystem_Mgr"); go.transform.SetParent(parent.transform);
            go.AddComponent<Gameplay.Feedback.DamageFeedbackSystem>();
            log.Add("  DamageFeedbackSystem (manager) â€” âš ï¸ assign damageNumberPrefab + hitIndicatorPrefab");
        }

        private static void BuildObjectives(GameObject parent, List<string> log)
        {
            // CaptureZone â€” center
            var cz = new GameObject("CaptureZone_Alpha"); cz.transform.SetParent(parent.transform);
            cz.transform.position = new Vector3(0, 0.5f, 0); SetLayer(cz, "Zone");
            cz.AddComponent<Gameplay.Objective.CaptureZoneObjective>();
            var czc = cz.AddComponent<SphereCollider>(); czc.isTrigger = true; czc.radius = 10f;
            AddVisCylinder(cz, 10f, new Color(1, 1, 0, 0.2f));

            // RadarStation â€” east
            var rd = new GameObject("RadarStation_East"); rd.transform.SetParent(parent.transform);
            rd.transform.position = new Vector3(60, 0.5f, 0);
            rd.AddComponent<Gameplay.Objective.RadarStationObjective>();
            var rdc = rd.AddComponent<BoxCollider>(); rdc.isTrigger = true; rdc.size = new Vector3(5, 3, 5);
            var rdv = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rdv.name = "Visual"; rdv.transform.SetParent(rd.transform);
            rdv.transform.localPosition = new Vector3(0, 3, 0); rdv.transform.localScale = new Vector3(3, 6, 3);
            if (rdv.TryGetComponent<Renderer>(out var rdr)) rdr.sharedMaterial = MakeMat(new Color(0.2f, 0.8f, 1f, 0.6f));
            if (rdv.TryGetComponent<Collider>(out var rdvc)) Object.DestroyImmediate(rdvc);

            // EMPNode â€” west
            var emp = new GameObject("EMPNode_West"); emp.transform.SetParent(parent.transform);
            emp.transform.position = new Vector3(-60, 0.5f, 0);
            emp.AddComponent<Gameplay.Objective.EMPNodeObjective>();
            var empc = emp.AddComponent<SphereCollider>(); empc.isTrigger = false; empc.radius = 2f;
            AddVisCylinder(emp, 3f, new Color(1, 0.5f, 0, 0.5f));

            log.Add("  CaptureZone_Alpha + RadarStation_East + EMPNode_West âœ…");
        }

        private static void AddVisCylinder(GameObject parent, float r, Color c)
        {
            var v = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            v.name = "Visual"; v.transform.SetParent(parent.transform);
            v.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);
            if (v.TryGetComponent<Renderer>(out var rr)) rr.sharedMaterial = MakeMat(c);
            if (v.TryGetComponent<Collider>(out var vc)) Object.DestroyImmediate(vc);
        }

        private static void BuildTestOrchestrator(GameObject parent, List<string> log)
        {
            var go   = new GameObject("[ TestOrchestrator ]"); go.transform.SetParent(parent.transform);
            var comp = go.AddComponent<Gameplay.Core.GameplayTestOrchestrator>();
            var so   = new SerializedObject(comp);
            SetSOObj(so, "_safeZoneManager", s_SafeZoneManager);
            SetSOObj(so, "_scoringSystem",   s_ScoringSystem);
            SetSOObj(so, "_bossSpawnManager",s_BossSpawnManager);
            // _lockdownZone removed -- SafeZoneManager handles zone logic
            so.ApplyModifiedProperties();
            log.Add("  [ TestOrchestrator ] ✅ safeZoneManager + scoringSystem + bossSpawnManager wired");
        }

        // â”€â”€ â‘¨ Minimap Camera â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildMinimapCamera(GameObject parent, List<string> log)
        {
            log.Add("\n[Minimap Camera]");
            const string rtPath = GeneratedDir + "/MinimapRT.renderTexture";
            s_MinimapRT = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            if (s_MinimapRT == null)
            {
                s_MinimapRT = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
                    { name = "MinimapRT", antiAliasing = 1 };
                AssetDatabase.CreateAsset(s_MinimapRT, rtPath);
                AssetDatabase.SaveAssets();
            }

            var camGo = new GameObject("MinimapCamera"); camGo.transform.SetParent(parent.transform);
            camGo.transform.position = new Vector3(0, 150f, 0);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic    = true; cam.orthographicSize = 100f;
            cam.nearClipPlane   = 1f;   cam.farClipPlane     = 300f;
            cam.targetTexture   = s_MinimapRT;
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            cam.cullingMask     = ~(1 << 5); // exclude UI layer
            cam.depth           = -2f;
            s_MinimapCamera    = cam;
            log.Add($"  MinimapCamera + MinimapRT 512Ã—512  â€” âš ï¸ wire into MinimapUI Inspector");
        }

        // â”€â”€ â‘© UI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildUI(GameObject parent, List<string> log)
        {
            log.Add("\n[UI]");

            // Try HUD.prefab first â€” if it exists, internal refs are already set
            const string hudPath = PrefabRoot + "/HUD.prefab";
            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(hudPath);
            if (hudPrefab != null)
            {
                var hud = PrefabUtility.InstantiatePrefab(hudPrefab, parent.transform) as GameObject;
                hud!.name = "HUD";
                log.Add("  âœ… HUD.prefab instantiated â€” all internal refs preserved");
            }
            else
            {
                log.Add($"  HUD.prefab not found at {hudPath} â€” building procedurally");
                BuildHUDWithScripts(parent, log);
            }

            // InventoryScreen_Canvas (hidden, separate canvas â€” UIRootController targets this)
            BuildInventoryCanvas(parent, log);

            // PersistentUICanvas
            var pcGo = new GameObject("PersistentUICanvas"); pcGo.transform.SetParent(parent.transform);
            var pc   = pcGo.AddComponent<Canvas>();
            pc.renderMode = RenderMode.ScreenSpaceOverlay; pc.sortingOrder = 9999;
            pcGo.AddComponent<CanvasScaler>(); pcGo.AddComponent<GraphicRaycaster>();
            foreach (var n in new[] { "ToastContainer", "LoadingOverlay", "ReconnectPopup", "GameModalWindow" })
            {
                var child = AddRectGO(pcGo, n); child.SetActive(false);
            }
            log.Add("  PersistentUICanvas (9999) â€” âš ï¸ attach PersistentUICanvas.cs + DDOL");

            // EventSystem
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem"); es.transform.SetParent(parent.transform);
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                log.Add("  EventSystem âœ…");
            }
        }

        private static void BuildHUDWithScripts(GameObject parent, List<string> log)
        {
            // ── GameHUD_Canvas (1920x1080 ref, ScaleWithScreenSize) ──────────
            var cGo = new GameObject("GameHUD_Canvas");
            cGo.transform.SetParent(parent.transform);
            var canvas = cGo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = cGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;
            cGo.AddComponent<GraphicRaycaster>();
            var gameHUDCtrl = cGo.AddComponent<NightHunt.UI.GameHUDController>();
            var hudSO       = new SerializedObject(gameHUDCtrl);
            log.Add("  GameHUD_Canvas (scale 1920x1080) — GameHUDController.cs");

            // ═══════════════════════════════════════════════════════════════
            // 1 PlayerHUDPanel — bottom-left 280x220, dark BG, RowContainer
            // ═══════════════════════════════════════════════════════════════
            var phGo = MakePanelGO(cGo, "PlayerHUDPanel",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(10f, 10f), new Vector2(280f, 220f),
                new Color(0f, 0f, 0f, 0.55f));
            var phComp = phGo.AddComponent<CanvasGroup>();
            // Header label
            var phHeader   = new GameObject("PlayerLabel"); phHeader.transform.SetParent(phGo.transform, false);
            var phHeaderRT = phHeader.AddComponent<RectTransform>();
            phHeaderRT.anchorMin = new Vector2(0f,1f); phHeaderRT.anchorMax = new Vector2(1f,1f);
            phHeaderRT.pivot = new Vector2(0.5f,1f); phHeaderRT.offsetMin = new Vector2(8f,-26f); phHeaderRT.offsetMax = new Vector2(-8f,0f);
            var phLbl = phHeader.AddComponent<TextMeshProUGUI>();
            phLbl.text = "Player HUD"; phLbl.fontSize = 11f; phLbl.fontStyle = FontStyles.Bold; phLbl.color = new Color(1f,1f,1f,0.6f);
            // RowContainer (VerticalLayoutGroup) — wired to _rowContainer
            var rowCont = new GameObject("RowContainer"); rowCont.transform.SetParent(phGo.transform, false);
            var rowRT = rowCont.AddComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0f,0f); rowRT.anchorMax = new Vector2(1f,1f);
            rowRT.offsetMin = new Vector2(8f,8f); rowRT.offsetMax = new Vector2(-8f,-30f);
            var vlg = rowCont.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5f; vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0,0,2,2);
            // 3 placeholder visual stat bars (Label | BarBg[Fill] | Value)
            AddStatBarRow(rowCont, "Health",  new Color(0.22f,0.76f,0.25f));
            AddStatBarRow(rowCont, "Armor",   new Color(0.27f,0.51f,0.89f));
            AddStatBarRow(rowCont, "Stamina", new Color(0.93f,0.78f,0.15f));
            var phSO = new SerializedObject(phComp);
            SetSOObj(phSO, "_rowContainer", rowCont.transform);
            phSO.ApplyModifiedProperties();
            SetSOObj(hudSO, "playerHUDPanel", phComp);
            log.Add("    PlayerHUDPanel — BG + RowContainer + 3 stat bar rows | assign _statRowPrefab/_statUIConfig");

            // ═══════════════════════════════════════════════════════════════
            // 2 CombatHUDPanel — bottom-right 380x280
            // ═══════════════════════════════════════════════════════════════
            var chGo = MakePanelGO(cGo, "CombatHUDPanel",
                new Vector2(1f,0f), new Vector2(1f,0f), new Vector2(1f,0f),
                new Vector2(-10f,10f), new Vector2(380f,280f),
                new Color(0f,0f,0f,0.45f));
            var chComp = chGo.AddComponent<GameplaySystems.UI.Combat.CombatHUDPanel>();
            var chSO   = new SerializedObject(chComp);
            // AudioSource on panel root
            var aSource = chGo.AddComponent<AudioSource>(); aSource.playOnAwake = false;
            SetSOObj(chSO, "_audioSource", aSource);
            // WeaponSlotsContainer (HorizontalLayoutGroup, top strip)
            var wscGo = new GameObject("WeaponSlotsContainer"); wscGo.transform.SetParent(chGo.transform, false);
            var wscRT = wscGo.AddComponent<RectTransform>();
            wscRT.anchorMin = new Vector2(0f,1f); wscRT.anchorMax = new Vector2(1f,1f);
            wscRT.pivot = new Vector2(0.5f,1f); wscRT.offsetMin = new Vector2(8f,-72f); wscRT.offsetMax = new Vector2(-8f,-8f);
            var hlg = wscGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            for (int i = 0; i < 3; i++)
            {
                var slot = new GameObject($"WeaponSlot_{i+1}"); slot.transform.SetParent(wscGo.transform, false);
                slot.AddComponent<RectTransform>().sizeDelta = new Vector2(74f,56f);
                slot.AddComponent<Image>().color = new Color(0.22f,0.22f,0.22f,0.9f);
                var sn = new GameObject("Num"); sn.transform.SetParent(slot.transform, false);
                var snRT = sn.AddComponent<RectTransform>(); snRT.anchorMin = Vector2.zero; snRT.anchorMax = Vector2.one; snRT.offsetMin = snRT.offsetMax = Vector2.zero;
                var snT = sn.AddComponent<TextMeshProUGUI>(); snT.text = (i+1).ToString(); snT.fontSize = 20f; snT.color = new Color(1f,1f,1f,0.35f); snT.alignment = TextAlignmentOptions.Center;
            }
            SetSOObj(chSO, "_weaponSlotsContainer", wscGo.transform);
            // FireButton (bottom-right of panel, 130x130, red)
            var fbGo = new GameObject("FireButton"); fbGo.transform.SetParent(chGo.transform, false);
            var fbRT = fbGo.AddComponent<RectTransform>();
            fbRT.anchorMin = fbRT.anchorMax = new Vector2(1f,0f); fbRT.pivot = new Vector2(1f,0f);
            fbRT.anchoredPosition = new Vector2(-8f,8f); fbRT.sizeDelta = new Vector2(130f,130f);
            fbGo.AddComponent<Image>().color = new Color(0.85f,0.15f,0.1f,0.85f);
            var fbLblGo = new GameObject("FireLabel"); fbLblGo.transform.SetParent(fbGo.transform, false);
            var fbLblRT = fbLblGo.AddComponent<RectTransform>(); fbLblRT.anchorMin = Vector2.zero; fbLblRT.anchorMax = Vector2.one; fbLblRT.offsetMin = fbLblRT.offsetMax = Vector2.zero;
            var fbLbl = fbLblGo.AddComponent<TextMeshProUGUI>(); fbLbl.text = "FIRE"; fbLbl.fontSize = 18f; fbLbl.fontStyle = FontStyles.Bold; fbLbl.color = Color.white; fbLbl.alignment = TextAlignmentOptions.Center;
            var fbComp = fbGo.AddComponent<GameplaySystems.UI.Combat.FireButton>();
            fbGo.AddComponent<GameplaySystems.UI.Combat.ButtonPulseRing>();
            // FireAimJoystick child (VariableJoystick for aim-while-firing, disabled)
            var fjGo = new GameObject("FireAimJoystick"); fjGo.transform.SetParent(fbGo.transform, false);
            fjGo.AddComponent<RectTransform>(); fjGo.SetActive(false);
            var fjBg = new GameObject("Background"); fjBg.transform.SetParent(fjGo.transform, false);
            var fjBgRT = fjBg.AddComponent<RectTransform>(); fjBgRT.anchorMin = Vector2.zero; fjBgRT.anchorMax = Vector2.one; fjBgRT.offsetMin = fjBgRT.offsetMax = Vector2.zero;
            fjBg.AddComponent<Image>().color = new Color(1f,1f,1f,0.2f);
            var fjHand = new GameObject("Handle"); fjHand.transform.SetParent(fjBg.transform, false);
            var fjHandRT = fjHand.AddComponent<RectTransform>(); fjHandRT.anchorMin = fjHandRT.anchorMax = new Vector2(0.5f,0.5f); fjHandRT.sizeDelta = new Vector2(50f,50f);
            fjHand.AddComponent<Image>().color = new Color(1f,1f,1f,0.5f);
            var fjComp = fjGo.AddComponent<VariableJoystick>();
            var fjSO   = new SerializedObject(fjComp);
            fjSO.FindProperty("background").objectReferenceValue = fjBgRT; fjSO.FindProperty("handle").objectReferenceValue = fjHandRT; fjSO.ApplyModifiedProperties();
            var fbSO = new SerializedObject(fbComp); SetSOObj(fbSO, "_joystick", fjComp); fbSO.ApplyModifiedProperties();
            SetSOObj(chSO, "_fireButton", fbComp);
            // CancelItemUseButton (hidden, center-bottom of panel above fire button)
            var cancelGo = new GameObject("CancelItemUseButton"); cancelGo.transform.SetParent(chGo.transform, false); cancelGo.SetActive(false);
            var cancelRT = cancelGo.AddComponent<RectTransform>();
            cancelRT.anchorMin = cancelRT.anchorMax = new Vector2(0.5f,0f); cancelRT.pivot = new Vector2(0.5f,0f);
            cancelRT.anchoredPosition = new Vector2(0f,148f); cancelRT.sizeDelta = new Vector2(130f,38f);
            cancelGo.AddComponent<Image>().color = new Color(0.8f,0.25f,0.1f,0.9f);
            var cancelBtn = cancelGo.AddComponent<Button>();
            var cancelLbl = new GameObject("Label"); cancelLbl.transform.SetParent(cancelGo.transform, false);
            var cancelLblRT = cancelLbl.AddComponent<RectTransform>(); cancelLblRT.anchorMin = Vector2.zero; cancelLblRT.anchorMax = Vector2.one; cancelLblRT.offsetMin = cancelLblRT.offsetMax = Vector2.zero;
            var cancelTMP = cancelLbl.AddComponent<TextMeshProUGUI>(); cancelTMP.text = "Cancel"; cancelTMP.fontSize = 15f; cancelTMP.color = Color.white; cancelTMP.alignment = TextAlignmentOptions.Center;
            SetSOObj(chSO, "_cancelItemUseButton", cancelBtn);
            chSO.ApplyModifiedProperties();
            SetSOObj(hudSO, "combatHUDPanel", chComp);
            log.Add("    CombatHUDPanel — BG + WeaponSlotsContainer(3 slots) + FireButton + AimJoystick + CancelBtn + AudioSource | assign _inventoryConfig + _weaponSlotPrefab");

            // ═══════════════════════════════════════════════════════════════
            // 3 MatchUI — top strip (full width, height 130)
            // ═══════════════════════════════════════════════════════════════
            var muGo = new GameObject("MatchUI"); muGo.transform.SetParent(cGo.transform, false);
            var muRT = muGo.AddComponent<RectTransform>();
            muRT.anchorMin = new Vector2(0f,1f); muRT.anchorMax = new Vector2(1f,1f); muRT.pivot = new Vector2(0.5f,1f);
            muRT.offsetMin = new Vector2(80f,-132f); muRT.offsetMax = new Vector2(-80f,0f);
            muGo.AddComponent<Image>().color = new Color(0f,0f,0f,0.4f);
            var muComp = muGo.AddComponent<NightHunt.UI.MatchUI>(); var muSO = new SerializedObject(muComp);
            // PhaseText (top-left)
            var phsTxtGo = new GameObject("PhaseText"); phsTxtGo.transform.SetParent(muGo.transform, false);
            var phsTxtRT = phsTxtGo.AddComponent<RectTransform>();
            phsTxtRT.anchorMin = new Vector2(0f,0.6f); phsTxtRT.anchorMax = new Vector2(0.3f,1f); phsTxtRT.offsetMin = new Vector2(12f,0f); phsTxtRT.offsetMax = new Vector2(-4f,-4f);
            var phsTMP = phsTxtGo.AddComponent<TextMeshProUGUI>(); phsTMP.text = "Warmup"; phsTMP.fontSize = 15f; phsTMP.fontStyle = FontStyles.Bold; phsTMP.color = new Color(0.9f,0.85f,0.4f);
            SetSOObj(muSO, "phaseText", phsTMP);
            // TimerText (top-center)
            var tmrTxtGo = new GameObject("TimerText"); tmrTxtGo.transform.SetParent(muGo.transform, false);
            var tmrTxtRT = tmrTxtGo.AddComponent<RectTransform>();
            tmrTxtRT.anchorMin = new Vector2(0.3f,0.55f); tmrTxtRT.anchorMax = new Vector2(0.7f,1f); tmrTxtRT.offsetMin = tmrTxtRT.offsetMax = Vector2.zero;
            var tmrTMP = tmrTxtGo.AddComponent<TextMeshProUGUI>(); tmrTMP.text = "00:30"; tmrTMP.fontSize = 28f; tmrTMP.fontStyle = FontStyles.Bold; tmrTMP.color = Color.white; tmrTMP.alignment = TextAlignmentOptions.Center;
            SetSOObj(muSO, "timerText", tmrTMP);
            // TeamScoreText (center)
            var tsTxtGo = new GameObject("TeamScoreText"); tsTxtGo.transform.SetParent(muGo.transform, false);
            var tsTxtRT = tsTxtGo.AddComponent<RectTransform>();
            tsTxtRT.anchorMin = new Vector2(0.3f,0f); tsTxtRT.anchorMax = new Vector2(0.7f,0.55f); tsTxtRT.offsetMin = tsTxtRT.offsetMax = Vector2.zero;
            var tsTMP = tsTxtGo.AddComponent<TextMeshProUGUI>(); tsTMP.text = "0  —  0"; tsTMP.fontSize = 22f; tsTMP.fontStyle = FontStyles.Bold; tsTMP.color = Color.white; tsTMP.alignment = TextAlignmentOptions.Center;
            SetSOObj(muSO, "teamScoreText", tsTMP);
            // PersonalScoreText (top-right)
            var psTxtGo = new GameObject("PersonalScoreText"); psTxtGo.transform.SetParent(muGo.transform, false);
            var psTxtRT = psTxtGo.AddComponent<RectTransform>();
            psTxtRT.anchorMin = new Vector2(0.7f,0.6f); psTxtRT.anchorMax = new Vector2(1f,1f); psTxtRT.offsetMin = new Vector2(4f,0f); psTxtRT.offsetMax = new Vector2(-12f,-4f);
            var psTMP = psTxtGo.AddComponent<TextMeshProUGUI>(); psTMP.text = "0 pts"; psTMP.fontSize = 13f; psTMP.color = new Color(0.8f,0.9f,0.4f); psTMP.alignment = TextAlignmentOptions.MidlineRight;
            SetSOObj(muSO, "personalScoreText", psTMP);
            // TeamAHeader (bottom-left)
            var taHGo = new GameObject("TeamAHeader"); taHGo.transform.SetParent(muGo.transform, false);
            var taHRT = taHGo.AddComponent<RectTransform>();
            taHRT.anchorMin = new Vector2(0f,0f); taHRT.anchorMax = new Vector2(0.28f,0.55f); taHRT.offsetMin = new Vector2(12f,0f); taHRT.offsetMax = new Vector2(-4f,0f);
            var taHTMP = taHGo.AddComponent<TextMeshProUGUI>(); taHTMP.text = "TEAM A"; taHTMP.fontSize = 12f; taHTMP.fontStyle = FontStyles.Bold; taHTMP.color = new Color(0.4f,0.7f,1f);
            SetSOObj(muSO, "teamAHeaderText", taHTMP);
            // TeamBHeader (bottom-right)
            var tbHGo = new GameObject("TeamBHeader"); tbHGo.transform.SetParent(muGo.transform, false);
            var tbHRT = tbHGo.AddComponent<RectTransform>();
            tbHRT.anchorMin = new Vector2(0.72f,0f); tbHRT.anchorMax = new Vector2(1f,0.55f); tbHRT.offsetMin = new Vector2(4f,0f); tbHRT.offsetMax = new Vector2(-12f,0f);
            var tbHTMP = tbHGo.AddComponent<TextMeshProUGUI>(); tbHTMP.text = "TEAM B"; tbHTMP.fontSize = 12f; tbHTMP.fontStyle = FontStyles.Bold; tbHTMP.color = new Color(1f,0.45f,0.45f); tbHTMP.alignment = TextAlignmentOptions.Right;
            SetSOObj(muSO, "teamBHeaderText", tbHTMP);
            // TeamAList (mid-left panel, independent — anchored to canvas)
            var taListGo = new GameObject("TeamAList"); taListGo.transform.SetParent(cGo.transform, false);
            var taListRT = taListGo.AddComponent<RectTransform>();
            taListRT.anchorMin = new Vector2(0f,0.5f); taListRT.anchorMax = new Vector2(0.18f,0.9f); taListRT.offsetMin = new Vector2(8f,0f); taListRT.offsetMax = Vector2.zero;
            taListGo.AddComponent<Image>().color = new Color(0.1f,0.2f,0.35f,0.55f);
            taListGo.AddComponent<VerticalLayoutGroup>().spacing = 2f;
            SetSOObj(muSO, "teamAListParent", taListGo.transform);
            // TeamBList (mid-right panel)
            var tbListGo = new GameObject("TeamBList"); tbListGo.transform.SetParent(cGo.transform, false);
            var tbListRT = tbListGo.AddComponent<RectTransform>();
            tbListRT.anchorMin = new Vector2(0.82f,0.5f); tbListRT.anchorMax = new Vector2(1f,0.9f); tbListRT.offsetMin = Vector2.zero; tbListRT.offsetMax = new Vector2(-8f,0f);
            tbListGo.AddComponent<Image>().color = new Color(0.35f,0.1f,0.1f,0.55f);
            tbListGo.AddComponent<VerticalLayoutGroup>().spacing = 2f;
            SetSOObj(muSO, "teamBListParent", tbListGo.transform);
            // WarningPanel (fullscreen overlay, hidden)
            var warnGo = new GameObject("WarningPanel"); warnGo.transform.SetParent(cGo.transform, false); warnGo.SetActive(false);
            var warnRT = warnGo.AddComponent<RectTransform>(); warnRT.anchorMin = Vector2.zero; warnRT.anchorMax = Vector2.one; warnRT.offsetMin = warnRT.offsetMax = Vector2.zero;
            var warnBg = warnGo.AddComponent<Image>(); warnBg.color = new Color(0.8f,0.1f,0.1f,0.22f);
            var warnTxtGo = new GameObject("WarningText"); warnTxtGo.transform.SetParent(warnGo.transform, false);
            var warnTxtRT = warnTxtGo.AddComponent<RectTransform>(); warnTxtRT.anchorMin = new Vector2(0f,0.5f); warnTxtRT.anchorMax = new Vector2(1f,0.7f); warnTxtRT.offsetMin = warnTxtRT.offsetMax = Vector2.zero;
            var warnTMP = warnTxtGo.AddComponent<TextMeshProUGUI>(); warnTMP.text = "! WARNING !"; warnTMP.fontSize = 38f; warnTMP.fontStyle = FontStyles.Bold; warnTMP.color = new Color(1f,0.25f,0.1f); warnTMP.alignment = TextAlignmentOptions.Center;
            SetSOObj(muSO, "warningPanel", warnGo); SetSOObj(muSO, "warningText", warnTMP); SetSOObj(muSO, "warningBackground", warnBg);
            muSO.ApplyModifiedProperties(); SetSOObj(hudSO, "matchUI", muComp);
            log.Add("    MatchUI — BG strip + PhaseText + TimerText + TeamScores + TeamHeaders + Lists + WarningPanel | assign teamMemberPrefab");

            // ═══════════════════════════════════════════════════════════════
            // 4 KillFeedUI — top-right below minimap
            // ═══════════════════════════════════════════════════════════════
            var kfGo = new GameObject("KillFeedUI"); kfGo.transform.SetParent(cGo.transform, false);
            var kfRT = kfGo.AddComponent<RectTransform>();
            kfRT.anchorMin = new Vector2(1f,1f); kfRT.anchorMax = new Vector2(1f,1f); kfRT.pivot = new Vector2(1f,1f);
            kfRT.anchoredPosition = new Vector2(-10f,-220f); kfRT.sizeDelta = new Vector2(320f,350f);
            var kfComp = kfGo.AddComponent<NightHunt.UI.KillFeedUI>();
            var kfContGo = new GameObject("KillFeedContainer"); kfContGo.transform.SetParent(kfGo.transform, false);
            var kfContRT = kfContGo.AddComponent<RectTransform>();
            kfContRT.anchorMin = new Vector2(0f,1f); kfContRT.anchorMax = new Vector2(1f,1f); kfContRT.pivot = new Vector2(0.5f,1f); kfContRT.offsetMin = new Vector2(0f,-350f); kfContRT.offsetMax = Vector2.zero;
            var kfVLG = kfContGo.AddComponent<VerticalLayoutGroup>(); kfVLG.spacing = 3f; kfVLG.childAlignment = TextAnchor.UpperRight; kfVLG.childControlWidth = true; kfVLG.childControlHeight = false; kfVLG.childForceExpandWidth = true; kfVLG.childForceExpandHeight = false;
            var kfSO = new SerializedObject(kfComp); SetSOObj(kfSO, "killFeedParent", kfContGo.transform); kfSO.ApplyModifiedProperties();
            SetSOObj(hudSO, "killFeedUI", kfComp);
            log.Add("    KillFeedUI — KillFeedContainer(VLG top-right) | assign killFeedItemPrefab");

            // ═══════════════════════════════════════════════════════════════
            // BossHUDPanel removed.

            // ═══════════════════════════════════════════════════════════════
            // 7 InteractionPromptUI — bottom-center 360x108, hidden
            // ═══════════════════════════════════════════════════════════════
            var ipGo = new GameObject("InteractionPromptUI"); ipGo.transform.SetParent(cGo.transform, false); ipGo.SetActive(false);
            var ipRT = ipGo.AddComponent<RectTransform>(); ipRT.anchorMin = new Vector2(0.5f,0f); ipRT.anchorMax = new Vector2(0.5f,0f); ipRT.pivot = new Vector2(0.5f,0f); ipRT.anchoredPosition = new Vector2(0f,90f); ipRT.sizeDelta = new Vector2(360f,108f);
            ipGo.AddComponent<Image>().color = new Color(0f,0f,0f,0.75f);
            var ipComp = ipGo.AddComponent<GameplaySystems.UI.Interaction.InteractionPromptUI>(); var ipSO = new SerializedObject(ipComp);
            // Action label + key badge + description
            var actionLabelGo = new GameObject("ActionLabelText"); actionLabelGo.transform.SetParent(ipGo.transform, false);
            var actionLabelRT = actionLabelGo.AddComponent<RectTransform>(); actionLabelRT.anchorMin = new Vector2(0f,0.4f); actionLabelRT.anchorMax = new Vector2(0f,1f); actionLabelRT.pivot = new Vector2(0f,0.5f); actionLabelRT.offsetMin = new Vector2(10f,0f); actionLabelRT.offsetMax = new Vector2(74f,0f);
            var actionLabelTMP = actionLabelGo.AddComponent<TextMeshProUGUI>(); actionLabelTMP.text = "Press"; actionLabelTMP.fontSize = 15f; actionLabelTMP.color = Color.white; actionLabelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            SetSOObj(ipSO, "_actionLabelText", actionLabelTMP);

            var keyBadge = new GameObject("KeyBadge"); keyBadge.transform.SetParent(ipGo.transform, false);
            var keyBadgeRT = keyBadge.AddComponent<RectTransform>(); keyBadgeRT.anchorMin = new Vector2(0f,0f); keyBadgeRT.anchorMax = new Vector2(0f,1f); keyBadgeRT.pivot = new Vector2(0f,0.5f); keyBadgeRT.offsetMin = new Vector2(78f,8f); keyBadgeRT.offsetMax = new Vector2(126f,-8f);
            keyBadge.AddComponent<Image>().color = new Color(0.25f,0.25f,0.25f,0.95f);
            var keyTxtGo = new GameObject("KeyText"); keyTxtGo.transform.SetParent(keyBadge.transform, false);
            var keyTxtRT = keyTxtGo.AddComponent<RectTransform>(); keyTxtRT.anchorMin = Vector2.zero; keyTxtRT.anchorMax = Vector2.one; keyTxtRT.offsetMin = keyTxtRT.offsetMax = Vector2.zero;
            var keyTMP = keyTxtGo.AddComponent<TextMeshProUGUI>(); keyTMP.text = "E"; keyTMP.fontSize = 22f; keyTMP.fontStyle = FontStyles.Bold; keyTMP.color = Color.white; keyTMP.alignment = TextAlignmentOptions.Center;
            SetSOObj(ipSO, "_keyText", keyTMP);
            var descGo = new GameObject("DescriptionText"); descGo.transform.SetParent(ipGo.transform, false);
            var descRT = descGo.AddComponent<RectTransform>(); descRT.anchorMin = new Vector2(0f,0.4f); descRT.anchorMax = new Vector2(1f,1f); descRT.offsetMin = new Vector2(138f,0f); descRT.offsetMax = new Vector2(-8f,0f);
            var descTMP = descGo.AddComponent<TextMeshProUGUI>(); descTMP.text = "to interact"; descTMP.fontSize = 15f; descTMP.color = Color.white; descTMP.alignment = TextAlignmentOptions.MidlineLeft;
            SetSOObj(ipSO, "_descriptionText", descTMP);
            // HoldProgressRoot (hidden, bottom strip)
            var holdRoot = new GameObject("HoldProgressRoot"); holdRoot.transform.SetParent(ipGo.transform, false); holdRoot.SetActive(false);
            var holdRT = holdRoot.AddComponent<RectTransform>(); holdRT.anchorMin = new Vector2(0f,0f); holdRT.anchorMax = new Vector2(1f,0.38f); holdRT.offsetMin = new Vector2(10f,4f); holdRT.offsetMax = new Vector2(-10f,0f);
            holdRoot.AddComponent<Image>().color = new Color(0.15f,0.15f,0.15f,0.9f);
            var holdSlider = holdRoot.AddComponent<Slider>(); holdSlider.minValue = 0f; holdSlider.maxValue = 1f; holdSlider.value = 0f;
            SetSOObj(ipSO, "_promptPanel", ipGo); SetSOObj(ipSO, "_holdProgressRoot", holdRoot); SetSOObj(ipSO, "_holdProgressSlider", holdSlider);
            ipSO.ApplyModifiedProperties(); SetSOObj(hudSO, "interactionPromptUI", ipComp);
            log.Add("    InteractionPromptUI — hidden | BG + KeyBadge([E]/text) + ActionText + HoldProgressRoot(Slider)");

            // ═══════════════════════════════════════════════════════════════
            // 8 MinimapUI — top-right corner 200x200, border + RawImage inset
            // ═══════════════════════════════════════════════════════════════
            var mmGo = new GameObject("MinimapUI"); mmGo.transform.SetParent(cGo.transform, false);
            var mmRT = mmGo.AddComponent<RectTransform>();
            mmRT.anchorMin = new Vector2(1f,1f); mmRT.anchorMax = new Vector2(1f,1f); mmRT.pivot = new Vector2(1f,1f);
            mmRT.anchoredPosition = new Vector2(-10f,-10f); mmRT.sizeDelta = new Vector2(200f,200f);
            mmGo.AddComponent<Image>().color = new Color(0.08f,0.08f,0.08f,0.92f); // border frame
            var mmComp = mmGo.AddComponent<GameplaySystems.UI.MinimapUI>(); var mmSO = new SerializedObject(mmComp);
            // RawImage (inset 4px — shows camera output)
            var mmRawGo = new GameObject("MinimapRawImage"); mmRawGo.transform.SetParent(mmGo.transform, false);
            var mmRawRT = mmRawGo.AddComponent<RectTransform>(); mmRawRT.anchorMin = Vector2.zero; mmRawRT.anchorMax = Vector2.one; mmRawRT.offsetMin = new Vector2(4f,4f); mmRawRT.offsetMax = new Vector2(-4f,-4f);
            var mmRaw = mmRawGo.AddComponent<RawImage>(); if (s_MinimapRT != null) mmRaw.texture = s_MinimapRT;
            SetSOObj(mmSO, "_minimapRawImage", mmRaw);
            if (s_MinimapRT != null)     SetSOObj(mmSO, "_minimapTexture",  s_MinimapRT);
            if (s_MinimapCamera != null) SetSOObj(mmSO, "_minimapCamera",   s_MinimapCamera);
            // PlayerIndicator (green dot, center of RawImage)
            var mmPI = new GameObject("PlayerIndicator"); mmPI.transform.SetParent(mmRawGo.transform, false);
            var mmPIRT = mmPI.AddComponent<RectTransform>(); mmPIRT.anchorMin = mmPIRT.anchorMax = new Vector2(0.5f,0.5f); mmPIRT.pivot = new Vector2(0.5f,0.5f); mmPIRT.sizeDelta = new Vector2(10f,10f);
            mmPI.AddComponent<Image>().color = Color.green;
            SetSOObj(mmSO, "_playerIndicator", mmPIRT);
            // ZoneCircle (ring, mostly transparent — overlaid on raw image)
            var mmZC = new GameObject("ZoneCircle"); mmZC.transform.SetParent(mmRawGo.transform, false);
            var mmZCRT = mmZC.AddComponent<RectTransform>(); mmZCRT.anchorMin = mmZCRT.anchorMax = new Vector2(0.5f,0.5f); mmZCRT.pivot = new Vector2(0.5f,0.5f); mmZCRT.sizeDelta = new Vector2(192f,192f);
            mmZC.AddComponent<Image>().color = new Color(1f,0.3f,0.3f,0.12f);
            SetSOObj(mmSO, "_zoneCircleRect", mmZCRT);
            SetSOObj(mmSO, "_minimapPanelRect", mmRT);
            // TeammateDotParent (empty container inside raw image)
            var mmDots = new GameObject("TeammateDotParent"); mmDots.transform.SetParent(mmRawGo.transform, false);
            var mmDotsRT = mmDots.AddComponent<RectTransform>(); mmDotsRT.anchorMin = Vector2.zero; mmDotsRT.anchorMax = Vector2.one; mmDotsRT.offsetMin = mmDotsRT.offsetMax = Vector2.zero;
            SetSOObj(mmSO, "_teammateDotParent", mmDotsRT);
            mmSO.ApplyModifiedProperties(); SetSOObj(hudSO, "minimapUI", mmComp);
            log.Add("    MinimapUI — BorderBG + RawImage(inset4px) + PlayerIndicator + ZoneCircle + TeammateDotParent | assign _minimapCamera if not auto-linked");

            // ═══════════════════════════════════════════════════════════════
            // 9 DeathScreen — fullscreen root(active) + DeathPanel child(hidden)
            // ═══════════════════════════════════════════════════════════════
            var dsGo = new GameObject("DeathScreen"); dsGo.transform.SetParent(cGo.transform, false);
            var dsRootRT = dsGo.AddComponent<RectTransform>(); dsRootRT.anchorMin = Vector2.zero; dsRootRT.anchorMax = Vector2.one; dsRootRT.offsetMin = dsRootRT.offsetMax = Vector2.zero;
            // DeathScreen script stays active so RegisterPlayer() can be called before death
            var dsComp = dsGo.AddComponent<NightHunt.UI.DeathScreen>(); var dsSO = new SerializedObject(dsComp);
            // DeathPanel child (fullscreen overlay, hidden until player dies)
            var dpGo = new GameObject("DeathPanel"); dpGo.transform.SetParent(dsGo.transform, false); dpGo.SetActive(false);
            var dpRT = dpGo.AddComponent<RectTransform>(); dpRT.anchorMin = Vector2.zero; dpRT.anchorMax = Vector2.one; dpRT.offsetMin = dpRT.offsetMax = Vector2.zero;
            dpGo.AddComponent<Image>().color = new Color(0f,0f,0f,0.8f); // dark full overlay
            SetSOObj(dsSO, "_deathPanel", dpGo);
            // Content card (center 480x380)
            var dCard = new GameObject("ContentCard"); dCard.transform.SetParent(dpGo.transform, false);
            var dCardRT = dCard.AddComponent<RectTransform>(); dCardRT.anchorMin = dCardRT.anchorMax = new Vector2(0.5f,0.5f); dCardRT.pivot = new Vector2(0.5f,0.5f); dCardRT.anchoredPosition = Vector2.zero; dCardRT.sizeDelta = new Vector2(480f,380f);
            dCard.AddComponent<Image>().color = new Color(0.07f,0.04f,0.04f,0.97f);
            // "YOU DIED" title
            var ydGo = new GameObject("YouDiedTitle"); ydGo.transform.SetParent(dCard.transform, false);
            var ydRT = ydGo.AddComponent<RectTransform>(); ydRT.anchorMin = new Vector2(0f,0.72f); ydRT.anchorMax = new Vector2(1f,1f); ydRT.offsetMin = new Vector2(18f,-8f); ydRT.offsetMax = new Vector2(-18f,-10f);
            var ydTMP = ydGo.AddComponent<TextMeshProUGUI>(); ydTMP.text = "YOU DIED"; ydTMP.fontSize = 42f; ydTMP.fontStyle = FontStyles.Bold; ydTMP.color = new Color(0.85f,0.1f,0.1f); ydTMP.alignment = TextAlignmentOptions.Center;
            // KilledByText
            var kbGo = new GameObject("KilledByText"); kbGo.transform.SetParent(dCard.transform, false);
            var kbRT = kbGo.AddComponent<RectTransform>(); kbRT.anchorMin = new Vector2(0f,0.52f); kbRT.anchorMax = new Vector2(1f,0.72f); kbRT.offsetMin = new Vector2(18f,0f); kbRT.offsetMax = new Vector2(-18f,0f);
            var kbTMP = kbGo.AddComponent<TextMeshProUGUI>(); kbTMP.text = "Killed by: —"; kbTMP.fontSize = 16f; kbTMP.color = new Color(0.85f,0.6f,0.6f); kbTMP.alignment = TextAlignmentOptions.Center;
            SetSOObj(dsSO, "_killedByText", kbTMP);
            // RespawnTimerText
            var rtGo = new GameObject("RespawnTimerText"); rtGo.transform.SetParent(dCard.transform, false);
            var rtRT = rtGo.AddComponent<RectTransform>(); rtRT.anchorMin = new Vector2(0f,0.35f); rtRT.anchorMax = new Vector2(1f,0.52f); rtRT.offsetMin = new Vector2(18f,0f); rtRT.offsetMax = new Vector2(-18f,0f);
            var rtTMP = rtGo.AddComponent<TextMeshProUGUI>(); rtTMP.text = "Respawn in 5..."; rtTMP.fontSize = 20f; rtTMP.color = new Color(0.7f,0.85f,1f); rtTMP.alignment = TextAlignmentOptions.Center;
            SetSOObj(dsSO, "_respawnTimerText", rtTMP);
            // Button row
            var btnRow = new GameObject("ButtonRow"); btnRow.transform.SetParent(dCard.transform, false);
            var btnRowRT = btnRow.AddComponent<RectTransform>(); btnRowRT.anchorMin = new Vector2(0f,0.04f); btnRowRT.anchorMax = new Vector2(1f,0.33f); btnRowRT.offsetMin = new Vector2(30f,0f); btnRowRT.offsetMax = new Vector2(-30f,0f);
            var btnHLG = btnRow.AddComponent<HorizontalLayoutGroup>(); btnHLG.spacing = 20f; btnHLG.childAlignment = TextAnchor.MiddleCenter; btnHLG.childControlWidth = true; btnHLG.childControlHeight = true; btnHLG.childForceExpandWidth = true; btnHLG.childForceExpandHeight = true;
            SetSOObj(dsSO, "_spectateButton", MakeTextBtn(btnRow, "SpectateButton", "Spectate", new Color(0.2f,0.3f,0.45f,0.9f)));
            SetSOObj(dsSO, "_respawnButton",  MakeTextBtn(btnRow, "RespawnButton",  "Respawn",  new Color(0.2f,0.45f,0.25f,0.9f)));
            dsSO.ApplyModifiedProperties(); SetSOObj(hudSO, "deathScreen", dsComp);
            log.Add("    DeathScreen — root(active) | DeathPanel(hidden,fullscreen overlay) > ContentCard > YouDied/KilledBy/Timer/SpectateBtn/RespawnBtn");

            // ═══════════════════════════════════════════════════════════════
            // 10 LootContainerUI — center-screen popup, hidden
            // ═══════════════════════════════════════════════════════════════
            var lcGo = new GameObject("LootContainerUI"); lcGo.transform.SetParent(cGo.transform, false);
            var lcRootRT = lcGo.AddComponent<RectTransform>(); lcRootRT.anchorMin = lcRootRT.anchorMax = new Vector2(0.5f,0.5f); lcRootRT.pivot = new Vector2(0.5f,0.5f); lcRootRT.anchoredPosition = Vector2.zero; lcRootRT.sizeDelta = new Vector2(480f,560f);
            var lcComp = lcGo.AddComponent<GameplaySystems.UI.LootContainerUI>(); var lcSO = new SerializedObject(lcComp);
            // ContainerPanel (visual child, hidden — LootContainerUI toggles this)
            var lcPanel = new GameObject("ContainerPanel"); lcPanel.transform.SetParent(lcGo.transform, false); lcPanel.SetActive(false);
            var lcPanelRT = lcPanel.AddComponent<RectTransform>(); lcPanelRT.anchorMin = Vector2.zero; lcPanelRT.anchorMax = Vector2.one; lcPanelRT.offsetMin = lcPanelRT.offsetMax = Vector2.zero;
            lcPanel.AddComponent<Image>().color = new Color(0.08f,0.08f,0.1f,0.97f);
            var lcCG = lcPanel.AddComponent<CanvasGroup>();
            SetSOObj(lcSO, "_containerPanel", lcPanel); SetSOObj(lcSO, "_canvasGroup", lcCG);
            // TitleBar
            var lcTitle = new GameObject("TitleBar"); lcTitle.transform.SetParent(lcPanel.transform, false);
            var lcTitleRT = lcTitle.AddComponent<RectTransform>(); lcTitleRT.anchorMin = new Vector2(0f,1f); lcTitleRT.anchorMax = new Vector2(1f,1f); lcTitleRT.pivot = new Vector2(0.5f,1f); lcTitleRT.offsetMin = new Vector2(0f,-50f); lcTitleRT.offsetMax = Vector2.zero;
            lcTitle.AddComponent<Image>().color = new Color(0.13f,0.13f,0.16f,1f);
            // ContainerNameText
            var lcName = new GameObject("ContainerNameText"); lcName.transform.SetParent(lcTitle.transform, false);
            var lcNameRT = lcName.AddComponent<RectTransform>(); lcNameRT.anchorMin = new Vector2(0f,0f); lcNameRT.anchorMax = new Vector2(0.78f,1f); lcNameRT.offsetMin = new Vector2(14f,0f); lcNameRT.offsetMax = Vector2.zero;
            var lcNameTMP = lcName.AddComponent<TextMeshProUGUI>(); lcNameTMP.text = "Container"; lcNameTMP.fontSize = 17f; lcNameTMP.fontStyle = FontStyles.Bold; lcNameTMP.color = Color.white; lcNameTMP.alignment = TextAlignmentOptions.MidlineLeft;
            SetSOObj(lcSO, "_containerNameText", lcNameTMP);
            // CloseButton (top-right of title bar)
            var lcClose = new GameObject("CloseButton"); lcClose.transform.SetParent(lcTitle.transform, false);
            var lcCloseRT = lcClose.AddComponent<RectTransform>(); lcCloseRT.anchorMin = new Vector2(1f,0f); lcCloseRT.anchorMax = new Vector2(1f,1f); lcCloseRT.pivot = new Vector2(1f,0.5f); lcCloseRT.offsetMin = new Vector2(-50f,4f); lcCloseRT.offsetMax = new Vector2(-4f,-4f);
            lcClose.AddComponent<Image>().color = new Color(0.6f,0.15f,0.15f,0.9f);
            var lcCloseBtn = lcClose.AddComponent<Button>();
            var lcXGo = new GameObject("X"); lcXGo.transform.SetParent(lcClose.transform, false);
            var lcXRT = lcXGo.AddComponent<RectTransform>(); lcXRT.anchorMin = Vector2.zero; lcXRT.anchorMax = Vector2.one; lcXRT.offsetMin = lcXRT.offsetMax = Vector2.zero;
            var lcXTMP = lcXGo.AddComponent<TextMeshProUGUI>(); lcXTMP.text = "X"; lcXTMP.fontSize = 18f; lcXTMP.fontStyle = FontStyles.Bold; lcXTMP.color = Color.white; lcXTMP.alignment = TextAlignmentOptions.Center;
            SetSOObj(lcSO, "_closeButton", lcCloseBtn);
            // SlotsParent (scrollable content area)
            var lcSlots = new GameObject("SlotsParent"); lcSlots.transform.SetParent(lcPanel.transform, false);
            var lcSlotsRT = lcSlots.AddComponent<RectTransform>(); lcSlotsRT.anchorMin = new Vector2(0f,0f); lcSlotsRT.anchorMax = new Vector2(1f,1f); lcSlotsRT.offsetMin = new Vector2(8f,58f); lcSlotsRT.offsetMax = new Vector2(-8f,-58f);
            var lcVLG = lcSlots.AddComponent<VerticalLayoutGroup>(); lcVLG.spacing = 4f; lcVLG.childControlWidth = true; lcVLG.childControlHeight = false; lcVLG.childForceExpandWidth = true; lcVLG.childForceExpandHeight = false;
            SetSOObj(lcSO, "_slotsParent", lcSlots.transform);
            // Bottom bar with TakeAll button
            var lcBottom = new GameObject("BottomBar"); lcBottom.transform.SetParent(lcPanel.transform, false);
            var lcBottomRT = lcBottom.AddComponent<RectTransform>(); lcBottomRT.anchorMin = new Vector2(0f,0f); lcBottomRT.anchorMax = new Vector2(1f,0f); lcBottomRT.pivot = new Vector2(0.5f,0f); lcBottomRT.offsetMin = new Vector2(8f,8f); lcBottomRT.offsetMax = new Vector2(-8f,52f);
            SetSOObj(lcSO, "_takeAllButton", MakeTextBtn(lcBottom, "TakeAllButton", "Take All", new Color(0.18f,0.42f,0.18f,0.9f)));
            // Wire debugConfig SO (also needed by LootContainerUI)
            var dbgCfg2 = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigRoot + "/NightHuntDebugConfig.asset");
            if (dbgCfg2 != null) SetSOObj(lcSO, "_debugConfig", dbgCfg2);
            lcSO.ApplyModifiedProperties(); SetSOObj(hudSO, "lootContainerUI", lcComp);
            log.Add("    LootContainerUI — center popup | ContainerPanel(hidden) > TitleBar(Name+X) + SlotsParent(VLG) + TakeAllBtn | assign _itemRowPrefab");

            // ── DamageFeedbackSystem (HUD canvas instance) ─────────────────
            var dfGo = new GameObject("DamageFeedbackSystem"); dfGo.transform.SetParent(cGo.transform, false);
            dfGo.AddComponent<RectTransform>();
            var dfComp = dfGo.AddComponent<Gameplay.Feedback.DamageFeedbackSystem>();
            SetSOObj(hudSO, "damageFeedback", dfComp);
            log.Add("    DamageFeedbackSystem | assign damageNumberPrefab + hitIndicatorPrefab");

            // ── MoveJoystick (mobile movement, bottom-left) ────────────────
            var mjGo = new GameObject("[MoveJoystick]"); mjGo.transform.SetParent(cGo.transform, false);
            var mjRT = mjGo.AddComponent<RectTransform>(); mjRT.anchorMin = Vector2.zero; mjRT.anchorMax = Vector2.zero; mjRT.pivot = Vector2.zero; mjRT.anchoredPosition = new Vector2(20f,20f); mjRT.sizeDelta = new Vector2(200f,200f);
            var mjBg = new GameObject("Background"); mjBg.transform.SetParent(mjGo.transform, false);
            var mjBgRT = mjBg.AddComponent<RectTransform>(); mjBgRT.anchorMin = Vector2.zero; mjBgRT.anchorMax = Vector2.one; mjBgRT.offsetMin = mjBgRT.offsetMax = Vector2.zero;
            mjBg.AddComponent<Image>().color = new Color(1f,1f,1f,0.12f);
            var mjHand = new GameObject("Handle"); mjHand.transform.SetParent(mjBg.transform, false);
            var mjHandRT = mjHand.AddComponent<RectTransform>(); mjHandRT.anchorMin = mjHandRT.anchorMax = new Vector2(0.5f,0.5f); mjHandRT.pivot = new Vector2(0.5f,0.5f); mjHandRT.sizeDelta = new Vector2(70f,70f);
            mjHand.AddComponent<Image>().color = new Color(1f,1f,1f,0.5f);
            var mjComp = mjGo.AddComponent<FixedJoystick>(); var mjSO = new SerializedObject(mjComp);
            mjSO.FindProperty("background").objectReferenceValue = mjBgRT; mjSO.FindProperty("handle").objectReferenceValue = mjHandRT; mjSO.ApplyModifiedProperties();
            mjGo.AddComponent<Gameplay.Input.Handlers.Movement.MobileMovementBridge>();
            log.Add("    [MoveJoystick] FixedJoystick + MobileMovementBridge | MANUAL: wire FixedJoystick → MobileHUDPanel._joystick in Inspector");
            // ── ThrowableAimController (fullscreen canvas overlay – throwable aim) ─
            var iacGo = new GameObject("ThrowableAimController"); iacGo.transform.SetParent(cGo.transform, false);
            var iacRT = iacGo.AddComponent<RectTransform>(); iacRT.anchorMin = Vector2.zero; iacRT.anchorMax = Vector2.one; iacRT.offsetMin = iacRT.offsetMax = Vector2.zero;
            iacGo.AddComponent<Image>().color = new Color(0f,0f,0f,0f);
            var iacCG = iacGo.AddComponent<CanvasGroup>(); iacCG.alpha = 0f; iacCG.blocksRaycasts = false; iacCG.interactable = false;
            var aimCursorGo = new GameObject("AimCursor"); aimCursorGo.transform.SetParent(iacGo.transform, false);
            var aimCursorRT = aimCursorGo.AddComponent<RectTransform>(); aimCursorRT.anchorMin = aimCursorRT.anchorMax = new Vector2(0.5f,0.5f); aimCursorRT.pivot = new Vector2(0.5f,0.5f); aimCursorRT.sizeDelta = new Vector2(32f,32f);
            aimCursorGo.AddComponent<Image>().color = new Color(1f,0.85f,0.2f,0.9f);
            var iacComp = iacGo.AddComponent<GameplaySystems.UI.Combat.ThrowableAimController>();
            var iacSO   = new SerializedObject(iacComp); SetSOObj(iacSO, "_aimCursor", aimCursorGo.transform); iacSO.ApplyModifiedProperties();
            var chSOAim = new SerializedObject(chComp);  SetSOObj(chSOAim, "_aimController", iacComp);        chSOAim.ApplyModifiedProperties();
            log.Add("    ThrowableAimController -- fullscreen overlay + AimCursor wired to CombatHUDPanel._aimController");

            // ── FilterPanels (Consumable + Throwable quick-select inside CombatHUDPanel) ─
            var fpRoot = new GameObject("FilterPanelsRoot"); fpRoot.transform.SetParent(chGo.transform, false);
            var fpRootRT = fpRoot.AddComponent<RectTransform>();
            fpRootRT.anchorMin = Vector2.zero; fpRootRT.anchorMax = new Vector2(0f,1f);
            fpRootRT.pivot     = Vector2.zero;
            fpRootRT.offsetMin = new Vector2(8f,8f); fpRootRT.offsetMax = new Vector2(158f,-80f);
            var fpRootVLG = fpRoot.AddComponent<VerticalLayoutGroup>(); fpRootVLG.spacing = 6f; fpRootVLG.childControlWidth = true; fpRootVLG.childControlHeight = false; fpRootVLG.childForceExpandWidth = true; fpRootVLG.childForceExpandHeight = false;
            string[] fpLabels   = { "Consumable", "Throwable" };
            int[]    fpTypeInts = { (int)GameplaySystems.Core.Data.ItemType.Consumable, (int)GameplaySystems.Core.Data.ItemType.Throwable };
            var fpComponents = new GameplaySystems.UI.Combat.ItemFilterPanel[2];
            for (int fi = 0; fi < 2; fi++)
            {
                var fpGo = new GameObject($"FilterPanel_{fpLabels[fi]}"); fpGo.transform.SetParent(fpRoot.transform, false);
                fpGo.AddComponent<Image>().color = new Color(0.14f,0.14f,0.17f,0.92f);
                var fpLE = fpGo.AddComponent<LayoutElement>(); fpLE.preferredHeight = 58f; fpLE.minHeight = 58f;
                // ExpandButton (top half)
                var fpExpGo = new GameObject("ExpandButton"); fpExpGo.transform.SetParent(fpGo.transform, false);
                var fpExpRT = fpExpGo.AddComponent<RectTransform>(); fpExpRT.anchorMin = new Vector2(0f,0.52f); fpExpRT.anchorMax = new Vector2(1f,1f); fpExpRT.offsetMin = new Vector2(2f,2f); fpExpRT.offsetMax = new Vector2(-2f,-2f);
                fpExpGo.AddComponent<Image>().color = new Color(0.22f,0.22f,0.28f,0.9f);
                var fpExpBtn = fpExpGo.AddComponent<Button>();
                var fpExpLblGo = new GameObject("Label"); fpExpLblGo.transform.SetParent(fpExpGo.transform, false);
                var fpExpLblRT = fpExpLblGo.AddComponent<RectTransform>(); fpExpLblRT.anchorMin = Vector2.zero; fpExpLblRT.anchorMax = Vector2.one; fpExpLblRT.offsetMin = fpExpLblRT.offsetMax = Vector2.zero;
                var fpExpTMP = fpExpLblGo.AddComponent<TextMeshProUGUI>(); fpExpTMP.text = fpLabels[fi]; fpExpTMP.fontSize = 11f; fpExpTMP.color = new Color(0.9f,0.9f,0.9f); fpExpTMP.alignment = TextAlignmentOptions.Center;
                // SlotButton (bottom half – selected item)
                var fpSlotGo = new GameObject("SlotButton"); fpSlotGo.transform.SetParent(fpGo.transform, false);
                var fpSlotRT  = fpSlotGo.AddComponent<RectTransform>(); fpSlotRT.anchorMin = new Vector2(0f,0f); fpSlotRT.anchorMax = new Vector2(1f,0.5f); fpSlotRT.offsetMin = new Vector2(2f,2f); fpSlotRT.offsetMax = new Vector2(-2f,-2f);
                fpSlotGo.AddComponent<Image>().color = new Color(0.12f,0.12f,0.16f,1f);
                fpSlotGo.AddComponent<Button>();
                var fpIconGo  = new GameObject("Icon");         fpIconGo.transform.SetParent(fpSlotGo.transform, false);
                var fpIconRT  = fpIconGo.AddComponent<RectTransform>(); fpIconRT.anchorMin = new Vector2(0.05f,0.05f); fpIconRT.anchorMax = new Vector2(0.85f,0.95f); fpIconRT.offsetMin = fpIconRT.offsetMax = Vector2.zero;
                var fpIconImg = fpIconGo.AddComponent<Image>(); fpIconImg.color = new Color(1f,1f,1f,0.18f);
                var fpQtyGo   = new GameObject("QuantityText"); fpQtyGo.transform.SetParent(fpSlotGo.transform, false);
                var fpQtyRT   = fpQtyGo.AddComponent<RectTransform>(); fpQtyRT.anchorMin = new Vector2(0.5f,0f); fpQtyRT.anchorMax = Vector2.one; fpQtyRT.offsetMin = fpQtyRT.offsetMax = Vector2.zero;
                var fpQtyTMP  = fpQtyGo.AddComponent<TextMeshProUGUI>(); fpQtyTMP.text = "0"; fpQtyTMP.fontSize = 10f; fpQtyTMP.color = Color.white; fpQtyTMP.alignment = TextAlignmentOptions.BottomRight;
                var fpEmptyGo = new GameObject("EmptyIndicator"); fpEmptyGo.transform.SetParent(fpSlotGo.transform, false);
                var fpEmptyRT = fpEmptyGo.AddComponent<RectTransform>(); fpEmptyRT.anchorMin = Vector2.zero; fpEmptyRT.anchorMax = Vector2.one; fpEmptyRT.offsetMin = fpEmptyRT.offsetMax = Vector2.zero;
                fpEmptyGo.AddComponent<Image>().color = new Color(0.3f,0.3f,0.3f,0.5f); fpEmptyGo.SetActive(false);
                var fpSlotComp = fpSlotGo.AddComponent<GameplaySystems.UI.Combat.SelectableItemButton>();
                var fpSlotSO   = new SerializedObject(fpSlotComp);
                SetSOObj(fpSlotSO, "_icon", fpIconImg); SetSOObj(fpSlotSO, "_quantityText", fpQtyTMP); SetSOObj(fpSlotSO, "_emptyIndicator", fpEmptyGo); fpSlotSO.ApplyModifiedProperties();
                // ListRoot (collapsed dropdown)
                var fpListGo    = new GameObject("ListRoot"); fpListGo.transform.SetParent(fpGo.transform, false); fpListGo.SetActive(false);
                var fpListRT    = fpListGo.AddComponent<RectTransform>(); fpListRT.anchorMin = new Vector2(0f,1f); fpListRT.anchorMax = new Vector2(1f,1f); fpListRT.pivot = new Vector2(0f,0f); fpListRT.offsetMin = new Vector2(0f,0f); fpListRT.offsetMax = new Vector2(0f,120f);
                fpListGo.AddComponent<Image>().color = new Color(0.1f,0.1f,0.13f,0.97f);
                var fpContentGo = new GameObject("ContentRoot"); fpContentGo.transform.SetParent(fpListGo.transform, false);
                var fpContentRT = fpContentGo.AddComponent<RectTransform>(); fpContentRT.anchorMin = Vector2.zero; fpContentRT.anchorMax = Vector2.one; fpContentRT.offsetMin = new Vector2(4f,4f); fpContentRT.offsetMax = new Vector2(-4f,-4f);
                fpContentGo.AddComponent<VerticalLayoutGroup>().spacing = 3f;
                var fpComp = fpGo.AddComponent<GameplaySystems.UI.Combat.ItemFilterPanel>();
                var fpSO   = new SerializedObject(fpComp);
                SetSOObj(fpSO, "_expandButton", fpExpBtn);
                SetSOObj(fpSO, "_slotButton",   fpSlotComp);
                SetSOObj(fpSO, "_listRoot",     fpListGo);
                SetSOObj(fpSO, "_contentRoot",  fpContentGo.transform);
                fpSO.ApplyModifiedProperties();
                fpComponents[fi] = fpComp;
            }
            // Wire _filterPanels array on CombatHUDPanel
            var chSOFP = new SerializedObject(chComp);
            var fpArr  = chSOFP.FindProperty("_filterPanels");
            fpArr.arraySize = 2;
            for (int fi = 0; fi < 2; fi++)
            {
                var el = fpArr.GetArrayElementAtIndex(fi);
                el.FindPropertyRelative("FilterType").intValue         = fpTypeInts[fi];
                el.FindPropertyRelative("Panel").objectReferenceValue = fpComponents[fi];
            }
            chSOFP.ApplyModifiedProperties();
            log.Add("    FilterPanelsRoot -- Consumable + Throwable FilterPanel instances wired to _filterPanels[2]");

            // ── OpenInventoryButton (bottom-left, right of joystick 230x20) ─
            var invBtnGo = new GameObject("OpenInventoryButton"); invBtnGo.transform.SetParent(cGo.transform, false);
            var invBtnRT = invBtnGo.AddComponent<RectTransform>();
            invBtnRT.anchorMin = invBtnRT.anchorMax = Vector2.zero; invBtnRT.pivot = Vector2.zero;
            invBtnRT.anchoredPosition = new Vector2(230f,20f); invBtnRT.sizeDelta = new Vector2(68f,68f);
            invBtnGo.AddComponent<Image>().color = new Color(0.15f,0.15f,0.2f,0.85f);
            invBtnGo.AddComponent<Button>();
            var invBtnLblGo = new GameObject("Label"); invBtnLblGo.transform.SetParent(invBtnGo.transform, false);
            var invBtnLblRT = invBtnLblGo.AddComponent<RectTransform>(); invBtnLblRT.anchorMin = Vector2.zero; invBtnLblRT.anchorMax = Vector2.one; invBtnLblRT.offsetMin = invBtnLblRT.offsetMax = Vector2.zero;
            var invBtnTMP = invBtnLblGo.AddComponent<TextMeshProUGUI>(); invBtnTMP.text = "INV"; invBtnTMP.fontSize = 14f; invBtnTMP.fontStyle = FontStyles.Bold; invBtnTMP.color = Color.white; invBtnTMP.alignment = TextAlignmentOptions.Center;
            log.Add("    OpenInventoryButton 68x68 bottom-left -- wire onClick to GameHUDController.ToggleInventory()");

            // ── Wire GameHUDController sub-panels ────────────────────────────────────
            SetSOObj(hudSO, "_playerHUDPanel", phComp);
            hudSO.ApplyModifiedProperties();
            log.Add("  GameHUD_Canvas — GameHUDController wired (run wizard ④ to auto-wire all refs)");
        }
            private static GameObject s_InvCanvas;           // kept to wire back into GameHUDController

        private static void BuildInventoryCanvas(GameObject parent, List<string> log)
        {
            var cGo = new GameObject("InventoryScreen_Canvas"); cGo.transform.SetParent(parent.transform);
            cGo.SetActive(false);
            var canvas = cGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1;
            var invScaler = cGo.AddComponent<CanvasScaler>();
            invScaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            invScaler.referenceResolution = new Vector2(1920f,1080f);
            invScaler.matchWidthOrHeight  = 0.5f;
            cGo.AddComponent<GraphicRaycaster>();

            // Full-screen dark background panel
            var invPanel = new GameObject("InventoryPanel"); invPanel.transform.SetParent(cGo.transform, false);
            var invPanelRT = invPanel.AddComponent<RectTransform>(); invPanelRT.anchorMin = Vector2.zero; invPanelRT.anchorMax = Vector2.one; invPanelRT.offsetMin = invPanelRT.offsetMax = Vector2.zero;
            invPanel.AddComponent<Image>().color = new Color(0.05f,0.05f,0.07f,0.97f);

            // Title bar (top 50px)
            var titleBar = new GameObject("TitleBar"); titleBar.transform.SetParent(invPanel.transform, false);
            var titleBarRT = titleBar.AddComponent<RectTransform>(); titleBarRT.anchorMin = new Vector2(0f,1f); titleBarRT.anchorMax = new Vector2(1f,1f); titleBarRT.pivot = new Vector2(0.5f,1f); titleBarRT.offsetMin = new Vector2(0f,-50f); titleBarRT.offsetMax = Vector2.zero;
            titleBar.AddComponent<Image>().color = new Color(0.1f,0.1f,0.14f,1f);
            var titleTxtGo = new GameObject("TitleText"); titleTxtGo.transform.SetParent(titleBar.transform, false);
            var titleTxtRT = titleTxtGo.AddComponent<RectTransform>(); titleTxtRT.anchorMin = Vector2.zero; titleTxtRT.anchorMax = Vector2.one; titleTxtRT.offsetMin = new Vector2(20f,0f); titleTxtRT.offsetMax = Vector2.zero;
            var titleTMP = titleTxtGo.AddComponent<TextMeshProUGUI>(); titleTMP.text = "INVENTORY"; titleTMP.fontSize = 22f; titleTMP.fontStyle = FontStyles.Bold; titleTMP.color = Color.white; titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            var closeBtnInv = MakeTextBtn(titleBar, "CloseButton", "X", new Color(0.6f,0.15f,0.15f,0.9f));
            var closeBtnInvRT = closeBtnInv.GetComponent<RectTransform>(); closeBtnInvRT.anchorMin = new Vector2(1f,0f); closeBtnInvRT.anchorMax = new Vector2(1f,1f); closeBtnInvRT.pivot = new Vector2(1f,0.5f); closeBtnInvRT.offsetMin = new Vector2(-50f,4f); closeBtnInvRT.offsetMax = new Vector2(-4f,-4f);

            // Body: 3-column HLG (Equipment | Grid | Weapon+Stats)
            var body = new GameObject("Body"); body.transform.SetParent(invPanel.transform, false);
            var bodyRT = body.AddComponent<RectTransform>(); bodyRT.anchorMin = Vector2.zero; bodyRT.anchorMax = Vector2.one; bodyRT.offsetMin = new Vector2(0f,4f); bodyRT.offsetMax = new Vector2(0f,-54f);
            var bodyHLG = body.AddComponent<HorizontalLayoutGroup>(); bodyHLG.spacing = 3f; bodyHLG.childControlWidth = false; bodyHLG.childControlHeight = true; bodyHLG.childForceExpandWidth = false; bodyHLG.childForceExpandHeight = true; bodyHLG.padding = new RectOffset(4,4,4,4);

            // --- LEFT: Equipment slots (280px) ---
            var eqPanel = new GameObject("EquipmentPanel"); eqPanel.transform.SetParent(body.transform, false);
            var eqPanelLE = eqPanel.AddComponent<LayoutElement>(); eqPanelLE.preferredWidth = 280f; eqPanelLE.minWidth = 280f;
            eqPanel.AddComponent<Image>().color = new Color(0.08f,0.08f,0.11f,1f);
            var eqLblGo = new GameObject("EquipLabel"); eqLblGo.transform.SetParent(eqPanel.transform, false);
            var eqLblRT = eqLblGo.AddComponent<RectTransform>(); eqLblRT.anchorMin = new Vector2(0f,1f); eqLblRT.anchorMax = new Vector2(1f,1f); eqLblRT.pivot = new Vector2(0.5f,1f); eqLblRT.offsetMin = new Vector2(8f,-26f); eqLblRT.offsetMax = new Vector2(-8f,0f);
            var eqLblTMP = eqLblGo.AddComponent<TextMeshProUGUI>(); eqLblTMP.text = "EQUIPMENT"; eqLblTMP.fontSize = 12f; eqLblTMP.fontStyle = FontStyles.Bold; eqLblTMP.color = new Color(0.6f,0.8f,1f); eqLblTMP.alignment = TextAlignmentOptions.Center;
            var eqRoot = new GameObject("EquipmentRoot"); eqRoot.transform.SetParent(eqPanel.transform, false);
            var eqRootRT = eqRoot.AddComponent<RectTransform>(); eqRootRT.anchorMin = Vector2.zero; eqRootRT.anchorMax = Vector2.one; eqRootRT.offsetMin = new Vector2(8f,8f); eqRootRT.offsetMax = new Vector2(-8f,-30f);
            eqRoot.AddComponent<VerticalLayoutGroup>().spacing = 4f;

            // --- CENTER: Inventory Grid ---
            var gridPanel = new GameObject("GridPanel"); gridPanel.transform.SetParent(body.transform, false);
            var gridPanelLE = gridPanel.AddComponent<LayoutElement>(); gridPanelLE.flexibleWidth = 1f;
            gridPanel.AddComponent<Image>().color = new Color(0.06f,0.06f,0.09f,1f);
            var gridLblGo = new GameObject("GridLabel"); gridLblGo.transform.SetParent(gridPanel.transform, false);
            var gridLblRT = gridLblGo.AddComponent<RectTransform>(); gridLblRT.anchorMin = new Vector2(0f,1f); gridLblRT.anchorMax = new Vector2(1f,1f); gridLblRT.pivot = new Vector2(0.5f,1f); gridLblRT.offsetMin = new Vector2(8f,-26f); gridLblRT.offsetMax = new Vector2(-8f,0f);
            var gridLblTMP = gridLblGo.AddComponent<TextMeshProUGUI>(); gridLblTMP.text = "BACKPACK"; gridLblTMP.fontSize = 12f; gridLblTMP.fontStyle = FontStyles.Bold; gridLblTMP.color = new Color(0.6f,0.8f,1f); gridLblTMP.alignment = TextAlignmentOptions.Center;
            var gridRoot = new GameObject("GridRoot"); gridRoot.transform.SetParent(gridPanel.transform, false);
            var gridRootRT = gridRoot.AddComponent<RectTransform>(); gridRootRT.anchorMin = Vector2.zero; gridRootRT.anchorMax = Vector2.one; gridRootRT.offsetMin = new Vector2(8f,42f); gridRootRT.offsetMax = new Vector2(-8f,-30f);
            var sortBtn = MakeTextBtn(gridPanel, "SortButton", "Sort", new Color(0.2f,0.3f,0.45f,0.85f));
            var sortBtnRT = sortBtn.GetComponent<RectTransform>(); sortBtnRT.anchorMin = new Vector2(1f,1f); sortBtnRT.anchorMax = new Vector2(1f,1f); sortBtnRT.pivot = new Vector2(1f,1f); sortBtnRT.offsetMin = new Vector2(-82f,-28f); sortBtnRT.offsetMax = new Vector2(-4f,0f);

            // --- RIGHT: Weapons + Stats (340px) ---
            var rightPanel = new GameObject("RightPanel"); rightPanel.transform.SetParent(body.transform, false);
            var rightPanelLE = rightPanel.AddComponent<LayoutElement>(); rightPanelLE.preferredWidth = 340f; rightPanelLE.minWidth = 340f;
            rightPanel.AddComponent<Image>().color = new Color(0.08f,0.08f,0.11f,1f);
            var wpLblGo = new GameObject("WeaponLabel"); wpLblGo.transform.SetParent(rightPanel.transform, false);
            var wpLblRT = wpLblGo.AddComponent<RectTransform>(); wpLblRT.anchorMin = new Vector2(0f,1f); wpLblRT.anchorMax = new Vector2(1f,1f); wpLblRT.pivot = new Vector2(0.5f,1f); wpLblRT.offsetMin = new Vector2(8f,-26f); wpLblRT.offsetMax = new Vector2(-8f,0f);
            var wpLblTMP = wpLblGo.AddComponent<TextMeshProUGUI>(); wpLblTMP.text = "WEAPONS"; wpLblTMP.fontSize = 12f; wpLblTMP.fontStyle = FontStyles.Bold; wpLblTMP.color = new Color(0.6f,0.8f,1f); wpLblTMP.alignment = TextAlignmentOptions.Center;
            var wpRoot = new GameObject("WeaponRoot"); wpRoot.transform.SetParent(rightPanel.transform, false);
            var wpRootRT = wpRoot.AddComponent<RectTransform>(); wpRootRT.anchorMin = new Vector2(0f,0.48f); wpRootRT.anchorMax = new Vector2(1f,1f); wpRootRT.offsetMin = new Vector2(8f,8f); wpRootRT.offsetMax = new Vector2(-8f,-30f);
            wpRoot.AddComponent<VerticalLayoutGroup>().spacing = 4f;
            // TrashSlot (bottom strip, red zone)
            var trashRt = new GameObject("TrashSlotRoot"); trashRt.transform.SetParent(rightPanel.transform, false);
            var trashRtRT = trashRt.AddComponent<RectTransform>(); trashRtRT.anchorMin = new Vector2(0f,0f); trashRtRT.anchorMax = new Vector2(1f,0f); trashRtRT.pivot = new Vector2(0.5f,0f); trashRtRT.offsetMin = new Vector2(8f,8f); trashRtRT.offsetMax = new Vector2(-8f,60f);
            trashRt.AddComponent<Image>().color = new Color(0.4f,0.15f,0.15f,0.5f);
            var trashLblGo = new GameObject("TrashLabel"); trashLblGo.transform.SetParent(trashRt.transform, false);
            var trashLblRT = trashLblGo.AddComponent<RectTransform>(); trashLblRT.anchorMin = Vector2.zero; trashLblRT.anchorMax = Vector2.one; trashLblRT.offsetMin = trashLblRT.offsetMax = Vector2.zero;
            var trashLblTMP = trashLblGo.AddComponent<TextMeshProUGUI>(); trashLblTMP.text = "TRASH"; trashLblTMP.fontSize = 12f; trashLblTMP.color = new Color(1f,0.4f,0.4f,0.8f); trashLblTMP.alignment = TextAlignmentOptions.Center;

            // PlayerStatUIPanel (mid section of right panel)
            var statPanelGo = new GameObject("PlayerStatPanel"); statPanelGo.transform.SetParent(rightPanel.transform, false);
            var statPanelRT = statPanelGo.AddComponent<RectTransform>(); statPanelRT.anchorMin = new Vector2(0f,0f); statPanelRT.anchorMax = new Vector2(1f,0.47f); statPanelRT.offsetMin = new Vector2(8f,68f); statPanelRT.offsetMax = new Vector2(-8f,0f);
            statPanelGo.AddComponent<Image>().color = new Color(0.07f,0.07f,0.1f,0.9f);
            var statLblGo = new GameObject("StatLabel"); statLblGo.transform.SetParent(statPanelGo.transform, false);
            var statLblRT = statLblGo.AddComponent<RectTransform>(); statLblRT.anchorMin = new Vector2(0f,1f); statLblRT.anchorMax = new Vector2(1f,1f); statLblRT.pivot = new Vector2(0.5f,1f); statLblRT.offsetMin = new Vector2(0f,-24f); statLblRT.offsetMax = Vector2.zero;
            var statLblTMP = statLblGo.AddComponent<TextMeshProUGUI>(); statLblTMP.text = "PLAYER STATS"; statLblTMP.fontSize = 11f; statLblTMP.fontStyle = FontStyles.Bold; statLblTMP.color = new Color(0.6f,0.8f,1f); statLblTMP.alignment = TextAlignmentOptions.Center;
            var statContGo = new GameObject("StatContainer"); statContGo.transform.SetParent(statPanelGo.transform, false);
            var statContRT = statContGo.AddComponent<RectTransform>(); statContRT.anchorMin = Vector2.zero; statContRT.anchorMax = Vector2.one; statContRT.offsetMin = new Vector2(4f,4f); statContRT.offsetMax = new Vector2(-4f,-26f);
            statContGo.AddComponent<VerticalLayoutGroup>().spacing = 3f;
            var statPanelComp = statPanelGo.AddComponent<GameplaySystems.UI.Inventory.PlayerStatUIPanel>();
            var statPanelSO   = new SerializedObject(statPanelComp);
            SetSOObj(statPanelSO, "_statContainer", statContRT); statPanelSO.ApplyModifiedProperties();

            // Wire InventoryScreen component on InventoryPanel
            var invComp = invPanel.AddComponent<GameplaySystems.UI.Inventory.InventoryScreen>();
            var invSO   = new SerializedObject(invComp);
            SetSOObj(invSO, "_inventoryGridRoot", gridRootRT);
            SetSOObj(invSO, "_sortButton",        sortBtn);
            SetSOObj(invSO, "_playerStatPanel",   statPanelComp);

            // WeaponEquipmentPanel handles weapon/equipment cards
            var wepComp = invPanel.AddComponent<GameplaySystems.UI.Inventory.WeaponEquipmentPanel>();
            var wepSO   = new SerializedObject(wepComp);
            SetSOObj(wepSO, "_weaponCardContainer",    wpRootRT);
            SetSOObj(wepSO, "_equipmentCardContainer", eqRootRT);
            wepSO.ApplyModifiedProperties();
            SetSOObj(invSO, "_weaponEquipmentPanel", wepComp);



            // AttachmentPanel (removed, functionality moved to WeaponEquipmentPanel)

            // ItemTooltip (floating tooltip, hidden)
            var tipGo = new GameObject("ItemTooltip"); tipGo.transform.SetParent(invPanel.transform, false); tipGo.SetActive(false);
            var tipRT = tipGo.AddComponent<RectTransform>(); tipRT.anchorMin = tipRT.anchorMax = Vector2.zero; tipRT.pivot = Vector2.zero; tipRT.sizeDelta = new Vector2(280f,340f);
            tipGo.AddComponent<Image>().color = new Color(0.06f,0.06f,0.09f,0.97f);
            var tipOverlayCanvas = tipGo.AddComponent<Canvas>(); tipOverlayCanvas.overrideSorting = true; tipOverlayCanvas.sortingOrder = 201;
            // SlotLabel
            var tipSlotLblGo = new GameObject("SlotLabel"); tipSlotLblGo.transform.SetParent(tipGo.transform, false);
            var tipSlotLblRT = tipSlotLblGo.AddComponent<RectTransform>(); tipSlotLblRT.anchorMin = new Vector2(0f,1f); tipSlotLblRT.anchorMax = new Vector2(1f,1f); tipSlotLblRT.pivot = new Vector2(0.5f,1f); tipSlotLblRT.offsetMin = new Vector2(8f,-22f); tipSlotLblRT.offsetMax = new Vector2(-8f,0f);
            var tipSlotTMP = tipSlotLblGo.AddComponent<TextMeshProUGUI>(); tipSlotTMP.text = "Slot"; tipSlotTMP.fontSize = 10f; tipSlotTMP.color = new Color(0.6f,0.6f,0.7f);
            // ItemName
            var tipNameGo = new GameObject("ItemName"); tipNameGo.transform.SetParent(tipGo.transform, false);
            var tipNameRT = tipNameGo.AddComponent<RectTransform>(); tipNameRT.anchorMin = new Vector2(0f,1f); tipNameRT.anchorMax = new Vector2(1f,1f); tipNameRT.pivot = new Vector2(0.5f,1f); tipNameRT.offsetMin = new Vector2(8f,-46f); tipNameRT.offsetMax = new Vector2(-8f,-22f);
            var tipNameTMP = tipNameGo.AddComponent<TextMeshProUGUI>(); tipNameTMP.text = "Item Name"; tipNameTMP.fontSize = 16f; tipNameTMP.fontStyle = FontStyles.Bold; tipNameTMP.color = Color.white;
            // ItemDescription
            var tipDescGo = new GameObject("ItemDescription"); tipDescGo.transform.SetParent(tipGo.transform, false);
            var tipDescRT = tipDescGo.AddComponent<RectTransform>(); tipDescRT.anchorMin = new Vector2(0f,1f); tipDescRT.anchorMax = new Vector2(1f,1f); tipDescRT.pivot = new Vector2(0.5f,1f); tipDescRT.offsetMin = new Vector2(8f,-98f); tipDescRT.offsetMax = new Vector2(-8f,-46f);
            var tipDescTMP = tipDescGo.AddComponent<TextMeshProUGUI>(); tipDescTMP.text = "Description..."; tipDescTMP.fontSize = 11f; tipDescTMP.color = new Color(0.75f,0.75f,0.75f);
#if TMP_2_2_0_PREVIEW_3_OR_LATER || UNITY_2023_2_OR_NEWER
            tipDescTMP.textWrappingMode = TMPro.TextWrappingModes.Normal;
#else
            tipDescTMP.enableWordWrapping = true;
#endif
            // ItemStatsSection
            var tipStatsSec = new GameObject("ItemStatsSection"); tipStatsSec.transform.SetParent(tipGo.transform, false);
            var tipStatsSecRT = tipStatsSec.AddComponent<RectTransform>(); tipStatsSecRT.anchorMin = new Vector2(0f,0.35f); tipStatsSecRT.anchorMax = new Vector2(1f,0.67f); tipStatsSecRT.offsetMin = new Vector2(8f,0f); tipStatsSecRT.offsetMax = new Vector2(-8f,0f);
            var tipStatContGo = new GameObject("StatsContainer"); tipStatContGo.transform.SetParent(tipStatsSec.transform, false);
            var tipStatContRT = tipStatContGo.AddComponent<RectTransform>(); tipStatContRT.anchorMin = Vector2.zero; tipStatContRT.anchorMax = Vector2.one; tipStatContRT.offsetMin = tipStatContRT.offsetMax = Vector2.zero;
            tipStatContGo.AddComponent<VerticalLayoutGroup>().spacing = 2f;
            // PlayerModifiersSection
            var tipModSec = new GameObject("PlayerModifiersSection"); tipModSec.transform.SetParent(tipGo.transform, false);
            var tipModSecRT = tipModSec.AddComponent<RectTransform>(); tipModSecRT.anchorMin = new Vector2(0f,0.05f); tipModSecRT.anchorMax = new Vector2(1f,0.34f); tipModSecRT.offsetMin = new Vector2(8f,0f); tipModSecRT.offsetMax = new Vector2(-8f,0f);
            var tipModContGo = new GameObject("ModifiersContainer"); tipModContGo.transform.SetParent(tipModSec.transform, false);
            var tipModContRT = tipModContGo.AddComponent<RectTransform>(); tipModContRT.anchorMin = Vector2.zero; tipModContRT.anchorMax = Vector2.one; tipModContRT.offsetMin = tipModContRT.offsetMax = Vector2.zero;
            tipModContGo.AddComponent<VerticalLayoutGroup>().spacing = 2f;
            var tipComp = tipGo.AddComponent<GameplaySystems.UI.Inventory.ItemTooltip>();
            var tipSO   = new SerializedObject(tipComp);
            SetSOObj(tipSO, "_slotLabelText",            tipSlotTMP);
            SetSOObj(tipSO, "_itemNameText",             tipNameTMP);
            SetSOObj(tipSO, "_itemDescriptionText",      tipDescTMP);
            SetSOObj(tipSO, "_itemStatsSection",         tipStatsSec);
            SetSOObj(tipSO, "_itemStatsContainer",       tipStatContRT);
            SetSOObj(tipSO, "_playerModifiersSection",   tipModSec);
            SetSOObj(tipSO, "_playerModifiersContainer", tipModContRT);
            tipSO.ApplyModifiedProperties();
            SetSOObj(invSO, "_itemTooltip", tipComp);

            // DropQuantityDialog (hidden center dialog)
            var dqdGo = new GameObject("DropQuantityDialog"); dqdGo.transform.SetParent(invPanel.transform, false); dqdGo.SetActive(false);
            var dqdRT = dqdGo.AddComponent<RectTransform>(); dqdRT.anchorMin = dqdRT.anchorMax = new Vector2(0.5f,0.5f); dqdRT.pivot = new Vector2(0.5f,0.5f); dqdRT.anchoredPosition = Vector2.zero; dqdRT.sizeDelta = new Vector2(360f,290f);
            dqdGo.AddComponent<Image>().color = new Color(0.08f,0.08f,0.12f,0.97f);
            dqdGo.AddComponent<Canvas>().overrideSorting = true;
            var dqdComp = dqdGo.AddComponent<GameplaySystems.UI.Inventory.DropQuantityDialog>();
            var dqdSO   = new SerializedObject(dqdComp);
            SetSOObj(dqdSO, "_root", dqdGo);
            // TitleText
            var dqdTitleGo = new GameObject("TitleText"); dqdTitleGo.transform.SetParent(dqdGo.transform, false);
            var dqdTitleRT = dqdTitleGo.AddComponent<RectTransform>(); dqdTitleRT.anchorMin = new Vector2(0f,1f); dqdTitleRT.anchorMax = new Vector2(1f,1f); dqdTitleRT.pivot = new Vector2(0.5f,1f); dqdTitleRT.offsetMin = new Vector2(12f,-46f); dqdTitleRT.offsetMax = new Vector2(-12f,-4f);
            var dqdTitleTMP = dqdTitleGo.AddComponent<TextMeshProUGUI>(); dqdTitleTMP.text = "Drop Item"; dqdTitleTMP.fontSize = 18f; dqdTitleTMP.fontStyle = FontStyles.Bold; dqdTitleTMP.color = Color.white; dqdTitleTMP.alignment = TextAlignmentOptions.Center;
            SetSOObj(dqdSO, "_titleText", dqdTitleTMP);
            // HintText
            var dqdHintGo = new GameObject("HintText"); dqdHintGo.transform.SetParent(dqdGo.transform, false);
            var dqdHintRT = dqdHintGo.AddComponent<RectTransform>(); dqdHintRT.anchorMin = new Vector2(0f,0.7f); dqdHintRT.anchorMax = new Vector2(1f,0.84f); dqdHintRT.offsetMin = new Vector2(12f,0f); dqdHintRT.offsetMax = new Vector2(-12f,0f);
            var dqdHintTMP = dqdHintGo.AddComponent<TextMeshProUGUI>(); dqdHintTMP.text = "How many to drop?"; dqdHintTMP.fontSize = 13f; dqdHintTMP.color = new Color(0.8f,0.8f,0.8f); dqdHintTMP.alignment = TextAlignmentOptions.Center;
            SetSOObj(dqdSO, "_hintText", dqdHintTMP);
            // Slider + container
            var dqdSliderContGo = new GameObject("SliderContainer"); dqdSliderContGo.transform.SetParent(dqdGo.transform, false);
            var dqdSliderContRT = dqdSliderContGo.AddComponent<RectTransform>(); dqdSliderContRT.anchorMin = new Vector2(0f,0.5f); dqdSliderContRT.anchorMax = new Vector2(1f,0.68f); dqdSliderContRT.offsetMin = new Vector2(20f,0f); dqdSliderContRT.offsetMax = new Vector2(-20f,0f);
            var dqdSlider = dqdSliderContGo.AddComponent<Slider>(); dqdSlider.minValue = 1f; dqdSlider.maxValue = 100f; dqdSlider.value = 1f;
            SetSOObj(dqdSO, "_sliderContainer", dqdSliderContGo);
            SetSOObj(dqdSO, "_quantitySlider",  dqdSlider);
            // Quantity input field
            var dqdInputGo = new GameObject("QuantityInput"); dqdInputGo.transform.SetParent(dqdGo.transform, false);
            var dqdInputRT = dqdInputGo.AddComponent<RectTransform>(); dqdInputRT.anchorMin = new Vector2(0.25f,0.35f); dqdInputRT.anchorMax = new Vector2(0.75f,0.49f); dqdInputRT.offsetMin = dqdInputRT.offsetMax = Vector2.zero;
            dqdInputGo.AddComponent<Image>().color = new Color(0.15f,0.15f,0.2f,1f);
            var dqdInputField = dqdInputGo.AddComponent<TMPro.TMP_InputField>();
            var dqdInputTxtGo = new GameObject("Text"); dqdInputTxtGo.transform.SetParent(dqdInputGo.transform, false);
            var dqdInputTxtRT = dqdInputTxtGo.AddComponent<RectTransform>(); dqdInputTxtRT.anchorMin = Vector2.zero; dqdInputTxtRT.anchorMax = Vector2.one; dqdInputTxtRT.offsetMin = new Vector2(4f,2f); dqdInputTxtRT.offsetMax = new Vector2(-4f,-2f);
            var dqdInputTMP = dqdInputTxtGo.AddComponent<TextMeshProUGUI>(); dqdInputTMP.text = "1"; dqdInputTMP.fontSize = 14f; dqdInputTMP.color = Color.white; dqdInputTMP.alignment = TextAlignmentOptions.Center;
            dqdInputField.textComponent = dqdInputTMP;
            SetSOObj(dqdSO, "_quantityInput", dqdInputField);
            // Button row (bottom)
            var dqdBtnRow = new GameObject("ButtonRow"); dqdBtnRow.transform.SetParent(dqdGo.transform, false);
            var dqdBtnRowRT = dqdBtnRow.AddComponent<RectTransform>(); dqdBtnRowRT.anchorMin = new Vector2(0f,0f); dqdBtnRowRT.anchorMax = new Vector2(1f,0.33f); dqdBtnRowRT.offsetMin = new Vector2(12f,8f); dqdBtnRowRT.offsetMax = new Vector2(-12f,-4f);
            var dqdBtnHLG = dqdBtnRow.AddComponent<HorizontalLayoutGroup>(); dqdBtnHLG.spacing = 6f; dqdBtnHLG.childForceExpandWidth = true; dqdBtnHLG.childForceExpandHeight = true; dqdBtnHLG.childControlWidth = true; dqdBtnHLG.childControlHeight = true;
            SetSOObj(dqdSO, "_cancelButton",  MakeTextBtn(dqdBtnRow, "CancelButton",  "Cancel",   new Color(0.4f,0.15f,0.15f,0.9f)));
            SetSOObj(dqdSO, "_dropOneButton", MakeTextBtn(dqdBtnRow, "DropOneButton", "Drop 1",   new Color(0.2f,0.3f,0.5f,0.9f)));
            SetSOObj(dqdSO, "_dropAllButton", MakeTextBtn(dqdBtnRow, "DropAllButton", "Drop All", new Color(0.5f,0.2f,0.15f,0.9f)));
            SetSOObj(dqdSO, "_dropButton",    MakeTextBtn(dqdBtnRow, "DropButton",    "Drop",     new Color(0.18f,0.45f,0.2f,0.9f)));
            dqdSO.ApplyModifiedProperties();
            SetSOObj(invSO, "_dropQuantityDialog", dqdComp);

            invSO.ApplyModifiedProperties();
            log.Add("  InventoryScreen_Canvas -- 3-col layout | Equipment + Grid + WeaponSlots + PlayerStatPanel + ContextMenu + AttachmentPanel + Tooltip + DropDialog all wired");

            // Wire GameHUDController._inventoryScreen + _inventoryRoot
            var ctrl = parent.GetComponentInChildren<NightHunt.UI.GameHUDController>(true);
            if (ctrl != null)
            {
                var ctrlSO = new SerializedObject(ctrl);
                SetSOObj(ctrlSO, "_inventoryScreen", invComp);
                SetSOObj(ctrlSO, "_inventoryRoot",   cGo);
                ctrlSO.ApplyModifiedProperties();
                log.Add("    GameHUDController._inventoryScreen + _inventoryRoot wired");
            }
            else log.Add("    GameHUDController not found -- wire _inventoryScreen + _inventoryRoot manually");

            s_InvCanvas = cGo;
        }

        // â”€â”€ â‘ª Input â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildInput(GameObject parent, List<string> log)
        {
            log.Add("\n[Input]");
            AddMgr<Gameplay.Input.Core.InputLayerManager>(parent, "InputLayerManager", log);
            AddMgr<Gameplay.Input.Core.InputManager>(parent,      "InputManager",      log);
            log.Add("  âš ï¸ Per-player input handlers live on the Player prefab (added on spawn)");
        }

        // â”€â”€ â‘« Camera â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildCamera(GameObject parent, List<string> log)
        {
            log.Add("\n[Camera]");
            var camGo = new GameObject("Main Camera"); camGo.transform.SetParent(parent.transform);
            camGo.transform.position = new Vector3(0, 15f, -20f);
            camGo.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 500f; cam.depth = 0f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Gameplay.Camera.CameraStateManager>();
            log.Add("  Main Camera + CameraStateManager  fov=60");
        }

        // â”€â”€ â‘¬ Lighting â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static void BuildLighting(GameObject parent, List<string> log)
        {
            log.Add("\n[Lighting]");
            var lgGo = new GameObject("DirectionalLight_Sun"); lgGo.transform.SetParent(parent.transform);
            lgGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var l = lgGo.AddComponent<Light>();
            l.type      = LightType.Directional; l.intensity = 1.2f;
            l.color     = new Color(1f, 0.95f, 0.85f); l.shadows = LightShadows.Soft;
            RenderSettings.ambientMode         = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.4f, 0.5f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.5f);
            RenderSettings.ambientGroundColor  = new Color(0.2f, 0.2f, 0.2f);
            log.Add("  DirectionalLight + trilight ambient");
        }

        // â”€â”€ Helpers â€” UI element factories â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static GameObject AddRectGO(GameObject parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        /// <summary>Creates a panel GO with an Image background and explicit anchor/size.</summary>
        private static GameObject MakePanelGO(
            GameObject parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 size, Color bgColor)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = bgColor;
            return go;
        }

        /// <summary>Adds a visual stat bar row (Label | BarBg[Fill] | Value) under a VerticalLayoutGroup parent.</summary>
        private static void AddStatBarRow(GameObject container, string label, Color fillColor)
        {
            var row = new GameObject($"StatRow_{label}"); row.transform.SetParent(container.transform, false);
            row.AddComponent<RectTransform>();
            row.AddComponent<LayoutElement>().preferredHeight = 28f;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 5f; hlg.childAlignment = TextAnchor.MiddleLeft; hlg.padding = new RectOffset(0,0,2,2);
            hlg.childControlWidth = false; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            // Label (fixed 62px)
            var lblGo = new GameObject("Label"); lblGo.transform.SetParent(row.transform, false);
            lblGo.AddComponent<RectTransform>().sizeDelta = new Vector2(62f,24f);
            lblGo.AddComponent<LayoutElement>().preferredWidth = 62f;
            var lblT = lblGo.AddComponent<TextMeshProUGUI>(); lblT.text = label; lblT.fontSize = 11f; lblT.color = Color.white; lblT.alignment = TextAlignmentOptions.MidlineRight;
            // Bar background (flex-fill)
            var barBg = new GameObject("Bar"); barBg.transform.SetParent(row.transform, false);
            barBg.AddComponent<RectTransform>().sizeDelta = new Vector2(100f,24f);
            barBg.AddComponent<LayoutElement>().flexibleWidth = 1f;
            barBg.AddComponent<Image>().color = new Color(0.12f,0.12f,0.12f,1f);
            // Fill (child of bar, full width = 100% default)
            var fill = new GameObject("Fill"); fill.transform.SetParent(barBg.transform, false);
            var fillRT = fill.AddComponent<RectTransform>(); fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one; fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
            fill.AddComponent<Image>().color = fillColor;
            // Value text (fixed 52px)
            var valGo = new GameObject("Value"); valGo.transform.SetParent(row.transform, false);
            valGo.AddComponent<RectTransform>().sizeDelta = new Vector2(52f,24f);
            valGo.AddComponent<LayoutElement>().preferredWidth = 52f;
            var valT = valGo.AddComponent<TextMeshProUGUI>(); valT.text = "100"; valT.fontSize = 11f; valT.color = Color.white; valT.alignment = TextAlignmentOptions.MidlineRight;
        }

        /// <summary>Creates a Button GO (stretch-to-parent) with text label and colored background.</summary>
        private static Button MakeTextBtn(GameObject parent, string name, string label, Color bgColor)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = bgColor;
            var btn = go.AddComponent<Button>();
            var lbl = new GameObject("Label"); lbl.transform.SetParent(go.transform, false);
            var lblRT = lbl.AddComponent<RectTransform>(); lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one; lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
            var t = lbl.AddComponent<TextMeshProUGUI>(); t.text = label; t.fontSize = 16f; t.fontStyle = FontStyles.Bold; t.color = Color.white; t.alignment = TextAlignmentOptions.Center;
            return btn;
        }


        private static TextMeshProUGUI AddTMP(GameObject parent, string name, string text = "")
        {
            var go = AddRectGO(parent, name);
            var t  = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = 14f; t.color = Color.white;
            return t;
        }

        private static Slider AddSlider(GameObject parent, string name)
        {
            var go = AddRectGO(parent, name);
            return go.AddComponent<Slider>();
        }

        private static Button AddBtn(GameObject parent, string name, string label = "")
        {
            var go = AddRectGO(parent, name);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 40);
            go.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            var btn = go.AddComponent<Button>();
            if (!string.IsNullOrEmpty(label))
            {
                var lbl = AddTMP(go, "Label", label);
                lbl.alignment = TextAlignmentOptions.Center;
            }
            return btn;
        }

        private static RectTransform AddLine(GameObject parent, string name, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center; rt.sizeDelta = size;
            go.AddComponent<Image>().color = Color.white;
            return rt;
        }

        // â”€â”€ Helpers â€” general â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static GameObject CreateEmpty(string name, GameObject parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent.transform);
            return go;
        }

        private static GameObject AddMgr<T>(GameObject parent, string name, List<string> log)
            where T : Component
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform);
            go.AddComponent<T>();
            log.Add($"  {name}");
            return go;
        }

        private static void SetStaticFlags(GameObject go) =>
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
#pragma warning disable CS0618
                StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic);
#pragma warning restore CS0618

        private static void SetLayer(GameObject go, string layerName)
        {
            int l = LayerMask.NameToLayer(layerName);
            if (l >= 0) go.layer = l;
        }

        // SerializedProperty setters â€” silently skip missing properties
        private static void SetSOStr(SerializedObject s, string p, string v)
        { var x = s.FindProperty(p); if (x != null) x.stringValue = v; }

        private static void SetSOFloat(SerializedObject s, string p, float v)
        { var x = s.FindProperty(p); if (x != null) x.floatValue = v; }

        private static void SetSOInt(SerializedObject s, string p, int v)
        { var x = s.FindProperty(p); if (x != null) x.enumValueIndex = v; }

        private static void SetSOObj(SerializedObject s, string p, Object v)
        { var x = s.FindProperty(p); if (x != null) x.objectReferenceValue = v; }

        private static void EnsureDir(string filePath)
        {
            var d = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(d) && !Directory.Exists(d)) Directory.CreateDirectory(d!);
        }

        private static Material MakeMat(Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (color.a < 1f) { m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); }
            m.color = color;
            return m;
        }
    }
}
