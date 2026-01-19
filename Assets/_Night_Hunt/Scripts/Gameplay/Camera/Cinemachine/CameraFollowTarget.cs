using UnityEngine;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Camera.Cinemachine
{
    /// <summary>
    /// Setup camera follow target
    /// </summary>
    public class CameraFollowTarget : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private bool autoFindPlayer = true;

        private CinemachineCameraController cameraController;

        private void Awake()
        {
            cameraController = GetComponent<CinemachineCameraController>();
            if (cameraController == null)
            {
                cameraController = GetComponentInParent<CinemachineCameraController>();
            }
        }

        private void Start()
        {
            SetupFollowTarget();
        }

        /// <summary>
        /// Setup follow target
        /// </summary>
        private void SetupFollowTarget()
        {
            if (followTarget == null && autoFindPlayer)
            {
                // Find local player
                var networkPlayer = FindFirstObjectByType<NetworkPlayer>();
                if (networkPlayer != null && networkPlayer.IsLocalPlayer)
                {
                    followTarget = networkPlayer.transform;
                }
            }

            if (cameraController != null && followTarget != null)
            {
                cameraController.SetFollowTarget(followTarget);
            }
        }

        /// <summary>
        /// Set follow target manually
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            if (cameraController != null)
            {
                cameraController.SetFollowTarget(target);
            }
        }
    }
}

