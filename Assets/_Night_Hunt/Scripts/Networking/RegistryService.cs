using System;
using System.Collections.Generic;
using NightHunt.Networking.Player;
using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// RegistryService - Lưu trữ PlayerRegistryData (private server data)
    /// Server-side only - clients không access
    /// </summary>
    public class RegistryService : MonoBehaviour
    {
        public static RegistryService Instance { get; private set; }
        
        // ===== PRIVATE DATA STORAGE =====
        
        // Backend ID → PlayerRegistryData (có BackendPlayerId)
        private readonly Dictionary<string, PlayerRegistryData> _playerDataByBackendId = new();
        
        // FishNet ClientId → Backend ID mapping
        private readonly Dictionary<int, string> _fishNetIdToBackendId = new();
        
        // FishNet ClientId → NetworkPlayer reference
        private readonly Dictionary<int, NetworkPlayer> _playersByFishNetId = new();
        
        private readonly List<NetworkPlayer> _allPlayers = new();
        
        // ===== EVENTS =====
        
        public event Action<NetworkPlayer, PlayerRegistryData> OnPlayerRegistered;
        public event Action<NetworkPlayer, PlayerRegistryData> OnPlayerUnregistered;
        public event Action<string, PlayerRegistryData> OnPlayerDataUpdated; // Backend ID, new data
        
        // ===== LIFECYCLE =====
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[RegistryService] Duplicate instance, destroying...");
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            Debug.Log("[RegistryService] ✅ Initialized");
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        // ===== REGISTRATION =====
        
        /// <summary>
        /// Server: Register player với private data
        /// </summary>
        public void RegisterPlayer(NetworkPlayer player, PlayerRegistryData privateData)
        {
            int fishnetId = player.Owner.ClientId;
            string backendId = privateData.BackendPlayerId;
            
            if (_playersByFishNetId.ContainsKey(fishnetId))
            {
                Debug.LogWarning($"[RegistryService] Player already registered - FishNet ID: {fishnetId}");
                return;
            }
            
            // Lưu private data
            _playerDataByBackendId[backendId] = privateData;
            
            // Mapping
            _fishNetIdToBackendId[fishnetId] = backendId;
            _playersByFishNetId[fishnetId] = player;
            _allPlayers.Add(player);
            
            Debug.Log($"[RegistryService] ✅ Registered - FishNet ID: {fishnetId}, Backend ID: {backendId}, Name: {privateData.DisplayName}, Team: {privateData.TeamId}");
            
            OnPlayerRegistered?.Invoke(player, privateData);
        }
        
        /// <summary>
        /// Server: Unregister player (giữ data cho reconnect)
        /// </summary>
        public void UnregisterPlayer(NetworkPlayer player)
        {
            int fishnetId = player.Owner.ClientId;
            
            if (!_fishNetIdToBackendId.TryGetValue(fishnetId, out string backendId))
            {
                Debug.LogWarning($"[RegistryService] No backend ID mapping for FishNet ID: {fishnetId}");
                return;
            }
            
            // Get private data
            if (!_playerDataByBackendId.TryGetValue(backendId, out PlayerRegistryData privateData))
            {
                Debug.LogWarning($"[RegistryService] No private data for Backend ID: {backendId}");
                return;
            }
            
            // Update status (giữ data cho reconnect)
            privateData.Status = PlayerConnectionStatus.Disconnected;
            _playerDataByBackendId[backendId] = privateData;
            
            // Remove active references
            _fishNetIdToBackendId.Remove(fishnetId);
            _playersByFishNetId.Remove(fishnetId);
            _allPlayers.Remove(player);
            
            Debug.Log($"[RegistryService] ❌ Unregistered - FishNet ID: {fishnetId}, Backend ID: {backendId} (Data preserved for reconnect)");
            
            OnPlayerUnregistered?.Invoke(player, privateData);
        }
        
        // ===== UPDATE DATA =====
        
        /// <summary>
        /// Server: Update player's private data
        /// </summary>
        public void UpdatePlayerData(string backendId, PlayerRegistryData newData)
        {
            _playerDataByBackendId[backendId] = newData;
            
            // Update NetworkPlayer's public data nếu đang connected
            NetworkPlayer player = GetActivePlayerByBackendId(backendId);
            if (player != null)
            {
                PlayerPublicData publicData = PlayerPublicData.FromRegistryData(newData);
                player.SetPublicData(publicData);
            }
            
            Debug.Log($"[RegistryService] Data updated - Backend ID: {backendId}");
            
            OnPlayerDataUpdated?.Invoke(backendId, newData);
        }
        
        // ===== QUERIES - BY FISHNET ID =====
        
        public NetworkPlayer GetPlayerByFishNetId(int fishnetId)
        {
            _playersByFishNetId.TryGetValue(fishnetId, out NetworkPlayer player);
            return player;
        }
        
        public PlayerRegistryData? GetPrivateDataByFishNetId(int fishnetId)
        {
            if (_fishNetIdToBackendId.TryGetValue(fishnetId, out string backendId))
            {
                if (_playerDataByBackendId.TryGetValue(backendId, out PlayerRegistryData data))
                    return data;
            }
            
            return null;
        }
        
        public string GetBackendIdByFishNetId(int fishnetId)
        {
            _fishNetIdToBackendId.TryGetValue(fishnetId, out string backendId);
            return backendId;
        }
        
        // ===== QUERIES - BY BACKEND ID =====
        
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
                {
                    return GetPlayerByFishNetId(kvp.Key);
                }
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
        
        // ===== QUERIES - ALL PLAYERS =====
        
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
        
        // ===== RECONNECT SUPPORT =====
        
        public bool HasPlayerData(string backendId)
        {
            return _playerDataByBackendId.ContainsKey(backendId);
        }
        
        public PlayerRegistryData? GetLastKnownData(string backendId)
        {
            return GetPrivateDataByBackendId(backendId);
        }
        
        // ===== DEBUG =====
        
        public string GetDebugInfo()
        {
            _allPlayers.RemoveAll(p => p == null);
            
            return $"Connected Players: {_allPlayers.Count}\n" +
                   $"Total Player Data: {_playerDataByBackendId.Count}\n" +
                   $"Active Mappings: {_fishNetIdToBackendId.Count}";
        }
    }
}