using UnityEngine;

namespace NightHunt.Networking.ClientOnly
{
    /// <summary>
    /// Marks a GameObject as client-only and controls what gets disabled on dedicated server.
    ///
    /// DisableObject   — SetActive(false). Use for pure-client objects that have NO colliders
    ///                   needed server-side (UI, particles, camera rigs, etc.).
    ///
    /// DisableRenderers — Only disables cached Renderer components (MeshRenderer, SkinnedMesh…).
    ///                    Colliders / physics stay fully active. Use this for map/environment meshes
    ///                    where the server still needs the colliders for physics but not the visuals.
    ///                    The renderer list is populated automatically via OnValidate (Editor only)
    ///                    and serialized into the scene — zero runtime GetComponentsInChildren cost.
    /// </summary>
    public class ClientOnlyGameObject : MonoBehaviour
    {
        public enum ServerBehaviourType
        {
            DisableObject,      // gameObject.SetActive(false) — kills everything incl. colliders
            DisableRenderers,   // renderer.enabled = false only — colliders stay alive
        }

        [Header("Settings")]
        [SerializeField] private ServerBehaviourType serverBehaviour = ServerBehaviourType.DisableObject;

        // Populated by OnValidate in Editor. Serialized so no runtime Find is needed.
        // Only used when serverBehaviour == DisableRenderers.
        [SerializeField] private Renderer[] _cachedRenderers = System.Array.Empty<Renderer>();

        // ── Editor-only: auto-fetch renderers in children whenever Inspector changes ──
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (serverBehaviour == ServerBehaviourType.DisableRenderers)
            {
                _cachedRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            }
            else
            {
                _cachedRenderers = System.Array.Empty<Renderer>();
            }
        }
#endif

        private void Awake()
        {
#if UNITY_SERVER
            ApplyServerBehaviour();
            return;
#endif

            // Editor / runtime DS check: server started but client NOT started (excludes host mode).
            bool isDedicatedServer = FishNet.InstanceFinder.IsServerStarted
                                     && !FishNet.InstanceFinder.IsClientStarted;
            if (isDedicatedServer)
                ApplyServerBehaviour();
        }

        private void ApplyServerBehaviour()
        {
            switch (serverBehaviour)
            {
                case ServerBehaviourType.DisableObject:
                    gameObject.SetActive(false);
                    break;

                case ServerBehaviourType.DisableRenderers:
                    foreach (Renderer r in _cachedRenderers)
                    {
                        if (r != null) r.enabled = false;
                    }
                    break;
            }
        }
    }
}
