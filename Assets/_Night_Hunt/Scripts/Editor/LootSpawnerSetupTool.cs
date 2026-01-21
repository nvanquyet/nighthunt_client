using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using NightHunt.Data;
using NightHunt.Gameplay.Loot;

namespace NightHunt.Editor
{
    /// <summary>
    /// Tool để setup LootSpawner với spawn points từ scene
    /// </summary>
    public class LootSpawnerSetupTool : EditorWindow
    {
        private GameObject selectedSpawnPointParent;
        private bool useSelectedObjects = false;

        [MenuItem("Night Hunt/Setup Tools/Loot Spawner Setup")]
        public static void ShowWindow()
        {
            GetWindow<LootSpawnerSetupTool>("Loot Spawner Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("Loot Spawner Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("Tool này sẽ tự động setup LootSpawner với spawn points từ scene.", MessageType.Info);
            EditorGUILayout.Space();

            useSelectedObjects = EditorGUILayout.Toggle("Use Selected Objects", useSelectedObjects);

            if (!useSelectedObjects)
            {
                selectedSpawnPointParent = (GameObject)EditorGUILayout.ObjectField(
                    "Spawn Points Parent:",
                    selectedSpawnPointParent,
                    typeof(GameObject),
                    true
                );
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Setup LootSpawner", GUILayout.Height(30)))
            {
                SetupLootSpawner();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Create Spawn Points Grid"))
            {
                CreateSpawnPointsGrid();
            }
        }

        /// <summary>
        /// Setup LootSpawner với spawn points
        /// </summary>
        private void SetupLootSpawner()
        {
            // Find or create LootSpawner
            LootSpawner spawner = FindObjectOfType<LootSpawner>();
            if (spawner == null)
            {
                GameObject spawnerObj = new GameObject("LootSpawner");
                spawner = spawnerObj.AddComponent<LootSpawner>();
            }

            // Get spawn points
            List<LootSpawnPoint> spawnPoints = new List<LootSpawnPoint>();

            if (useSelectedObjects)
            {
                // Use selected GameObjects
                foreach (GameObject obj in Selection.gameObjects)
                {
                    LootSpawnPoint point = obj.GetComponent<LootSpawnPoint>();
                    if (point == null)
                    {
                        point = obj.AddComponent<LootSpawnPoint>();
                    }
                    spawnPoints.Add(point);
                }
            }
            else if (selectedSpawnPointParent != null)
            {
                // Get all children
                foreach (Transform child in selectedSpawnPointParent.transform)
                {
                    LootSpawnPoint point = child.GetComponent<LootSpawnPoint>();
                    if (point == null)
                    {
                        point = child.gameObject.AddComponent<LootSpawnPoint>();
                    }
                    spawnPoints.Add(point);
                }
            }
            else
            {
                // Find all LootSpawnPoint in scene
                spawnPoints.AddRange(FindObjectsOfType<LootSpawnPoint>());
            }

            if (spawnPoints.Count == 0)
            {
                EditorUtility.DisplayDialog("Warning", "No spawn points found. Please create spawn points first.", "OK");
                return;
            }

            // Setup spawner
            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty spawnPointsProp = so.FindProperty("spawnPoints");
            spawnPointsProp.arraySize = spawnPoints.Count;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                spawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            }
            so.ApplyModifiedProperties();

            EditorUtility.DisplayDialog("Success", $"LootSpawner setup với {spawnPoints.Count} spawn points!", "OK");
        }

        /// <summary>
        /// Tạo grid spawn points
        /// </summary>
        private void CreateSpawnPointsGrid()
        {
            GameObject parent = new GameObject("LootSpawnPoints_Grid");
            
            int gridSize = 5;
            float spacing = 3f;
            Vector3 startPos = new Vector3(-gridSize * spacing / 2f, 0.5f, -gridSize * spacing / 2f);

            for (int x = 0; x < gridSize; x++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 pos = startPos + new Vector3(x * spacing, 0, z * spacing);
                    GameObject spawnPoint = new GameObject($"SpawnPoint_{x}_{z}");
                    spawnPoint.transform.SetParent(parent.transform);
                    spawnPoint.transform.position = pos;

                    LootSpawnPoint point = spawnPoint.AddComponent<LootSpawnPoint>();
                    
                    // Visual marker
                    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    marker.transform.SetParent(spawnPoint.transform);
                    marker.transform.localPosition = Vector3.zero;
                    marker.transform.localScale = Vector3.one * 0.3f;
                    marker.name = "Marker";
                    marker.GetComponent<Renderer>().material.color = Color.green;
                }
            }

            selectedSpawnPointParent = parent;
            Selection.activeGameObject = parent;
            
            EditorUtility.DisplayDialog("Success", $"Created {gridSize * gridSize} spawn points in grid!", "OK");
        }
    }
}

