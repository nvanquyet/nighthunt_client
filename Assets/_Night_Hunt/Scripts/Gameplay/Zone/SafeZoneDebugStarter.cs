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

        [Header("Gizmos (Editor Only)")]
        [Tooltip("Toggle gizmo drawing on/off. Right-click → 'Draw: Toggle Zone Gizmos'.")]
        [SerializeField] private bool _drawGizmos = true;

        [Tooltip("Show labels for each phase ring (radius value, phase index).")]
        [SerializeField] private bool _drawLabels = true;

        [Tooltip("Opacity of the filled circle overlay (0 = wire only, 1 = fully opaque).")]
        [SerializeField] [Range(0f, 1f)] private float _gizmoFillAlpha = 0.08f;

        // Color palette: one color per phase, auto-cycled if phases > palette size
        private static readonly Color[] PhaseColors = new Color[]
        {
            new Color(0.2f, 1f, 0.4f),   // phase 0 — green  (start radius)
            new Color(1f, 0.85f, 0.1f),  // phase 1 — yellow
            new Color(1f, 0.5f, 0.1f),   // phase 2 — orange
            new Color(1f, 0.2f, 0.2f),   // phase 3 — red
            new Color(0.4f, 0.6f, 1f),   // phase 4 — blue
            new Color(0.9f, 0.3f, 1f),   // phase 5 — purple
        };

        private void OnDrawGizmos()
        {
            if (!_drawGizmos) return;
            if (_config == null || _config.phases == null || _config.phases.Count == 0) return;

            // Center point: use this GameObject's position as the zone center origin
            // (matches how SafeZoneManager resolves centerMode == Fixed / this transform)
            Vector3 center = transform.position;

            // ── Draw initialRadius (the very first "safe" circle players start inside) ──
            DrawCircle(center, _config.initialRadius, Color.cyan, _gizmoFillAlpha, "Initial\nr=" + _config.initialRadius.ToString("F1"));

            // ── Draw each phase's endRadius ──
            for (int i = 0; i < _config.phases.Count; i++)
            {
                var phase = _config.phases[i];
                Color col = PhaseColors[i % PhaseColors.Length];

                DrawCircle(center, phase.endRadius, col, _gizmoFillAlpha,
                    _drawLabels ? $"Phase {i}\nr={phase.endRadius:F1}" : null);
            }

            // ── Highlight: make sure initialRadius ≥ phase[0].endRadius connection obvious ──
            if (_config.phases.Count > 0)
            {
                float p0End = _config.phases[0].endRadius;
                bool mismatch = _config.initialRadius < p0End;

                // Draw a bright dashed-style thick wire between the two radii to spot gaps
                Color warnCol = mismatch ? new Color(1f, 0.2f, 0.2f, 0.9f) : new Color(0.2f, 1f, 0.4f, 0.6f);
                DrawRadiusBand(center, p0End, _config.initialRadius, warnCol);

                if (_drawLabels && mismatch)
                {
                    UnityEditor.Handles.color = Color.red;
                    UnityEditor.Handles.Label(
                        center + Vector3.right * _config.initialRadius + Vector3.up * 0.5f,
                        "⚠ initialRadius < phase[0].endRadius!\nPlayers start OUTSIDE safe zone.");
                }
            }
        }

        /// <summary>Draws a wire circle + translucent filled disc on the XZ plane.</summary>
        private void DrawCircle(Vector3 center, float radius, Color col, float fillAlpha, string label)
        {
            if (radius <= 0f) return;

            // Wire ring
            UnityEditor.Handles.color = col;
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);

            // Filled disc
            Color fill = col;
            fill.a = fillAlpha;
            UnityEditor.Handles.color = fill;
            UnityEditor.Handles.DrawSolidDisc(center, Vector3.up, radius);

            // Label at right edge
            if (_drawLabels && !string.IsNullOrEmpty(label))
            {
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(center + Vector3.right * radius + Vector3.up * 0.3f, label);
            }
        }

        /// <summary>Draws a coloured band (annulus) between two radii to visualise the gap/overlap.</summary>
        private void DrawRadiusBand(Vector3 center, float innerR, float outerR, Color col)
        {
            if (innerR <= 0f || outerR <= innerR) return;

            int segments = 64;
            float angleStep = 360f / segments;
            Color bandCol = col;
            bandCol.a = 0.15f;
            UnityEditor.Handles.color = bandCol;

            // Fill the band with quads
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Deg2Rad * (i * angleStep);
                float a1 = Mathf.Deg2Rad * ((i + 1) * angleStep);
                Vector3 i0 = center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * innerR;
                Vector3 i1 = center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * innerR;
                Vector3 o0 = center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * outerR;
                Vector3 o1 = center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * outerR;
                UnityEditor.Handles.DrawAAConvexPolygon(i0, i1, o1, o0);
            }
        }

        [ContextMenu("Draw: Toggle Zone Gizmos")]
        private void DbgToggleGizmos()
        {
            UnityEditor.Undo.RecordObject(this, "Toggle Zone Gizmos");
            _drawGizmos = !_drawGizmos;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[SafeZoneDebugStarter] Gizmos → {(_drawGizmos ? "ON" : "OFF")}");
        }

        [ContextMenu("Draw: Toggle Labels")]
        private void DbgToggleLabels()
        {
            UnityEditor.Undo.RecordObject(this, "Toggle Zone Labels");
            _drawLabels = !_drawLabels;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[SafeZoneDebugStarter] Labels → {(_drawLabels ? "ON" : "OFF")}");
        }

#endif
    }
}
#endif