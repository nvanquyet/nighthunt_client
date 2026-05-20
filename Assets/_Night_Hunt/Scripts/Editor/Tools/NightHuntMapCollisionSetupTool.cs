#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using NightHunt.Gameplay.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightHunt.Editor.Tools
{
    public static class NightHuntMapCollisionSetupTool
    {
        private const string Map05PrefabPath = "Assets/_Night_Hunt/Prefabs/Maps/Map 05.prefab";
        private const string ReportDirectory = "Assets/_Night_Hunt/Reports";
        private const string GeneratedPrefix = "NH_AutoCollider_";

        private static readonly LayerSpec[] RequiredLayers =
        {
            new(10, "Interactable"),
            new(11, "Zone"),
            new(12, "Throwable"),
            new(15, "MapObstacle"),
            new(20, "Items"),
            new(21, "Minimap"),
            new(22, "MapStatic"),
            new(23, "Wall"),
            new(24, "Ground"),
            new(25, "SeeThrough"),
        };

        private static readonly string[] RequiredTags =
        {
            "Enemy",
            "Pickup",
            "Destroyable",
            "Usable",
            "Beacon",
            "Vehicle",
            "AIPlayer",
            "AINeutral",
        };

        private static readonly string[] VfxTerms =
        {
            "vfx", "fx", "particle", "smoke", "fire", "flame", "spark", "muzzle", "trail",
            "decal", "blood", "splash", "glow", "lens", "flare", "fog", "mist", "steam",
            "ui", "canvas", "icon", "marker", "minimap"
        };

        private static readonly string[] GroundTerms =
        {
            "ground", "floor", "terrain", "road", "street", "asphalt", "pavement", "path",
            "sidewalk", "walkway", "plane", "tile", "grass", "sand", "soil", "dirt",
            "concrete", "bridge_floor", "platform_floor"
        };

        private static readonly string[] WallTerms =
        {
            "wall", "fence", "barrier", "gate", "door", "window", "building", "house",
            "hangar", "warehouse", "facade", "partition", "roof", "ceiling"
        };

        private static readonly string[] ObstacleTerms =
        {
            "crate", "box", "container", "cover", "rock", "stone", "tree", "trunk", "bush",
            "vehicle", "car", "truck", "barrel", "tank", "pillar", "column", "stairs",
            "stair", "ramp", "rail", "bench", "table", "chair", "sandbag", "prop"
        };

        [MenuItem("NightHunt/Tools/Map Collision Setup/Apply Map 05 Phase Test Colliders", priority = 30)]
        public static void ApplyMap05ForPhaseTest()
        {
            EnsureLayerAndTagProjectSettings();
            ApplyToPrefab(Map05PrefabPath, clearGeneratedFirst: true, addMissingColliders: true);
        }

        [MenuItem("NightHunt/Tools/Map Collision Setup/Audit Map 05 Colliders", priority = 31)]
        public static void AuditMap05()
        {
            EnsureLayerAndTagProjectSettings();
            AuditPrefab(Map05PrefabPath);
        }

        [MenuItem("NightHunt/Tools/Map Collision Setup/Clear Generated Map 05 Colliders", priority = 32)]
        public static void ClearGeneratedMap05Colliders()
        {
            ClearGeneratedOnPrefab(Map05PrefabPath);
        }

        [MenuItem("NightHunt/Tools/Map Collision Setup/Apply To Selected Root", priority = 40)]
        public static void ApplyToSelectedRoot()
        {
            EnsureLayerAndTagProjectSettings();
            var selection = Selection.activeObject;
            string assetPath = AssetDatabase.GetAssetPath(selection);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
            {
                ApplyToPrefab(assetPath, clearGeneratedFirst: true, addMissingColliders: true);
                return;
            }

            var root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("[MapCollisionSetup] Select a scene root or prefab asset first.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Apply NightHunt map colliders");
            var report = ProcessRoot(root, clearGeneratedFirst: true, addMissingColliders: true);
            EditorSceneManager.MarkSceneDirty(root.scene);
            WriteReport(report, SanitizeFileName(root.name));
            Debug.Log(report.ToConsoleSummary());
        }

        [MenuItem("NightHunt/Tools/Map Collision Setup/Apply To Selected Root", true)]
        private static bool ValidateApplyToSelectedRoot()
        {
            return Selection.activeObject != null;
        }

        [MenuItem("NightHunt/Tools/Map Collision Setup/Clear Generated On Selected Root", priority = 41)]
        public static void ClearGeneratedOnSelectedRoot()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("[MapCollisionSetup] Select a scene root first.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Clear NightHunt generated map colliders");
            var report = new CollisionReport(root.name);
            ClearGeneratedColliders(root, report);
            EditorSceneManager.MarkSceneDirty(root.scene);
            WriteReport(report, SanitizeFileName(root.name));
            Debug.Log(report.ToConsoleSummary());
        }

        private static void ApplyToPrefab(string prefabPath, bool clearGeneratedFirst, bool addMissingColliders)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var report = ProcessRoot(root, clearGeneratedFirst, addMissingColliders);
                bool success;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out success);
                WriteReport(report, SanitizeFileName(Path.GetFileNameWithoutExtension(prefabPath)));
                Debug.Log(report.ToConsoleSummary() + $" saved={success} path={prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AuditPrefab(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var report = ProcessRoot(root, clearGeneratedFirst: false, addMissingColliders: false);
                WriteReport(report, SanitizeFileName(Path.GetFileNameWithoutExtension(prefabPath)));
                Debug.Log(report.ToConsoleSummary() + $" auditOnly=true path={prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ClearGeneratedOnPrefab(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var report = new CollisionReport(root.name);
                ClearGeneratedColliders(root, report);
                bool success;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out success);
                WriteReport(report, SanitizeFileName(Path.GetFileNameWithoutExtension(prefabPath)));
                Debug.Log(report.ToConsoleSummary() + $" saved={success} path={prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static CollisionReport ProcessRoot(GameObject root, bool clearGeneratedFirst, bool addMissingColliders)
        {
            var report = new CollisionReport(root.name);

            if (clearGeneratedFirst)
                ClearGeneratedColliders(root, report);

            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (var renderer in renderers)
            {
                report.ScannedRenderers++;

                if (!IsCandidateRenderer(renderer, report))
                    continue;

                MapCollisionClass classification = Classify(renderer);
                if (classification == MapCollisionClass.Skip)
                {
                    report.SkippedDecorative++;
                    continue;
                }

                int layer = ResolveLayer(classification);
                if (layer < 0)
                {
                    report.MissingLayerCount++;
                    report.AddIssue($"Missing layer for {classification}: {GetHierarchyPath(renderer.transform, root.transform)}");
                    continue;
                }

                SetRendererLayer(renderer, layer, report);

                if (HasExistingBlockingCollider(renderer.transform, root.transform))
                {
                    report.SkippedExistingCollider++;
                    continue;
                }

                report.NeedsCollider++;
                if (!addMissingColliders)
                {
                    report.AddIssue($"Missing collider: {classification} {GetHierarchyPath(renderer.transform, root.transform)}");
                    continue;
                }

                if (TryAddGeneratedCollider(renderer, root.transform, classification, layer, report))
                    report.AddedColliderCount++;
            }

            return report;
        }

        private static bool IsCandidateRenderer(Renderer renderer, CollisionReport report)
        {
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            {
                report.SkippedRendererType++;
                return false;
            }

            if (renderer.GetComponentInParent<Canvas>() != null ||
                renderer.GetComponentInParent<ParticleSystem>() != null)
            {
                report.SkippedRendererType++;
                return false;
            }

            string path = GetHierarchyPath(renderer.transform, null).ToLowerInvariant();
            if (ContainsAny(path, VfxTerms))
            {
                report.SkippedVfx++;
                return false;
            }

            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                report.SkippedNoMesh++;
                return false;
            }

            Vector3 size = renderer.bounds.size;
            if (size.x < 0.05f || size.y < 0.02f || size.z < 0.05f)
            {
                report.SkippedTiny++;
                return false;
            }

            if (Mathf.Max(size.x, size.y, size.z) < 0.25f)
            {
                report.SkippedTiny++;
                return false;
            }

            return true;
        }

        private static MapCollisionClass Classify(Renderer renderer)
        {
            string path = GetHierarchyPath(renderer.transform, null).ToLowerInvariant();
            Vector3 size = renderer.bounds.size;

            if (ContainsAny(path, GroundTerms))
                return MapCollisionClass.Ground;

            if (ContainsAny(path, WallTerms))
                return MapCollisionClass.Wall;

            if (ContainsAny(path, ObstacleTerms))
                return MapCollisionClass.MapObstacle;

            if (IsFlatHorizontal(size))
                return MapCollisionClass.Ground;

            if (IsTallThinVertical(size))
                return MapCollisionClass.Wall;

            if (size.y > 0.6f && Mathf.Max(size.x, size.z) > 0.4f)
                return MapCollisionClass.MapObstacle;

            if (Mathf.Max(size.x, size.z) > 2f)
                return MapCollisionClass.MapStatic;

            return MapCollisionClass.Skip;
        }

        private static bool TryAddGeneratedCollider(
            Renderer renderer,
            Transform root,
            MapCollisionClass classification,
            int layer,
            CollisionReport report)
        {
            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return false;

            Mesh mesh = meshFilter.sharedMesh;
            var colliderObject = new GameObject($"{GeneratedPrefix}{SanitizeObjectName(renderer.gameObject.name)}");
            colliderObject.transform.SetParent(renderer.transform, worldPositionStays: false);
            colliderObject.transform.localPosition = Vector3.zero;
            colliderObject.transform.localRotation = Quaternion.identity;
            colliderObject.transform.localScale = Vector3.one;
            colliderObject.layer = layer;
            colliderObject.isStatic = true;

            var marker = colliderObject.AddComponent<NightHuntAutoMapCollider>();
            marker.Initialize(GetHierarchyPath(renderer.transform, root), classification.ToString());

            if (ShouldUseMeshCollider(classification, renderer))
            {
                var meshCollider = colliderObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
                meshCollider.convex = false;
                meshCollider.isTrigger = false;
                report.AddedMeshColliders++;
            }
            else
            {
                var box = colliderObject.AddComponent<BoxCollider>();
                Bounds bounds = mesh.bounds;
                box.center = bounds.center;
                box.size = EnsureMinimumBoxSize(bounds.size);
                box.isTrigger = false;
                report.AddedBoxColliders++;
            }

            report.IncrementLayer(classification.ToString());
            return true;
        }

        private static bool ShouldUseMeshCollider(MapCollisionClass classification, Renderer renderer)
        {
            if (classification == MapCollisionClass.Ground)
                return true;

            string path = GetHierarchyPath(renderer.transform, null).ToLowerInvariant();
            if (path.Contains("ramp") || path.Contains("stair") || path.Contains("terrain"))
                return true;

            return false;
        }

        private static Vector3 EnsureMinimumBoxSize(Vector3 size)
        {
            size.x = Mathf.Max(size.x, 0.05f);
            size.y = Mathf.Max(size.y, 0.05f);
            size.z = Mathf.Max(size.z, 0.05f);
            return size;
        }

        private static bool HasExistingBlockingCollider(Transform transform, Transform root)
        {
            var childColliders = transform.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                Collider collider = childColliders[i];
                if (collider == null || collider.isTrigger)
                    continue;
                return true;
            }

            Transform current = transform.parent;
            while (current != null)
            {
                var colliders = current.GetComponents<Collider>();
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider collider = colliders[i];
                    if (collider != null && !collider.isTrigger)
                        return true;
                }

                if (current == root)
                    break;
                current = current.parent;
            }

            return false;
        }

        private static void SetRendererLayer(Renderer renderer, int layer, CollisionReport report)
        {
            if (renderer.gameObject.layer == layer)
                return;

            renderer.gameObject.layer = layer;
            report.AssignedRendererLayers++;
        }

        private static void ClearGeneratedColliders(GameObject root, CollisionReport report)
        {
            var markers = root.GetComponentsInChildren<NightHuntAutoMapCollider>(includeInactive: true);
            for (int i = markers.Length - 1; i >= 0; i--)
            {
                var marker = markers[i];
                if (marker == null)
                    continue;

                GameObject go = marker.gameObject;
                if (go != null && go.name.StartsWith(GeneratedPrefix))
                {
                    Object.DestroyImmediate(go);
                    report.ClearedGeneratedColliders++;
                }
            }
        }

        private static int ResolveLayer(MapCollisionClass classification)
        {
            return classification switch
            {
                MapCollisionClass.Ground => LayerMask.NameToLayer("Ground"),
                MapCollisionClass.Wall => LayerMask.NameToLayer("Wall"),
                MapCollisionClass.MapObstacle => LayerMask.NameToLayer("MapObstacle"),
                MapCollisionClass.MapStatic => LayerMask.NameToLayer("MapStatic"),
                _ => -1
            };
        }

        private static bool IsFlatHorizontal(Vector3 size)
        {
            float horizontal = Mathf.Max(size.x, size.z);
            float depth = Mathf.Min(size.x, size.z);
            return horizontal > 1.2f && depth > 0.5f && size.y <= Mathf.Max(0.35f, depth * 0.16f);
        }

        private static bool IsTallThinVertical(Vector3 size)
        {
            float horizontalMin = Mathf.Min(size.x, size.z);
            float horizontalMax = Mathf.Max(size.x, size.z);
            return size.y > 1.0f && horizontalMax > 0.5f && horizontalMin <= 0.55f;
        }

        private static bool ContainsAny(string value, string[] terms)
        {
            for (int i = 0; i < terms.Length; i++)
                if (value.Contains(terms[i]))
                    return true;
            return false;
        }

        private static string GetHierarchyPath(Transform transform, Transform stopAt)
        {
            if (transform == null)
                return string.Empty;

            var stack = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                stack.Push(current.name);
                if (current == stopAt)
                    break;
                current = current.parent;
            }

            return string.Join("/", stack);
        }

        private static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Renderer";

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return builder.ToString();
        }

        private static string SanitizeFileName(string value)
        {
            string sanitized = SanitizeObjectName(value);
            return string.IsNullOrEmpty(sanitized) ? "Map" : sanitized;
        }

        private static void WriteReport(CollisionReport report, string mapName)
        {
            if (!Directory.Exists(ReportDirectory))
                Directory.CreateDirectory(ReportDirectory);

            string path = $"{ReportDirectory}/MapCollisionAudit_{mapName}.txt";
            File.WriteAllText(path, report.ToDetailedReport());
            AssetDatabase.Refresh();
        }

        private static void EnsureLayerAndTagProjectSettings()
        {
            Object tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var tagManager = new SerializedObject(tagManagerAsset);

            SerializedProperty layers = tagManager.FindProperty("layers");
            foreach (var layer in RequiredLayers)
                EnsureLayer(layers, layer.Slot, layer.Name);

            SerializedProperty tags = tagManager.FindProperty("tags");
            foreach (string tag in RequiredTags)
                EnsureTag(tags, tag);

            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureLayer(SerializedProperty layers, int slot, string name)
        {
            SerializedProperty property = layers.GetArrayElementAtIndex(slot);
            if (property == null)
                return;

            string current = property.stringValue;
            if (string.IsNullOrEmpty(current))
                property.stringValue = name;
            else if (current != name)
                Debug.LogWarning($"[MapCollisionSetup] Layer slot {slot} has '{current}', expected '{name}'. Existing value kept.");
        }

        private static void EnsureTag(SerializedProperty tags, string name)
        {
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == name)
                    return;
            }

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = name;
        }

        private readonly struct LayerSpec
        {
            public readonly int Slot;
            public readonly string Name;

            public LayerSpec(int slot, string name)
            {
                Slot = slot;
                Name = name;
            }
        }

        private enum MapCollisionClass
        {
            Skip,
            Ground,
            Wall,
            MapObstacle,
            MapStatic,
        }

        private sealed class CollisionReport
        {
            private readonly string _targetName;
            private readonly Dictionary<string, int> _layerCounts = new();
            private readonly List<string> _issues = new();

            public int ScannedRenderers;
            public int NeedsCollider;
            public int AddedColliderCount;
            public int AddedBoxColliders;
            public int AddedMeshColliders;
            public int ClearedGeneratedColliders;
            public int AssignedRendererLayers;
            public int MissingLayerCount;
            public int SkippedExistingCollider;
            public int SkippedVfx;
            public int SkippedTiny;
            public int SkippedNoMesh;
            public int SkippedRendererType;
            public int SkippedDecorative;

            public CollisionReport(string targetName)
            {
                _targetName = targetName;
            }

            public void IncrementLayer(string name)
            {
                _layerCounts.TryGetValue(name, out int count);
                _layerCounts[name] = count + 1;
            }

            public void AddIssue(string issue)
            {
                if (_issues.Count < 400)
                    _issues.Add(issue);
            }

            public string ToConsoleSummary()
            {
                return $"[MapCollisionSetup] target={_targetName} scanned={ScannedRenderers} " +
                       $"needsCollider={NeedsCollider} added={AddedColliderCount} " +
                       $"box={AddedBoxColliders} mesh={AddedMeshColliders} " +
                       $"cleared={ClearedGeneratedColliders} layersSet={AssignedRendererLayers} " +
                       $"existingCollider={SkippedExistingCollider} vfx={SkippedVfx} tiny={SkippedTiny} " +
                       $"noMesh={SkippedNoMesh} decorative={SkippedDecorative} missingLayer={MissingLayerCount}";
            }

            public string ToDetailedReport()
            {
                var builder = new StringBuilder(4096);
                builder.AppendLine(ToConsoleSummary());
                builder.AppendLine();
                builder.AppendLine("Generated collider layer counts:");
                foreach (var kvp in _layerCounts)
                    builder.AppendLine($"- {kvp.Key}: {kvp.Value}");

                builder.AppendLine();
                builder.AppendLine("Skipped:");
                builder.AppendLine($"- Existing blocking collider: {SkippedExistingCollider}");
                builder.AppendLine($"- VFX/effect/minimap/UI: {SkippedVfx}");
                builder.AppendLine($"- Tiny decorative: {SkippedTiny}");
                builder.AppendLine($"- No MeshFilter mesh: {SkippedNoMesh}");
                builder.AppendLine($"- Unsupported renderer type: {SkippedRendererType}");
                builder.AppendLine($"- Decorative/unclassified: {SkippedDecorative}");

                if (_issues.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("Issues:");
                    for (int i = 0; i < _issues.Count; i++)
                        builder.AppendLine($"- {_issues[i]}");
                }

                return builder.ToString();
            }
        }
    }
}
#endif
