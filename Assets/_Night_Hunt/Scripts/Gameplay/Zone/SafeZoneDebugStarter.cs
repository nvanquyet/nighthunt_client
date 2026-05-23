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
//   2. Choose a MapPreset in the Inspector (Standard_4v4 or Small_1v1).
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
    /// Stripped from dedicated-server builds.  See file header for setup instructions.
    /// </summary>
    [AddComponentMenu("NightHunt/Zone/Safe Zone Debug Starter")]
    public class SafeZoneDebugStarter : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        public enum MapPreset
        {
            /// <summary>Standard 4v4 map — 5 phases, R=400 → 10.</summary>
            Standard_4v4,
            /// <summary>Small 1v1 arena — 3 phases, R=100 → 10, Fixed center.</summary>
            Small_1v1,
            /// <summary>Use the config defined in _customConfig below.</summary>
            Custom,
        }

        [Header("Debug Toggle")]
        [Tooltip("Uncheck to disable auto-start entirely. Zone will NOT begin on Play.\n" +
                 "You can still trigger manually via right-click → 'Debug: Start Zone Now'.")]
        [SerializeField] private bool _enableAutoStart = true;

        [Header("Zone Preset")]
        [Tooltip("Which SafeZoneMatchConfig preset to inject when the server starts.\n" +
                 "Standard_4v4 → 5 phases (R=400→10).\n" +
                 "Small_1v1    → 3 phases (R=100→10), Fixed center, shorter timers.\n" +
                 "Custom       → use _customConfig below.")]
        [SerializeField] private MapPreset _preset = MapPreset.Standard_4v4;

        [Header("Custom Config (only if preset = Custom)")]
        [Tooltip("Fully custom SafeZoneMatchConfig. Only used when Preset = Custom.")]
        [SerializeField] private SafeZoneMatchConfig _customConfig;

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

            // Do nothing when a real ServerBootstrap is running (DS build in Editor, or
            // the user opened a scene that is part of the full boot sequence).
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
            // Wait until FishNet server is actually running (host or dedicated).
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

            SafeZoneMatchConfig config = BuildConfig();
            mgr.BeginMatch(config);
            Debug.Log($"[SafeZoneDebugStarter] Zone started — preset={_preset}  phases={config.phases.Count}  " +
                      $"initialRadius={config.initialRadius}  centerMode={config.centerMode}");
        }

        private SafeZoneMatchConfig BuildConfig()
        {
            return _preset switch
            {
                MapPreset.Small_1v1 => SafeZoneMatchConfig.Small1v1(),
                MapPreset.Custom    => _customConfig ?? SafeZoneMatchConfig.Standard(),
                _                   => SafeZoneMatchConfig.Standard(),
            };
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

        [UnityEditor.MenuItem("CONTEXT/SafeZoneDebugStarter/Preview: Standard 4v4 config")]
        private static void PrintStandard(UnityEditor.MenuCommand cmd)
        {
            var c = SafeZoneMatchConfig.Standard();
            Debug.Log($"[SafeZoneDebugStarter] Standard 4v4 config:\n" +
                      $"  initialRadius={c.initialRadius}  phases={c.phases.Count}  centerMode={c.centerMode}\n" +
                      string.Join("\n", c.phases.ConvertAll(p =>
                          $"  [{p.zoneIndex}] {p.startRadius}→{p.endRadius}m  wait={p.waitBeforeShrink}s  shrink={p.shrinkDuration}s  dmg={p.damagePerSecond}/s" +
                          (p.isScoreBonusZone ? "  [BONUS ×" + p.zoneBonusMultiplier + "]" : ""))));
        }

        [UnityEditor.MenuItem("CONTEXT/SafeZoneDebugStarter/Preview: Small 1v1 config")]
        private static void PrintSmall1v1(UnityEditor.MenuCommand cmd)
        {
            var c = SafeZoneMatchConfig.Small1v1();
            Debug.Log($"[SafeZoneDebugStarter] Small 1v1 config:\n" +
                      $"  initialRadius={c.initialRadius}  phases={c.phases.Count}  centerMode={c.centerMode}\n" +
                      string.Join("\n", c.phases.ConvertAll(p =>
                          $"  [{p.zoneIndex}] {p.startRadius}→{p.endRadius}m  wait={p.waitBeforeShrink}s  shrink={p.shrinkDuration}s  dmg={p.damagePerSecond}/s" +
                          (p.isScoreBonusZone ? "  [BONUS ×" + p.zoneBonusMultiplier + "]" : ""))));
        }
#endif
    }
}
#endif
