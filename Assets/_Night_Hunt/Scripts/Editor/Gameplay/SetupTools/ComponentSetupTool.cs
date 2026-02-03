using UnityEngine;
using UnityEditor;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Vision;
using NightHunt.Gameplay.Camera.Cinemachine;
using NightHunt.Networking;

namespace NightHunt.Editor.Gameplay.SetupTools
{
    /// <summary>
    /// Auto-add required components on objects
    /// </summary>
    public static class ComponentSetupTool
    {
        [MenuItem("Tools/Night Hunt/Setup Player Prefab")]
        public static void SetupPlayerPrefab()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a GameObject to setup!", "OK");
                return;
            }

            SetupPlayerComponents(selected);
            EditorUtility.DisplayDialog("Success", "Player prefab setup complete!", "OK");
        }

        [MenuItem("GameObject/Night Hunt/Setup Components", false, 10)]
        public static void SetupComponentsContextMenu()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) return;

            SetupPlayerComponents(selected);
        }

        /// <summary>
        /// Setup player components
        /// </summary>
        private static void SetupPlayerComponents(GameObject obj)
        {
            // Network components
            if (obj.GetComponent<NetworkPlayer>() == null)
            {
                obj.AddComponent<NetworkPlayer>();
            }

            // Character components
            if (obj.GetComponent<CharacterController>() == null)
            {
                obj.AddComponent<CharacterController>();
            }

            if (obj.GetComponent<CharacterStats>() == null)
            {
                obj.AddComponent<CharacterStats>();
            }

            if (obj.GetComponent<IMovementController>() == null)
            {
                obj.AddComponent<CharacterNormalMovement>();
            }

            if (obj.GetComponent<CharacterCombat>() == null)
            {
                obj.AddComponent<CharacterCombat>();
            }

            // Input components
            if (obj.GetComponent<PlayerInputHandler>() == null)
            {
                obj.AddComponent<PlayerInputHandler>();
            }

            // Vision components
            if (obj.GetComponent<VisionRevealer>() == null)
            {
                obj.AddComponent<VisionRevealer>();
            }

            // Camera components
            if (obj.GetComponent<CinemachineCameraController>() == null)
            {
                obj.AddComponent<CinemachineCameraController>();
            }

            Debug.Log($"[ComponentSetupTool] Setup complete for: {obj.name}");
        }
    }
}

