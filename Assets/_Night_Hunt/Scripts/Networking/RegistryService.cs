using System;
using System.Collections.Generic;
using FishNet.Connection;
using NightHunt.Core;
using NightHunt.Networking.Player;
using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// Server-side player registry. Keeps private backend identity data while exposing
    /// active FishNet client mappings used by gameplay systems.
    /// </summary>
    public class RegistryService : Singleton<RegistryService>
    {
        private readonly Dictionary<string, PlayerRegistryData> _playerDataByBackendId = new();
        private readonly Dictionary<int, string> _fishNetIdToBackendId = new();
        private readonly Dictionary<int, NetworkPlayer> _playersByFishNetId = new();
        private readonly List<NetworkPlayer> _allPlayers = new();

        public event Action<NetworkPlayer, PlayerRegistryData> OnPlayerRegistered;
        public event Action<NetworkPlayer, PlayerRegistryData> OnPlayerUnregistered;
        public event Action<string, PlayerRegistryData> OnPlayerDataUpdated;

        protected override void OnSingletonAwake()
        {
            Debug.Log("[RegistryService] Initialized");
        }

        public void RegisterPlayer(NetworkPlayer player, PlayerRegistryData privateData)
        {
            if (player == null)
                return;

            int fishnetId = player.Owner.ClientId;
            string backendId = privateData.BackendPlayerId;

            if (_playersByFishNetId.ContainsKey(fishnetId))
            {
                Debug.LogWarning($"[RegistryService] Player already registered - FishNet ID: {fishnetId}");
                return;
            }

            _playerDataByBackendId[backendId] = privateData;
            _fishNetIdToBackendId[fishnetId] = backendId;
            _playersByFishNetId[fishnetId] = player;
            if (!_allPlayers.Contains(player))
                _allPlayers.Add(player);

            Debug.Log($"[RegistryService] Registered - FishNet ID: {fishnetId}, Backend ID: {backendId}, Name: {privateData.DisplayName}, Team: {privateData.TeamId}");
            OnPlayerRegistered?.Invoke(player, privateData);
        }

        public void UnregisterPlayer(NetworkPlayer player)
        {
            if (player == null)
                return;

            UnregisterPlayerByFishNetId(player.Owner.ClientId);
        }

        public void UnregisterPlayerByFishNetId(int fishnetId)
        {
            NetworkPlayer player = GetPlayerByFishNetId(fishnetId);

            if (!_fishNetIdToBackendId.TryGetValue(fishnetId, out string backendId))
            {
                Debug.LogWarning($"[RegistryService] No backend ID mapping for FishNet ID: {fishnetId}");
                return;
            }

            if (!_playerDataByBackendId.TryGetValue(backendId, out PlayerRegistryData privateData))
            {
                Debug.LogWarning($"[RegistryService] No private data for Backend ID: {backendId}");
                return;
            }

            privateData.Status = PlayerConnectionStatus.Disconnected;
            _playerDataByBackendId[backendId] = privateData;

            _fishNetIdToBackendId.Remove(fishnetId);
            _playersByFishNetId.Remove(fishnetId);
            if (player != null)
                _allPlayers.Remove(player);

            Debug.Log($"[RegistryService] Unregistered - FishNet ID: {fishnetId}, Backend ID: {backendId} (data preserved for reconnect)");

            if (player != null)
                OnPlayerUnregistered?.Invoke(player, privateData);
        }

        public bool TryGetFishNetIdByBackendId(string backendId, out int fishnetId)
        {
            foreach (var kvp in _fishNetIdToBackendId)
            {
                if (kvp.Value == backendId)
                {
                    fishnetId = kvp.Key;
                    return true;
                }
            }

            fishnetId = -1;
            return false;
        }

        public void MarkPlayerReconnecting(int fishnetId)
        {
            if (!_fishNetIdToBackendId.TryGetValue(fishnetId, out string backendId))
                return;

            if (!_playerDataByBackendId.TryGetValue(backendId, out PlayerRegistryData privateData))
                return;

            privateData.Status = PlayerConnectionStatus.Reconnecting;
            _playerDataByBackendId[backendId] = privateData;

            NetworkPlayer player = GetPlayerByFishNetId(fishnetId);
            if (player != null)
                player.SetPublicData(PlayerPublicData.FromRegistryData(privateData));

            OnPlayerDataUpdated?.Invoke(backendId, privateData);
            Debug.Log($"[RegistryService] Marked reconnecting - FishNet ID: {fishnetId}, Backend ID: {backendId}");
        }

        public void RemapPlayerConnection(NetworkPlayer player, int previousFishNetId, NetworkConnection newConnection, PlayerRegistryData privateData)
        {
            if (player == null || newConnection == null)
                return;

            string backendId = privateData.BackendPlayerId;
            if (string.IsNullOrEmpty(backendId) && _fishNetIdToBackendId.TryGetValue(previousFishNetId, out string previousBackendId))
                backendId = previousBackendId;

            if (string.IsNullOrEmpty(backendId))
            {
                Debug.LogWarning($"[RegistryService] Cannot remap player without backend id. previousFishNetId={previousFishNetId} newFishNetId={newConnection.ClientId}");
                return;
            }

            _fishNetIdToBackendId.Remove(previousFishNetId);
            _playersByFishNetId.Remove(previousFishNetId);

            privateData.BackendPlayerId = backendId;
            privateData.Status = PlayerConnectionStatus.InGame;

            _playerDataByBackendId[backendId] = privateData;
            _fishNetIdToBackendId[newConnection.ClientId] = backendId;
            _playersByFishNetId[newConnection.ClientId] = player;

            if (!_allPlayers.Contains(player))
                _allPlayers.Add(player);

            player.SetPublicData(PlayerPublicData.FromRegistryData(privateData));

            Debug.Log($"[RegistryService] Remapped reconnect - Backend ID: {backendId}, FishNet {previousFishNetId} -> {newConnection.ClientId}");
            OnPlayerDataUpdated?.Invoke(backendId, privateData);
        }

        public void UpdatePlayerData(string backendId, PlayerRegistryData newData)
        {
            _playerDataByBackendId[backendId] = newData;

            NetworkPlayer player = GetActivePlayerByBackendId(backendId);
            if (player != null)
                player.SetPublicData(PlayerPublicData.FromRegistryData(newData));

            Debug.Log($"[RegistryService] Data updated - Backend ID: {backendId}");
            OnPlayerDataUpdated?.Invoke(backendId, newData);
        }

        public NetworkPlayer GetPlayerByFishNetId(int fishnetId)
        {
            _playersByFishNetId.TryGetValue(fishnetId, out NetworkPlayer player);
            return player;
        }

        public PlayerRegistryData? GetPrivateDataByFishNetId(int fishnetId)
        {
            if (_fishNetIdToBackendId.TryGetValue(fishnetId, out string backendId) &&
                _playerDataByBackendId.TryGetValue(backendId, out PlayerRegistryData data))
            {
                return data;
            }

            return null;
        }

        public string GetBackendIdByFishNetId(int fishnetId)
        {
            _fishNetIdToBackendId.TryGetValue(fishnetId, out string backendId);
            return backendId;
        }

        public PlayerRegistryData? GetPrivateDataByBackendId(string backendId)
        {
            if (_playerDataByBackendId.TryGetValue(backendId, out PlayerRegistryData data))
                return data;

            return null;
        }

        public NetworkPlayer GetActivePlayerByBackendId(string backendId)
        {
            foreach (var kvp in _fishNetIdToBackendId)
            {
                if (kvp.Value == backendId)
                    return GetPlayerByFishNetId(kvp.Key);
            }

            return null;
        }

        public bool IsPlayerConnected(string backendId)
        {
            foreach (var kvp in _fishNetIdToBackendId)
            {
                if (kvp.Value == backendId)
                    return true;
            }

            return false;
        }

        public NetworkPlayer[] GetAllPlayers()
        {
            _allPlayers.RemoveAll(p => p == null);
            return _allPlayers.ToArray();
        }

        public NetworkPlayer[] GetPlayersByTeam(int teamId)
        {
            List<NetworkPlayer> teamPlayers = new();
            foreach (NetworkPlayer player in _allPlayers)
            {
                if (player != null && player.TeamId == teamId)
                    teamPlayers.Add(player);
            }

            return teamPlayers.ToArray();
        }

        public PlayerRegistryData[] GetAllPrivateData()
        {
            List<PlayerRegistryData> allData = new();
            allData.AddRange(_playerDataByBackendId.Values);
            return allData.ToArray();
        }

        public int GetConnectedPlayerCount()
        {
            _allPlayers.RemoveAll(p => p == null);
            return _allPlayers.Count;
        }

        public int GetAliveCount(int teamId)
        {
            int count = 0;
            foreach (var player in _allPlayers)
            {
                if (player != null && player.TeamId == teamId && player.IsAlive)
                    count++;
            }

            return count;
        }

        public NetworkPlayer[] GetAlivePlayersByTeam(int teamId)
        {
            var list = new List<NetworkPlayer>();
            foreach (var player in _allPlayers)
            {
                if (player != null && player.TeamId == teamId && player.IsAlive)
                    list.Add(player);
            }

            return list.ToArray();
        }

        public bool HasPlayerData(string backendId)
        {
            return _playerDataByBackendId.ContainsKey(backendId);
        }

        public PlayerRegistryData? GetLastKnownData(string backendId)
        {
            return GetPrivateDataByBackendId(backendId);
        }

        public string GetDebugInfo()
        {
            _allPlayers.RemoveAll(p => p == null);
            return $"Connected Players: {_allPlayers.Count}\n" +
                   $"Total Player Data: {_playerDataByBackendId.Count}\n" +
                   $"Active Mappings: {_fishNetIdToBackendId.Count}";
        }
    }
}
