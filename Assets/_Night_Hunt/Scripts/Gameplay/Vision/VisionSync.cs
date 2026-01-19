using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace NightHunt.Gameplay.Vision
{
    /// <summary>
    /// Network sync for vision state
    /// </summary>
    public class VisionSync : NetworkBehaviour
    {
        private readonly SyncVar<float> networkVisionRadius = new SyncVar<float>();

        private VisionSystem visionSystem;
        private VisionRevealer visionRevealer;

        private void Awake()
        {
            visionSystem = GetComponent<VisionSystem>();
            visionRevealer = GetComponent<VisionRevealer>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            networkVisionRadius.OnChange += OnVisionRadiusChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (networkVisionRadius != null)
                networkVisionRadius.OnChange -= OnVisionRadiusChanged;
        }

        private void Update()
        {
            if (!IsServer) return;

            // Server: Sync vision radius
            if (visionSystem != null)
            {
                networkVisionRadius.Value = visionSystem.GetVisionRadius();
            }
        }

        private void OnVisionRadiusChanged(float oldRadius, float newRadius, bool asServer)
        {
            if (!asServer && visionRevealer != null)
            {
                // Client: Apply server vision radius
                visionRevealer.SetVisionRadius(newRadius);
            }
        }
    }
}

