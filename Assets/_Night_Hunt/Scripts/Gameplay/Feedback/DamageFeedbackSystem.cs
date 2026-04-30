using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cam = UnityEngine.Camera;
using NightHunt.Gameplay.ClientEffects;

namespace NightHunt.Gameplay.Feedback
{
    /// <summary>
    /// Manages pooled floating damage numbers and directional hit indicators.
    ///
    /// POOLING STRATEGY:
    ///   Typed Stack&lt;T&gt; pools per prefab type — instances stay under this transform
    ///   so they remain inside the Canvas hierarchy (UI elements must be Canvas children).
    ///   Pool return is callback-based: Initialize(... onComplete: () => ReturnXxx(instance)).
    ///   Zero Instantiate/Destroy calls after pool warm-up.
    ///
    /// SETUP (in GameHUD canvas):
    ///   1. Place this component on a child of the HUD Canvas (e.g. [DamageFeedback] GO).
    ///   2. Assign damageNumberPrefab (must have DamageNumber + TextMeshProUGUI).
    ///   3. Assign hitIndicatorPrefab (must have HitIndicator + Image).
    ///   4. Wire this component to GameHUD.damageFeedback in the Inspector.
    ///
    /// CONNECTION:
    ///   GameHUD.HandleAnyHitReceived → ShowDamageNumber / ShowHitIndicator
    ///   (GameHUD filters by localShooterNetObjId before calling here)
    /// </summary>
    [DisallowMultipleComponent]
    public class DamageFeedbackSystem : MonoBehaviour
    {
        [Header("Damage Number Settings")]
        [Tooltip("Assign DamageNumber_Template (NightHunt/Tools/Build Template Prefabs). " +
                 "Root must have a DamageNumber script; child 'Text' must have TextMeshProUGUI. " +
                 "This prefab MUST be a child of a Canvas — DamageFeedbackSystem instantiates it " +
                 "under its own transform which sits inside the HUD Canvas.")]
        [SerializeField] private GameObject damageNumberPrefab;
        [SerializeField] private float      numberLifetime        = 2f;
        [SerializeField] private float      numberSpeed           = 2f;
        [SerializeField] private Color      normalDamageColor     = Color.white;
        [SerializeField] private Color      criticalDamageColor   = Color.yellow;
        [SerializeField] private Color      headshotColor         = Color.red;

        [Header("Hit Indicator Settings")]
        [Tooltip("Assign HitIndicator_Template (NightHunt/Tools/Build Template Prefabs). " +
                 "Root must have a HitIndicator script + Image component. " +
                 "Open the template in prefab edit mode and assign a directional arrow/wedge sprite to the Image. " +
                 "Pivot must be at the center (0.5, 0.5) and the sprite must point UP by default.")]
        [SerializeField] private GameObject hitIndicatorPrefab;
        [SerializeField] private float      indicatorLifetime     = 0.5f;

        [Header("Hit Sparks")]
        [Tooltip("World-space particle prefab spawned at the impact point when the local player lands a hit. " +
                 "Must be a SimpleEffectPool-compatible prefab (ParticleSystem, loops=false).")]
        [SerializeField] private GameObject _hitSparksPrefab;
        [SerializeField] private float      _hitSparksLifetime   = 1f;

        [Header("Heal Burst")]
        [Tooltip("World-space particle prefab spawned at the player's position on heal.")]
        [SerializeField] private GameObject _healBurstPrefab;
        [SerializeField] private float      _healBurstLifetime   = 1.5f;

        [Header("Pool Warm-up")]
        [Tooltip("Instances of DamageNumber pre-spawned at Start to eliminate first-shot allocation.")]
        [SerializeField] private int _initialDamageNumberPoolSize = 8;
        [Tooltip("Instances of HitIndicator pre-spawned at Start.")]
        [SerializeField] private int _initialHitIndicatorPoolSize = 4;

        // Typed pools — instances stay under this Canvas GO.
        private readonly Stack<DamageNumber>  _numberPool    = new Stack<DamageNumber>(8);
        private readonly Stack<HitIndicator>  _indicatorPool = new Stack<HitIndicator>(4);

        private Cam _playerCamera;

        // -----------------------------------------------------------------
        // Unity lifecycle
        // -----------------------------------------------------------------

        private void Start()
        {
#if UNITY_2023_2_OR_NEWER
            _playerCamera = Cam.main ?? FindFirstObjectByType<Cam>();
#else
            _playerCamera = Cam.main ?? FindObjectOfType<Cam>();
#endif
            WarmPools();
        }

        private void WarmPools()
        {
            if (damageNumberPrefab != null)
                for (int i = 0; i < _initialDamageNumberPoolSize; i++)
                {
                    var n = Instantiate(damageNumberPrefab, transform).GetComponent<DamageNumber>();
                    if (n == null) break;
                    n.gameObject.SetActive(false);
                    _numberPool.Push(n);
                }

            if (hitIndicatorPrefab != null)
                for (int i = 0; i < _initialHitIndicatorPoolSize; i++)
                {
                    var h = Instantiate(hitIndicatorPrefab, transform).GetComponent<HitIndicator>();
                    if (h == null) break;
                    h.gameObject.SetActive(false);
                    _indicatorPool.Push(h);
                }
        }

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Display a floating damage number at a world position.
        /// Call this only on the local client (GameHUD already filters by shooter ID).
        /// </summary>
        public void ShowDamageNumber(Vector3 worldPosition, float damage,
                                     bool isHeadshot = false, bool isCritical = false)
        {
            if (damageNumberPrefab == null || _playerCamera == null) return;

            Vector3 screenPos = _playerCamera.WorldToScreenPoint(worldPosition);
            if (screenPos.z < 0f) return; // behind camera

            Color color = isHeadshot ? headshotColor
                        : isCritical ? criticalDamageColor
                        : normalDamageColor;

            DamageNumber number = RentNumber();
            number.Initialize(screenPos, damage, color, numberLifetime, numberSpeed,
                              onComplete: () => ReturnNumber(number));
        }

        /// <summary>
        /// Spawn a world-space hit-spark particle at the impact point.
        /// Call only on the local client (same filter as ShowDamageNumber).
        /// </summary>
        public void ShowHitEffect(Vector3 worldPos, Vector3 hitNormal)
        {
            if (_hitSparksPrefab == null) return;
            var rot = hitNormal.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(hitNormal)
                : Quaternion.identity;
            SpawnWorldEffect(_hitSparksPrefab, worldPos, rot, _hitSparksLifetime);
        }

        /// <summary>
        /// Spawn a world-space heal-burst particle at the given position.
        /// </summary>
        public void ShowHealEffect(Vector3 worldPos)
        {
            if (_healBurstPrefab == null) return;
            SpawnWorldEffect(_healBurstPrefab, worldPos, Quaternion.identity, _healBurstLifetime);
        }

        /// <summary>
        /// Show a directional hit indicator (the red arrow pointing toward incoming fire).
        /// hitDirection should be the incoming bullet direction (not the normal).
        /// </summary>
        public void ShowHitIndicator(Vector3 hitDirection)
        {
            if (hitIndicatorPrefab == null) return;

            HitIndicator indicator = RentIndicator();
            indicator.Initialize(hitDirection, indicatorLifetime,
                                 onComplete: () => ReturnIndicator(indicator));
        }

        // -----------------------------------------------------------------
        // Pool management
        // -----------------------------------------------------------------

        private void SpawnWorldEffect(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
        {
            var pool = SimpleEffectPool.Instance;
            if (pool != null)
            {
                pool.Play(prefab, position, rotation, lifetime);
                return;
            }
            // Fallback: SimpleEffectPool not placed in scene yet.
            Destroy(Instantiate(prefab, position, rotation), lifetime);
        }

        private DamageNumber RentNumber()
        {
            DamageNumber n = _numberPool.Count > 0
                ? _numberPool.Pop()
                : Instantiate(damageNumberPrefab, transform).GetComponent<DamageNumber>();

            n.gameObject.SetActive(true);
            return n;
        }

        private void ReturnNumber(DamageNumber n)
        {
            if (n == null) return;
            n.gameObject.SetActive(false);
            _numberPool.Push(n);
        }

        private HitIndicator RentIndicator()
        {
            HitIndicator h = _indicatorPool.Count > 0
                ? _indicatorPool.Pop()
                : Instantiate(hitIndicatorPrefab, transform).GetComponent<HitIndicator>();

            h.gameObject.SetActive(true);
            return h;
        }

        private void ReturnIndicator(HitIndicator h)
        {
            if (h == null) return;
            h.gameObject.SetActive(false);
            _indicatorPool.Push(h);
        }

#if UNITY_EDITOR
        // ── Editor — Default Config ───────────────────────────────────────────

        [ContextMenu("NightHunt/Tools/Build Template Prefabs")]
        private void Editor_BuildTemplatePrefabs()
        {
            const string uiFolder      = "Assets/_Night_Hunt/Prefabs/UI";
            const string damageNumPath = uiFolder + "/DamageNumber_Template.prefab";
            const string hitIndPath    = uiFolder + "/HitIndicator_Template.prefab";

            if (!UnityEditor.AssetDatabase.IsValidFolder(uiFolder))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_Night_Hunt/Prefabs", "UI");

            // ── DamageNumber_Template ──────────────────────────────────────
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(damageNumPath) == null)
            {
                var root = new GameObject("DamageNumber_Template");
                root.AddComponent<RectTransform>();
                root.AddComponent<DamageNumber>();

                var child = new GameObject("Text");
                child.transform.SetParent(root.transform, false);
                child.AddComponent<RectTransform>();
                child.AddComponent<TMPro.TextMeshProUGUI>().text = "0";

                UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, damageNumPath);
                DestroyImmediate(root);
                Debug.Log($"[DamageFeedbackSystem] Created prefab: {damageNumPath}");
            }
            else
            {
                Debug.Log($"[DamageFeedbackSystem] DamageNumber_Template already exists at {damageNumPath}");
            }

            // ── HitIndicator_Template ──────────────────────────────────────
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(hitIndPath) == null)
            {
                var root = new GameObject("HitIndicator_Template");
                root.AddComponent<RectTransform>();
                root.AddComponent<Image>();
                root.AddComponent<HitIndicator>();

                UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, hitIndPath);
                DestroyImmediate(root);
                Debug.Log($"[DamageFeedbackSystem] Created prefab: {hitIndPath}");
            }
            else
            {
                Debug.Log($"[DamageFeedbackSystem] HitIndicator_Template already exists at {hitIndPath}");
            }

            // Auto-assign
            var dnPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(damageNumPath);
            var hiPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(hitIndPath);
            if (dnPrefab != null) damageNumberPrefab  = dnPrefab;
            if (hiPrefab != null) hitIndicatorPrefab  = hiPrefab;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log("[DamageFeedbackSystem] Template prefabs built and assigned. Assign sprites/materials in prefab edit mode.");
        }

        [ContextMenu("NightHunt/Log DamageFeedback Default Values")]
        private void Editor_LogDefaults()
        {
            Debug.Log(
                "[DamageFeedbackSystem] Current settings:\n" +
                $"  damageNumberPrefab  : {(damageNumberPrefab != null ? damageNumberPrefab.name : "NOT ASSIGNED — run 'NightHunt/Tools/Build Template Prefabs' context menu.")}\n" +
                $"  numberLifetime      : {numberLifetime}s  (default: 2s)\n" +
                $"  numberSpeed         : {numberSpeed}      (default: 2 — world units/s upward drift)\n" +
                $"  normalDamageColor   : {normalDamageColor}  (default: white)\n" +
                $"  criticalDamageColor : {criticalDamageColor}  (default: yellow)\n" +
                $"  headshotColor       : {headshotColor}  (default: red)\n" +
                $"  hitIndicatorPrefab  : {(hitIndicatorPrefab != null ? hitIndicatorPrefab.name : "NOT ASSIGNED — run 'NightHunt/Tools/Build Template Prefabs' context menu.")}\n" +
                $"  indicatorLifetime   : {indicatorLifetime}s  (default: 0.5s)"
            );
        }

        [ContextMenu("NightHunt/Reset DamageFeedback To Defaults")]
        private void Editor_ResetToDefaults()
        {
            numberLifetime      = 2f;
            numberSpeed         = 2f;
            normalDamageColor   = Color.white;
            criticalDamageColor = Color.yellow;
            headshotColor       = Color.red;
            indicatorLifetime   = 0.5f;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[DamageFeedbackSystem] Reset to default values. Save scene để áp dụng.");
        }
#endif
    }
}
