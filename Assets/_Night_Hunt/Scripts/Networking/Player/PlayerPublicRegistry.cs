using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace NightHunt.Networking.Player
{
    [Serializable]
    public struct PlayerPublicEntry
    {
        public int ObjectId;
        public PlayerPublicData Data;
        public NetworkPlayer NetworkPlayer;
        public bool HasNetworkPlayer => NetworkPlayer != null;
    }

    public class PlayerPublicRegistry : MonoBehaviour
    {
        public static PlayerPublicRegistry Instance { get; private set; }
        public event Action OnRegistryChanged;

        private readonly Dictionary<int, PlayerPublicData> players
            = new(); // FishNetId -> PublicData

        private readonly Dictionary<int, NetworkPlayer> networkPlayers
            = new(); // FishNetId -> NetworkPlayer

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Registers identity-only data. This survives NetworkPlayer observer
        /// despawn/culling so UI and team state do not depend on actor visibility.
        /// </summary>
        public void RegisterIdentity(int fishNetId, PlayerPublicData data)
        {
            players[fishNetId] = data;
            OnRegistryChanged?.Invoke();
        }

        public void Register(int fishNetId, PlayerPublicData data, NetworkPlayer networkPlayer)
        {
            players[fishNetId] = data;
            if (networkPlayer != null)
                networkPlayers[fishNetId] = networkPlayer;
            OnRegistryChanged?.Invoke();
        }

        public void UpdatePublicData(int fishNetId, PlayerPublicData data)
        {
            players[fishNetId] = data;
            OnRegistryChanged?.Invoke();
        }

        public bool HasNetworkPlayer(int fishNetId)
        {
            return networkPlayers.TryGetValue(fishNetId, out NetworkPlayer player) && player != null;
        }

        public bool TryGetPublicData(int fishNetId, out PlayerPublicData data)
        {
            return players.TryGetValue(fishNetId, out data);
        }

        public void UnregisterNetworkPlayer(int fishNetId)
        {
            if (networkPlayers.Remove(fishNetId))
                OnRegistryChanged?.Invoke();
        }

        public void UnregisterIdentity(int fishNetId)
        {
            bool changed = players.Remove(fishNetId);
            changed |= networkPlayers.Remove(fishNetId);
            if (changed)
                OnRegistryChanged?.Invoke();
        }

        public void Unregister(int fishNetId)
        {
            UnregisterIdentity(fishNetId);
        }

        // ===== CLIENT API =====
        public NetworkPlayer[] GetAllPlayers()
        {
            PruneMissingNetworkPlayers();
            var result = new List<NetworkPlayer>(networkPlayers.Count);
            foreach (var player in networkPlayers.Values)
            {
                if (player != null)
                    result.Add(player);
            }
            return result.ToArray();
        }

        public List<PlayerPublicEntry> GetAllPublicEntries()
        {
            PruneMissingNetworkPlayers();
            var result = new List<PlayerPublicEntry>(players.Count);
            foreach (var kvp in players)
            {
                networkPlayers.TryGetValue(kvp.Key, out NetworkPlayer networkPlayer);
                result.Add(new PlayerPublicEntry
                {
                    ObjectId = kvp.Key,
                    Data = kvp.Value,
                    NetworkPlayer = networkPlayer
                });
            }
            return result;
        }

        // Th�m v�o PlayerPublicRegistry.cs
        public List<NetworkPlayer> GetPlayersByTeam(int teamId)
        {
            PruneMissingNetworkPlayers();
            var result = new List<NetworkPlayer>();
            foreach (var kvp in networkPlayers)
                if (kvp.Value != null && kvp.Value.TeamId == teamId)
                    result.Add(kvp.Value);
            return result;
        }

        private void PruneMissingNetworkPlayers()
        {
            List<int> staleIds = null;
            foreach (var kvp in networkPlayers)
            {
                if (kvp.Value != null) continue;
                staleIds ??= new List<int>();
                staleIds.Add(kvp.Key);
            }

            if (staleIds == null) return;
            foreach (int id in staleIds)
            {
                networkPlayers.Remove(id);
            }
            OnRegistryChanged?.Invoke();
        }

    }
}
