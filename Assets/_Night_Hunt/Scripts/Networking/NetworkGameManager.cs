using FishNet.Managing;
using FishNet.Transporting;
using NightHunt.Core;
using NightHunt.State;
using System.Threading.Tasks;
using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// Network Game Manager - Manages Server and Client connections.
    ///
    /// Supports two connection modes:
    ///   Ranked_DS     : Client connects directly to a Dedicated Server (existing flow)
    ///   Custom_Relay  : Host starts FishNet Host; all players connect via Mini Relay server
    ///
    /// Relay topology (Custom mode):
    ///   All packets route through relay:sessionPort (unique UDP port per session).
    ///   The relay forwards every incoming packet to all other registered connections
    ///   in the same session – no game-logic parsing on the relay side.
    ///   The host player runs FishNet Host (Server + Client) bound to localhost;
    ///   the relay proxy exposes the session port to the internet.
    /// </summary>
    public class NetworkGameManager : SingletonPersistent<NetworkGameManager>
    {


        [Header("Network Manager Reference")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Network Settings")]
        [SerializeField] private ushort port = 7777;
        [SerializeField] private string defaultServerAddress = "localhost";

        public NetworkManager NetworkManager => networkManager;
        public bool IsServer => networkManager != null && networkManager.IsServerStarted;
        public bool IsClient => networkManager != null && networkManager.IsClientStarted;

        protected override void OnSingletonAwake()
        {
            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
                if (networkManager == null)
                    Debug.LogError("[NetworkGameManager] NetworkManager not found! Please add NetworkManager to scene.");
            }
        }

        // ── Dedicated Server (Ranked mode) ────────────────────────────────────

        /// <summary>
        /// Start Server only (dedicated server build).
        /// </summary>
        public void StartServer()
        {
            if (networkManager == null) { Debug.LogError("[NetworkGameManager] NetworkManager is null!"); return; }
            Debug.Log($"[NetworkGameManager] Starting Dedicated Server on port {port}...");
            if (!networkManager.ServerManager.StartConnection())
                Debug.LogError("[NetworkGameManager] Failed to start server!");
            else
                Debug.Log("[NetworkGameManager] Dedicated Server started successfully!");
        }

        /// <summary>
        /// Connect client to a Dedicated Server (Ranked_DS mode).
        /// </summary>
        public void StartClientDS(string dsIp = null, ushort dsPort = 0)
        {
            if (networkManager == null) { Debug.LogError("[NetworkGameManager] NetworkManager is null!"); return; }
            string ip = string.IsNullOrEmpty(dsIp) ? defaultServerAddress : dsIp;
            ushort p  = dsPort > 0 ? dsPort : port;
            Debug.Log($"[NetworkGameManager] Connecting to DS {ip}:{p}...");
            SetTransportAddress(ip, p);
            if (!networkManager.ClientManager.StartConnection())
                Debug.LogError("[NetworkGameManager] Failed to start DS client connection!");
        }

        /// <summary>Legacy overload kept for backwards compatibility.</summary>
        public void StartClient(string serverAddress = null) => StartClientDS(serverAddress);

        // ── Relay (Custom mode) ───────────────────────────────────────────────

        /// <summary>
        /// Start FishNet HOST through the relay – used by the room owner (Custom mode).
        ///
        /// How it works:
        ///   1. FishNet Server binds locally (loopback only – not internet-exposed).
        ///   2. FishNet Client connects outbound to relay:sessionPort
        ///      (relay registered this connection as "host" via POST /api/relay/create).
        ///   3. Relay forwards all subsequent packets between host and clients.
        ///
        /// NOTE: This requires the Tugboat transport to be configured so the *server*
        /// bind address is 127.0.0.1 (localhost) and the *client* address is the relay.
        /// A custom RelayTransport shim (future work) will handle the packet header.
        /// </summary>
        public void StartHostWithRelay(string relayIp, ushort relayPort, string sessionId)
        {
            if (networkManager == null) { Debug.LogError("[NetworkGameManager] NetworkManager is null!"); return; }
            Debug.Log($"[NetworkGameManager] Starting HOST via Relay {relayIp}:{relayPort} session={sessionId}");

            // Bind server to loopback so it never accepts direct internet connections
            SetTransportServerBindAddress("127.0.0.1");

            // Start server (loopback only)
            if (!networkManager.ServerManager.StartConnection())
            {
                Debug.LogError("[NetworkGameManager] Failed to start relay host server!");
                return;
            }

            // Connect client side to relay (host traffic is proxied by relay to all clients)
            SetTransportAddress(relayIp, relayPort);
            if (!networkManager.ClientManager.StartConnection())
                Debug.LogError("[NetworkGameManager] Failed to start relay host client!");
            else
                Debug.Log("[NetworkGameManager] Relay Host started.");
        }

        /// <summary>
        /// Connect as a client through the relay – used by non-host players (Custom mode).
        ///
        /// The relay:sessionPort acts as the FishNet server endpoint from the client's view.
        /// </summary>
        public void StartClientWithRelay(string relayIp, ushort relayPort, string sessionId)
        {
            if (networkManager == null) { Debug.LogError("[NetworkGameManager] NetworkManager is null!"); return; }
            Debug.Log($"[NetworkGameManager] Connecting CLIENT via Relay {relayIp}:{relayPort} session={sessionId}");
            SetTransportAddress(relayIp, relayPort);
            if (!networkManager.ClientManager.StartConnection())
                Debug.LogError("[NetworkGameManager] Failed to start relay client connection!");
        }

        // ── Disconnect ────────────────────────────────────────────────────────

        /// <summary>
        /// Disconnect and optionally notify the relay/backend based on game mode.
        /// Call this from match-end or user leaving.
        /// </summary>
        public async Task DisconnectWithCleanup()
        {
            var roomState = RoomState.Instance;

            if (roomState != null && roomState.CurrentGameMode == GameMode.Custom_Relay
                && !string.IsNullOrEmpty(roomState.RelaySessionId))
            {
                // Notify backend to clean up relay session
                await NotifyRelayCleanup(roomState.RelaySessionId);
            }

            Disconnect();
            roomState?.ClearNetworkSession();
        }

        /// <summary>Immediately stop all FishNet connections.</summary>
        public void Disconnect()
        {
            if (networkManager == null) return;
            Debug.Log("[NetworkGameManager] Disconnecting...");
            if (IsServer) networkManager.ServerManager.StopConnection(true);
            if (IsClient) networkManager.ClientManager.StopConnection();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        public int GetPlayerCount()
            => networkManager?.ServerManager?.Clients?.Count ?? 0;

        private void SetTransportAddress(string address, ushort targetPort)
        {
            var transport = networkManager.TransportManager.Transport;
            if (transport == null) { Debug.LogWarning("[NetworkGameManager] Transport is null!"); return; }
            var t = transport.GetType();
            TrySetProperty(transport, t, "ClientAddress", address);
            TrySetProperty(transport, t, "Port", targetPort);
        }

        private void SetTransportServerBindAddress(string bindAddress)
        {
            var transport = networkManager.TransportManager.Transport;
            if (transport == null) return;
            // Tugboat exposes IPv4BindAddress; fall back silently if not found
            TrySetProperty(transport, transport.GetType(), "IPv4BindAddress", bindAddress,
                logMissing: false);
        }

        private static void TrySetProperty(object target, System.Type type, string propName,
            object value, bool logMissing = true)
        {
            try
            {
                var prop = type.GetProperty(propName);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(target, value);
                    return;
                }
                if (logMissing)
                    Debug.LogWarning($"[NetworkGameManager] Property '{propName}' not found on {type.Name}.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkGameManager] Could not set '{propName}': {e.Message}");
            }
        }

        /// <summary>
        /// Tell backend to release the relay session.
        /// Fire-and-forget; failures are logged but not fatal.
        /// </summary>
        private async Task NotifyRelayCleanup(string sessionId)
        {
            try
            {
                var backend = NightHunt.Core.GameManager.Instance?.BackendClient;
                if (backend == null) return;
                await backend.DeleteAsync<object>($"/api/relay/{sessionId}");
                Debug.Log($"[NetworkGameManager] Relay session {sessionId} cleaned up.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkGameManager] Relay cleanup failed (non-fatal): {e.Message}");
            }
        }
    }
}
