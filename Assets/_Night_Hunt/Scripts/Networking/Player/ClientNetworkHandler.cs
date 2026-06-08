using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using NightHunt.Gameplay.Character.Data;
using NightHunt.Networking.Player;
using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// Legacy owner-RPC identity sender.
    /// Custom_Relay uses NetworkGameManager connection-level broadcast instead.
    /// </summary>
    public class ClientNetworkHandler : NetworkBehaviour
    {
        private const float OwnerWaitTimeoutSeconds = 5.0f;

        private PlayerRegistryData _cachedPlayerData;
        private bool _playerDataSent;
        private Coroutine _ownerWaitCoroutine;

        public PlayerPublicData GetPublicPlayerData() => PlayerPublicData.FromRegistryData(_cachedPlayerData);

        public override void OnStartClient()
        {
            base.OnStartClient();
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
            StartCoroutine(RequestPlayerDataFromClient());
        }

        public override void OnStopClient()
        {
            StopOwnerWaitCoroutine();
            _playerDataSent = false;
            base.OnStopClient();
        }

        [Server]
        private IEnumerator RequestPlayerDataFromClient()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (IsSpawned && Owner != null && Owner.IsActive)
                RpcRequestPlayerData();
        }

        [TargetRpc]
        private void RpcRequestPlayerData(NetworkConnection conn = null)
        {
            if (_playerDataSent)
                return;

            Debug.Log("[NH_HANDSHAKE][CLIENT_HANDLER][TARGET_RPC] Server requested player data via TargetRpc.");
            TrySendPlayerDataToServer("ServerRequest");
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
                    "Server spawned the legacy identity handler, but this client never gained ownership.");
            }
        }

        [Client]
        private void SendPlayerDataToServer(string source)
        {
            PlayerRegistryData data = PlayerIdentityFactory.BuildLocalPlayerData();
            Debug.Log($"[NH_HANDSHAKE][CLIENT_HANDLER][SEND_RPC] source={source} backendId={data.BackendPlayerId} name={data.DisplayName} charModelIdx={data.CharacterModelIndex} teamId={data.TeamId}.");
            RpcSendPlayerData(data);
        }

        [ServerRpc(RequireOwnership = true)]
        private void RpcSendPlayerData(PlayerRegistryData data, NetworkConnection conn = null)
        {
            _cachedPlayerData = data;
            Debug.Log($"[NH_HANDSHAKE][SERVER_RPC][PLAYER_DATA] backendId={data.BackendPlayerId} name={data.DisplayName} teamId={data.TeamId} t={System.DateTime.UtcNow:HH:mm:ss.fff}.");
            ServerGameManager.Instance.OnClientDataReceived(conn ?? Owner, data);
        }
    }
}
