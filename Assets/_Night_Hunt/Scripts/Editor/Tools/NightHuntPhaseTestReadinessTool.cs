using System.Collections.Generic;
using System.IO;
using System.Text;
using NightHunt.Core;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.Gameplay.Map;
using NightHunt.GameplaySystems.Core.Configs;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightHunt.Editor.Tools
{
    public static class NightHuntPhaseTestReadinessTool
    {
        private const string DebugConfigPath = "Assets/_Night_Hunt/Data/Configs/CoreSystem Config/Resources/NightHuntDebugConfig.asset";
        private const string InputActionsPath = "Assets/_Night_Hunt/Inputs/InputSystem_Actions.inputactions";
        private const string WeaponFolder = "Assets/_Night_Hunt/Prefabs/Items/Weapon";
        private const string Map05Path = "Assets/_Night_Hunt/Prefabs/Maps/Map 05.prefab";
        private const string ReportPath = "Assets/_Night_Hunt/Reports/PhaseTestReadinessReport.md";

        [MenuItem("NightHunt/Tools/Phase Test/Write Readiness Report")]
        public static void WriteReadinessReport()
        {
            EnsureFolder(Path.GetDirectoryName(ReportPath)?.Replace("\\", "/"));

            var sb = new StringBuilder();
            sb.AppendLine("# NightHunt Phase Test Readiness");
            sb.AppendLine();
            AppendDebugConfig(sb);
            AppendLayers(sb);
            AppendInput(sb);
            AppendWeaponPrefabs(sb);
            AppendMap05Collision(sb);

            File.WriteAllText(ReportPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(ReportPath);
            AssetDatabase.Refresh();
            Debug.Log($"[NightHuntPhaseTestReadinessTool] Report written: {ReportPath}");
        }

        [MenuItem("NightHunt/Tools/Phase Test/Enable Full Phase Test Logs")]
        public static void EnableFullPhaseTestLogs()
        {
            SetPhaseTestLogs(true);
        }

        [MenuItem("NightHunt/Tools/Phase Test/Disable Phase Test Logs")]
        public static void DisablePhaseTestLogs()
        {
            SetPhaseTestLogs(false);
        }

        private static void SetPhaseTestLogs(bool enabled)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<NightHuntDebugConfig>(DebugConfigPath);
            if (cfg == null)
            {
                Debug.LogWarning($"[NightHuntPhaseTestReadinessTool] Debug config not found: {DebugConfigPath}");
                return;
            }

            var so = new SerializedObject(cfg);
            SetBool(so, "EnablePhaseTestLogs", enabled);
            SetString(so, "PhaseTestLogFilter", string.Empty);

            string[] categoryFields =
            {
                "PhaseTestLogInput",
                "PhaseTestLogWeapon",
                "PhaseTestLogAnimation",
                "PhaseTestLogIK",
                "PhaseTestLogInteraction",
                "PhaseTestLogItemUse",
                "PhaseTestLogDeploy",
                "PhaseTestLogThrowable",
                "PhaseTestLogDeath",
                "PhaseTestLogSpectate",
                "PhaseTestLogScore",
                "PhaseTestLogPhysics"
            };

            for (int i = 0; i < categoryFields.Length; i++)
                SetBool(so, categoryFields[i], enabled);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NightHuntPhaseTestReadinessTool] Phase test logs {(enabled ? "enabled" : "disabled")}.");
        }

        private static void AppendDebugConfig(StringBuilder sb)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<NightHuntDebugConfig>(DebugConfigPath);
            sb.AppendLine("## Debug Config");
            sb.AppendLine();
            if (cfg == null)
            {
                sb.AppendLine($"- Missing `{DebugConfigPath}`");
                sb.AppendLine();
                return;
            }

            sb.AppendLine($"- Phase logs: `{cfg.EnablePhaseTestLogs}`");
            sb.AppendLine($"- Filter: `{cfg.PhaseTestLogFilter}`");
            sb.AppendLine($"- Projectile logs: `{cfg.EnableProjectileDebugLogs}`");
            sb.AppendLine($"- Interaction logs: `{cfg.EnableInteractionDebugLogs}`");
            sb.AppendLine($"- Deploy logs: `{cfg.EnableDeployableDebugLogs}`");
            sb.AppendLine();
        }

        private static void AppendLayers(StringBuilder sb)
        {
            sb.AppendLine("## Layers And Masks");
            sb.AppendLine();
            string[] layers =
            {
                NightHuntLayers.Player,
                NightHuntLayers.PlayerHitBox,
                NightHuntLayers.Projectile,
                NightHuntLayers.Interactable,
                NightHuntLayers.Zone,
                NightHuntLayers.Throwable,
                NightHuntLayers.DeadCharacter,
                NightHuntLayers.MapObstacle,
                NightHuntLayers.Items,
                NightHuntLayers.MapStatic,
                NightHuntLayers.Wall,
                NightHuntLayers.Ground,
                NightHuntLayers.SeeThrough
            };

            for (int i = 0; i < layers.Length; i++)
                sb.AppendLine($"- `{layers[i]}` -> `{LayerMask.NameToLayer(layers[i])}`");

            sb.AppendLine();
            sb.AppendLine($"- `MaskHitscanFull`: `{NightHuntLayers.MaskHitscanFull.value}`");
            sb.AppendLine($"- `MaskPlacementSurface`: `{NightHuntLayers.MaskPlacementSurface.value}`");
            sb.AppendLine($"- `MaskGroundCheck`: `{NightHuntLayers.MaskGroundCheck.value}`");
            sb.AppendLine($"- `MaskInteractScan`: `{NightHuntLayers.MaskInteractScan.value}`");
            sb.AppendLine();
        }

        private static void AppendInput(StringBuilder sb)
        {
            sb.AppendLine("## Input Actions");
            sb.AppendLine();

            if (!File.Exists(InputActionsPath))
            {
                sb.AppendLine($"- Missing `{InputActionsPath}`");
                sb.AppendLine();
                return;
            }

            var asset = InputActionAsset.FromJson(File.ReadAllText(InputActionsPath));
            string[] requiredActions =
            {
                "Player/Move",
                "Player/Sprint",
                "Player/Crouch",
                "Player/Jump",
                "Player/Roll",
                "Player/Interact",
                "Player/Pickup",
                "Combat/Fire",
                "Combat/Reload",
                "Combat/WeaponSlot1",
                "Combat/WeaponSlot2",
                "Combat/WeaponSlot3",
                "Combat/ThrowGrenade",
                "Combat/UseAbility",
                "Combat/MousePosition"
            };

            for (int i = 0; i < requiredActions.Length; i++)
            {
                string[] parts = requiredActions[i].Split('/');
                var map = asset.FindActionMap(parts[0], throwIfNotFound: false);
                var action = map?.FindAction(parts[1], throwIfNotFound: false);
                if (action == null)
                {
                    sb.AppendLine($"- MISSING `{requiredActions[i]}`");
                    continue;
                }

                sb.AppendLine($"- `{requiredActions[i]}` -> {FormatBindings(action)}");
            }

            sb.AppendLine();
            AppendDuplicateInputBindings(sb, asset);
            sb.AppendLine();
        }

        private static void AppendDuplicateInputBindings(StringBuilder sb, InputActionAsset asset)
        {
            sb.AppendLine("### Duplicate Bindings In Same Map");
            bool any = false;
            foreach (var map in asset.actionMaps)
            {
                var byPath = new Dictionary<string, List<string>>();
                foreach (var binding in map.bindings)
                {
                    if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrWhiteSpace(binding.effectivePath))
                        continue;

                    if (!byPath.TryGetValue(binding.effectivePath, out var actions))
                    {
                        actions = new List<string>();
                        byPath[binding.effectivePath] = actions;
                    }

                    actions.Add(binding.action);
                }

                foreach (var pair in byPath)
                {
                    if (pair.Value.Count <= 1)
                        continue;

                    any = true;
                    sb.AppendLine($"- `{map.name}` path `{pair.Key}` used by `{string.Join("`, `", pair.Value)}`");
                }
            }

            if (!any)
                sb.AppendLine("- none");
        }

        private static void AppendWeaponPrefabs(StringBuilder sb)
        {
            sb.AppendLine("## Weapon Prefab IK And Ballistics");
            sb.AppendLine();
            sb.AppendLine("| Prefab | Component | Projectile | FirePoint | LeftHandIK | Range | Speed | Base Pos | Base Rot | Renderer Bounds |");
            sb.AppendLine("|---|---|---|---|---:|---:|---:|---|---|---|");

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { WeaponFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var weapon = root.GetComponentInChildren<WeaponBase>(true);
                    if (weapon == null)
                    {
                        sb.AppendLine($"| `{Path.GetFileName(path)}` | MISSING WeaponBase | - | - | - | - | - | - | - | - |");
                        continue;
                    }

                    Bounds bounds = CalculateRendererBounds(root);
                    string projectile = weapon.ProjectilePrefab != null ? weapon.ProjectilePrefab.name : "null";
                    sb.AppendLine(
                        $"| `{Path.GetFileName(path)}` | `{weapon.GetType().Name}` | `{projectile}` | `{DescribeLocal(weapon.FirePoint)}` | `{DescribeLocal(weapon.LeftHandIKTarget)}` | {weapon.MaxRange:F1} | {weapon.ProjectileSpeed:F1} | `{weapon.BaseLocalPosition:F3}` | `{weapon.BaseLocalRotation:F1}` | `{bounds.size:F2}` |");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            sb.AppendLine();
        }

        private static void AppendMap05Collision(StringBuilder sb)
        {
            sb.AppendLine("## Map 05 Collision");
            sb.AppendLine();
            if (!File.Exists(Map05Path))
            {
                sb.AppendLine($"- Missing `{Map05Path}`");
                sb.AppendLine();
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(Map05Path);
            try
            {
                var colliders = root.GetComponentsInChildren<Collider>(true);
                var generated = root.GetComponentsInChildren<NightHuntAutoMapCollider>(true);
                var byLayer = new Dictionary<string, int>();
                for (int i = 0; i < colliders.Length; i++)
                {
                    var col = colliders[i];
                    string layer = LayerMask.LayerToName(col.gameObject.layer);
                    if (string.IsNullOrWhiteSpace(layer))
                        layer = col.gameObject.layer.ToString();

                    byLayer.TryGetValue(layer, out int count);
                    byLayer[layer] = count + 1;
                }

                sb.AppendLine($"- Total colliders: `{colliders.Length}`");
                sb.AppendLine($"- Generated auto colliders: `{generated.Length}`");
                foreach (var pair in byLayer)
                    sb.AppendLine($"- Layer `{pair.Key}` colliders: `{pair.Value}`");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            sb.AppendLine();
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static string DescribeLocal(Transform target)
        {
            if (target == null)
                return "null";

            return $"{target.name} pos={target.localPosition:F3} rot={target.localEulerAngles:F1}";
        }

        private static string FormatBindings(InputAction action)
        {
            var parts = new List<string>();
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                    continue;
                parts.Add($"`{binding.effectivePath}`");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "`no binding`";
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string name = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetBool(SerializedObject so, string propertyName, bool value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null)
                prop.boolValue = value;
        }

        private static void SetString(SerializedObject so, string propertyName, string value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null)
                prop.stringValue = value ?? string.Empty;
        }
    }
}
