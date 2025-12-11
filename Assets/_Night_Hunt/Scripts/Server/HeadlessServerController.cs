using UnityEngine;

namespace NightHunt.Server
{
    /// <summary>
    /// Headless Server Controller - DISABLED
    /// Headless server functionality has been disabled.
    /// This controller remains for backward compatibility but all methods are no-ops.
    /// </summary>
    public class HeadlessServerController : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("Headless server disabled - HeadlessServerController is no-op");
        }

        public void ShutdownServer()
        {
            Debug.Log("Headless server disabled - ShutdownServer is no-op");
        }

        public bool IsServerRunning()
        {
            return false;
        }

        public int GetConnectedClients()
        {
            return 0;
        }
    }
}
