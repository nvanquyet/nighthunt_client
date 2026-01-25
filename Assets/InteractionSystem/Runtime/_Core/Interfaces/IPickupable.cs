using _Night_Hunt.Scripts.Gameplay.Systems.Core.Abstractions;
using FishNet.Connection;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core
{
    public interface IPickupable
    {
        ItemDataBase ItemData { get; }
        int Quantity { get; }
        bool CanPickup(NetworkConnection player);
        void OnPickedUp(NetworkConnection player);
        Vector3 WorldPosition { get; }
    }
}