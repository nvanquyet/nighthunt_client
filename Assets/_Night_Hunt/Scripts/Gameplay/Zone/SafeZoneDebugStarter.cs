// SafeZoneDebugStarter.cs
// ─────────────────────────────────────────────────────────────────────────────
// Editor / development helper — starts the zone simulation directly in a game
// scene WITHOUT going through the Home → Matchmaking → ServerBootstrap flow.
//
// Typical use cases:
//   A) Open the game scene directly in Editor, press Play, click "Start Host"
//      in the FishNet NetworkHudCanvases → zone auto-starts after _startDelay.
//
//   B) Start Host via NetworkHudCanvases from the game scene and iterate on
//      zone visuals (SafeZoneWorldVisual) or HUD without a real backend.
//
// How to set up:
//   1. Add this component to the same GameObject as SafeZoneManager in the scene.
//   2. Right-click the component → "Fill: Standard 4v4" or "Fill: Small 1v1"
//      to populate the config fields, then tweak as needed.
//   3. Optionally set _startDelay (default 2 s) so you have time after pressing
//      "Start Host" before the zone kicks off.
//   4. The component is stripped from UNITY_SERVER builds automatically.
//
// Safety:
//   - Does nothing if ServerBootstrap.Instance exists (real DS boot handles it).
//   - Does nothing on the client (only runs on server / host).
// ─────────────────────────────────────────────────────────────────────────────

#if !UNITY_SERVER
using System.Collections;
using FishNet;
using NightHunt.Data;
using NightHunt.Server;
using UnityEngine;

namespace NightHunt.Gameplay.Zone
{
    /// <summary>
    /// Editor/debug helper — starts the safe-zone sequence in-scene without a backend.
    /// Stripped from dedicated-server builds. See file header for setup instructions.
    /// </summary>
    [AddComponentMenu("NightHunt/Zone/Safe Zone Debug Starter")]
    public class SafeZoneDebugStarter : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Debug Toggle")]
        [Tooltip("Uncheck to disable auto-start entirely. Zone will NOT begin on Play.\n" +
                 "You can still trigger manually via right-click → 'Debug: Start Zone Now'.")]
        [SerializeField] private bool _enableAutoStart = true;

        [Header("Zone Config")]
        [Tooltip("Config used when the server starts. Right-click → 'Fill: Standard 4v4' or 'Fill: Small 1v1' to populate defaults.")]
        [SerializeField] private SafeZoneMatchConfig _config = new();

        [Header("Timing")]
        [Tooltip("Seconds to wait after the server starts before calling BeginMatch.\n" +
                 "Gives you time to check the scene before zones activate.")]
        [SerializeField] [Min(0f)] private float _startDelay = 2f;

        // ── Runtime state ──────────────────────────────────────────────────────
        private Coroutine _waitCoroutine;

        // ── MonoBehaviour ──────────────────────────────────────────────────────

        private void Start()
        {
            if (!_enableAutoStart)
            {
                Debug.Log("[SafeZoneDebugStarter] Auto-start disabled. Use right-click → 'Debug: Start Zone Now' to trigger manually.");
                return;
            }

            if (ServerBootstrap.Instance != null)
            {
                Debug.Log("[SafeZoneDebugStarter] ServerBootstrap detected — deferring to real boot sequence.");
                return;
            }

            _waitCoroutine = StartCoroutine(WaitAndBegin());
        }

        private void OnDisable()
        {
            if (_waitCoroutine != null)
            {
                StopCoroutine(_waitCoroutine);
                _waitCoroutine = null;
            }
        }

        private IEnumerator WaitAndBegin()
        {
            yield return new WaitUntil(() =>
                InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started);

            if (_startDelay > 0f)
                yield return new WaitForSeconds(_startDelay);

            var mgr = SafeZoneManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning("[SafeZoneDebugStarter] SafeZoneManager.Instance is null. " +
                                 "Make sure SafeZoneManager is in the scene and is a NetworkObject.");
                yield break;
            }

            mgr.BeginMatch(_config);
            Debug.Log($"[SafeZoneDebugStarter] Zone started — phases={_config.phases.Count}  " +
                      $"initialRadius={_config.initialRadius}  centerMode={_config.centerMode}");
        }

        // ── Editor context menus ───────────────────────────────────────────────
#if UNITY_EDITOR
        [ContextMenu("Debug: Start Zone Now")]
        private void DbgStartNow()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[SafeZoneDebugStarter] Enter Play mode first."); return; }
            if (ServerBootstrap.Instance != null) { Debug.LogWarning("[SafeZoneDebugStarter] ServerBootstrap present — skipping."); return; }
            if (_waitCoroutine != null) StopCoroutine(_waitCoroutine);
            _waitCoroutine = StartCoroutine(WaitAndBegin());
            Debug.Log("[SafeZoneDebugStarter] Manual start triggered.");
        }

        [ContextMenu("Debug: Stop / Cancel Pending Start")]
        private void DbgStop()
        {
            if (_waitCoroutine != null)
            {
                StopCoroutine(_waitCoroutine);
                _waitCoroutine = null;
                Debug.Log("[SafeZoneDebugStarter] Pending start cancelled.");
            }
            else
            {
                Debug.Log("[SafeZoneDebugStarter] Nothing to cancel.");
            }
        }

        [ContextMenu("Fill: Standard 4v4")]
        private void FillStandard4v4()
        {
            UnityEditor.Undo.RecordObject(this, "Fill Standard 4v4 Config");
            _config = SafeZoneMatchConfig.Standard();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[SafeZoneDebugStarter] Filled Standard 4v4 config.");
        }

        [ContextMenu("Fill: Small 1v1")]
        private void FillSmall1v1()
        {
            UnityEditor.Undo.RecordObject(this, "Fill Small 1v1 Config");
            _config = SafeZoneMatchConfig.Small1v1();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[SafeZoneDebugStarter] Filled Small 1v1 config.");
        }
#endif
    }
}
#endif