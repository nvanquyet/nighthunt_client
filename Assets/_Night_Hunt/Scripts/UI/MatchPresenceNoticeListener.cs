using System.Collections;
using NightHunt.Core;
using NightHunt.Services.Game;
using NightHunt.State;
using UnityEngine;

namespace NightHunt.UI
{
    [DisallowMultipleComponent]
    public sealed class MatchPresenceNoticeListener : MonoBehaviour
    {
        private bool _subscribed;

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
            string title = ResolveTitle(state, isLocalUser);
            string name = !string.IsNullOrEmpty(evt.displayName) ? evt.displayName : $"Player {evt.userId}";
            string message = !string.IsNullOrEmpty(evt.message) ? evt.message : $"{name}: {state}";
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

        private static string ResolveTitle(string state, bool isLocalUser)
        {
            if (string.Equals(state, "CONNECTED", System.StringComparison.OrdinalIgnoreCase))
                return isLocalUser ? "Reconnected" : "Player Reconnected";
            if (string.Equals(state, "ABANDONED", System.StringComparison.OrdinalIgnoreCase))
                return isLocalUser ? "Removed From Match" : "Player Removed";
            return isLocalUser ? "Connection Lost" : "Player Disconnected";
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
