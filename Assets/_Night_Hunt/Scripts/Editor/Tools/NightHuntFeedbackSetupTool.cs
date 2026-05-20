using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Gameplay.ClientEffects;
using NightHunt.Gameplay.Feedback;

namespace NightHunt.Editor.Tools
{
    public static class NightHuntFeedbackSetupTool
    {
        private const string DamageNumberPrefabPath = "Assets/_Night_Hunt/Prefabs/UI/Prefab_DamageNumber.prefab";
        private const string HitIndicatorPrefabPath = "Assets/_Night_Hunt/Prefabs/UI/Prefab_HitIndicator.prefab";
        private const string HitConfirmPrefabPath = "Assets/_Night_Hunt/Prefabs/UI/Prefab_HitConfirm.prefab";
        private const string HitSparkPrefabPath = "Assets/_Night_Hunt/Prefabs/VFX/VFX_HitSpark_Template.prefab";
        private const string HealBurstPrefabPath = "Assets/_Night_Hunt/Prefabs/VFX/VFX_HealBurst_Template.prefab";
        private const string SystemsPrefabPath = "Assets/_Night_Hunt/Prefabs/Audio/Systems.prefab";

        private static readonly string[] GameplayScenes =
        {
            "Assets/_Night_Hunt/Scenes/02_Map_01.unity",
            "Assets/_Night_Hunt/Scenes/02_Map_02.unity"
        };

        [MenuItem("NightHunt/Tools/Feedback/Setup Feedback References")]
        public static void SetupFromMenu()
            => SetupAll();

        public static void SetupAll()
        {
            GameObject hitConfirmPrefab = EnsureHitConfirmPrefab();
            EnsureSimpleEffectPoolOnSystems();
            AssignDamageFeedbackReferences(hitConfirmPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NightHuntFeedbackSetupTool] Feedback prefab/scene setup complete.");
        }

        public static void SetupFeedbackAndWarfareAudio()
        {
            SetupAll();
            WarfareAudioAssigner.RunAutoAssignFromCommandLine();
            Debug.Log("[NightHuntFeedbackSetupTool] Feedback setup and WARFARE audio assignment complete.");
        }

        private static GameObject EnsureHitConfirmPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HitConfirmPrefabPath);
            if (existing != null)
                return existing;

            var root = new GameObject("Prefab_HitConfirm", typeof(RectTransform), typeof(CanvasGroup), typeof(HitConfirmIndicator));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(96f, 96f);

            var indicator = root.GetComponent<HitConfirmIndicator>();
            var group = root.GetComponent<CanvasGroup>();
            var segments = new Image[4];
            segments[0] = CreateSegment(root.transform, "TopRight", new Vector2(17f, 17f), -45f);
            segments[1] = CreateSegment(root.transform, "TopLeft", new Vector2(-17f, 17f), 45f);
            segments[2] = CreateSegment(root.transform, "BottomRight", new Vector2(17f, -17f), 45f);
            segments[3] = CreateSegment(root.transform, "BottomLeft", new Vector2(-17f, -17f), -45f);

            var serialized = new SerializedObject(indicator);
            serialized.FindProperty("_canvasGroup").objectReferenceValue = group;
            var array = serialized.FindProperty("_segments");
            array.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HitConfirmPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[NightHuntFeedbackSetupTool] Created {HitConfirmPrefabPath}");
            return prefab;
        }

        private static Image CreateSegment(Transform parent, string name, Vector2 anchoredPosition, float rotationZ)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(4f, 22f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private static void EnsureSimpleEffectPoolOnSystems()
        {
            var root = PrefabUtility.LoadPrefabContents(SystemsPrefabPath);
            if (root == null)
            {
                Debug.LogWarning($"[NightHuntFeedbackSetupTool] Systems prefab not found at {SystemsPrefabPath}; SimpleEffectPool was not added.");
                return;
            }

            try
            {
                if (root.GetComponentInChildren<SimpleEffectPool>(true) == null)
                {
                    root.AddComponent<SimpleEffectPool>();
                    PrefabUtility.SaveAsPrefabAsset(root, SystemsPrefabPath);
                    Debug.Log("[NightHuntFeedbackSetupTool] Added SimpleEffectPool to Systems prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssignDamageFeedbackReferences(GameObject hitConfirmPrefab)
        {
            var damageNumberPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DamageNumberPrefabPath);
            var hitIndicatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitIndicatorPrefabPath);
            var hitSparkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitSparkPrefabPath);
            var healBurstPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealBurstPrefabPath);

            foreach (string scenePath in GameplayScenes)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                bool changed = false;
                foreach (var feedback in Object.FindObjectsByType<DamageFeedbackSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var serialized = new SerializedObject(feedback);
                    changed |= Assign(serialized, "damageNumberPrefab", damageNumberPrefab);
                    changed |= Assign(serialized, "hitIndicatorPrefab", hitIndicatorPrefab);
                    changed |= Assign(serialized, "hitConfirmPrefab", hitConfirmPrefab);
                    changed |= Assign(serialized, "_hitSparksPrefab", hitSparkPrefab);
                    changed |= Assign(serialized, "_healBurstPrefab", healBurstPrefab);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (changed)
                        EditorUtility.SetDirty(feedback);
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[NightHuntFeedbackSetupTool] Updated {scenePath}");
                }
            }
        }

        private static bool Assign(SerializedObject serialized, string propertyName, Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value)
                return false;

            property.objectReferenceValue = value;
            return true;
        }
    }
}
