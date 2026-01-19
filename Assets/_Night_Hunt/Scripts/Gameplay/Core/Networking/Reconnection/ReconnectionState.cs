using System;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Networking.Reconnection
{
    /// <summary>
    /// State to restore after reconnection
    /// </summary>
    [Serializable]
    public class ReconnectionState
    {
        public Vector3 PlayerPosition;
        public Quaternion PlayerRotation;
        public float CurrentHP;
        public float CurrentStamina;
        public int CurrentPhase;
        public int Score;
        public int TeamId;
        public string InventoryData; // JSON serialized inventory
        public DateTime SaveTime;

        public ReconnectionState()
        {
            SaveTime = DateTime.Now;
        }
    }
}

