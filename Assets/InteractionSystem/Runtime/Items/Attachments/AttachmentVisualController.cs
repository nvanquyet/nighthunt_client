using System.Collections.Generic;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Items
{
    public class AttachmentVisualController : MonoBehaviour
    {
        [Header("Visual Settings")] [SerializeField]
        private Material highlightMaterial;

        [SerializeField] private Color attachedColor = Color.green;
        [SerializeField] private Color emptyColor = Color.gray;

        private AttachmentManager attachmentManager;

        private Dictionary<AttachmentSlotType, GameObject> slotIndicators =
            new Dictionary<AttachmentSlotType, GameObject>();

        private void Awake()
        {
            attachmentManager = GetComponent<AttachmentManager>();
        }

        public void CreateSlotIndicators(EquipmentDataBase equipment, Transform parent)
        {
            ClearIndicators();

            if (equipment.attachmentSlots == null) return;

            foreach (var slotDef in equipment.attachmentSlots)
            {
                GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                indicator.transform.SetParent(parent);
                indicator.transform.localPosition = slotDef.attachmentPointOffset;
                indicator.transform.localScale = Vector3.one * 0.05f;

                // Visual only
                Destroy(indicator.GetComponent<Collider>());

                Renderer renderer = indicator.GetComponent<Renderer>();
                renderer.material = new Material(highlightMaterial);
                renderer.material.color = emptyColor;

                slotIndicators[slotDef.slotType] = indicator;
            }
        }

        public void UpdateSlotVisual(AttachmentSlotType slotType, bool isOccupied)
        {
            if (!slotIndicators.TryGetValue(slotType, out GameObject indicator))
            {
                return;
            }

            Renderer renderer = indicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = isOccupied ? attachedColor : emptyColor;
            }
        }

        public void HighlightSlot(AttachmentSlotType slotType, bool highlight)
        {
            if (!slotIndicators.TryGetValue(slotType, out GameObject indicator))
            {
                return;
            }

            indicator.transform.localScale = highlight ? Vector3.one * 0.07f : Vector3.one * 0.05f;
        }

        private void ClearIndicators()
        {
            foreach (var indicator in slotIndicators.Values)
            {
                if (indicator != null)
                {
                    Destroy(indicator);
                }
            }

            slotIndicators.Clear();
        }

        private void OnDestroy()
        {
            ClearIndicators();
        }
    }
}