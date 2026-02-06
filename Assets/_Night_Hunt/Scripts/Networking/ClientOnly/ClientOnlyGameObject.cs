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
            #if UNITY_SERVER
            if (disableOnServer)
            {
                gameObject.SetActive(false);
            }
            #endif
        }
    }
}
