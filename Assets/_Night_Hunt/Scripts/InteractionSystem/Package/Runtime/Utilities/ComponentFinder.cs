using UnityEngine;

namespace NightHunt.InteractionSystem.Utilities
{
    /// <summary>
    /// Helper utilities for finding components in GameObject hierarchy.
    /// Provides centralized component finding logic to avoid code duplication.
    /// </summary>
    public static class ComponentFinder
    {
        /// <summary>
        /// Find component using comprehensive search strategy:
        /// 1. Current GameObject
        /// 2. Parent (GetComponentInParent)
        /// 3. Children (GetComponentInChildren)
        /// 4. Root GameObject
        /// 5. Root's children (GetComponentInChildren on root)
        /// </summary>
        /// <typeparam name="T">Component type to find</typeparam>
        /// <param name="gameObject">Starting GameObject</param>
        /// <param name="includeInactive">Whether to include inactive GameObjects in search</param>
        /// <returns>Found component or null if not found</returns>
        public static T FindComponentInHierarchy<T>(GameObject gameObject, bool includeInactive = false) where T : Component
        {
            if (gameObject == null)
                return null;

            // Strategy 1: Current GameObject
            T component = gameObject.GetComponent<T>();
            if (component != null)
                return component;

            // Strategy 2: Parent
            component = gameObject.GetComponentInParent<T>(includeInactive);
            if (component != null)
                return component;

            // Strategy 3: Children
            component = gameObject.GetComponentInChildren<T>(includeInactive);
            if (component != null)
                return component;

            // Strategy 4: Root GameObject
            Transform root = gameObject.transform.root;
            if (root != null && root.gameObject != gameObject)
            {
                component = root.GetComponent<T>();
                if (component != null)
                    return component;

                // Strategy 5: Root's children
                component = root.GetComponentInChildren<T>(includeInactive);
                if (component != null)
                    return component;
            }

            return null;
        }

        /// <summary>
        /// Find component using comprehensive search strategy (extension method for GameObject).
        /// </summary>
        /// <typeparam name="T">Component type to find</typeparam>
        /// <param name="gameObject">Starting GameObject</param>
        /// <param name="includeInactive">Whether to include inactive GameObjects in search</param>
        /// <returns>Found component or null if not found</returns>
        public static T FindInHierarchy<T>(this GameObject gameObject, bool includeInactive = false) where T : Component
        {
            return FindComponentInHierarchy<T>(gameObject, includeInactive);
        }

        /// <summary>
        /// Find component using comprehensive search strategy (extension method for Component).
        /// </summary>
        /// <typeparam name="T">Component type to find</typeparam>
        /// <param name="component">Starting Component</param>
        /// <param name="includeInactive">Whether to include inactive GameObjects in search</param>
        /// <returns>Found component or null if not found</returns>
        public static T FindInHierarchy<T>(this Component component, bool includeInactive = false) where T : Component
        {
            if (component == null)
                return null;

            return FindComponentInHierarchy<T>(component.gameObject, includeInactive);
        }

        /// <summary>
        /// Find component that implements interface using comprehensive search strategy.
        /// Unity's GetComponent can find interfaces, so we search through hierarchy.
        /// </summary>
        /// <typeparam name="T">Interface type to find</typeparam>
        /// <param name="gameObject">Starting GameObject</param>
        /// <param name="includeInactive">Whether to include inactive GameObjects in search</param>
        /// <returns>Found component implementing interface or null if not found</returns>
        public static T FindInterfaceInHierarchy<T>(GameObject gameObject, bool includeInactive = false) where T : class
        {
            if (gameObject == null)
                return null;

            // Strategy 1: Current GameObject
            T component = gameObject.GetComponent<T>();
            if (component != null)
                return component;

            // Strategy 2: Parent
            if (includeInactive)
            {
                Transform parent = gameObject.transform.parent;
                while (parent != null)
                {
                    component = parent.GetComponent<T>();
                    if (component != null)
                        return component;
                    parent = parent.parent;
                }
            }
            else
            {
                Transform parent = gameObject.transform.parent;
                while (parent != null && parent.gameObject.activeSelf)
                {
                    component = parent.GetComponent<T>();
                    if (component != null)
                        return component;
                    parent = parent.parent;
                }
            }

            // Strategy 3: Children
            component = gameObject.GetComponentInChildren<T>(includeInactive);
            if (component != null)
                return component;

            // Strategy 4: Root GameObject
            Transform root = gameObject.transform.root;
            if (root != null && root.gameObject != gameObject)
            {
                component = root.GetComponent<T>();
                if (component != null)
                    return component;

                // Strategy 5: Root's children
                component = root.GetComponentInChildren<T>(includeInactive);
                if (component != null)
                    return component;
            }

            return null;
        }

        /// <summary>
        /// Find component that implements interface (extension method for GameObject).
        /// </summary>
        /// <typeparam name="T">Interface type to find</typeparam>
        /// <param name="gameObject">Starting GameObject</param>
        /// <param name="includeInactive">Whether to include inactive GameObjects in search</param>
        /// <returns>Found component implementing interface or null if not found</returns>
        public static T FindInterface<T>(this GameObject gameObject, bool includeInactive = false) where T : class
        {
            return FindInterfaceInHierarchy<T>(gameObject, includeInactive);
        }

        /// <summary>
        /// Find component that implements interface (extension method for Component).
        /// </summary>
        /// <typeparam name="T">Interface type to find</typeparam>
        /// <param name="component">Starting Component</param>
        /// <param name="includeInactive">Whether to include inactive GameObjects in search</param>
        /// <returns>Found component implementing interface or null if not found</returns>
        public static T FindInterface<T>(this Component component, bool includeInactive = false) where T : class
        {
            if (component == null)
                return null;

            return FindInterfaceInHierarchy<T>(component.gameObject, includeInactive);
        }
    }
}
