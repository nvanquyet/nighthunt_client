using FishNet.Broadcast;
using NightHunt.Networking.Player;

namespace NightHunt.Networking
{
    public struct RequestPlayerDataBroadcast : IBroadcast { }
    public struct SubmitPlayerDataBroadcast : IBroadcast
    {
        public PlayerRegistryData Data;
    }
}