using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

namespace NightHunt.Networking.ClientOnly
{
    /// <summary>
    /// Automatically disables all rendering components when building dedicated server.
    /// This script runs at build time (UNITY_SERVER define) to optimize server builds.
    /// </summary>
    public class RenderDisabler : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private void Awake()
        {
            #if UNITY_SERVER
            DisableAllRenderComponents();
            #else
            // Not a server build, disable this component
            enabled = false;
            #endif
        }

        /// <summary>
        /// Disables all rendering-related components in the scene
        /// </summary>
        private void DisableAllRenderComponents()
        {
            if (enableDebugLogs)
                Debug.Log("[RenderDisabler] Starting to disable all render components for dedicated server...");

            int disabledCount = 0;
            var disabledComponents = new List<string>();

            // Disable all Camera components
            Camera[] cameras = FindObjectsOfType<Camera>(true);
            foreach (var camera in cameras)
            {
                if (camera != null && camera.enabled)
                {
                    camera.enabled = false;
                    disabledCount++;
                    disabledComponents.Add($"Camera: {camera.name}");
                }
            }

            // Disable all CinemachineCamera components
            CinemachineCamera[] cinemachineCameras = FindObjectsOfType<CinemachineCamera>(true);
            foreach (var cmCamera in cinemachineCameras)
            {
                if (cmCamera != null && cmCamera.enabled)
                {
                    cmCamera.enabled = false;
                    disabledCount++;
                    disabledComponents.Add($"CinemachineCamera: {cmCamera.name}");
                }
            }

            // Disable all MeshRenderer components
            MeshRenderer[] meshRenderers = FindObjectsOfType<MeshRenderer>(true);
            foreach (var renderer in meshRenderers)
            {
                if (renderer != null && renderer.enabled)
                {
                    renderer.enabled = false;
                    disabledCount++;
                    disabledComponents.Add($"MeshRenderer: {renderer.name}");
                }
            }

            // Disable all SkinnedMeshRenderer components
            SkinnedMeshRenderer[] skinnedMeshRenderers = FindObjectsOfType<SkinnedMeshRenderer>(true);
            foreach (var renderer in skinnedMeshRenderers)
            {
                if (renderer != null && renderer.enabled)
                {
                    renderer.enabled = false;
                    disabledCount++;
                    disabledComponents.Add($"SkinnedMeshRenderer: {renderer.name}");
                }
            }

            // Disable all ParticleSystem components
            ParticleSystem[] particleSystems = FindObjectsOfType<ParticleSystem>(true);
            foreach (var ps in particleSystems)
            {
                if (ps != null && ps.gameObject.activeSelf)
                {
                    ps.gameObject.SetActive(false);
                    disabledCount++;
                    disabledComponents.Add($"ParticleSystem: {ps.name}");
                }
            }

            // Disable all LineRenderer components
            LineRenderer[] lineRenderers = FindObjectsOfType<LineRenderer>(true);
            foreach (var lr in lineRenderers)
            {
                if (lr != null && lr.enabled)
                {
                    lr.enabled = false;
                    disabledCount++;
                    disabledComponents.Add($"LineRenderer: {lr.name}");
                }
            }

            // Disable all TrailRenderer components
            TrailRenderer[] trailRenderers = FindObjectsOfType<TrailRenderer>(true);
            foreach (var tr in trailRenderers)
            {
                if (tr != null && tr.enabled)
                {
                    tr.enabled = false;
                    disabledCount++;
                    disabledComponents.Add($"TrailRenderer: {tr.name}");
                }
            }

            // Disable all Canvas components (UI not needed on server)
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            foreach (var canvas in canvases)
            {
                if (canvas != null && canvas.gameObject.activeSelf)
                {
                    canvas.gameObject.SetActive(false);
                    disabledCount++;
                    disabledComponents.Add($"Canvas: {canvas.name}");
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[RenderDisabler] Disabled {disabledCount} render components for dedicated server build.");
                if (disabledComponents.Count > 0 && disabledComponents.Count <= 20)
                {
                    Debug.Log($"[RenderDisabler] Disabled components:\n{string.Join("\n", disabledComponents)}");
                }
                else if (disabledComponents.Count > 20)
                {
                    Debug.Log($"[RenderDisabler] Disabled {disabledComponents.Count} components (too many to list)");
                }
            }
        }
    }
}
