using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// NOTE: GameWebSocketService is in Assembly-CSharp (no custom asmdef).
// This test uses reflection to avoid a compile-time dependency on that assembly,
// which cannot be directly referenced from a custom test asmdef in Unity 2021+.

namespace NightHunt.Tests
{
    public class GameWebSocketServicePresenceTests
    {
        private const string ServiceTypeName = "NightHunt.Services.Game.GameWebSocketService";

        private GameObject _go;
        private MonoBehaviour _service;
        private MethodInfo _handleMessage;
        private Type _serviceType;

        [SetUp]
        public void SetUp()
        {
            _serviceType = Type.GetType(ServiceTypeName + ", Assembly-CSharp");
            if (_serviceType == null)
                _serviceType = FindTypeAcrossAssemblies(ServiceTypeName);

            Assert.That(_serviceType, Is.Not.Null,
                $"Could not find type '{ServiceTypeName}' — ensure GameWebSocketService.cs compiles correctly.");

            _go = new GameObject("GameWebSocketServicePresenceTests");
            _service = (MonoBehaviour)_go.AddComponent(_serviceType);

            _handleMessage = _serviceType.GetMethod(
                "HandleMessage",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void MatchPresenceNotice_ParsesPayloadAndRaisesEvent()
        {
            // Subscribe to OnMatchPresenceNotice via reflection
            var eventInfo = _serviceType.GetEvent("OnMatchPresenceNotice");
            Assert.That(eventInfo, Is.Not.Null, "OnMatchPresenceNotice event not found");

            object received = null;
            // Build a delegate that captures 'received'
            var handlerType  = eventInfo.EventHandlerType;
            var handlerParam = handlerType.GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var capture      = new Action<object>(evt => received = evt);
            var handler      = DelegateHelper.CreateHandler(handlerType, capture);
            eventInfo.AddEventHandler(_service, handler);

            InvokeMessage("{\"type\":\"match_presence_notice\",\"data\":\"{\\\"matchId\\\":\\\"match-1\\\",\\\"userId\\\":42,\\\"displayName\\\":\\\"Alice\\\",\\\"state\\\":\\\"DISCONNECTED\\\",\\\"reason\\\":\\\"TRANSPORT_DROP\\\",\\\"graceSeconds\\\":60,\\\"message\\\":\\\"Player disconnected. Slot held for 60s.\\\"}\"}");

            Assert.That(received, Is.Not.Null, "OnMatchPresenceNotice was not fired");
            Assert.That(GetField(received, "matchId"),    Is.EqualTo("match-1"));
            Assert.That(GetField(received, "userId"),     Is.EqualTo(42L).Or.EqualTo(42));
            Assert.That(GetField(received, "state"),      Is.EqualTo("DISCONNECTED"));
            Assert.That(GetField(received, "reason"),     Is.EqualTo("TRANSPORT_DROP"));
            Assert.That(GetField(received, "graceSeconds"), Is.EqualTo(60));
        }

        [Test]
        public void YouWereKicked_ParsesAfkPayloadAndRaisesEvent()
        {
            var eventInfo = _serviceType.GetEvent("OnYouWereKicked");
            Assert.That(eventInfo, Is.Not.Null, "OnYouWereKicked event not found");

            object received = null;
            var capture = new Action<object>(evt => received = evt);
            var handler = DelegateHelper.CreateHandler(eventInfo.EventHandlerType, capture);
            eventInfo.AddEventHandler(_service, handler);

            InvokeMessage("{\"type\":\"you_were_kicked\",\"data\":\"{\\\"roomId\\\":7,\\\"matchId\\\":\\\"match-1\\\",\\\"reason\\\":\\\"afk_abandoned\\\",\\\"message\\\":\\\"Your slot was released after the reconnect grace window.\\\",\\\"graceSeconds\\\":60}\"}");

            Assert.That(received, Is.Not.Null, "OnYouWereKicked was not fired");
            Assert.That(GetField(received, "roomId"),      Is.EqualTo(7L).Or.EqualTo(7));
            Assert.That(GetField(received, "matchId"),     Is.EqualTo("match-1"));
            Assert.That(GetField(received, "reason"),      Is.EqualTo("afk_abandoned"));
            Assert.That(GetField(received, "message").ToString(), Does.Contain("reconnect grace"));
            Assert.That(GetField(received, "graceSeconds"), Is.EqualTo(60));
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void InvokeMessage(string json)
        {
            Assert.That(_handleMessage, Is.Not.Null, "HandleMessage(string) private method not found");
            _handleMessage.Invoke(_service, new object[] { json });
        }

        private static object GetField(object obj, string fieldName)
        {
            return obj.GetType()
                      .GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     ?.GetValue(obj);
        }

        private static Type FindTypeAcrossAssemblies(string fullTypeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullTypeName);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// Creates a delegate of the given type that calls the Action&lt;object&gt; capture.
        /// Required because event handler types are generic (Action&lt;PresenceNoticeEvent&gt;) and
        /// cannot be directly constructed without a compile-time reference to the payload type.
        /// </summary>
        private static class DelegateHelper
        {
            public static Delegate CreateHandler(Type delegateType, Action<object> capture)
            {
                // delegateType is Action<T> for some T we don't know at compile time.
                // We wrap it: create a lambda (T arg) => capture(arg).
                var invokeMethod = delegateType.GetMethod("Invoke")!;
                var paramType    = invokeMethod.GetParameters()[0].ParameterType;

                // Use System.Linq.Expressions to build the wrapper
                var param = System.Linq.Expressions.Expression.Parameter(paramType, "arg");
                var captureConst = System.Linq.Expressions.Expression.Constant(capture);
                var invokeCapture = System.Linq.Expressions.Expression.Call(
                    captureConst,
                    typeof(Action<object>).GetMethod("Invoke")!,
                    System.Linq.Expressions.Expression.Convert(param, typeof(object)));
                var lambda = System.Linq.Expressions.Expression.Lambda(delegateType, invokeCapture, param);
                return lambda.Compile();
            }
        }
    }
}
