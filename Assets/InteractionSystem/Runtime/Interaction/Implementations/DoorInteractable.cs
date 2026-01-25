using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Interaction
{
    public class DoorInteractable : InteractableBase
    {
        [Header("Door Settings")] [SerializeField]
        private DoorMode doorMode = DoorMode.Automatic;

        [SerializeField] private Transform doorTransform;
        [SerializeField] private Vector3 openRotation = new Vector3(0, 90, 0);
        [SerializeField] private float openSpeed = 2f;

        [SyncVar(OnChange = nameof(OnDoorStateChanged))]
        private bool isOpen;

        private Quaternion closedRotation;
        private Quaternion targetOpenRotation;

        public enum DoorMode
        {
            Immediate, // Instant open
            Hold, // Hold to open
            Automatic // Auto open on proximity
        }

        private void Start()
        {
            closedRotation = doorTransform.localRotation;
            targetOpenRotation = Quaternion.Euler(openRotation);

            // Setup interaction type based on mode
            interactionType = doorMode switch
            {
                DoorMode.Immediate => InteractionType.Immediate,
                DoorMode.Hold => InteractionType.Hold,
                DoorMode.Automatic => InteractionType.Toggle,
                _ => InteractionType.Immediate
            };

            if (doorMode == DoorMode.Hold)
            {
                holdDuration = 2f;
                interactionPrompt = "Hold to open door";
            }
        }

        public override bool CanInteract(NetworkConnection player)
        {
            if (doorMode == DoorMode.Automatic) return false; // No manual interaction
            return base.CanInteract(player);
        }

        public override void OnInteract(NetworkConnection player)
        {
            if (!IsServer) return;

            ToggleDoor();
        }

        [Server]
        private void ToggleDoor()
        {
            isOpen = !isOpen;
        }

        private void OnDoorStateChanged(bool oldValue, bool newValue, bool asServer)
        {
            StopAllCoroutines();
            StartCoroutine(AnimateDoor(newValue));
        }

        private IEnumerator AnimateDoor(bool open)
        {
            Quaternion startRot = doorTransform.localRotation;
            Quaternion endRot = open ? targetOpenRotation : closedRotation;

            float elapsed = 0f;
            float duration = 1f / openSpeed;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                doorTransform.localRotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }

            doorTransform.localRotation = endRot;
        }

        // For automatic doors
        private void OnTriggerEnter(Collider other)
        {
            if (doorMode != DoorMode.Automatic) return;
            if (!IsServer) return;

            if (other.CompareTag("Player"))
            {
                isOpen = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (doorMode != DoorMode.Automatic) return;
            if (!IsServer) return;

            if (other.CompareTag("Player"))
            {
                isOpen = false;
            }
        }
    }
}