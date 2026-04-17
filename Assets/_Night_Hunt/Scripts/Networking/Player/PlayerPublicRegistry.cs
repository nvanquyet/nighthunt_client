using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace NightHunt.Networking.Player
{
    public class PlayerPublicRegistry : MonoBehaviour
    {
        public static PlayerPublicRegistry Instance { get; private set; }

        private readonly Dictionary<int, PlayerPublicData> players
            = new(); // FishNetId ? PublicData

        private readonly Dictionary<int, NetworkPlayer> networkPlayers
            = new(); // FishNetId ? PublicData 

        private void Awake()
        {
            Instance = this;
        }

        public void Register(int fishNetId, PlayerPublicData data, NetworkPlayer networkPlayer)
        {
            players[fishNetId] = data;
            networkPlayers[fishNetId] = networkPlayer;
        }

        public void UpdatePublicData(int fishNetId, PlayerPublicData data)
        {
            players[fishNetId] = data;
        }

        public void Unregister(int fishNetId)
        {
            players.Remove(fishNetId);
            networkPlayers.Remove(fishNetId);
        }

        // ===== CLIENT API =====
        public NetworkPlayer[] GetAllPlayers()
        {
            NetworkPlayer[] result = new NetworkPlayer[networkPlayers.Count];
            networkPlayers.Values.CopyTo(result, 0);
            return result;
        }

        // Th�m v�o PlayerPublicRegistry.cs
        public List<NetworkPlayer> GetPlayersByTeam(int teamId)
        {
            var result = new List<NetworkPlayer>();
            foreach (var kvp in networkPlayers)
                if (kvp.Value != null && kvp.Value.TeamId == teamId)
                    result.Add(kvp.Value);
            return result;
        }

    }
}