using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using NightHunt.Common;
using NightHunt.Gameplay.Character.Data;
using NightHunt.Networking.Player;
using NightHunt.State;
using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// Client handler - gửi player data lên server khi connect
    /// </summary>
    public class ClientNetworkHandler : NetworkBehaviour
    {
        // ===== CLIENT SENDS DATA TO SERVER =====
        // Cache the data locally so the server can query it after receiving via RPC
        private PlayerRegistryData _cachedPlayerData;
        private const float OwnerWaitTimeoutSeconds = 5.0f;
        private bool _playerDataSent;
        private Coroutine _ownerWaitCoroutine;

        public PlayerPublicData GetPublicPlayerData() => PlayerPublicData.FromRegistryData(_cachedPlayerData);

        public override void OnStartClient()
        {
            base.OnStartClient();
            // FIX ROOT BUG: điều kiện cũ (IsClientOnly || IsHost) là FALSE với non-host relay client
            // (IsClientOnly=false vì host server chạy cùng process, IsHost=false vì đây không phải host).
            // Kết quả: RegisterBroadcast không được gọi → server broadcast RequestPlayerDataBroadcast
            // bị DROP silently → ClientDataTimeout sau 5s.
            // FIX: dùng IsClient — đúng cho mọi loại FishNet client (ClientOnly, Host, non-host relay).
            if (IsClient && ClientManager != null)
            {
                ClientManager.RegisterBroadcast<RequestPlayerDataBroadcast>(OnServerRequestedData);
            }
            TrySendPlayerDataToServer("OnStartClient");
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            TrySendPlayerDataToServer("OnOwnershipClient");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // FIX: Delay đủ lớn để spawn packet traverse relay trước khi TargetRpc được gửi.
            // 2 frames (~33ms) không đủ qua relay (round-trip thường 50–300ms).
            // Nếu RpcRequestPlayerData đến client trước khi NetworkObject được spawn,
            // FishNet sẽ DROP TargetRpc → ClientDataTimeout sau 5s.
            StartCoroutine(RequestPlayerDataFromClient());
        }

        [Server]
        private IEnumerator RequestPlayerDataFromClient()
        {
            // 0.5s đủ cho relay round-trip trong mọi điều kiện thực tế.
            // DataRequestRetryCoroutine trong ServerGameManager sẽ cover nếu 0.5s vẫn chưa đủ.
            yield return new WaitForSecondsRealtime(0.5f);
            if (IsSpawned && Owner != null && Owner.IsActive)
                RpcRequestPlayerData();
        }

        /// <summary>
        /// Server → Client (TargetRpc): Yêu cầu client gửi data lên
        /// </summary>
        [TargetRpc]
        private void RpcRequestPlayerData(NetworkConnection conn = null)
        {
            if (_playerDataSent)
                return;

            Debug.Log("[NH_HANDSHAKE][CLIENT_HANDLER][TARGET_RPC] Server requested player data via TargetRpc.");
            TrySendPlayerDataToServer("ServerRequest");
        }

        public override void OnStopClient()
        {
            // Unregister chỉ khi IsClient để match với điều kiện đăng ký ở OnStartClient.
            if (IsClient && ClientManager != null)
                ClientManager.UnregisterBroadcast<RequestPlayerDataBroadcast>(OnServerRequestedData);
            StopOwnerWaitCoroutine();
            _playerDataSent = false;
            base.OnStopClient();
        }

        private void OnServerRequestedData(RequestPlayerDataBroadcast broadcast, Channel channel)
        {
            if (_playerDataSent)
                return;
        
            Debug.Log("[NH_HANDSHAKE][CLIENT_HANDLER][BROADCAST] Received server data request broadcast.");
            // Broadcast handlers are global per client. Every ClientNetworkHandler instance on
            // that client receives this callback, including handlers owned by other players.
            // Route through the same ownership gate used by OnStartClient/OnOwnershipClient;
            // otherwise a non-owner handler calls ServerRpc(RequireOwnership=true), FishNet
            // drops it, and the local _playerDataSent guard can permanently suppress retries.
            TrySendPlayerDataToServer("BroadcastRequest");
        }
        
        private void TrySendPlayerDataToServer(string source)
        {
            if (_playerDataSent)
                return;

            if (!IsOwner)
            {
                if (IsLocalOwnerCandidate())
                {
                    Debug.Log($"[NH_HANDSHAKE][CLIENT_HANDLER][OWNER_WAIT] source={source} ownerId={OwnerId} localId={LocalConnection?.ClientId ?? -1}; waiting before sending player data.");
                    StartOwnerWaitCoroutine();
                }
                else
                {
                    Debug.Log($"[NH_HANDSHAKE][CLIENT_HANDLER][IGNORE_NON_OWNER] source={source} ownerId={OwnerId} localId={LocalConnection?.ClientId ?? -1}; ignoring.");
                }

                return;
            }

            _playerDataSent = true;
            StopOwnerWaitCoroutine();
            Debug.Log($"[NH_HANDSHAKE][CLIENT_HANDLER][SEND_READY] source={source} ownerId={OwnerId} localId={LocalConnection?.ClientId ?? -1} t={System.DateTime.UtcNow:HH:mm:ss.fff}.");
            SendPlayerDataToServer(source);
        }

        private bool IsLocalOwnerCandidate()
        {
            if (Owner.IsLocalClient)
                return true;

            return Owner.IsValid
                   && LocalConnection != null
                   && LocalConnection.IsValid
                   && Owner.ClientId == LocalConnection.ClientId;
        }

        private void StartOwnerWaitCoroutine()
        {
            if (_ownerWaitCoroutine != null)
                return;

            _ownerWaitCoroutine = StartCoroutine(WaitForOwnershipAndSend());
        }

        private void StopOwnerWaitCoroutine()
        {
            if (_ownerWaitCoroutine == null)
                return;

            StopCoroutine(_ownerWaitCoroutine);
            _ownerWaitCoroutine = null;
        }

        private IEnumerator WaitForOwnershipAndSend()
        {
            float deadline = Time.realtimeSinceStartup + OwnerWaitTimeoutSeconds;
            while (!_playerDataSent && Time.realtimeSinceStartup < deadline)
            {
                if (IsOwner)
                {
                    _ownerWaitCoroutine = null;
                    TrySendPlayerDataToServer("OwnerWait");
                    yield break;
                }

                yield return null;
            }

            _ownerWaitCoroutine = null;
            if (!_playerDataSent && IsLocalOwnerCandidate())
            {
                Debug.LogError(
                    $"[NH_HANDSHAKE][NH_DROP][CLIENT_HANDLER][OWNER_TIMEOUT] owner wait timeout after {OwnerWaitTimeoutSeconds:F1}s. " +
                    $"ownerId={OwnerId} localId={LocalConnection?.ClientId ?? -1} IsOwner={IsOwner}. " +
                    "Server spawned the identity handler, but this client never gained ownership, so RpcSendPlayerData cannot be sent.");
            }
        }

        /// <summary>
        /// Client: Send data lên server bằng ServerRpc
        /// SyncVar với ClientUnsynchronized KHÔNG truyền data lên server - phải dùng ServerRpc
        /// </summary>
        [Client]
        private void SendPlayerDataToServer(string source)
        {
            PlayerRegistryData data = BuildLocalPlayerData();
            Debug.Log($"[NH_HANDSHAKE][CLIENT_HANDLER][SEND_RPC] source={source} backendId={data.BackendPlayerId} name={data.DisplayName} charModelIdx={data.CharacterModelIndex} teamId={data.TeamId}.");
            RpcSendPlayerData(data);
        }

        /// <summary>
        /// ServerRpc: Run trên server, is called bởi client owner
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RpcSendPlayerData(PlayerRegistryData data, NetworkConnection conn = null)
        {
            _cachedPlayerData = data;
            Debug.Log($"[NH_HANDSHAKE][SERVER_RPC][PLAYER_DATA] backendId={data.BackendPlayerId} name={data.DisplayName} teamId={data.TeamId} t={System.DateTime.UtcNow:HH:mm:ss.fff}.");
            ServerGameManager.Instance.OnClientDataReceived(conn ?? Owner, data);
        }

        /// <summary>
        /// Reads player identity from the active SessionState (set during login / auto-login).
        /// Falls back to a guest entry when no authenticated session is present.
        ///
        /// CHARACTER RESOLUTION FLOW:
        ///   1. Character-select screen saves the backend string ID (e.g. "character_02")
        ///      to PlayerPrefs key "SelectedCharacterId".
        ///   2. GetLocalPlayerData() reads the string and resolves it to an array index
        ///      via CharacterDatabase.GetIndexById().
        ///   3. The resolved int is sent to the server as CharacterModelIndex.
        ///   4. If the string is missing or unknown, falls back to index 0 (default skin).
        /// </summary>
        public static PlayerRegistryData BuildLocalPlayerData()
        {
            var session = SessionState.Instance;

            // Resolve selected character: string ID from character-select screen → array index
            string savedCharacterId = PlayerPrefs.GetString(Constants.PREFS_SELECTED_CHARACTER_ID, "");
            int characterModelIndex = 0;

            if (!string.IsNullOrEmpty(savedCharacterId) && CharacterDatabase.Instance != null)
            {
                int resolved = CharacterDatabase.Instance.GetIndexById(savedCharacterId);
                if (resolved >= 0)
                    characterModelIndex = resolved;
                else
                    Debug.LogWarning($"[ClientNetworkHandler] Unknown character ID '{savedCharacterId}' — " +
                                     "falling back to index 0 (default skin).");
            }

            if (session != null && session.IsAuthenticated)
            {
                int requestedTeamId = ResolveRequestedGameplayTeamId(session.UserId);
                return new PlayerRegistryData
                {
                    BackendPlayerId = session.UserId.ToString(),
                    DisplayName = !string.IsNullOrEmpty(session.Username)
                                              ? session.Username
                                              : $"Player_{session.UserId}",
                    // TeamId = -1: yêu cầu server tự gán team qua load-balancing (GetSmallestTeam).
                    // Không được hardcode 0 vì ResolveTeam chấp nhận 0 là valid → load-balancing bị bỏ qua.
                    // Khi có matchmaking / team-select screen, backend sẽ truyền teamId thực qua SessionState.
                    TeamId = requestedTeamId,
                    Status = PlayerConnectionStatus.Connected,
                    CharacterModelIndex = characterModelIndex
                };
            }

            Debug.LogWarning("[ClientNetworkHandler] No authenticated session found — using fallback guest data.");
            return new PlayerRegistryData
            {
                BackendPlayerId = $"guest_{Random.Range(1000, 9999)}",
                DisplayName = $"Guest_{Random.Range(1, 100)}",
                TeamId = -1, // -1 = auto-assign by server load-balancing
                Status = PlayerConnectionStatus.Connected,
                CharacterModelIndex = characterModelIndex
            };
        }

        private static int ResolveRequestedGameplayTeamId(long userId)
        {
            var players = RoomState.Instance?.CurrentRoom?.players;
            var player = players?.Find(p => p.userId == userId);
            if (player == null)
                return -1;

            // Lobby/backend teams are 1/2; gameplay teams are 0/1.
            if (player.team == Constants.TEAM_1)
                return 0;
            if (player.team == Constants.TEAM_2)
                return 1;

            return player.team >= 0 ? player.team : -1;
        }
    }
}
