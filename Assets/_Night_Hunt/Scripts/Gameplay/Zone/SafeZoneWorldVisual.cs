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
    ///   SafeZoneVisual  (this component)   ← root, auto-moves to zone center
    ///     ├─ ZoneWall   (child GO)
    ///     │    MeshFilter + MeshRenderer  — Unity Cylinder primitive
    ///     │    Material: URP Transparent, Cull Off, semi-transparent blue
    ///     │    Scale X/Z = radius*2  (world diameter)
    ///     │    Scale Y   = wallHeight*0.5  (world height — FIXED, only XZ shrinks)
    ///     └─ MinimapRing  (child GO)
    ///          SpriteRenderer — ring/donut sprite (white or zone color)
    ///          localRotation = Euler(90,0,0)  — face up for ortho top-down camera
    ///          Layer = "Minimap"
    ///          Scale X/Y = radius*2  (localY maps to world Z after 90° rotation)
    ///
    /// ── Right-click component → "Build Children" to auto-create the hierarchy.
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

        // ── Minimap ring ──────────────────────────────────────────────────────
        [Header("Minimap Ring")]
        [Tooltip("Child GO: SpriteRenderer, layer = 'Minimap', localRotation = Euler(90,0,0).")]
        [SerializeField] private Transform _minimapRing;
        [Tooltip("Radius of the ring sprite in world units at scale (1,1,1) — same as RangeIndicator._baseRadius.\n" +
                 "Determines: scale = currentRadius / baseRadius.\n" +
                 "256px sprite @ PPU=256 → baseRadius=0.5  (diameter=1 world unit at scale 1)\n" +
                 "256px sprite @ PPU=128 → baseRadius=1.0\n" +
                 "Match this to whatever sprite you assign in the Inspector.")]
        [SerializeField] [Min(0.01f)] private float _minimapRingBaseRadius = 0.5f;

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

        // ─────────────────────────────────────────────────────────────────────
        #region Unity lifecycle

        private void Awake()
        {
            if (_minimapRing != null && _minimapRing.gameObject.layer != LayerMask.NameToLayer("Minimap"))
                Debug.LogWarning($"[SafeZoneWorldVisual] MinimapRing '{_minimapRing.name}' is NOT on layer 'Minimap' — won't show on minimap camera.");

            SetWallVisible(false);
        }

        private void OnEnable()
        {
            SafeZoneHUDProxy.OnRadiusChanged      += HandleRadiusChanged;
            SafeZoneHUDProxy.OnShrinkStateChanged += HandleShrinkStateChanged;
        }

        private void OnDisable()
        {
            SafeZoneHUDProxy.OnRadiusChanged      -= HandleRadiusChanged;
            SafeZoneHUDProxy.OnShrinkStateChanged -= HandleShrinkStateChanged;
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
            if (_wallRenderer == null) return;

            // Tint the wall material — requires the material to use _BaseColor property (URP Lit/Unlit)
            _wallRenderer.material.color = shrinking ? _shrinkingColor : _normalColor;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Transform

        private void ApplyTransforms()
        {
            // Move root to new zone center (XZ only — Y stays at scene ground level)
            transform.position = new Vector3(_currentCenter.x, transform.position.y, _currentCenter.z);

            // ── Zone wall (Unity Cylinder) ───────────────────────────────────
            // Cylinder at scale(1,1,1): diameter = 1m on XZ, height = 2m on Y
            //   scale.x/z = radius * 2   → world diameter = radius*2
            //   scale.y   = height * 0.5 → world height   = _wallHeight  (fixed)
            if (_zoneWall != null)
                _zoneWall.localScale = new Vector3(
                    _currentRadius * 2f,
                    _wallHeight * 0.5f,
                    _currentRadius * 2f);

            // ── Minimap ring (SpriteRenderer rotated 90° to face up) ─────────
            // Same pattern as RangeIndicator: scale = range / baseRadius
            // baseRadius = world-space radius of the sprite at scale(1,1,1)
            // localRotation = Euler(90,0,0) → sprite local XY maps to world XZ
            if (_minimapRing != null)
            {
                float s = _currentRadius / _minimapRingBaseRadius;
                _minimapRing.localScale = new Vector3(s, s, 1f);
            }
        }

        private void SetWallVisible(bool visible)
        {
            if (_zoneWall != null)    _zoneWall.gameObject.SetActive(visible);
            if (_minimapRing != null) _minimapRing.gameObject.SetActive(visible);
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
        /// Right-click this component in Inspector → "Build Children"
        /// Creates ZoneWall (Cylinder) and MinimapRing (SpriteRenderer) as children.
        /// Run once after adding this component to an empty GO.
        /// </summary>
        [ContextMenu("Build Children")]
        private void BuildChildren()
        {
            // ── ZoneWall ─────────────────────────────────────────────────────
            if (_zoneWall == null)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wall.name = "ZoneWall";
                wall.transform.SetParent(transform, false);
                wall.transform.localPosition = Vector3.zero;
                // Cylinder primitive adds CapsuleCollider — not needed for visual only
                DestroyImmediate(wall.GetComponent<CapsuleCollider>());
                // Layer "Zone" (11) is NOT in the minimap camera mask — cylinder visible in game view only
                wall.layer   = LayerMask.NameToLayer("Zone");

                _zoneWall    = wall.transform;
                _wallRenderer = wall.GetComponent<MeshRenderer>();

                // Create a basic URP transparent material
                _wallRenderer.sharedMaterial = CreateZoneWallMaterial();
                Debug.Log("[SafeZoneWorldVisual] ZoneWall created. Assign or tweak zone_wall.mat in Assets.");
            }

            // ── MinimapRing ───────────────────────────────────────────────────
            if (_minimapRing == null)
            {
                GameObject ring = new GameObject("MinimapRing");
                ring.transform.SetParent(transform, false);
                ring.transform.localPosition = Vector3.zero;
                // Rotate 90° so sprite face (local Z+) points up → visible by top-down ortho camera
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.layer = LayerMask.NameToLayer("Minimap");

                var sr = ring.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                // Assign any ring/donut sprite from Inspector — no sprite generated here.
                // Set _minimapRingBaseRadius to match your sprite's radius at scale(1,1,1).

                _minimapRing = ring.transform;
                Debug.Log("[SafeZoneWorldVisual] MinimapRing created on 'Minimap' layer. Assign a ring/donut sprite in Inspector.");
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
                      $"ring={(_minimapRing != null ? "OK" : "NULL")}  " +
                      $"overlay={(_outsideZoneOverlay != null ? _outsideZoneOverlay.name : "NULL")}");
        }

        [ContextMenu("Debug: Toggle Outside-Zone Overlay")]
        private void DbgToggleOverlay()
        {
            SetOutsideZoneOverlay(!_outsideZone);
            Debug.Log($"[SafeZoneWorldVisual] DEBUG overlay forced {(_outsideZone ? "ON" : "OFF")}  " +
                      $"GO={(  _outsideZoneOverlay != null ? _outsideZoneOverlay.name : "NOT ASSIGNED — drag a Canvas GO here")}");
        }

        // Scene-view gizmos: cyan = current visual zone, orange = target (server) zone while lerping
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

        private Material CreateZoneWallMaterial()
        {
            // Try URP Unlit (simpler, works without lighting)
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.name = "zone_wall";

            // URP Unlit transparent setup
            mat.SetFloat("_Surface", 1);          // 0=Opaque, 1=Transparent
            mat.SetFloat("_Blend", 0);            // Alpha blend
            mat.SetFloat("_Cull", 0);             // Cull Off — visible inside AND outside
            mat.SetFloat("_ZWrite", 0);
            mat.color = new Color(0.1f, 0.5f, 1f, 0.25f);

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Save to Assets folder
            string path = "Assets/_Night_Hunt/Materials/zone_wall.mat";
            System.IO.Directory.CreateDirectory("Assets/_Night_Hunt/Materials");
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SafeZoneWorldVisual] Material saved to {path}");
            return mat;
        }

#endif

        #endregion
    }
}

