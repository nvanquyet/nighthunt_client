using System.Collections.Generic;
using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Dynamically spawns and manages PlayerStatUIView rows based on PlayerStatUIConfig.
    /// Only stats with ShowInUI = true are rendered.
    /// </summary>
    public class PlayerStatUIPanel : MonoBehaviour
    {
        [Header("Prefab")] [Tooltip("Row prefab – must have a PlayerStatUIView component")] [SerializeField]
        private GameObject _statRowPrefab;

        [Header("Layout")] [Tooltip("Container for spawned rows (VerticalLayoutGroup recommended)")] [SerializeField]
        private RectTransform _statContainer;

        [Header("Config")]
        [Tooltip("UI config for stat display. If null, assign manually before Initialize is called.")]
        [SerializeField]
        private PlayerStatUIConfig _statUIConfig;

        private UIDomainBridge _domainBridge;

        private Dictionary<PlayerStatType, PlayerStatUIView> _statViews =
            new Dictionary<PlayerStatType, PlayerStatUIView>();

        private bool _isInitialized = false;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void Initialize(UIDomainBridge bridge)
        {
            _domainBridge = bridge;

            if (_statUIConfig == null)
            {
                Debug.LogError("[PlayerStatUIPanel] PlayerStatUIConfig is not assigned!");
                return;
            }

            Debug.Log(
                $"[PlayerStatUIPanel] Initialize: bridge.IsReady={bridge?.IsReady}, bridge.Bridge={bridge?.Bridge}");

            BuildStatUI();
            SubscribeBridgeEvents(true);

            // Refresh all stats after subscribing to ensure we have current values
            RefreshAllStats();

            _isInitialized = true;

            Debug.Log($"[PlayerStatUIPanel] Initialize complete: {_statViews.Count} stat views created");
        }

        /// <summary>
        /// Call when switching to a different player (e.g. spectate mode).
        /// </summary>
        public void RefreshForNewPlayer(UIDomainBridge bridge)
        {
            // Unsubscribe from previous bridge (nếu có)
            if (_domainBridge != null)
            {
                _domainBridge.OnStatChanged -= HandleStatChanged;
                Debug.Log(
                    $"[PlayerStatUIPanel] RefreshForNewPlayer: Unsubscribed from old UIDomainBridge #{_domainBridge.GetHashCode()}");
            }

            // Gán bridge mới
            _domainBridge = bridge;

            // Nếu panel chưa từng Initialize trước đó → dùng lại flow Initialize
            if (!_isInitialized)
            {
                Debug.Log(
                    "[PlayerStatUIPanel] RefreshForNewPlayer: Panel not initialized yet, calling Initialize with new bridge.");
                Initialize(bridge);
                return;
            }

            // Rebuild UI cho player mới
            BuildStatUI();

            // Đăng ký lại event với bridge mới
            _domainBridge.OnStatChanged += HandleStatChanged;
            Debug.Log(
                $"[PlayerStatUIPanel] RefreshForNewPlayer: Subscribed to OnStatChanged on UIDomainBridge #{_domainBridge.GetHashCode()}");

            // Refresh snapshot hiện tại vào UI
            RefreshAllStats();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            SubscribeBridgeEvents(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Build
        // ─────────────────────────────────────────────────────────────────────

        private void BuildStatUI()
        {
            if (_statRowPrefab == null || _statContainer == null || _statUIConfig == null)
            {
                Debug.LogError("[PlayerStatUIPanel] Missing prefab, container, or UI config!");
                return;
            }

            ClearStatViews();

            foreach (var uiDef in _statUIConfig.Stats)
            {
                if (uiDef.ShowInUI)
                {
                    SpawnStatRow(uiDef);
                }
            }
        }

        private void SpawnStatRow(PlayerStatUIDefinition uiDef)
        {
            var rowGO = Instantiate(_statRowPrefab, _statContainer, false);
            var statView = ComponentResolver.Find<PlayerStatUIView>(rowGO)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] PlayerStatUIView not found")
                .Resolve();

            if (statView == null)
            {
                Debug.LogError("[PlayerStatUIPanel] Stat row prefab is missing PlayerStatUIView component!");
                Destroy(rowGO);
                return;
            }

            statView.Initialize(uiDef.Type, uiDef, _domainBridge);
            _statViews[uiDef.Type] = statView;
            rowGO.SetActive(true); // Activate after initialization to avoid showing uninitialized values
        }

        private void ClearStatViews()
        {
            foreach (var kvp in _statViews)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }

            _statViews.Clear();
        }

        /// <summary>
        /// Refresh all stat views with current values from bridge
        /// </summary>
        private void RefreshAllStats()
        {
            if (_domainBridge == null || !_domainBridge.IsReady || _domainBridge.Bridge == null)
            {
                Debug.LogWarning("[PlayerStatUIPanel] RefreshAllStats: bridge not ready");
                return;
            }

            Debug.Log($"[PlayerStatUIPanel] RefreshAllStats: refreshing {_statViews.Count} stat views");

            foreach (var kvp in _statViews)
            {
                var statType = kvp.Key;
                var view = kvp.Value;

                if (view != null)
                {
                    // Get current value from bridge and update view
                    float currentValue = _domainBridge.Bridge.GetStat(statType);
                    view.UpdateValue(currentValue);

                    if (statType == PlayerStatType.CurrentWeight || statType == PlayerStatType.WeightCapacity)
                    {
                        Debug.Log($"[PlayerStatUIPanel] RefreshAllStats: {statType} = {currentValue:F1}");
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────────────────────────────

        private void SubscribeBridgeEvents(bool subscribe)
        {
            if (_domainBridge == null)
            {
                Debug.LogWarning("[PlayerStatUIPanel] SubscribeBridgeEvents: _domainBridge is null!");
                return;
            }

            if (subscribe)
            {
                _domainBridge.OnStatChanged += HandleStatChanged;
                Debug.Log(
                    $"[PlayerStatUIPanel] Subscribed to OnStatChanged on UIDomainBridge #{_domainBridge.GetHashCode()}");
            }
            else
            {
                _domainBridge.OnStatChanged -= HandleStatChanged;
                Debug.Log(
                    $"[PlayerStatUIPanel] Unsubscribed from OnStatChanged on UIDomainBridge #{_domainBridge.GetHashCode()}");
            }
        }

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            // Debug log để xác nhận event được fire
            if (type == PlayerStatType.CurrentWeight || type == PlayerStatType.WeightCapacity)
            {
                Debug.Log($"[PlayerStatUIPanel] HandleStatChanged: {type} {oldValue:F1} -> {newValue:F1}");
            }

            // Update the row that directly owns this stat type
            if (_statViews.TryGetValue(type, out var view))
            {
                view.UpdateValue(newValue);
            }
            else
            {
                // Only warn for stats that are expected to have a view (ShowInUI = true).
                // Stats like VisionRange (ShowInUI = false) are broadcast but intentionally not rendered.
                if (_statUIConfig != null)
                {
                    foreach (var def in _statUIConfig.Stats)
                    {
                        if (def.Type == type && def.ShowInUI)
                        {
                            Debug.LogWarning(
                                $"[PlayerStatUIPanel] No view found for stat type: {type} (old={oldValue:F1}, new={newValue:F1})");
                            break;
                        }
                    }
                }
            }

            // If this type is used as a RelatedMaxStatType by another visible row,
            // that row also needs a refresh (its slider fill ratio changed).
            if (_statUIConfig != null)
            {
                foreach (var uiDef in _statUIConfig.Stats)
                {
                    if (!uiDef.ShowInUI) continue;
                    if (uiDef.RelatedMaxStatType != type) continue;
                    if (uiDef.Type == type) continue; // Avoid re-triggering the same stat

                    if (_statViews.TryGetValue(uiDef.Type, out var relatedView))
                    {
                        relatedView.UpdateValue();
                    }
                }
            }
        }

#if UNITY_EDITOR
        // ── Editor — Context Menu: Auto-assign / Create Stat Row Prefab ───────

        [ContextMenu("NightHunt/Auto-Assign Stat Row Prefab")]
        private void Editor_AutoAssignStatRowPrefab()
        {
            if (_statRowPrefab != null) { Debug.Log("[PlayerStatUIPanel] _statRowPrefab already assigned."); return; }

            string[] candidates =
            {
                "Assets/_Night_Hunt/Prefabs/UI/StatPrefabs.prefab",
                "Assets/_Night_Hunt/Prefabs/UI/StatRow.prefab",
                "Assets/_Night_Hunt/Prefabs/UI/TooltipStatRow.prefab",
            };
            foreach (var p in candidates)
            {
                var found = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (found != null)
                {
                    _statRowPrefab = found;
                    UnityEditor.EditorUtility.SetDirty(this);
                    Debug.Log($"[PlayerStatUIPanel] Auto-assigned _statRowPrefab from {p}");
                    return;
                }
            }
            Debug.LogWarning("[PlayerStatUIPanel] Stat row prefab not found — use 'Create Stat Row Template Prefab'.");
        }

        [ContextMenu("NightHunt/Create Stat Row Template Prefab")]
        private void Editor_CreateStatRowPrefab()
        {
            const string dir  = "Assets/_Night_Hunt/Prefabs/UI";
            const string path = dir + "/StatRow_Template.prefab";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PlayerStatUIPanel] StatRow_Template already exists at {path}");
                return;
            }

            var go = new GameObject("StatRow_Template");
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(300f, 28f);
            var hlg = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childControlWidth  = true;
            hlg.childControlHeight = true;
            hlg.spacing            = 6f;

            var labelGo = new GameObject("StatLabel", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            labelGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 100f;
            labelGo.GetComponent<TMPro.TextMeshProUGUI>().text = "Health";

            var sliderGo = new GameObject("StatSlider", typeof(RectTransform), typeof(UnityEngine.UI.Slider));
            sliderGo.transform.SetParent(go.transform, false);
            sliderGo.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;

            var valGo = new GameObject("StatValue", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            valGo.transform.SetParent(go.transform, false);
            valGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 60f;
            valGo.GetComponent<TMPro.TextMeshProUGUI>().text = "100";

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            if (_statRowPrefab == null)
            {
                _statRowPrefab = saved;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            Debug.Log($"[PlayerStatUIPanel] Created StatRow_Template at {path}. " +
                      "Add PlayerStatUIView component and wire label/slider/value fields.");
        }
#endif
    }
}