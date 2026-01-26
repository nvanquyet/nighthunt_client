using UnityEngine;

namespace NightHunt.InteractionSystem.Utilities
{
    /// <summary>
    /// Organizes components into child GameObjects for better hierarchy management.
    /// </summary>
    public static class ComponentOrganizer
    {
        /// <summary>
        /// Get or create a child GameObject with the specified name.
        /// </summary>
        public static GameObject GetOrCreateChild(GameObject parent, string childName)
        {
            if (parent == null)
                return null;

            // Try to find existing child
            Transform child = parent.transform.Find(childName);
            if (child != null)
                return child.gameObject;

            // Create new child
            GameObject childObj = new GameObject(childName);
            childObj.transform.SetParent(parent.transform);
            childObj.transform.localPosition = Vector3.zero;
            childObj.transform.localRotation = Quaternion.identity;
            childObj.transform.localScale = Vector3.one;

            return childObj;
        }

        /// <summary>
        /// Get or create the PickupSystem child object.
        /// </summary>
        public static GameObject GetPickupSystemObject(GameObject parent)
        {
            return GetOrCreateChild(parent, "PickupSystem");
        }

        /// <summary>
        /// Get or create the InteractionSystem child object.
        /// </summary>
        public static GameObject GetInteractionSystemObject(GameObject parent)
        {
            return GetOrCreateChild(parent, "InteractionSystem");
        }

        /// <summary>
        /// Get or create the InventorySystem child object.
        /// </summary>
        public static GameObject GetInventorySystemObject(GameObject parent)
        {
            return GetOrCreateChild(parent, "InventorySystem");
        }

        /// <summary>
        /// Get or create the EquipmentSystem child object.
        /// </summary>
        public static GameObject GetEquipmentSystemObject(GameObject parent)
        {
            return GetOrCreateChild(parent, "EquipmentSystem");
        }
    }
}
