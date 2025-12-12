using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace _Night_Hunt.Scripts.Gameplay.Character
{
    /// <summary>
    /// Placeholder for PlayerNetworkController referenced in NetworkManager
    /// </summary>
    public class PlayerNetworkController : NetworkBehaviour
    {
        [SyncVar] public string playerName;
        [SyncVar] public int teamId;
        
        private PlayerController playerController;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (IsOwner)
            {
                // This is our player
                Debug.Log("[PlayerNetwork] Local player spawned");
            }
        }

        [ServerRpc]
        public void CmdSetPlayerInfo(string name, int team)
        {
            playerName = name;
            teamId = team;
        }

        public PlayerController GetPlayerController() => playerController;
    }
}