using NightHunt.Core;
using UnityEngine;

namespace NightHunt.UI
{
    [DisallowMultipleComponent]
    public sealed class SessionTerminationListener : SingletonPersistent<SessionTerminationListener>
    {
        private bool _subscribed;
        private Coroutine _subscribeRoutine;

        protected override void OnSingletonAwake()
        {
        }

        private void OnEnable()
        {
            TrySubscribe();
            if (!_subscribed)
                _subscribeRoutine = StartCoroutine(SubscribeWhenBusReady());
        }

        private void OnDisable()
        {
            if (_subscribeRoutine != null)
            {
                StopCoroutine(_subscribeRoutine);
                _subscribeRoutine = null;
            }

            Unsubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;

            var bus = GameEventBus.Instance;
            if (bus == null) return;

            bus.OnForceLogout += HandleForceLogout;
            bus.OnSessionExpired += HandleSessionExpired;
            _subscribed = true;
            _subscribeRoutine = null;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            var bus = GameEventBus.Instance;
            if (bus != null)
            {
                bus.OnForceLogout -= HandleForceLogout;
                bus.OnSessionExpired -= HandleSessionExpired;
            }

            _subscribed = false;
        }

        private System.Collections.IEnumerator SubscribeWhenBusReady()
        {
            const float timeout = 15f;
            float elapsed = 0f;

            while (!_subscribed && elapsed < timeout)
            {
                TrySubscribe();
                if (_subscribed)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _subscribeRoutine = null;
        }

        private static void HandleForceLogout()
        {
            SessionTerminationFlow.ShowAndLogout(
                "Forced Logout",
                "Your account has been logged in from another location.");
        }

        private static void HandleSessionExpired()
        {
            SessionTerminationFlow.ShowAndLogout(
                "Session Expired",
                "Your session has expired. Please log in again.");
        }
    }
}
