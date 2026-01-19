using UnityEngine;
using System.Collections.Generic;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Capture zone objective
    /// </summary>
    public class CaptureZoneObjective : MonoBehaviour, IObjective
    {
        [Header("Capture Zone Settings")]
        [SerializeField] private string objectiveId = "CAPTURE_ZONE";
        [SerializeField] private string objectiveName = "Capture Zone";
        [SerializeField] private float captureRadius = 10f;
        [SerializeField] private float captureTime = 10f;
        [SerializeField] private int requiredPlayers = 1;

        public string ObjectiveId => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool IsCompleted { get; private set; }
        public float Progress { get; private set; }

        private float captureProgress = 0f;
        private List<NetworkPlayer> playersInZone = new List<NetworkPlayer>();

        public void OnStart()
        {
            IsCompleted = false;
            Progress = 0f;
            captureProgress = 0f;
        }

        public void OnUpdate()
        {
            UpdatePlayersInZone();

            if (playersInZone.Count >= requiredPlayers)
            {
                captureProgress += Time.deltaTime / captureTime;
                captureProgress = Mathf.Clamp01(captureProgress);
                Progress = captureProgress;

                if (captureProgress >= 1f && !IsCompleted)
                {
                    OnComplete();
                }
            }
            else
            {
                // Decay capture progress if not enough players
                captureProgress = Mathf.Max(0f, captureProgress - Time.deltaTime / captureTime);
                Progress = captureProgress;
            }
        }

        public void OnComplete()
        {
            IsCompleted = true;
            Progress = 1f;
            Debug.Log($"[CaptureZoneObjective] Zone captured: {objectiveName}");
        }

        public void OnFail()
        {
            // Capture zone doesn't fail
        }

        private void UpdatePlayersInZone()
        {
            playersInZone.Clear();
            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            
            foreach (var player in allPlayers)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance <= captureRadius)
                {
                    playersInZone.Add(player);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, captureRadius);
        }
    }
}

