using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Managing;

namespace NightHunt.Networking
{
    /// <summary>
    /// Place ONE instance anywhere in the scene (e.g. on the NetworkManager GO).
    ///
    /// On a dedicated server startup, disables every Canvas root in the scene so:
    ///   • No UI rendering overhead on server.
    ///   • Child MonoBehaviour UI scripts that override Awake/Start
    ///     are never Init'd on server (Canvas disabled → OnEnable not called).
    ///   • Any child NetworkBehaviour that would throw NullRef because
    ///     NetworkObject isn't spawned yet is silenced.
    ///
    /// Order of events:
    ///   This script runs in Awake with ExecutionOrder -50 (after InputLayerManager at -100,
    ///   before normal Awake scripts), so Canvas children haven't had a chance to
    ///   run their Awake yet when we disable.
    ///
    ///   If a Canvas needs to stay active on server (e.g. server admin panel),
    ///   tag it with the "ServerCanvas" tag to exclude it from suppression.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ServerUISuppressor : MonoBehaviour
    {
        [Tooltip("Tag applied to any Canvas that should remain active on the server.")]
        [SerializeField] private string _serverCanvasTag = "ServerCanvas";

        private void Awake()
        {
            // Dedicated server: IsServerStarted=true AND IsClientStarted=false
            bool isDedicatedServer = InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted;
            if (!isDedicatedServer) return;

            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int disabled = 0;
            foreach (var canvas in canvases)
            {
                // Skip root-level if Canvas is on a NetworkObject spawned per-player
                // (those are disabled automatically when server has no observer)
                if (!string.IsNullOrEmpty(_serverCanvasTag) && canvas.CompareTag(_serverCanvasTag)) continue;
                // Only disable top-level canvas roots (not nested ones — parent handles them)
                if (canvas.transform.parent == null || canvas.transform.parent.GetComponentInParent<Canvas>() == null)
                {
                    canvas.gameObject.SetActive(false);
                    disabled++;
                }
            }
            Debug.Log($"[ServerUISuppressor] Disabled {disabled} Canvas root(s) on dedicated server.");
        }
    }
}
