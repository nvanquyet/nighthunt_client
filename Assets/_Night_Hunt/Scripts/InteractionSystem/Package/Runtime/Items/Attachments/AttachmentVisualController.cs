using UnityEngine;
using NightHunt.InteractionSystem.Items.Data;

namespace NightHunt.InteractionSystem.Items.Attachments
{
    /// <summary>
    /// Controls visual representation of attachments on equipment.
    /// </summary>
    public class AttachmentVisualController : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private bool usePooling = true;
        [SerializeField] private Transform attachmentParent;

        private AttachmentManager attachmentManager;

        private void Awake()
        {
            attachmentManager = GetComponent<AttachmentManager>();
            if (attachmentManager == null)
            {
                attachmentManager = GetComponentInParent<AttachmentManager>();
            }

            if (attachmentParent == null)
            {
                attachmentParent = transform;
            }
        }

        /// <summary>
        /// Spawn attachment visual at attachment point.
        /// </summary>
        public GameObject SpawnAttachmentVisual(AttachmentData attachment, Transform attachmentPoint, Vector3 position, Vector3 rotation, float scale)
        {
            if (attachment == null || attachment.AttachmentPrefab == null)
                return null;

            GameObject visual = Instantiate(attachment.AttachmentPrefab, attachmentPoint);
            visual.transform.localPosition = position;
            visual.transform.localRotation = Quaternion.Euler(rotation);
            visual.transform.localScale = Vector3.one * scale;

            return visual;
        }

        /// <summary>
        /// Destroy attachment visual.
        /// </summary>
        public void DestroyAttachmentVisual(GameObject visual)
        {
            if (visual != null)
            {
                Destroy(visual);
            }
        }
    }
}
