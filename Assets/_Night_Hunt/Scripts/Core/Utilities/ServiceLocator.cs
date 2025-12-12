using UnityEngine;

namespace NightHunt.Core.Utilities
{
    /// <summary>
    /// Service Locator - Dependency Injection alternative
    /// Thread-safe singleton access to core systems
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly System.Collections.Generic.Dictionary<System.Type, object> services = 
            new System.Collections.Generic.Dictionary<System.Type, object>();
        
        private static bool initialized = false;

        public static void Initialize()
        {
            if (initialized) return;
            services.Clear();
            initialized = true;
        }

        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Service {type.Name} already registered. Replacing...");
                services[type] = service;
            }
            else
            {
                services.Add(type, service);
            }
        }

        public static T Get<T>() where T : class
        {
            var type = typeof(T);
            if (services.TryGetValue(type, out var service))
            {
                return service as T;
            }
            
            Debug.LogError($"[ServiceLocator] Service {type.Name} not found!");
            return null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            var type = typeof(T);
            if (services.TryGetValue(type, out var s))
            {
                service = s as T;
                return service != null;
            }
            service = null;
            return false;
        }

        public static void Unregister<T>() where T : class
        {
            services.Remove(typeof(T));
        }

        public static void Clear()
        {
            services.Clear();
            initialized = false;
        }
    }

}