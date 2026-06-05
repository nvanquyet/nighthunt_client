using UnityEngine;
using NightHunt.Gameplay.Spectator;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NightHunt.Gameplay.Zone
{
    /// <summary>
    /// 3D world-space visual for the PUBG-style safe zone ring.
    ///
    /// ── Prefab/Scene structure ──────────────────────────────────────────────
    ///
    ///   SafeZoneVisual  (this component)   ← root, stays at ORIGIN — does NOT move
    ///     ├─ ZoneWall   (child GO)         ← world-position moves to current zone center
    ///     │    MeshFilter  → Zone.FBX from Cool Battle Royale Zone package
    ///     │    MeshRenderer → CurrentSafeZone.mat  (package material)
    ///     │    Scale X/Z = radius*2  (world diameter)
    ///     │    Scale Y   = wallHeight*0.5  (world height — FIXED, only XZ shrinks)
    ///     │    └─ ZoneWallOutline  (child GO)
    ///     │         MeshFilter  → same Zone.FBX
    ///     │         MeshRenderer → CurrentSafeZone Outline.mat  (package material)
    ///     └─ NextZoneRing  (child GO)      ← world-position moves to next zone target center
    ///          MeshFilter  → Zone.FBX
    ///          MeshRenderer → NextSafeZone Outline.mat  (package material, outline only)
    ///          Matches package's NextSafeZoneVisualizer pattern (no main body, outline only).
    ///
    /// ── Right-click component → "Build Children" to auto-create the hierarchy.
    ///    Requires Cool Battle Royale Zone package to be imported.
    ///
    /// ── Outside-zone HUD overlay ───────────────────────────────────────────
    ///   Drag any screen-space GO (vignette Image, warning panel) into
    ///   _outsideZoneOverlay. Activates automatically when local player exits zone.
    /// </summary>
    public class SafeZoneWorldVisual : MonoBehaviour
    {
        // ── Zone wall ─────────────────────────────────────────────────────────
        [Header("Zone Wall")]
        [Tooltip("Child GO with Unity Cylinder mesh + transparent material.")]
        [SerializeField] private Transform    _zoneWall;
        [Tooltip("MeshRenderer on ZoneWall — used to tint when zone is actively shrinking.")]
        [SerializeField] private MeshRenderer _wallRenderer;
        [Tooltip("Fixed world-space height of the cylinder. Only XZ shrinks, Y never changes.")]
        [SerializeField] private float        _wallHeight = 80f;

        [Header("Zone Wall Tint")]
        [Tooltip("Material color when zone is idle / waiting.")]
        [SerializeField] private Color _normalColor   = new Color(0.1f, 0.5f, 1f, 0.25f);
        [Tooltip("Material color when zone is actively closing — warning pulse.")]
        [SerializeField] private Color _shrinkingColor = new Color(1f, 0.2f, 0.1f, 0.45f);

        // ── Next zone ring (package pattern: NextSafeZoneVisualizer) ──────────
        [Header("Next Zone Ring")]
        [Tooltip("Child GO: Zone.FBX outline showing where the zone will shrink TO. Shown once server computes next target.")]
        [SerializeField] private Transform    _nextZoneRing;
        [Tooltip("MeshRenderer on NextZoneRing — uses package NextSafeZone Outline.mat.")]
        [SerializeField] private MeshRenderer _nextZoneRenderer;

        // ── Outside-zone HUD ──────────────────────────────────────────────────
        [Header("Outside Zone HUD")]
        [Tooltip("Screen-space GO (vignette, damage warning) shown when local player is outside zone.")]
        [SerializeField] private GameObject _outsideZoneOverlay;
        [Tooltip("How often to check if local player is inside zone (seconds). 0 = every frame.")]
        [SerializeField] [Min(0f)] private float _checkInterval = 0.2f;

        // Visual-only lerp speed to fill the gap between network ticks (server sends ~30 Hz,
        // client renders 60 fps). Does NOT affect gameplay — shrink timing is SafeZonePhaseConfig.shrinkDuration.
        // Damage outside zone is SafeZonePhaseConfig.damagePerSecond (both are server config, not here).
        private const float VisualLerpSpeed = 8f;

        // ── Runtime state ─────────────────────────────────────────────────────
        private float   _currentRadius;   // visually rendered value (lerps toward target)
        private Vector3 _currentCenter;
        private float   _targetRadius;    // server-authoritative value
        private Vector3 _targetCenter;
        private bool    _initialized;     // true after first server update — prevents lerp from zero
        private bool    _outsideZone;
        private float   _nextCheckTime;

        // Next zone target (from package's NextSafeZoneVisualizer pattern)
        private float   _nextTargetRadius;   // 0 = not yet computed / cleared
        private Vector3 _nextTargetCenter;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity lifecycle

        private void Awake()
        {
            SetWallVisible(false);
        }

        private void OnEnable()
        {
            SafeZoneHUDProxy.OnRadiusChanged      += HandleRadiusChanged;
            SafeZoneHUDProxy.OnShrinkStateChanged += HandleShrinkStateChanged;
            SafeZoneHUDProxy.OnNextZoneChanged    += HandleNextZoneChanged;
            SafeZoneHUDProxy.OnZoneIndexChanged   += HandleZoneIndexChanged;
            SafeZoneManager.Instance?.ReplayCurrentHudState();
        }

        private void OnDisable()
        {
            SafeZoneHUDProxy.OnRadiusChanged      -= HandleRadiusChanged;
            SafeZoneHUDProxy.OnShrinkStateChanged -= HandleShrinkStateChanged;
            SafeZoneHUDProxy.OnNextZoneChanged    -= HandleNextZoneChanged;
            SafeZoneHUDProxy.OnZoneIndexChanged   -= HandleZoneIndexChanged;
            SetOutsideZoneOverlay(false);
        }

        private void Update()
        {
            // Smooth lerp toward server zone values every frame.
            // Correctly handles PUBG-style zone where next phase center is OFFSET from current —
            // cylinder moves XZ + shrinks simultaneously without snap.
            if (_initialized && (_currentRadius != _targetRadius || _currentCenter != _targetCenter))
            {
                float t = 1f - Mathf.Exp(-VisualLerpSpeed * Time.deltaTime); // framerate-independent
                _currentRadius = Mathf.Lerp(_currentRadius, _targetRadius, t);
                _currentCenter = Vector3.Lerp(_currentCenter, _targetCenter, t);
                // Snap when negligibly close to avoid floating-point perpetual micro-updates
                if (Mathf.Abs(_currentRadius - _targetRadius) < 0.01f)
                {
                    _currentRadius = _targetRadius;
                    _currentCenter = _targetCenter;
                }
                ApplyTransforms();
            }

            // Throttled check for outside-zone overlay
            if (_checkInterval > 0f && Time.unscaledTime < _nextCheckTime)
                return;
            _nextCheckTime = Time.unscaledTime + _checkInterval;
            CheckLocalPlayerInsideZone();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region SafeZoneHUDProxy callbacks

        private void HandleRadiusChanged(float radius, Vector3 center)
        {
            _targetRadius = radius;
            _targetCenter = center;
            if (!_initialized)
            {
                // Snap immediately on first receive — do NOT lerp from zero on match start
                _currentRadius = radius;
                _currentCenter = center;
                _initialized   = true;
                ApplyTransforms();
            }
            SetWallVisible(true);
        }

        private void HandleShrinkStateChanged(bool shrinking)
        {
            // Tint the wall material — requires the material to use _BaseColor property (URP Lit/Unlit)
            if (_wallRenderer != null)
                _wallRenderer.material.color = shrinking ? _shrinkingColor : _normalColor;
            // NextZoneRing stays visible during shrink — players need to see where to run.
            // Ring is hidden when zone index advances (HandleZoneIndexChanged) or final zone.
        }

        // Inspired by package's NextSafeZoneVisualizer: show where zone will shrink TO.
        // radius=0 means no next zone yet (server hasn't computed it).
        private void HandleNextZoneChanged(float radius, Vector3 center)
        {
            _nextTargetRadius = radius;
            _nextTargetCenter = center;
            bool hasNext = radius > 0.1f;
            SetNextZoneVisible(hasNext);
            if (hasNext) ApplyNextZoneTransforms();
        }

        // Zone advanced → next ring cleared (current zone IS the new zone, no pending target)
        private void HandleZoneIndexChanged(int index)
        {
            _nextTargetRadius = 0f;
            SetNextZoneVisible(false);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Transform

        private void ApplyTransforms()
        {
            if (_zoneWall == null) return;

            // Root stays at ORIGIN — only ZoneWall moves (world position, not parent).
            // This prevents NextZoneRing (sibling child) from being dragged by root movement.
            // Zone.FBX: scale.x/z = radius*2 (world diameter), scale.y = _wallHeight*0.5
            _zoneWall.position  = new Vector3(_currentCenter.x, _zoneWall.position.y, _currentCenter.z);
            _zoneWall.localScale = new Vector3(
                _currentRadius * 2f,
                _wallHeight * 0.5f,
                _currentRadius * 2f);
        }

        private void SetWallVisible(bool visible)
        {
            if (_zoneWall != null) _zoneWall.gameObject.SetActive(visible);
        }

        // Next zone ring is placed at its own world position (independent of root transform).
        // Inspired by package's NextSafeZoneVisualizer.UpdateZone() pattern.
        private void ApplyNextZoneTransforms()
        {
            if (_nextZoneRing == null) return;
            float y = _nextZoneRing.position.y;
            _nextZoneRing.position   = new Vector3(_nextTargetCenter.x, y, _nextTargetCenter.z);
            _nextZoneRing.localScale = new Vector3(
                _nextTargetRadius * 2f,
                _wallHeight * 0.5f,
                _nextTargetRadius * 2f);
        }

        private void SetNextZoneVisible(bool visible)
        {
            if (_nextZoneRing != null) _nextZoneRing.gameObject.SetActive(visible);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Outside zone check

        private void CheckLocalPlayerInsideZone()
        {
            var mgr = SafeZoneManager.Instance;
            if (mgr == null || !mgr.MatchActive)
            {
                SetOutsideZoneOverlay(false);
                return;
            }

            var local = SpectateManager.Instance?.GetLocalPlayer();
            if (local == null) return;

            bool outside = !mgr.IsInsideSafeZone(local.transform.position);
            if (outside != _outsideZone)
                SetOutsideZoneOverlay(outside);
        }

        private void SetOutsideZoneOverlay(bool outside)
        {
            _outsideZone = outside;
            if (_outsideZoneOverlay != null)
                _outsideZoneOverlay.SetActive(outside);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Editor — auto build hierarchy

#if UNITY_EDITOR
        /// <summary>
        /// Right-click this component in Inspector → "Build Children".
        /// Instantiates the Cool Battle Royale Zone package prefabs — no manual GO creation.
        /// Run once after adding this component to an empty GO.
        /// </summary>
        [ContextMenu("Build Children")]
        private void BuildChildren()
        {
            // ── Package prefab paths ──────────────────────────────────────────
            // CurrentSafeZoneVisualizer.prefab — "zone đang co lại" (current shrinking wall):
            //   root  ZoneWall                    Zone.FBX + CurrentSafeZone.mat       [enabled]
            //   child DangerZoneVisualizerOutline  Zone.FBX + CurrentSafeZone Outline.mat [enabled]
            //
            // NextSafeZoneVisualizer.prefab — "zone trắng" / target players must run to:
            //   root  NextZoneRing               Zone.FBX + CurrentSafeZone.mat        [DISABLED in prefab]
            //   child DangerZoneVisualizerOutline Zone.FBX + NextSafeZone Outline.mat  [enabled]
            //
            // Package event wiring (Zone.cs inspector):
            //   "Start Waiting For Shrinking" → BOTH CurrentSafeZoneVisualizer.UpdateZone
            //                                        AND NextSafeZoneVisualizer.UpdateZone
            //   "Area is Shrinking"           → CurrentSafeZoneVisualizer.UpdateZone only
            // We replicate: OnNextZoneChanged shows NextZoneRing; OnZoneIndexChanged hides it.
            const string k_PrefabCurrent = "Assets/Cool Battle Royale Zone/Demo/Prefabs/CurrentSafeZoneVisualizer.prefab";
            const string k_PrefabNext    = "Assets/Cool Battle Royale Zone/Demo/Prefabs/NextSafeZoneVisualizer.prefab";

            int zoneLayer = LayerMask.NameToLayer("Zone");

            // ── ZoneWall — CurrentSafeZoneVisualizer.prefab ───────────────────
            if (_zoneWall == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_PrefabCurrent);
                if (prefab == null)
                {
                    Debug.LogError($"[SafeZoneWorldVisual] Prefab not found: '{k_PrefabCurrent}'. Is 'Cool Battle Royale Zone' imported?");
                    return;
                }

                // Instantiate the real package prefab → correct Zone.FBX mesh + materials
                var inst = PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
                inst.name = "ZoneWall";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localScale    = Vector3.one;

                foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = zoneLayer;

                // Strip package's Zone-coupled script — SafeZoneHUDProxy drives us, Zone.Instance unused
                var pkgScript = inst.GetComponent("CurrentSafeZoneVisualizer");
                if (pkgScript != null) DestroyImmediate(pkgScript);

                // Strip CapsuleColliders — damage is server-side (SafeZoneManager), visual only here
                foreach (var col in inst.GetComponentsInChildren<CapsuleCollider>(true))
                    DestroyImmediate(col);

                _zoneWall     = inst.transform;
                _wallRenderer = inst.GetComponent<MeshRenderer>(); // root renderer = CurrentSafeZone.mat

                Debug.Log("[SafeZoneWorldVisual] ZoneWall: CurrentSafeZoneVisualizer.prefab instantiated.\n" +
                          "  root  → CurrentSafeZone.mat\n" +
                          "  child → CurrentSafeZone Outline.mat (DangerZoneVisualizerOutline)");
            }

            // ── NextZoneRing — NextSafeZoneVisualizer.prefab ("zone trắng") ───
            // Matches package pattern: shown during "Waiting For Shrinking" phase.
            // Outline only (DangerZoneVisualizerOutline child, NextSafeZone Outline.mat).
            // Placed as CHILD of SafeZoneWorldVisual — root stays at origin so world position
            // set in ApplyNextZoneTransforms() is never disturbed by parent movement.
            if (_nextZoneRing == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_PrefabNext);
                if (prefab == null)
                {
                    Debug.LogError($"[SafeZoneWorldVisual] Prefab not found: '{k_PrefabNext}'. Is 'Cool Battle Royale Zone' imported?");
                    return;
                }

                // Instantiate as child of this GO (root stays at origin, so world pos = local pos)
                var inst = PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
                inst.name = "NextZoneRing";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localScale    = Vector3.one;

                foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = zoneLayer;

                var pkgScript = inst.GetComponent("NextSafeZoneVisualizer");
                if (pkgScript != null) DestroyImmediate(pkgScript);

                foreach (var col in inst.GetComponentsInChildren<CapsuleCollider>(true))
                    DestroyImmediate(col);

                _nextZoneRing     = inst.transform;
                _nextZoneRenderer = inst.GetComponent<MeshRenderer>();

                inst.SetActive(false); // hidden until SafeZoneHUDProxy.OnNextZoneChanged fires
                Debug.Log("[SafeZoneWorldVisual] NextZoneRing: NextSafeZoneVisualizer.prefab instantiated as CHILD.\n" +
                          "  Outline only → NextSafeZone Outline.mat (DangerZoneVisualizerOutline child).\n" +
                          "  Shown when server broadcasts next zone position (Waiting-For-Shrinking phase).");
            }

            EditorUtility.SetDirty(this);
            Debug.Log("[SafeZoneWorldVisual] Build complete. Wire _outsideZoneOverlay in Inspector.");
        }

        // ── Debug / test ──────────────────────────────────────────────────────
        // Right-click component in Inspector to simulate zone states without a server.
        // ZoneWall is VISUAL ONLY (no collider) — players walk through it freely.
        // Actual outside-zone damage is server-side (SafeZoneManager tick).
        // _outsideZoneOverlay must be a screen-space Canvas GO (Image vignette, etc.)

        [ContextMenu("Debug: Zone R=100  center=(0,0,0)")]
        private void DbgZone100() => DbgSimulateZone(100f, Vector3.zero);

        [ContextMenu("Debug: Shrink → R=50  center=(20,0,15)  [offset center]")]
        private void DbgZone50()  => DbgSimulateZone(50f,  new Vector3(20f, 0f, 15f));

        [ContextMenu("Debug: Final  → R=20  center=(35,0,25)")]
        private void DbgZone20()  => DbgSimulateZone(20f,  new Vector3(35f, 0f, 25f));

        private void DbgSimulateZone(float radius, Vector3 center)
        {
            HandleRadiusChanged(radius, center);
            Debug.Log($"[SafeZoneWorldVisual] DEBUG zone: radius={radius}  center={center}  " +
                      $"wall={(_zoneWall != null ? "OK" : "NULL")}  " +
                      $"overlay={(_outsideZoneOverlay != null ? _outsideZoneOverlay.name : "NULL")}  " +
                      $"ZoneWall world pos={(_zoneWall != null ? _zoneWall.position.ToString() : "—")}");
        }

        [ContextMenu("Debug: Next Zone R=50  center=(20,0,15)")]
        private void DbgNextZone50() => DbgSimulateNextZone(50f, new Vector3(20f, 0f, 15f));

        [ContextMenu("Debug: Next Zone R=20  center=(35,0,25)")]
        private void DbgNextZone20() => DbgSimulateNextZone(20f, new Vector3(35f, 0f, 25f));

        [ContextMenu("Debug: Clear Next Zone")]
        private void DbgClearNextZone() => HandleNextZoneChanged(0f, Vector3.zero);

        private void DbgSimulateNextZone(float radius, Vector3 center)
        {
            HandleNextZoneChanged(radius, center);
            string ringStatus = _nextZoneRing != null ? "OK" : "NULL — run Build Children first";
            Debug.Log($"[SafeZoneWorldVisual] DEBUG next zone: radius={radius}  center={center}  nextRing={ringStatus}");
        }

        [ContextMenu("Debug: Toggle Outside-Zone Overlay")]
        private void DbgToggleOverlay()
        {
            SetOutsideZoneOverlay(!_outsideZone);
            Debug.Log($"[SafeZoneWorldVisual] DEBUG overlay forced {(_outsideZone ? "ON" : "OFF")}  " +
                      $"GO={(  _outsideZoneOverlay != null ? _outsideZoneOverlay.name : "NOT ASSIGNED — drag a Canvas GO here")}");
        }

        // Scene-view gizmos: cyan = current visual zone, orange = lerp target, purple = next zone target
        private void OnDrawGizmos()
        {
            if (_currentRadius > 0f)
            {
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
                DrawGizmoCircle(new Vector3(_currentCenter.x, transform.position.y, _currentCenter.z), _currentRadius);
            }
            if (_targetRadius > 0f && (_targetRadius != _currentRadius || _targetCenter != _currentCenter))
            {
                Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.5f);
                DrawGizmoCircle(new Vector3(_targetCenter.x, transform.position.y, _targetCenter.z), _targetRadius);
            }
            // Next zone target (package's NextSafeZoneVisualizer equivalent)
            if (_nextTargetRadius > 0.1f)
            {
                Gizmos.color = new Color(1f, 0.85f, 0f, 0.6f);  // yellow = "move here"
                float y = _nextZoneRing != null ? _nextZoneRing.position.y : transform.position.y;
                DrawGizmoCircle(new Vector3(_nextTargetCenter.x, y, _nextTargetCenter.z), _nextTargetRadius);
            }
        }

        private static void DrawGizmoCircle(Vector3 center, float radius, int segments = 64)
        {
            float step = 2f * Mathf.PI / segments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * step;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

#endif

        #endregion
    }
}

