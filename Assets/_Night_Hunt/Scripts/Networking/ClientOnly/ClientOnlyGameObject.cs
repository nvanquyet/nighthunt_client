using UnityEngine;

namespace NightHunt.Networking.ClientOnly
{
    /// <summary>
    /// Marks a GameObject as client-only.
    /// Automatically disables the GameObject on server builds (UNITY_SERVER).
    /// Use this for GameObjects that are only needed on client (e.g., visual effects, UI elements, etc.)
    /// </summary>
    public class ClientOnlyGameObject : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool disableOnServer = true;

        private void Awake()
        {
            // Build-time check: always disable in dedicated server builds.
            #if UNITY_SERVER
            if (disableOnServer)
            {
                gameObject.SetActive(false);
                return;
            }
            #endif

            // Runtime check: also disable when running as dedicated server in Editor
            // (server started but client NOT started — excludes host mode where both run).
            bool isDedicatedServer = FishNet.InstanceFinder.IsServerStarted
                                     && !FishNet.InstanceFinder.IsClientStarted;
            if (disableOnServer && isDedicatedServer)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
