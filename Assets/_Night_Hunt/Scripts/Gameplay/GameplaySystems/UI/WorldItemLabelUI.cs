using UnityEngine;
using TMPro;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Loot;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI
{
    /// <summary>
    /// World-space label shown above a dropped item.
    ///
    /// Attach this component to world item prefabs (or a WorldSpace Canvas child of them).
    /// The label hides automatically when the player is beyond <see cref="_visibleDistance"/>.
    ///
    /// Setup:
    ///   1. Create a child GameObject "Label" with a Canvas (World Space) + TMP on the
    ///      WorldItem prefab.  Set Canvas.sortingLayerName = "Interactables".
    ///   2. Add this component to the WorldItem prefab root.
    ///   3. Assign _labelRoot, _nameText, _actionHintText in the Inspector.
    /// </summary>
    [RequireComponent(typeof(WorldItem))]
    public class WorldItemLabelUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Label objects")]
        [SerializeField] private GameObject          _labelRoot;
        [SerializeField] private TextMeshPro         _nameText;       // world-space TMP
        [SerializeField] private TextMeshPro         _actionHintText; // "[F] Pick up" or "[E] Pick up"

        [Header("Visibility")]
        [Tooltip("Maximum camera distance at which the label is shown")]
        [SerializeField] private float _visibleDistance = 8f;
        [Tooltip("Label always faces the main camera (billboard)")]
        [SerializeField] private bool  _billboard = true;

        [Header("Offset")]
        [Tooltip("World-space offset above the item pivot")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0.6f, 0f);

        // ── Runtime ───────────────────────────────────────────────────────────

        private WorldItem _worldItem;
        private Camera    _mainCamera;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _worldItem  = ComponentResolver.Find<WorldItem>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] WorldItem not found")
        .Resolve();
            _mainCamera = Camera.main;

            RefreshLabel();

            if (_labelRoot != null)
                _labelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            RefreshLabel();
        }

        private void Update()
        {
            if (_labelRoot == null) return;

            // Distance check
            if (_mainCamera == null) _mainCamera = Camera.main;
            bool inRange = _mainCamera != null &&
                           Vector3.Distance(_mainCamera.transform.position,
                                             transform.position + _offset) <= _visibleDistance;

            _labelRoot.SetActive(inRange);

            if (!inRange) return;

            // Reposition
            _labelRoot.transform.position = transform.position + _offset;

            // Billboard
            if (_billboard && _mainCamera != null)
            {
                _labelRoot.transform.LookAt(
                    _labelRoot.transform.position + _mainCamera.transform.rotation * Vector3.forward,
                    _mainCamera.transform.rotation * Vector3.up);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void RefreshLabel()
        {
            if (_worldItem == null) return;

            string definitionID = _worldItem.ItemDefinitionID;
            var def = ItemDatabase.GetDefinition(definitionID);
            string displayName = def != null ? def.DisplayName : definitionID;
            int qty = _worldItem.Quantity;

            if (_nameText != null)
                _nameText.text = qty > 1 ? $"{displayName} ×{qty}" : displayName;

            if (_actionHintText != null)
                _actionHintText.text = "[F] Pick up";
        }
    }
}
