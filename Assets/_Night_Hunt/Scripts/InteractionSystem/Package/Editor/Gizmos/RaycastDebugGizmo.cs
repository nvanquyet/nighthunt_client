#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NightHunt.InteractionSystem.Interaction.Detection;
using NightHunt.InteractionSystem.Pickup.Detection;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.InteractionSystem.Editor.Gizmos
{
    /// <summary>
    /// Draws raycast gizmos for InteractionDetector and PickupDetector in Scene View.
    /// Works even when objects are not selected.
    /// </summary>
    [InitializeOnLoad]
    public static class RaycastDebugGizmo
    {
        private static bool showAllRaycasts = true;

        static RaycastDebugGizmo()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!showAllRaycasts)
                return;

            // Draw InteractionDetector raycasts
            InteractionDetector[] interactionDetectors = Object.FindObjectsOfType<InteractionDetector>();
            foreach (var detector in interactionDetectors)
            {
                if (detector == null)
                    continue;

                DrawInteractionRaycast(detector);
            }

            // Draw PickupDetector raycasts
            PickupDetector[] pickupDetectors = Object.FindObjectsOfType<PickupDetector>();
            foreach (var detector in pickupDetectors)
            {
                if (detector == null)
                    continue;

                DrawPickupRaycast(detector);
            }
        }

        private static void DrawInteractionRaycast(InteractionDetector detector)
        {
            if (detector == null)
                return;

            Vector3 start, end;
            bool hasHit;
            RaycastHit hit;
            detector.GetRaycastInfo(out start, out end, out hasHit, out hit);

            IInteractable currentTarget = detector.GetCurrentTarget();

            if (hasHit)
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null && currentTarget == interactable)
                {
                    Handles.color = Color.green;
                }
                else if (interactable != null)
                {
                    Handles.color = Color.yellow; // Hit interactable but not current target
                }
                else
                {
                    Handles.color = Color.red; // Hit something but not interactable
                }
            }
            else
            {
                Handles.color = Color.cyan; // No hit
            }

            // Draw line
            Handles.DrawLine(start, end);

            // Draw hit point
            if (hasHit)
            {
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(hit.point, hit.normal, 0.2f);
            }

            // Draw label
            if (currentTarget != null)
            {
                Handles.Label(end + Vector3.up * 0.5f, $"Target: {currentTarget.GetInteractionText()}", EditorStyles.boldLabel);
            }
        }

        private static void DrawPickupRaycast(PickupDetector detector)
        {
            if (detector == null)
                return;

            Vector3 start, end;
            bool hasHit;
            RaycastHit hit;
            detector.GetRaycastInfo(out start, out end, out hasHit, out hit);

            IPickupable currentTarget = detector.GetCurrentTarget();

            if (hasHit)
            {
                IPickupable pickupable = hit.collider.GetComponent<IPickupable>();
                if (pickupable != null && currentTarget == pickupable)
                {
                    Handles.color = Color.green;
                }
                else if (pickupable != null)
                {
                    Handles.color = Color.magenta; // Hit pickupable but not current target
                }
                else
                {
                    Handles.color = Color.red; // Hit something but not pickupable
                }
            }
            else
            {
                Handles.color = Color.blue; // No hit
            }

            // Draw line
            Handles.DrawLine(start, end);

            // Draw hit point
            if (hasHit)
            {
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(hit.point, hit.normal, 0.2f);
            }

            // Draw label
            if (currentTarget != null)
            {
                Handles.Label(end + Vector3.up * 0.5f, $"Pickup: {currentTarget.GetDisplayName()}", EditorStyles.boldLabel);
            }
        }

        [MenuItem("NightHunt/InteractionSystem/Toggle Raycast Gizmos")]
        private static void ToggleRaycastGizmos()
        {
            showAllRaycasts = !showAllRaycasts;
            Debug.Log($"[RaycastDebugGizmo] Raycast gizmos: {(showAllRaycasts ? "ENABLED" : "DISABLED")}");
        }
    }
}
#endif
