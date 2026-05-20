using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace NightHunt.UI
{
    /// <summary>
    /// Central fallback for UI animation callbacks.
    /// Inspector UnityEvents are still supported, but views no longer break when
    /// a scene misses the callback wiring: the invoker searches nearby components
    /// for common Shift/MUIP animation methods such as WindowIn/WindowOut.
    /// </summary>
    public static class UIAnimationCallbackInvoker
    {
        private static readonly string[] OpenFallbacks =
        {
            "WindowIn",
            "ModalWindowIn",
            "Open",
            "Show"
        };

        private static readonly string[] CloseFallbacks =
        {
            "WindowOut",
            "ModalWindowOut",
            "Close",
            "Hide"
        };

        public static bool InvokeOpen(UnityEvent callback, Component owner) =>
            Invoke(callback, owner, OpenFallbacks);

        public static bool InvokeClose(UnityEvent callback, Component owner) =>
            Invoke(callback, owner, CloseFallbacks);

        public static bool Invoke(UnityEvent callback, Component owner, params string[] fallbackMethodNames)
        {
            if (HasValidPersistentListener(callback))
            {
                callback.Invoke();
                return true;
            }

            if (owner == null || fallbackMethodNames == null || fallbackMethodNames.Length == 0)
                return false;

            if (ContainsMethodName(fallbackMethodNames, "WindowIn") &&
                ShiftUIBridge.OpenShiftWindow(owner))
            {
                return true;
            }

            if (ContainsMethodName(fallbackMethodNames, "WindowOut") &&
                ShiftUIBridge.CloseShiftWindow(owner))
            {
                return true;
            }

            var behaviours = owner.GetComponentsInParent<MonoBehaviour>(true);
            if (TryInvokeFallback(behaviours, owner, fallbackMethodNames))
                return true;

            behaviours = owner.GetComponentsInChildren<MonoBehaviour>(true);
            return TryInvokeFallback(behaviours, owner, fallbackMethodNames);
        }

        private static bool HasValidPersistentListener(UnityEvent callback)
        {
            if (callback == null)
                return false;

            int count = callback.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                if (callback.GetPersistentTarget(i) != null &&
                    !string.IsNullOrWhiteSpace(callback.GetPersistentMethodName(i)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryInvokeFallback(MonoBehaviour[] behaviours, Component owner, string[] methodNames)
        {
            if (behaviours == null)
                return false;

            foreach (var behaviour in behaviours)
            {
                if (behaviour == null || behaviour == owner)
                    continue;

                Type type = behaviour.GetType();
                foreach (string methodName in methodNames)
                {
                    var method = type.GetMethod(
                        methodName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null);

                    if (method == null)
                        continue;

                    method.Invoke(behaviour, null);
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsMethodName(string[] methodNames, string expected)
        {
            if (methodNames == null)
                return false;

            foreach (string methodName in methodNames)
            {
                if (string.Equals(methodName, expected, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
