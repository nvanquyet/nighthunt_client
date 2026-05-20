using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using NightHunt.Audio;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Editor.Tools
{
    public sealed class WarfareAudioAssigner : EditorWindow
    {
        private const string Root = "Assets/WARFARE SOUNDS";
        private const string LibraryPath = "Assets/_Night_Hunt/Audio/AudioLibrary.asset";
        private const string WeaponProfileFolder = "Assets/_Night_Hunt/Audio/WeaponProfiles";

        private Vector2 _scroll;
        private string _lastReport = "No scan has run yet.";

        [MenuItem("NightHunt/Tools/Audio/Warfare Audio Assigner")]
        public static void ShowWindow()
            => GetWindow<WarfareAudioAssigner>("Warfare Audio");

        public static void RunAutoAssignFromCommandLine()
            => Run(assign: true);

        public static void RunDryRunFromCommandLine()
            => Run(assign: false);

        private void OnGUI()
        {
            GUILayout.Label("WARFARE SOUNDS deterministic assigner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Source of truth: Assets/WARFARE SOUNDS only. Dry Run reports the exact clips before writing AudioLibrary and WAP assets.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Dry Run"))
                    _lastReport = Run(assign: false);
                if (GUILayout.Button("Assign Now"))
                    _lastReport = Run(assign: true);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private static string Run(bool assign)
        {
            var report = new StringBuilder();
            var db = new ClipCatalog(Root);
            report.AppendLine($"WARFARE clips indexed: {db.Count}");

            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null)
            {
                string message = $"AudioLibrary not found at {LibraryPath}";
                Debug.LogError(message);
                return message;
            }

            var plan = BuildLibraryPlan(db);
            AppendPlan(report, "AudioLibrary", plan);
            if (assign)
                ApplyLibraryPlan(library, plan);

            string[] profileGuids = AssetDatabase.FindAssets("t:WeaponAudioProfile", new[] { WeaponProfileFolder });
            foreach (string guid in profileGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<WeaponAudioProfile>(path);
                if (profile == null) continue;

                var weaponPlan = BuildWeaponPlan(db, profile.weaponClass);
                AppendPlan(report, profile.name, weaponPlan);
                if (assign)
                    ApplyWeaponPlan(profile, weaponPlan);
            }

            if (assign)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                report.AppendLine("Applied and saved.");
            }

            string text = report.ToString();
            Debug.Log(text);
            return text;
        }

        private static Dictionary<string, AudioClip[]> BuildLibraryPlan(ClipCatalog db)
        {
            return new Dictionary<string, AudioClip[]>
            {
                ["gameIntro"] = One(db.Path("Miscellaneous_Sounds/Cinematic Sounds/cinematic_deep_boom_impact_01.wav")),
                ["uiClick"] = One(db.Path("Miscellaneous_Sounds/UI _Switch/ui_menu_button_beep_01.wav")),
                ["uiHover"] = One(db.Path("Miscellaneous_Sounds/UI _Switch/ui_menu_button_beep_02.wav")),
                ["uiPanelOpen"] = One(db.Path("Miscellaneous_Sounds/UI _Switch/switch_button_push_on_off_03.wav")),
                ["uiPanelClose"] = One(db.Path("Miscellaneous_Sounds/UI _Switch/switch_button_push_on_off_04.wav")),
                ["uiNotification"] = One(db.Path("Miscellaneous_Sounds/UI _Switch/ui_menu_button_beep_13.wav")),
                ["uiCountdownBeep"] = One(db.Path("Miscellaneous_Sounds/UI _Switch/ui_menu_button_beep_03.wav")),
                ["uiCountdownFinal"] = One(db.Path("Miscellaneous_Sounds/UI _Switch/ui_menu_button_beep_15.wav")),
                ["uiMatchFound"] = One(db.Path("Announcer_Classic_FPS/announcer_voice_classic_FPS_fight.wav")),
                ["hitMarkerTick"] = One(db.Path("Weapons/Bullets/bullet_impact_body_thump_01.wav")),
                ["hitMarkerHeadshot"] = One(db.Path("Announcer_Classic_FPS/announcer_voice_classic_FPS_headshot.wav")),
                ["weaponDepleted"] = One(db.Path("Weapons/Guns/gun_pistol_dry_fire_01.wav")),
                ["killConfirm"] = One(db.Path("Miscellaneous_Sounds/Radio/radio_voice_male_soldier_enemy_down.wav")),
                ["multiKillClips"] = Many(
                    db.Path("Announcer_Classic_FPS/announcer_voice_classic_FPS_doublekill.wav"),
                    db.Path("Announcer_Classic_FPS/announcer_voice_classic_FPS_tripplekill.wav"),
                    db.Path("Announcer_Classic_FPS/announcer_voice_classic_FPS_rampage.wav"),
                    db.Path("Announcer_Classic_FPS/announcer_voice_classic_FPS_fury.wav")),
                ["lowHealthHeartbeat"] = One(null),
                ["playerDeathStinger"] = One(db.Path("Announcer_Classic_FPS/announcer_voice_classic_FPS_gameover.wav")),
                ["bgmPlaylist"] = db.Folder("Music").OrderBy(c => c.name).ToArray(),
                ["bgmHome"] = One(db.Path("Music/music_cinematic_reveal.wav")),
                ["bgmMatch"] = One(db.Path("Music/music_modern_war.wav")),
                ["bgmIntense"] = One(db.Path("Music/music_epic_fallen_empire.wav")),
                ["bgmResults"] = One(db.Path("Music/music_cinematic_darkness_falls.wav")),
                ["footstepWalk"] = db.Series("Footsteps", @"^footstep_concrete_walk_\d+\.wav$", 8),
                ["footstepSprint"] = db.Series("Footsteps", @"^footstep_concrete_run_\d+\.wav$", 8),
                ["footstepLand"] = db.Series("Footsteps", @"^footstep_concrete_land_\d+\.wav$", 5),
                ["footstepJump"] = One(db.Path("Foley/foley_jump_movement_throw_01.wav")),
                ["footstepRoll"] = One(db.Path("Foley/foley_combat_fight_grab_throw_01.wav")),
                ["explosionGrenade"] = One(db.Path("Explosion_Fire_Gas/explosion_med_long_tail_01.wav")),
                ["explosionRocket"] = One(db.Path("Explosion_Fire_Gas/explosion_large_01.wav")),
                ["bulletImpact"] = One(db.Path("Weapons/Bullets/bullet_impact_concrete_brick_02.wav")),
                ["consumableUse"] = One(db.Path("Foley/foley_object_grab_pickup_01.wav")),
                ["throwablePull"] = One(db.Path("Miscellaneous_Sounds/Metal/metal_tiny_hit_impact_01.wav")),
                ["ambienceVariants"] = db.Series("Ambiences/Urban_Tones_Backgrounds", @"^background_.*\.wav$", 6)
            };
        }

        private static Dictionary<string, AudioClip[]> BuildWeaponPlan(ClipCatalog db, WeaponClass weaponClass)
        {
            var plan = new Dictionary<string, AudioClip[]>
            {
                ["fireClips"] = System.Array.Empty<AudioClip>(),
                ["fireSuppressedClips"] = System.Array.Empty<AudioClip>(),
                ["reloadStartClip"] = One(null),
                ["reloadEndClip"] = One(null),
                ["reloadTacticalClip"] = One(null),
                ["drawClip"] = One(null),
                ["holsterClip"] = One(null),
                ["emptyClip"] = One(null),
                ["bulletImpactOverride"] = One(db.Path("Weapons/Bullets/bullet_impact_concrete_brick_02.wav"))
            };

            switch (weaponClass)
            {
                case WeaponClass.Pistol:
                    plan["fireClips"] = db.Series("Weapons/Guns", @"^gun_pistol_shot_\d+\.wav$", 5);
                    plan["fireSuppressedClips"] = db.Series("Weapons/Guns", @"^gun_pistol_shot_silenced_\d+\.wav$", 4);
                    plan["reloadStartClip"] = One(db.Path("Weapons/Guns/gun_pistol_remove_mag_01.wav"));
                    plan["reloadEndClip"] = One(db.Path("Weapons/Guns/gun_pistol_insert_mag_01.wav"));
                    plan["reloadTacticalClip"] = One(db.Path("Weapons/Guns/gun_pistol_load_bullet_01.wav"));
                    plan["drawClip"] = One(db.Path("Weapons/Guns/gun_pistol_general_handling_01.wav"));
                    plan["holsterClip"] = One(db.Path("Weapons/Guns/gun_pistol_general_handling_02.wav"));
                    plan["emptyClip"] = One(db.Path("Weapons/Guns/gun_pistol_dry_fire_01.wav"));
                    break;
                case WeaponClass.SMG:
                    plan["fireClips"] = db.Series("Weapons/Guns", @"^gun_submachine_auto_shot_\d+\.wav$", 8);
                    plan["fireSuppressedClips"] = db.Series("Weapons/Guns_Silenced", @"^gun_silenced_semi_sub_shot_\d+\.wav$", 4);
                    plan["reloadStartClip"] = One(db.Path("Weapons/Guns/gun_submachine_auto_magazine_unload_01.wav"));
                    plan["reloadEndClip"] = One(db.Path("Weapons/Guns/gun_submachine_auto_magazine_load_01.wav"));
                    plan["reloadTacticalClip"] = One(db.Path("Weapons/Guns/gun_submachine_auto_load_bullet_01.wav"));
                    plan["drawClip"] = One(db.Path("Weapons/Guns/gun_submachine_auto_cock_01.wav"));
                    plan["holsterClip"] = One(db.Path("Weapons/Guns/gun_submachine_auto_cock_02.wav"));
                    plan["emptyClip"] = One(db.Path("Weapons/Guns/gun_submachine_auto_dry_fire_01.wav"));
                    break;
                case WeaponClass.Rifle:
                case WeaponClass.MachineGun:
                    plan["fireClips"] = db.Series("Weapons/Guns", @"^gun_rifle_shot_\d+\.wav$", 4);
                    plan["fireSuppressedClips"] = db.Series("Weapons/Guns_Silenced", @"^gun_silenced_rifle1_shot_\d+\.wav$", 4);
                    plan["reloadStartClip"] = One(db.Path("Weapons/Guns/gun_rifle_magazine_unload_01.wav"));
                    plan["reloadEndClip"] = One(db.Path("Weapons/Guns/gun_rifle_magazine_load_01.wav"));
                    plan["reloadTacticalClip"] = One(db.Path("Weapons/Guns/gun_rifle_load_bullet_02.wav"));
                    plan["drawClip"] = One(db.Path("Weapons/Guns/gun_rifle_grab_pickup_01.wav"));
                    plan["holsterClip"] = One(db.Path("Weapons/Guns/gun_rifle_cock_01.wav"));
                    plan["emptyClip"] = One(db.Path("Weapons/Guns/gun_rifle_dry_fire_01.wav"));
                    break;
                case WeaponClass.Shotgun:
                    plan["fireClips"] = db.Series("Weapons/Guns", @"^gun_shotgun_shot_\d+\.wav$", 4);
                    plan["reloadStartClip"] = One(db.Path("Weapons/Guns/gun_shotgun_cock_01.wav"));
                    plan["reloadEndClip"] = One(db.Path("Weapons/Guns/gun_shotgun_load_bullet_01.wav"));
                    plan["reloadTacticalClip"] = One(db.Path("Weapons/Guns/gun_shotgun_load_bullet_02.wav"));
                    plan["drawClip"] = One(db.Path("Weapons/Guns/gun_shotgun_pickup_01.wav"));
                    plan["holsterClip"] = One(db.Path("Weapons/Guns/gun_shotgun_pickup_02.wav"));
                    plan["emptyClip"] = One(db.Path("Weapons/Guns/gun_shotgun_dry_fire_01.wav"));
                    break;
                case WeaponClass.Sniper:
                    plan["fireClips"] = db.Series("Weapons/Guns", @"^gun_rifle_sniper_shot_\d+\.wav$", 4);
                    plan["fireSuppressedClips"] = db.Series("Weapons/Guns_Silenced", @"^gun_silenced_sniper1_shot_\d+\.wav$", 4);
                    plan["reloadStartClip"] = One(db.Path("Weapons/Guns/gun_rifle_sniper_cock_01.wav"));
                    plan["reloadEndClip"] = One(db.Path("Weapons/Guns/gun_rifle_sniper_load_bullet_01.wav"));
                    plan["reloadTacticalClip"] = One(db.Path("Weapons/Guns/gun_rifle_sniper_load_bullet_02.wav"));
                    plan["drawClip"] = One(db.Path("Weapons/Guns/gun_rifle_sniper_scope_zoom_lens_01.wav"));
                    plan["holsterClip"] = One(db.Path("Weapons/Guns/gun_rifle_sniper_cock_02.wav"));
                    plan["emptyClip"] = One(db.Path("Weapons/Guns/gun_rifle_sniper_dry_fire_01.wav"));
                    break;
                case WeaponClass.Launcher:
                    plan["fireClips"] = db.Series("Weapons/Guns", @"^gun_grenade_launcher_shot_\d+\.wav$", 4);
                    plan["reloadStartClip"] = One(db.Path("Weapons/Guns/gun_grenade_launcher_trigger_01.wav"));
                    plan["reloadEndClip"] = One(db.Path("Weapons/Guns/gun_grenade_launcher_reload_01.wav"));
                    plan["reloadTacticalClip"] = One(db.Path("Weapons/Guns/gun_grenade_launcher_reload_02.wav"));
                    plan["drawClip"] = One(db.Path("Weapons/Guns/gun_grenade_launcher_trigger_02.wav"));
                    plan["holsterClip"] = One(null);
                    plan["emptyClip"] = One(db.Path("Weapons/Guns/gun_pistol_dry_fire_02.wav"));
                    plan["bulletImpactOverride"] = One(db.Path("Explosion_Fire_Gas/explosion_large_01.wav"));
                    break;
                case WeaponClass.Melee:
                    plan["fireClips"] = db.Series("Weapons/Knife", @"^whoosh_weapon_knife_swing_\d+\.wav$", 4);
                    plan["drawClip"] = One(db.Path("Weapons/Knife/knife_unsheathe_01.wav"));
                    plan["holsterClip"] = One(db.Path("Weapons/Knife/knife_unsheathe_02.wav"));
                    plan["bulletImpactOverride"] = One(db.Path("Weapons/Knife/knife_hit_small_01.wav"));
                    break;
            }

            return plan;
        }

        private static void ApplyLibraryPlan(AudioLibrary library, Dictionary<string, AudioClip[]> plan)
        {
            library.gameIntro = Single(plan, "gameIntro");
            library.uiClick = Single(plan, "uiClick");
            library.uiHover = Single(plan, "uiHover");
            library.uiPanelOpen = Single(plan, "uiPanelOpen");
            library.uiPanelClose = Single(plan, "uiPanelClose");
            library.uiNotification = Single(plan, "uiNotification");
            library.uiCountdownBeep = Single(plan, "uiCountdownBeep");
            library.uiCountdownFinal = Single(plan, "uiCountdownFinal");
            library.uiMatchFound = Single(plan, "uiMatchFound");
            library.hitMarkerTick = Single(plan, "hitMarkerTick");
            library.hitMarkerHeadshot = Single(plan, "hitMarkerHeadshot");
            library.weaponDepleted = Single(plan, "weaponDepleted");
            library.killConfirm = Single(plan, "killConfirm");
            library.multiKillClips = plan["multiKillClips"];
            library.lowHealthHeartbeat = Single(plan, "lowHealthHeartbeat");
            library.playerDeathStinger = Single(plan, "playerDeathStinger");
            library.bgmPlaylist = plan["bgmPlaylist"];
            library.bgmHome = Single(plan, "bgmHome");
            library.bgmMatch = Single(plan, "bgmMatch");
            library.bgmIntense = Single(plan, "bgmIntense");
            library.bgmResults = Single(plan, "bgmResults");
            library.footstepWalk = plan["footstepWalk"];
            library.footstepSprint = plan["footstepSprint"];
            library.footstepLand = plan["footstepLand"];
            library.footstepJump = Single(plan, "footstepJump");
            library.footstepRoll = Single(plan, "footstepRoll");
            library.explosionGrenade = Single(plan, "explosionGrenade");
            library.explosionRocket = Single(plan, "explosionRocket");
            library.bulletImpact = Single(plan, "bulletImpact");
            library.consumableUse = Single(plan, "consumableUse");
            library.throwablePull = Single(plan, "throwablePull");
            library.ambienceVariants = plan["ambienceVariants"];
            EditorUtility.SetDirty(library);
        }

        private static void ApplyWeaponPlan(WeaponAudioProfile profile, Dictionary<string, AudioClip[]> plan)
        {
            profile.fireClips = plan["fireClips"];
            profile.fireSuppressedClips = plan["fireSuppressedClips"];
            profile.reloadStartClip = Single(plan, "reloadStartClip");
            profile.reloadEndClip = Single(plan, "reloadEndClip");
            profile.reloadTacticalClip = Single(plan, "reloadTacticalClip");
            profile.drawClip = Single(plan, "drawClip");
            profile.holsterClip = Single(plan, "holsterClip");
            profile.emptyClip = Single(plan, "emptyClip");
            profile.bulletImpactOverride = Single(plan, "bulletImpactOverride");
            EditorUtility.SetDirty(profile);
        }

        private static AudioClip[] One(AudioClip clip)
            => clip != null ? new[] { clip } : System.Array.Empty<AudioClip>();

        private static AudioClip[] Many(params AudioClip[] clips)
            => clips.Where(c => c != null).ToArray();

        private static AudioClip Single(Dictionary<string, AudioClip[]> plan, string key)
            => plan.TryGetValue(key, out var clips) && clips.Length > 0 ? clips[0] : null;

        private static void AppendPlan(StringBuilder report, string label, Dictionary<string, AudioClip[]> plan)
        {
            report.AppendLine();
            report.AppendLine(label);
            foreach (var kvp in plan)
            {
                string value = kvp.Value == null || kvp.Value.Length == 0
                    ? "<none>"
                    : string.Join(", ", kvp.Value.Select(c => c != null ? c.name : "null"));
                report.AppendLine($"  {kvp.Key}: {value}");
            }
        }

        private sealed class ClipCatalog
        {
            private readonly Dictionary<string, AudioClip> _byPath;
            private readonly AudioClip[] _clips;

            public ClipCatalog(string root)
            {
                string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { root });
                _byPath = new Dictionary<string, AudioClip>();
                var clips = new List<AudioClip>(guids.Length);

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid).Replace("\\", "/");
                    if (!path.StartsWith(root + "/", System.StringComparison.Ordinal))
                        continue;

                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null)
                        continue;

                    _byPath[path] = clip;
                    clips.Add(clip);
                }

                _clips = clips.ToArray();
            }

            public int Count => _clips.Length;

            public AudioClip Path(string relativePath)
                => _byPath.TryGetValue($"{Root}/{relativePath}", out var clip) ? clip : null;

            public AudioClip[] Folder(string relativeFolder)
            {
                string prefix = $"{Root}/{relativeFolder}/";
                return _byPath
                    .Where(kvp => kvp.Key.StartsWith(prefix, System.StringComparison.Ordinal))
                    .Select(kvp => kvp.Value)
                    .ToArray();
            }

            public AudioClip[] Series(string relativeFolder, string fileNamePattern, int max)
            {
                var regex = new Regex(fileNamePattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                string prefix = $"{Root}/{relativeFolder}/";
                return _byPath
                    .Where(kvp => kvp.Key.StartsWith(prefix, System.StringComparison.Ordinal)
                                  && regex.IsMatch(System.IO.Path.GetFileName(kvp.Key)))
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => kvp.Value)
                    .Take(max)
                    .ToArray();
            }
        }
    }
}
