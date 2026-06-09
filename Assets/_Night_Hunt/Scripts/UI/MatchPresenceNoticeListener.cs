using System.Collections;
using System.Collections.Generic;
using NightHunt.Core;
using NightHunt.Services.Game;
using NightHunt.State;
using UnityEngine;

namespace NightHunt.UI
{
    [DisallowMultipleComponent]
    public sealed class MatchPresenceNoticeListener : MonoBehaviour
    {
        private const float DuplicateConnectedNoticeWindowSeconds = 5f;

        private bool _subscribed;
        private readonly Dictionary<string, float> _recentConnectedNoticeTimes = new();

        private IEnumerator Start()
        {
            float deadline = Time.unscaledTime + 15f;
            while (GameEventBus.Instance == null && Time.unscaledTime < deadline)
                yield return null;

            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || GameEventBus.Instance == null)
                return;

            GameEventBus.Instance.OnMatchPresenceNotice += HandleMatchPresenceNotice;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || GameEventBus.Instance == null)
                return;

            GameEventBus.Instance.OnMatchPresenceNotice -= HandleMatchPresenceNotice;
            _subscribed = false;
        }

        private void HandleMatchPresenceNotice(GameWebSocketService.MatchPresenceNoticeEvent evt)
        {
            if (evt == null)
                return;

            var nav = UINavigator.Instance;
            if (nav != null && (nav.CurrentPanel == PanelType.PartyCustomMode || nav.CurrentPanel == PanelType.Lobby))
                return;

            long localUserId = SessionState.Instance?.UserId ?? 0L;
            bool isLocalUser = localUserId > 0L && evt.userId == localUserId;
            string state = evt.state ?? "";
            string title = ResolveTitle(state, evt.reason, isLocalUser);
            string name = !string.IsNullOrEmpty(evt.displayName) ? evt.displayName : $"Player {evt.userId}";
            string message = !string.IsNullOrEmpty(evt.message) ? evt.message : $"{name}: {state}";
            if (ShouldSuppressDuplicateConnectedNotice(evt, state, out string dedupeKey))
            {
                Debug.Log(
                    $"[NH_PRESENCE][NOTICE_DEDUP] state={state} userId={evt.userId} key={dedupeKey} " +
                    $"window={DuplicateConnectedNoticeWindowSeconds:F1}s message='{message}'");
                return;
            }

            Debug.Log(
                $"[NH_PRESENCE][NOTICE] state={state} userId={evt.userId} localUserId={localUserId} " +
                $"isLocal={isLocalUser} roomId={evt.room?.roomId ?? 0} message='{message}'");

            if (isLocalUser && string.Equals(state, "ABANDONED", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[NH_PRESENCE][LOAD_HOME] Local player abandoned userId={evt.userId}; loading Home.");
                GameModalWindow.Instance?.Close();
                RoomState.Instance?.ClearRoom();
                RoomState.Instance?.ClearNetworkSession();
                GameModalWindow.Instance?.ShowNotice(title, message);
                SceneLoader.LoadHome();
                return;
            }

            PersistentUICanvas.Instance?.ToastService?.Show(title, $"{name}: {message}");
        }

        private bool ShouldSuppressDuplicateConnectedNotice(
            GameWebSocketService.MatchPresenceNoticeEvent evt,
            string state,
            out string key)
        {
            long roomId = evt.room?.roomId ?? 0L;
            string connectedKind = string.Equals(state, "CONNECTED", System.StringComparison.OrdinalIgnoreCase)
                ? ConnectedNoticeKind(evt.reason)
                : "";
            key = $"{evt.matchId ?? ""}|{roomId}|{evt.userId}|{state ?? ""}|{connectedKind}";

            if (!string.Equals(state, "CONNECTED", System.StringComparison.OrdinalIgnoreCase))
                return false;

            float now = Time.unscaledTime;
            if (_recentConnectedNoticeTimes.TryGetValue(key, out float lastAt)
                && (now - lastAt) < DuplicateConnectedNoticeWindowSeconds)
            {
                _recentConnectedNoticeTimes[key] = now;
                return true;
            }

            _recentConnectedNoticeTimes[key] = now;
            return false;
        }

        private static string ResolveTitle(string state, string reason, bool isLocalUser)
        {
            if (string.Equals(state, "CONNECTED", System.StringComparison.OrdinalIgnoreCase))
            {
                bool reconnected = IsReconnectReason(reason);
                if (reconnected)
                    return isLocalUser ? "Reconnected" : "Player Reconnected";

                return isLocalUser ? "Connected" : "Player Connected";
            }

            if (string.Equals(state, "ABANDONED", System.StringComparison.OrdinalIgnoreCase))
                return isLocalUser ? "Removed From Match" : "Player Removed";
            return isLocalUser ? "Connection Lost" : "Player Disconnected";
        }

        private static string ConnectedNoticeKind(string reason)
        {
            return IsReconnectReason(reason) ? "reconnected" : "connected";
        }

        private static bool IsReconnectReason(string reason)
        {
            return !string.IsNullOrEmpty(reason)
                && reason.IndexOf("RECONNECTED", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

#if UNITY_EDITOR
        // ── Editor ───────────────────────────────────────────────────────────

        [ContextMenu("NightHunt/Register with PersistentUICanvas")]
        private void Editor_RegisterWithPersistentUI()
        {
            var canvas = FindFirstObjectByType<PersistentUICanvas>();
            if (canvas == null)
            {
                Debug.LogWarning(
                    "[MatchPresenceNoticeListener] PersistentUICanvas not found in scene. \n" +
                    "Add it first, then re-run this menu.");
                return;
            }

            var so = new UnityEditor.SerializedObject(canvas);
            var prop = so.FindProperty("matchPresenceNoticeListener");
            if (prop != null)
            {
                prop.objectReferenceValue = this;
                so.ApplyModifiedProperties();
                UnityEditor.EditorUtility.SetDirty(canvas);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
                Debug.Log("[MatchPresenceNoticeListener] Registered in PersistentUICanvas.");
            }
            else
            {
                Debug.LogWarning("[MatchPresenceNoticeListener] 'matchPresenceNoticeListener' field not found on PersistentUICanvas.");
            }
        }

        [ContextMenu("NightHunt/Move Under PersistentUICanvas")]
        private void Editor_MoveUnderPersistentUI()
        {
            var canvas = FindFirstObjectByType<PersistentUICanvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[MatchPresenceNoticeListener] PersistentUICanvas not found in scene.");
                return;
            }

            transform.SetParent(canvas.transform, false);
            Editor_RegisterWithPersistentUI();
            Debug.Log("[MatchPresenceNoticeListener] Moved under PersistentUICanvas.");
        }
#endif
    }
}
