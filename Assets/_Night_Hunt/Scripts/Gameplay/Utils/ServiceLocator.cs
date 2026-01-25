using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Night_Hunt.Scripts.Gameplay.Utils
{
    public class ServiceLocator
    {
        private static ServiceLocator _instance;
        public static ServiceLocator Instance => _instance ??= new ServiceLocator();

        private readonly Dictionary<Type, object> _services = new();

        public void Register<T>(T service)
        {
            _services[typeof(T)] = service;
        }

        public T Get<T>()
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;

            Debug.LogError($"Service {typeof(T).Name} not registered");
            return default;
        }

        public void Clear() => _services.Clear();
    }
}
