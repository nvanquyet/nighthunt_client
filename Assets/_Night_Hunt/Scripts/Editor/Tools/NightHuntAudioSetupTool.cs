using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using NightHunt.Audio;
using NightHunt.Graphics;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Gameplay.Character;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// NightHunt Audio + Graphics Setup Tool
    ///
    /// Menu: NightHunt / Tools / Audio & Graphics Setup
    ///
    /// Automates every setup step except:
    ///   - Assigning actual audio clips into AudioLibrary / WeaponAudioProfiles (user does this)
    ///   - AudioMixer group wiring in the Inspector (done via button per group field)
    ///
    /// Steps performed:
    ///   1. Create folder structure
    ///   2. Create NH_Master.mixer with 9 groups + expose 9 parameters
    ///   3. Create AudioLibrary.asset
    ///   4. Create 7 WeaponAudioProfile assets (one per WeaponClass)
    ///   5. Add AudioManager to Systems GO, auto-assign library
    ///   6. Add CombatAudioController to Systems GO
    ///   7. Add PostProcessStateManager to PostProcess GO, create 5 Global Volumes
    ///   8. Add WeaponAudioController + CharacterAudioController to player prefab
    ///   9. Fix all Audio script .meta files (MonoImporter block)
    /// </summary>
    public sealed class NightHuntAudioSetupTool : EditorWindow
    {
        // ── Paths ──────────────────────────────────────────────────────────────
        private const string MixerPath       = "Assets/_Night_Hunt/Audio/NH_Master.mixer";
        private const string LibraryPath     = "Assets/_Night_Hunt/Audio/AudioLibrary.asset";
        private const string WAPFolder       = "Assets/_Night_Hunt/Audio/WeaponProfiles";
        private const string AudioFolder     = "Assets/_Night_Hunt/Audio";
        private const string ScriptsAudioFolder = "Assets/_Night_Hunt/Scripts/Audio";

        private static readonly (WeaponClass cls, string file)[] WAPDefs =
        {
            (WeaponClass.Pistol,   "WAP_Pistol"),
            (WeaponClass.SMG,      "WAP_SMG"),
            (WeaponClass.Rifle,    "WAP_Rifle"),
            (WeaponClass.Shotgun,  "WAP_Shotgun"),
            (WeaponClass.Sniper,   "WAP_Sniper"),
            (WeaponClass.Melee,    "WAP_Melee"),
            (WeaponClass.Launcher, "WAP_Launcher"),
        };

        // ── GUI state ──────────────────────────────────────────────────────────
        private Vector2  _scroll;
        private string   _log = "";
        private string   _playerPrefabPath = "Assets/_Night_Hunt/Prefabs/Player/";
        private bool     _showPaths = false;

        // ── Open ───────────────────────────────────────────────────────────────
        [MenuItem("NightHunt/Tools/Audio & Graphics Setup")]
        public static void Open() => GetWindow<NightHuntAudioSetupTool>("Audio & Graphics Setup");

        // ── Status helpers ─────────────────────────────────────────────────────
        private bool MixerExists()    => AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null;
        private bool LibraryExists()  => AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath) != null;
        private bool WAPsExist()
        {
            foreach (var (_, file) in WAPDefs)
                if (AssetDatabase.LoadAssetAtPath<WeaponAudioProfile>($"{WAPFolder}/{file}.asset") == null)
                    return false;
            return true;
        }
        private bool AudioManagerInScene()  => FindFirstObjectByType<AudioManager>() != null;
        private bool CombatControllerInScene() => FindFirstObjectByType<CombatAudioController>() != null;
        private bool PostProcessInScene()   => FindFirstObjectByType<PostProcessStateManager>() != null;

        // ── GUI ────────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            // Header
            EditorGUILayout.Space(6);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("NightHunt — Audio & Graphics Setup", titleStyle);
            EditorGUILayout.LabelField("Click each step or Run All. Assign audio clips manually afterward.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            // Paths foldout
            _showPaths = EditorGUILayout.Foldout(_showPaths, "Paths (expand to customise)");
            if (_showPaths)
            {
                EditorGUI.indentLevel++;
                _playerPrefabPath = EditorGUILayout.TextField("Player Prefab Search Path", _playerPrefabPath);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── Run All ──────────────────────────────────────────────────────
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f);
            if (GUILayout.Button("▶  Run All Steps (1–8)", GUILayout.Height(34)))
                RunAll();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Individual Steps", EditorStyles.boldLabel);

            DrawStep("1. Create folder structure",
                () => Directory.Exists($"{Application.dataPath}/_Night_Hunt/Audio"),
                Step1_Folders);

            DrawStep("2. Create NH_Master.mixer (9 groups + 9 exposed params)",
                MixerExists,
                Step2_Mixer);

            DrawStep("3. Create AudioLibrary.asset",
                LibraryExists,
                Step3_Library);

            DrawStep("4. Create 7 WeaponAudioProfile assets",
                WAPsExist,
                Step4_WeaponProfiles);

            DrawStep("5. Add AudioManager to Systems GO (scene)",
                AudioManagerInScene,
                Step5_AudioManager);

            DrawStep("6. Add CombatAudioController to Systems GO (scene)",
                CombatControllerInScene,
                Step6_CombatController);

            DrawStep("7. Add PostProcessStateManager + 5 Global Volumes (scene)",
                PostProcessInScene,
                Step7_PostProcess);

            DrawStep("8. Add Audio Controllers to player prefab",
                () => PlayerPrefabHasAudioControllers(),
                Step8_PlayerPrefab);

            DrawStep("9. Fix Audio script .meta files (MonoImporter)",
                () => MetaFilesAreComplete(),
                Step9_FixMetas);

            // ── Log ──────────────────────────────────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(140));
            EditorGUILayout.TextArea(_log, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Clear Log"))
                _log = "";

            // ── Reminder ──────────────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "After running all steps:\n" +
                "  • Open AudioLibrary.asset and assign audio clips (Step 2 in guide)\n" +
                "  • Open each WAP_*.asset and assign fire/reload/draw/empty clips\n" +
                "  • Open AudioManager in Inspector and assign Mixer Groups from NH_Master.mixer\n" +
                "  • (Optional) Add Animation Events to walk/run clips → OnAnimEventFootstep",
                MessageType.Info);
        }

        private void DrawStep(string label, Func<bool> statusCheck, Action action)
        {
            bool done = false;
            try { done = statusCheck(); } catch { }

            EditorGUILayout.BeginHorizontal();

            // Status indicator
            var icon = done ? "✅" : "⬜";
            var style = new GUIStyle(EditorStyles.label) { richText = true };
            EditorGUILayout.LabelField($"{icon}  {label}", style, GUILayout.ExpandWidth(true));

            GUI.backgroundColor = done ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.75f, 1f);
            if (GUILayout.Button(done ? "Re-run" : "Run", GUILayout.Width(60)))
                SafeRun(label, action);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void SafeRun(string label, Action action)
        {
            try
            {
                Log($"\n── {label} ──────────────────────");
                action();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Repaint();
            }
            catch (Exception ex)
            {
                Log($"❌ ERROR: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void RunAll()
        {
            _log = "";
            SafeRun("Step 1 — Folders",           Step1_Folders);
            SafeRun("Step 2 — Mixer",              Step2_Mixer);
            SafeRun("Step 3 — AudioLibrary",       Step3_Library);
            SafeRun("Step 4 — WeaponProfiles",     Step4_WeaponProfiles);
            SafeRun("Step 5 — AudioManager",       Step5_AudioManager);
            SafeRun("Step 6 — CombatController",   Step6_CombatController);
            SafeRun("Step 7 — PostProcess",        Step7_PostProcess);
            SafeRun("Step 8 — Player Prefab",      Step8_PlayerPrefab);
            SafeRun("Step 9 — Fix Metas",          Step9_FixMetas);
            Log("\n✅ All steps complete! Assign audio clips in AudioLibrary.asset and WAP_*.assets.");
        }

        private void Log(string msg)
        {
            _log += msg + "\n";
            Repaint();
        }

        // ══════════════════════════════════════════════════════════════════════
        // STEP IMPLEMENTATIONS
        // ══════════════════════════════════════════════════════════════════════

        // ── Step 1: Folders ────────────────────────────────────────────────────
        private void Step1_Folders()
        {
            EnsureFolder("Assets/_Night_Hunt/Audio");
            EnsureFolder("Assets/_Night_Hunt/Audio/WeaponProfiles");
            EnsureFolder("Assets/_Night_Hunt/Prefabs");
            Log("✅ Folder structure ready.");
        }

        // ── Step 2: AudioMixer ─────────────────────────────────────────────────
        private void Step2_Mixer()
        {
            if (MixerExists())
            {
                Log("ℹ️  NH_Master.mixer already exists. Exposing parameters...");
                TryExposeAllMixerParams();
                return;
            }

            if (!TryCreateMixerAsset())
            {
                Log("⚠️  Unity 6 blocks programmatic AudioMixer creation.");
                Log("    ── MANUAL STEP ──────────────────────────────────────────────────");
                Log("    1. In the Project window, navigate to Assets/_Night_Hunt/Audio/");
                Log("    2. Right-click inside that folder → Create → Audio Mixer");
                Log("    3. Name it exactly:  NH_Master");
                Log("    4. Re-run Step 2 — it will auto-expose all 9 volume parameters.");
                Log("    ─────────────────────────────────────────────────────────────────");
            }
        }

        private bool TryCreateMixerAsset()
        {
            // Attempt 1: short unqualified name (works in some Unity versions)
            UnityEngine.Object mixerInstance = null;
            try { mixerInstance = ScriptableObject.CreateInstance("AudioMixerController"); } catch { }

            // Attempt 2: fully qualified internal type
            if (mixerInstance == null)
            {
                var t = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
                if (t != null)
                    try { mixerInstance = ScriptableObject.CreateInstance(t); } catch { }
            }

            if (mixerInstance == null) return false;

            EnsureFolder("Assets/_Night_Hunt/Audio");
            AssetDatabase.CreateAsset(mixerInstance, MixerPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) return false;

            Log($"✅ Created NH_Master.mixer at {MixerPath}");
            CreateMixerGroups(mixer);
            TryExposeAllMixerParams();
            return true;
        }

        private void CreateMixerGroups(AudioMixer mixer)
        {
            if (mixer == null) return;

            // We need to call the internal CreateNewGroup method via reflection
            var controllerType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
            if (controllerType == null) { Log("⚠️  Cannot create groups — internal type missing."); return; }

            // AudioMixerController.masterGroup property
            var masterGroupProp = controllerType.GetProperty("masterGroup",
                BindingFlags.Public | BindingFlags.Instance);
            if (masterGroupProp == null) { Log("⚠️  Cannot find masterGroup property."); return; }

            var masterGroup = masterGroupProp.GetValue(mixer) as AudioMixerGroup;
            Log($"   Master group: {masterGroup?.name ?? "null"}");

            // CreateNewGroup(string name, bool addToMixer) — internal method
            var createGroupMethod = controllerType.GetMethod("CreateNewGroup",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (createGroupMethod == null) { Log("⚠️  CreateNewGroup method not found — groups must be created manually."); return; }

            // Group hierarchy: Master > Music, SFX > UI, Weapon, Footstep, Explosion, Voice, Ambience
            var musicGroup      = InvokeCreateGroup(mixer, createGroupMethod, masterGroup, "Music");
            var sfxGroup        = InvokeCreateGroup(mixer, createGroupMethod, masterGroup, "SFX");
            InvokeCreateGroup(mixer, createGroupMethod, sfxGroup, "UI");
            InvokeCreateGroup(mixer, createGroupMethod, sfxGroup, "Weapon");
            InvokeCreateGroup(mixer, createGroupMethod, sfxGroup, "Footstep");
            InvokeCreateGroup(mixer, createGroupMethod, sfxGroup, "Explosion");
            InvokeCreateGroup(mixer, createGroupMethod, sfxGroup, "Voice");
            InvokeCreateGroup(mixer, createGroupMethod, sfxGroup, "Ambience");

            AssetDatabase.SaveAssets();
            Log("✅ Mixer groups created: Master > Music, SFX > (UI, Weapon, Footstep, Explosion, Voice, Ambience)");
        }

        private AudioMixerGroup InvokeCreateGroup(AudioMixer mixer, MethodInfo createMethod, AudioMixerGroup parent, string name)
        {
            try
            {
                // Signature varies by Unity version: (string name, bool addToMixer) or (AudioMixerGroup parent, string name)
                var paramTypes = createMethod.GetParameters();
                object result = null;
                if (paramTypes.Length == 2 && paramTypes[0].ParameterType == typeof(string))
                    result = createMethod.Invoke(mixer, new object[] { name, true });
                else if (paramTypes.Length >= 1)
                    result = createMethod.Invoke(mixer, new object[] { name });
                else
                    result = createMethod.Invoke(mixer, null);

                Log($"   Created group: {name}");
                return result as AudioMixerGroup;
            }
            catch (Exception ex)
            {
                Log($"   ⚠️  Could not create group '{name}': {ex.Message}");
                return null;
            }
        }

        private void TryExposeAllMixerParams()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) { Log("⚠️  Mixer not found — skipping param exposure."); return; }

            var controllerType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
            if (controllerType == null) { Log("⚠️  Cannot expose params via reflection."); return; }

            // Try to expose params by using SetFloat with a specific name first (Unity exposes internally)
            // Fallback: show instructions
            string[] paramNames = {
                AudioManager.ParamMaster, AudioManager.ParamMusic,    AudioManager.ParamSFX,
                AudioManager.ParamUI,     AudioManager.ParamWeapon,   AudioManager.ParamFootstep,
                AudioManager.ParamExplosion, AudioManager.ParamVoice, AudioManager.ParamAmbience
            };

            // Expose via AddExposedParameter reflection
            var addExposedMethod = controllerType.GetMethod("AddExposedParameter",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (addExposedMethod != null)
            {
                foreach (var p in paramNames)
                {
                    try { addExposedMethod.Invoke(mixer, new object[] { p }); }
                    catch { /* ignore per-param failures */ }
                }
                Log($"✅ Attempted to expose {paramNames.Length} mixer parameters.");
            }
            else
            {
                Log("⚠️  Expose params: right-click each channel Volume slider → 'Expose ... to script' and rename as:");
                foreach (var p in paramNames) Log($"   • {p}");
            }
        }

        // ── Step 3: AudioLibrary ───────────────────────────────────────────────
        private void Step3_Library()
        {
            if (LibraryExists())
            {
                Log("ℹ️  AudioLibrary.asset already exists — skipping.");
                return;
            }

            var lib = CreateAsset<AudioLibrary>(LibraryPath);
            Log($"✅ Created AudioLibrary.asset at {LibraryPath}");
            Log("   → Open it and assign audio clips to each field.");
            Selection.activeObject = lib;
        }

        // ── Step 4: WeaponAudioProfiles ────────────────────────────────────────
        private void Step4_WeaponProfiles()
        {
            EnsureFolder(WAPFolder);
            int created = 0;
            foreach (var (cls, file) in WAPDefs)
            {
                string path = $"{WAPFolder}/{file}.asset";
                if (AssetDatabase.LoadAssetAtPath<WeaponAudioProfile>(path) != null)
                {
                    Log($"   ℹ️  {file}.asset already exists — skipping.");
                    continue;
                }

                var wap = CreateAsset<WeaponAudioProfile>(path);
                // Set weapon class field via SerializedObject to survive domain reloads
                var so = new SerializedObject(wap);
                var prop = so.FindProperty("weaponClass");
                if (prop != null)
                {
                    prop.enumValueIndex = (int)cls;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Log($"   ✅ {file}.asset — WeaponClass.{cls}");
                created++;
            }
            Log($"✅ WeaponAudioProfile step done. {created} new assets created.");
            Log("   → Open each WAP_*.asset and assign fire/reload/draw/empty clips.");
        }

        // ── Step 5: AudioManager ───────────────────────────────────────────────
        private void Step5_AudioManager()
        {
            var existing = FindFirstObjectByType<AudioManager>();
            if (existing != null)
            {
                Log("ℹ️  AudioManager already in scene.");
                AutoAssignLibrary(existing);
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var systemsGO = GetOrCreateSystemsGO();
            var am = Undo.AddComponent<AudioManager>(systemsGO);
            AutoAssignLibrary(am);

            Log($"✅ AudioManager added to '{systemsGO.name}'.");
            Log("   → In Inspector: assign NH_Master.mixer and all 7 Mixer Groups.");
            Selection.activeGameObject = systemsGO;
        }

        private void AutoAssignLibrary(AudioManager am)
        {
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (lib == null) { Log("   ⚠️  AudioLibrary.asset not found — run Step 3 first."); return; }

            var so   = new SerializedObject(am);
            var prop = so.FindProperty("library");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = lib;
                so.ApplyModifiedPropertiesWithoutUndo();
                Log("   ✅ AudioLibrary auto-assigned to AudioManager.");
            }
        }

        // ── Step 6: CombatAudioController ──────────────────────────────────────
        private void Step6_CombatController()
        {
            var existing = FindFirstObjectByType<CombatAudioController>();
            if (existing != null)
            {
                Log("ℹ️  CombatAudioController already in scene.");
                return;
            }

            var systemsGO = GetOrCreateSystemsGO();
            Undo.AddComponent<CombatAudioController>(systemsGO);
            Log($"✅ CombatAudioController added to '{systemsGO.name}'.");
            Log("   → Call combatController.Initialize(statSystem, playerName) when local player spawns.");
        }

        // ── Step 7: PostProcessStateManager ───────────────────────────────────
        private void Step7_PostProcess()
        {
            var existing = FindFirstObjectByType<PostProcessStateManager>();
            if (existing != null)
            {
                Log("ℹ️  PostProcessStateManager already in scene.");
                return;
            }

            // Create PostProcess GO
            var ppGO = new GameObject("PostProcess");
            Undo.RegisterCreatedObjectUndo(ppGO, "Create PostProcess GO");
            var manager = Undo.AddComponent<PostProcessStateManager>(ppGO);

            // Create 5 Global Volumes
            var (baseVol,      _) = CreateGlobalVolume("Vol_Base",      ppGO, priority: 1);
            var (homeVol,      _) = CreateGlobalVolume("Vol_Home",      ppGO, priority: 2);
            var (lowHealthVol, _) = CreateGlobalVolume("Vol_LowHealth", ppGO, priority: 10);
            var (deathVol,     _) = CreateGlobalVolume("Vol_Death",     ppGO, priority: 20);
            var (spectatorVol, _) = CreateGlobalVolume("Vol_Spectator", ppGO, priority: 15);

            // Wire volumes into PostProcessStateManager
            var so = new SerializedObject(manager);
            SetVolumeProp(so, "baseVolume",      baseVol);
            SetVolumeProp(so, "homeVolume",      homeVol);
            SetVolumeProp(so, "lowHealthVolume", lowHealthVol);
            SetVolumeProp(so, "deathVolume",     deathVol);
            SetVolumeProp(so, "spectatorVolume", spectatorVol);
            so.ApplyModifiedPropertiesWithoutUndo();

            Log("✅ PostProcessStateManager + 5 Global Volumes created and wired.");
            Log("   → Open each Vol_*.asset Profile and add desired post-process effects.");
            Selection.activeGameObject = ppGO;
        }

        private (Volume vol, GameObject go) CreateGlobalVolume(string goName, GameObject parent, int priority)
        {
            var go  = new GameObject(goName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {goName}");
            go.transform.SetParent(parent.transform, false);

            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = priority;
            vol.weight   = 0f;

            // Create a blank profile asset
            EnsureFolder("Assets/_Night_Hunt/Rendering");
            string profilePath = $"Assets/_Night_Hunt/Rendering/{goName}_Profile.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath) == null)
            {
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            Log($"   ✅ {goName} — priority {priority}, profile: {profilePath}");
            return (vol, go);
        }

        private void SetVolumeProp(SerializedObject so, string propName, Volume vol)
        {
            var prop = so.FindProperty(propName);
            if (prop != null) prop.objectReferenceValue = vol;
            else Log($"   ⚠️  Property '{propName}' not found on PostProcessStateManager.");
        }

        // Prefabs known to be outdated / non-player — skip these during auto-detection
        private static readonly string[] OutdatedPrefabNames =
        {
            "Network_Player Rigidbody Predict",
            "Network_Player",
        };

        private bool IsOutdatedPrefab(string path)
        {
            foreach (var name in OutdatedPrefabNames)
                if (path.Contains(name)) return true;
            return false;
        }

        // ── Step 8: Player Prefab ──────────────────────────────────────────────
        private void Step8_PlayerPrefab()
        {
            GameObject playerPrefab = null;

            // Priority 1: look for preferred names first
            string[] preferredNames = { "PlayerPrefab", "Player_Prefab", "NetworkPlayer", "Player" };
            string[] allGuids = AssetDatabase.FindAssets("t:Prefab");

            foreach (var preferredName in preferredNames)
            {
                foreach (var guid in allGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.Contains("_Night_Hunt")) continue;
                    if (IsOutdatedPrefab(path)) continue;
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (!fileName.Equals(preferredName, StringComparison.OrdinalIgnoreCase)) continue;
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null)
                    {
                        playerPrefab = go;
                        Log($"   Found player prefab by name: {path}");
                        break;
                    }
                }
                if (playerPrefab != null) break;
            }

            // Priority 2: look for prefab containing WeaponSystem component name
            if (playerPrefab == null)
            {
                foreach (var guid in allGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.Contains("_Night_Hunt")) continue;
                    if (IsOutdatedPrefab(path)) continue;
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;
                    foreach (var comp in go.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (comp == null) continue;
                        if (comp.GetType().Name == "WeaponSystem")
                        {
                            playerPrefab = go;
                            Log($"   Found player prefab by WeaponSystem component: {path}");
                            break;
                        }
                    }
                    if (playerPrefab != null) break;
                }
            }

            // Priority 3: BaseCharacterPredictedMovement fallback
            if (playerPrefab == null)
            {
                foreach (var guid in allGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.Contains("_Night_Hunt")) continue;
                    if (IsOutdatedPrefab(path)) continue;
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null && go.GetComponentInChildren<BaseCharacterPredictedMovement>(true) != null)
                    {
                        playerPrefab = go;
                        Log($"   Found player prefab by movement component: {path}");
                        break;
                    }
                }
            }

            if (playerPrefab == null)
            {
                Log("⚠️  Player prefab not found. Add WeaponAudioController + CharacterAudioController manually.");
                Log("   Hint: prefab must contain BaseCharacterPredictedMovement.");
                return;
            }

            bool modified = false;
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(playerPrefab)))
            {
                var root = editScope.prefabContentsRoot;

                // Find WeaponSystem GO
                var weaponGO = FindGOWithComponent<IWeaponSystemMarker>(root)
                            ?? FindGOByName(root, "WeaponSystem")
                            ?? root;

                if (weaponGO.GetComponent<WeaponAudioController>() == null)
                {
                    weaponGO.AddComponent<WeaponAudioController>();
                    AutoAssignWAPsToController(weaponGO);
                    Log($"   ✅ WeaponAudioController added to '{weaponGO.name}'.");
                    Log("   → Assign Muzzle Point in Inspector.");
                    modified = true;
                }
                else
                {
                    Log("   ℹ️  WeaponAudioController already present.");
                }

                // Find Character movement GO
                var movementGO = root.GetComponentInChildren<BaseCharacterPredictedMovement>(true)?.gameObject
                              ?? root;

                if (movementGO.GetComponent<CharacterAudioController>() == null)
                {
                    movementGO.AddComponent<CharacterAudioController>();
                    Log($"   ✅ CharacterAudioController added to '{movementGO.name}'.");
                    Log("   → Optionally assign foot bones in Inspector.");
                    modified = true;
                }
                else
                {
                    Log("   ℹ️  CharacterAudioController already present.");
                }
            }

            if (modified)
            {
                Log("✅ Player prefab updated.");
                Log("   → Open prefab and wire Muzzle Point + foot bones.");
            }
        }

        private void AutoAssignWAPsToController(GameObject go)
        {
            var controller = go.GetComponent<WeaponAudioController>();
            if (controller == null) return;

            var so = new SerializedObject(controller);
            var profilesProp = so.FindProperty("profiles");
            if (profilesProp == null) return;

            profilesProp.arraySize = WAPDefs.Length;
            for (int i = 0; i < WAPDefs.Length; i++)
            {
                var (cls, file) = WAPDefs[i];
                var wap = AssetDatabase.LoadAssetAtPath<WeaponAudioProfile>($"{WAPFolder}/{file}.asset");

                var elem     = profilesProp.GetArrayElementAtIndex(i);
                var classProp   = elem.FindPropertyRelative("weaponClass");
                var profileProp = elem.FindPropertyRelative("profile");

                if (classProp   != null) classProp.enumValueIndex       = (int)cls;
                if (profileProp != null) profileProp.objectReferenceValue = wap;
            }

            // Set default profile to Rifle
            var defaultProp = so.FindProperty("defaultProfile");
            if (defaultProp != null)
            {
                var rifleWap = AssetDatabase.LoadAssetAtPath<WeaponAudioProfile>($"{WAPFolder}/WAP_Rifle.asset");
                defaultProp.objectReferenceValue = rifleWap;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Log("   ✅ WeaponAudioProfiles auto-assigned to WeaponAudioController.");
        }

        // ── Step 9: Fix Meta files ─────────────────────────────────────────────
        private void Step9_FixMetas()
        {
            int fixed_ = 0;
            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { ScriptsAudioFolder });
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string metaPath  = assetPath + ".meta";
                if (!File.Exists(metaPath)) continue;

                string content = File.ReadAllText(metaPath);
                if (!content.Contains("MonoImporter"))
                {
                    // Extract guid line
                    string guidLine = $"guid: {guid}";
                    string newContent =
                        $"fileFormatVersion: 2\n{guidLine}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";
                    File.WriteAllText(metaPath, newContent);
                    Log($"   ✅ Fixed meta: {Path.GetFileName(metaPath)}");
                    fixed_++;
                }
            }
            if (fixed_ == 0)
                Log("ℹ️  All Audio script .meta files are already complete.");
            else
                Log($"✅ Fixed {fixed_} .meta file(s). Unity will reimport these scripts.");

            AssetDatabase.Refresh();
        }

        // ══════════════════════════════════════════════════════════════════════
        // STATUS CHECKS
        // ══════════════════════════════════════════════════════════════════════

        private bool PlayerPrefabHasAudioControllers()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("_Night_Hunt")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponentInChildren<CharacterAudioController>(true) != null)
                    return true;
            }
            return false;
        }

        private bool MetaFilesAreComplete()
        {
            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { ScriptsAudioFolder });
            foreach (var guid in guids)
            {
                string metaPath = AssetDatabase.GUIDToAssetPath(guid) + ".meta";
                if (File.Exists(metaPath) && !File.ReadAllText(metaPath).Contains("MonoImporter"))
                    return false;
            }
            return true;
        }

        // ══════════════════════════════════════════════════════════════════════
        // UTILITIES
        // ══════════════════════════════════════════════════════════════════════

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int lastSlash = path.LastIndexOf('/');
            string parent = path.Substring(0, lastSlash);
            string child  = path.Substring(lastSlash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, child);
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject GetOrCreateSystemsGO()
        {
            // Look for existing Systems GO in scene
            var names = new[] { "Systems", "PersistentSystems", "Managers", "GameSystems" };
            foreach (var name in names)
            {
                var existing = GameObject.Find(name);
                if (existing != null) return existing;
            }

            var go = new GameObject("Systems");
            Undo.RegisterCreatedObjectUndo(go, "Create Systems GO");
            return go;
        }

        private static GameObject FindGOByName(GameObject root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root.transform)
            {
                var found = FindGOByName(child.gameObject, name);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindGOWithComponent<T>(GameObject root) where T : class
        {
            if (root.GetComponent<T>() != null) return root;
            foreach (Transform child in root.transform)
            {
                var found = FindGOWithComponent<T>(child.gameObject);
                if (found != null) return found;
            }
            return null;
        }
    }

    // ── Marker interface used to find WeaponSystem GO (avoids hard coupling) ────
    internal interface IWeaponSystemMarker { }
}
