using UnityEngine;
using FOW;
using FishNet.Object;

namespace NightHunt.Gameplay.Vision
{
    /// <summary>
    /// Deployable vision devices
    /// Creates new revealer when deployed
    /// </summary>
    public class VisionDevice : NetworkBehaviour
    {
        [Header("Vision Device Settings")]
        [SerializeField] private float visionRadius = 10f;
        [SerializeField] private float duration = 60f;
        [SerializeField] private GameObject revealerPrefab;

        private FogOfWarRevealer3D deployedRevealer;
        private float deployTime;

        /// <summary>
        /// Deploy vision device
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void DeployDevice(Vector3 position)
        {
            // Server: Create revealer
            CreateRevealer(position);
            
            // Broadcast to clients
            DeployDeviceClient(position);
        }

        /// <summary>
        /// Client: Create revealer locally
        /// </summary>
        [ObserversRpc]
        private void DeployDeviceClient(Vector3 position)
        {
            if (!IsServer)
            {
                CreateRevealer(position);
            }
        }

        /// <summary>
        /// Create revealer at position
        /// </summary>
        private void CreateRevealer(Vector3 position)
        {
            GameObject revealerObj;
            if (revealerPrefab != null)
            {
                revealerObj = Instantiate(revealerPrefab, position, Quaternion.identity);
            }
            else
            {
                revealerObj = new GameObject("VisionDeviceRevealer");
                revealerObj.transform.position = position;
            }

            deployedRevealer = revealerObj.AddComponent<FogOfWarRevealer3D>();
            FogOfWarHelper.SetRayDistance(deployedRevealer, visionRadius);
            deployTime = Time.time;

            // Auto-destroy after duration
            if (duration > 0f)
            {
                Destroy(revealerObj, duration);
            }
        }

        /// <summary>
        /// Remove deployed device
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RemoveDevice()
        {
            if (deployedRevealer != null)
            {
                Destroy(deployedRevealer.gameObject);
            }
        }
    }
}

