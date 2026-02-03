using UnityEngine;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Utility for finding components in hierarchy.
    /// Used when components are not on the same GameObject but in child objects.
    /// </summary>
    public static class ComponentFinder
    {
        /// <summary>
        /// Find component in hierarchy (self, children, parent).
        /// </summary>
        public static T FindInHierarchy<T>(GameObject root) where T : Component
        {
            if (root == null) return null;
            
            // Check self
            T component = root.GetComponent<T>();
            if (component != null) return component;
            
            // Check children
            component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
            
            // Check parent
            component = root.GetComponentInParent<T>();
            if (component != null) return component;
            
            return null;
        }
        
        /// <summary>
        /// Find component in hierarchy starting from transform.
        /// </summary>
        public static T FindInHierarchy<T>(Transform root) where T : Component
        {
            if (root == null) return null;
            return FindInHierarchy<T>(root.gameObject);
        }
        
        /// <summary>
        /// Find component in hierarchy starting from component.
        /// </summary>
        public static T FindInHierarchy<T>(Component from) where T : Component
        {
            if (from == null) return null;
            return FindInHierarchy<T>(from.gameObject);
        }
    }
}
