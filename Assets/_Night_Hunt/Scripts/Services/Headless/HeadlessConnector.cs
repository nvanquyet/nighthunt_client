using UnityEngine;

namespace NightHunt.Services.Headless
{
    /// <summary>
    /// Headless connectivity disabled; kept as stub for references.
    /// </summary>
    public class HeadlessConnector : MonoBehaviour
    {
        public bool ConnectToHeadless(object _) => false;
        public void Disconnect() {}
        public bool IsConnected() => false;
        public void SendJoinPayload(string matchId, string joinToken, long playerId) {}
    }
}
