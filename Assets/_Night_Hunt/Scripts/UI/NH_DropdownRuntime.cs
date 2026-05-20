using System.Collections.Generic;
using System.Reflection;
using Michsky.MUIP;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Runtime guard for MUIP dropdowns used by Home and Party Custom Mode.
    /// Keeps scene/prefab templates intact while rebuilding values from server/config state.
    /// </summary>
    public static class NH_DropdownRuntime
    {
        private const float MinPanelHeight = 42f;
        private const float DefaultPanelHeight = 178f;
        private const float ItemHeight = 34f;

        public static void Populate(CustomDropdown dropdown, IReadOnlyList<string> names, int selectIndex, bool interactable = true)
        {
            if (dropdown == null || dropdown.gameObject == null)
                return;

            Prepare(dropdown);
            float openPanelSize = ResolveOpenPanelSize(dropdown, names != null ? names.Count : 0);
            Close(dropdown);
            dropdown.items.Clear();

            if (names == null || names.Count == 0)
            {
                if (dropdown.selectedText != null)
                    dropdown.selectedText.text = "No options";

                dropdown.Interactable(false);
                return;
            }

            for (int i = 0; i < names.Count; i++)
                dropdown.CreateNewItem(string.IsNullOrWhiteSpace(names[i]) ? "Option" : names[i], notify: false);

            int safeIndex = Mathf.Clamp(selectIndex, 0, dropdown.items.Count - 1);
            dropdown.selectedItemIndex = safeIndex;
            dropdown.index = safeIndex;
            dropdown.panelSize = openPanelSize;

            dropdown.SetupDropdown();
            Style(dropdown);
            ShiftUIBridge.SetDropdownIndexSilently(dropdown, safeIndex);
            Close(dropdown);
            dropdown.Interactable(interactable);
        }

        public static void NormalizeModeMapOrder(CustomDropdown modeDropdown, CustomDropdown mapDropdown)
        {
            if (modeDropdown == null || mapDropdown == null)
                return;

            var modeRect = modeDropdown.transform as RectTransform;
            var mapRect = mapDropdown.transform as RectTransform;
            if (modeRect == null || mapRect == null || modeRect.parent != mapRect.parent)
                return;

            // Keep Home and Party Custom Mode consistent: mode selector above map selector.
            if (modeRect.anchoredPosition.y >= mapRect.anchoredPosition.y)
                return;

            var modePos = modeRect.anchoredPosition;
            var mapPos = mapRect.anchoredPosition;
            modeRect.anchoredPosition = new Vector2(modePos.x, mapPos.y);
            mapRect.anchoredPosition = new Vector2(mapPos.x, modePos.y);
        }

        private static void Prepare(CustomDropdown dropdown)
        {
            var manager = dropdown.GetComponent<UIManagerDropdown>();
            if (manager != null)
                manager.overrideColors = true;

            dropdown.setHighPriority = true;
            dropdown.saveSelected = false;
            dropdown.invokeAtStart = false;
            dropdown.initAtStart = false;
            dropdown.updateOnEnable = false;
            dropdown.enableIcon = false;
            dropdown.enableScrollbar = true;
            dropdown.itemPaddingTop = 6;
            dropdown.itemPaddingBottom = 6;
            dropdown.itemPaddingLeft = 6;
            dropdown.itemPaddingRight = 6;
            dropdown.itemSpacing = 4;

            if (dropdown.itemObject != null && dropdown.itemObject.scene.IsValid())
                dropdown.itemObject.SetActive(false);

            if (dropdown.listCG != null)
            {
                dropdown.contentCG = dropdown.listCG;
                EnsureCanvas(dropdown.listCG.gameObject, 32000);
            }

            var portal = dropdown.GetComponent<NH_DropdownOverlayPortal>();
            if (portal == null)
                portal = dropdown.gameObject.AddComponent<NH_DropdownOverlayPortal>();
            portal.Bind(dropdown);
        }

        private static void Style(CustomDropdown dropdown)
        {
            if (dropdown.scrollbar != null)
                dropdown.scrollbar.SetActive(dropdown.items.Count > 4);

            if (dropdown.listRect != null)
            {
                var listImage = dropdown.listRect.GetComponent<Image>();
                if (listImage != null)
                    listImage.color = new Color(0.08f, 0.16f, 0.23f, 0.98f);
            }

            if (dropdown.selectedText != null)
                StyleLabel(dropdown.selectedText, 21f, TextOverflowModes.Ellipsis);

            if (dropdown.itemParent == null)
                return;

            int itemIndex = 0;
            for (int i = 0; i < dropdown.itemParent.childCount; i++)
            {
                var child = dropdown.itemParent.GetChild(i);
                if (child == null || !child.gameObject.activeSelf)
                    continue;

                string title = itemIndex < dropdown.items.Count ? dropdown.items[itemIndex].itemName : child.gameObject.name;
                StyleItem(child.gameObject, title);
                itemIndex++;
            }
        }

        private static void StyleItem(GameObject item, string title)
        {
            if (item == null)
                return;

            var managerItem = item.GetComponent<UIManagerDropdownItem>();
            if (managerItem != null)
                managerItem.overrideColors = true;

            var rootImage = item.GetComponent<Image>();
            if (rootImage != null)
                rootImage.color = new Color(0.11f, 0.20f, 0.29f, 0.98f);

            var layout = item.GetComponent<LayoutElement>() ?? item.AddComponent<LayoutElement>();
            layout.minHeight = ItemHeight;
            layout.preferredHeight = ItemHeight;
            layout.flexibleHeight = 0f;

            ApplyKnownItemBindings(item, title);

            var labels = item.GetComponentsInChildren<TextMeshProUGUI>(true);
            TextMeshProUGUI primary = ResolvePrimaryLabel(labels);

            for (int i = 0; i < labels.Length; i++)
            {
                bool keep = labels[i] == primary;
                labels[i].gameObject.SetActive(keep);
                if (keep)
                {
                    labels[i].text = title;
                    StyleLabel(labels[i], 19f, TextOverflowModes.Ellipsis);
                }
            }

            var icon = item.transform.Find("Icon");
            if (icon != null)
                icon.gameObject.SetActive(false);
        }

        private static void ApplyKnownItemBindings(GameObject item, string title)
        {
            var components = item.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                    continue;

                SetStringMember(component, "buttonText", title);
                SetStringMember(component, "buttonTitle", title);
                SetStringMember(component, "itemText", title);
                SetStringMember(component, "itemTitle", title);
                SetStringMember(component, "modeTitle", title);

                ApplyTextField(component, "modeNameText", title, true);
                ApplyTextField(component, "normalText", title, true);
                ApplyTextField(component, "highlightedText", title, true);
                ApplyTextField(component, "pressedText", title, true);
                ApplyTextField(component, "statusBadgeText", string.Empty, false);
                ApplyGameObjectField(component, "readyIcon", false);
                ApplyGameObjectField(component, "lockedOverlay", false);
                ApplyButtonField(component, "button", true);
            }
        }

        private static void SetStringMember(Component component, string memberName, string value)
        {
            var type = component.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var field = type.GetField(memberName, flags);
            if (field != null && field.FieldType == typeof(string))
            {
                field.SetValue(component, value);
                return;
            }

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.CanWrite && property.PropertyType == typeof(string))
                property.SetValue(component, value);
        }

        private static void ApplyTextField(Component component, string fieldName, string value, bool active)
        {
            var text = GetFieldValue<TextMeshProUGUI>(component, fieldName);
            if (text == null)
                return;

            text.text = value;
            text.gameObject.SetActive(active);
        }

        private static void ApplyGameObjectField(Component component, string fieldName, bool active)
        {
            var go = GetFieldValue<GameObject>(component, fieldName);
            if (go != null)
                go.SetActive(active);
        }

        private static void ApplyButtonField(Component component, string fieldName, bool interactable)
        {
            var button = GetFieldValue<Button>(component, fieldName);
            if (button != null)
                button.interactable = interactable;
        }

        private static T GetFieldValue<T>(Component component, string fieldName) where T : class
        {
            var field = component.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return field != null ? field.GetValue(component) as T : null;
        }

        private static TextMeshProUGUI ResolvePrimaryLabel(TextMeshProUGUI[] labels)
        {
            if (labels == null || labels.Length == 0)
                return null;

            TextMeshProUGUI best = labels[0];
            float bestScore = float.MinValue;
            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null)
                    continue;

                float score = label.fontSize;
                var rect = label.rectTransform;
                if (rect != null)
                    score += Mathf.Abs(rect.rect.width) * 0.05f + Mathf.Abs(rect.rect.height) * 0.02f;

                string n = label.gameObject.name.ToLowerInvariant();
                if (n.Contains("title") || n.Contains("text") || n.Contains("label") || n.Contains("name"))
                    score += 100f;
                if (n.Contains("available") || n.Contains("status") || n.Contains("badge"))
                    score -= 200f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = label;
                }
            }

            return best;
        }

        private static void StyleLabel(TextMeshProUGUI label, float maxFontSize, TextOverflowModes overflow)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = overflow;
            label.fontSize = Mathf.Min(label.fontSize, maxFontSize);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
        }

        private static void Close(CustomDropdown dropdown)
        {
            var portal = dropdown.GetComponent<NH_DropdownOverlayPortal>();
            if (portal != null)
                portal.ForceClose();

            dropdown.isOn = false;

            if (dropdown.listCG != null)
            {
                dropdown.listCG.alpha = 0f;
                dropdown.listCG.interactable = false;
                dropdown.listCG.blocksRaycasts = false;
                dropdown.listCG.gameObject.SetActive(false);
            }

            PreserveClosedListHeight(dropdown);

            if (dropdown.triggerObject != null && dropdown.enableTrigger)
                dropdown.triggerObject.SetActive(false);
        }

        private static float ResolveOpenPanelSize(CustomDropdown dropdown, int itemCount)
        {
            if (dropdown == null)
                return DefaultPanelHeight;

            float listHeight = dropdown.listRect != null ? dropdown.listRect.sizeDelta.y : 0f;
            if (listHeight > MinPanelHeight && Mathf.Abs(listHeight - dropdown.panelSize) > 0.5f)
                return Mathf.Max(MinPanelHeight, listHeight);

            if (dropdown.panelSize > MinPanelHeight)
                return dropdown.panelSize;

            float contentHeight = itemCount > 0 ? itemCount * ItemHeight + 14f : DefaultPanelHeight;
            return Mathf.Max(MinPanelHeight, Mathf.Min(contentHeight, DefaultPanelHeight));
        }

        private static void PreserveClosedListHeight(CustomDropdown dropdown)
        {
            if (dropdown == null || dropdown.listRect == null)
                return;

            float openHeight = ResolveOpenPanelSize(dropdown, dropdown.items != null ? dropdown.items.Count : 0);
            dropdown.panelSize = openHeight;
            dropdown.listRect.sizeDelta = new Vector2(dropdown.listRect.sizeDelta.x, openHeight);
        }

        private static void EnsureCanvas(GameObject target, int sortingOrder)
        {
            if (target == null)
                return;

            var canvas = target.GetComponent<Canvas>();
            if (canvas == null)
                canvas = target.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            if (target.GetComponent<GraphicRaycaster>() == null)
                target.AddComponent<GraphicRaycaster>();
        }
    }

    internal sealed class NH_DropdownOverlayPortal : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        private const int OverlaySortingOrder = 32000;
        private static readonly List<NH_DropdownOverlayPortal> Portals = new List<NH_DropdownOverlayPortal>();
        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly Vector3[] RootCorners = new Vector3[4];

        private CustomDropdown _dropdown;
        private RectTransform _listRect;
        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Vector2 _originalAnchorMin;
        private Vector2 _originalAnchorMax;
        private Vector2 _originalPivot;
        private Vector2 _originalAnchoredPosition;
        private Vector2 _originalSizeDelta;
        private float _originalPanelSize;
        private bool _isPortalized;

        public void Bind(CustomDropdown dropdown)
        {
            _dropdown = dropdown;
            _listRect = dropdown != null ? dropdown.listRect : null;
        }

        private void OnEnable()
        {
            if (!Portals.Contains(this))
                Portals.Add(this);
        }

        private void OnDisable()
        {
            Portals.Remove(this);
            Restore();
        }

        private void OnDestroy()
        {
            Portals.Remove(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            SyncOpenState();
            StartCoroutine(SyncAfterEvent());
        }

        public void OnSubmit(BaseEventData eventData)
        {
            SyncOpenState();
            StartCoroutine(SyncAfterEvent());
        }

        private System.Collections.IEnumerator SyncAfterEvent()
        {
            yield return null;
            SyncOpenState();
        }

        private void LateUpdate()
        {
            SyncOpenState();
        }

        private void SyncOpenState()
        {
            if (_dropdown == null)
                _dropdown = GetComponent<CustomDropdown>();
            if (_dropdown == null)
                return;

            _listRect = _dropdown.listRect;

            if (_dropdown.isOn)
            {
                if (!_isPortalized)
                {
                    CloseOtherDropdowns();
                    Portalize();
                }

                PositionList();
            }
            else if (_isPortalized)
            {
                Restore();
            }
        }

        public void ForceClose()
        {
            if (_dropdown == null)
                _dropdown = GetComponent<CustomDropdown>();
            if (_dropdown == null)
                return;

            _dropdown.StopCoroutine("StartExpand");
            _dropdown.StopCoroutine("StartMinimize");
            _dropdown.isOn = false;

            if (_dropdown.listCG != null)
            {
                _dropdown.listCG.alpha = 0f;
                _dropdown.listCG.interactable = false;
                _dropdown.listCG.blocksRaycasts = false;
                _dropdown.listCG.gameObject.SetActive(false);
            }

            if (_dropdown.listRect != null)
                _dropdown.listRect.sizeDelta = new Vector2(_dropdown.listRect.sizeDelta.x, Mathf.Max(1f, _dropdown.panelSize));

            if (_dropdown.triggerObject != null && _dropdown.enableTrigger)
                _dropdown.triggerObject.SetActive(false);

            Restore();
        }

        private void CloseOtherDropdowns()
        {
            for (int i = Portals.Count - 1; i >= 0; i--)
            {
                var portal = Portals[i];
                if (portal == null || portal == this)
                    continue;

                if (portal._dropdown != null && portal._dropdown.isOn)
                    portal.ForceClose();
            }
        }

        private void Portalize()
        {
            if (_listRect == null)
                return;

            var overlay = ResolveOverlay();
            if (overlay == null)
                return;

            _originalParent = _listRect.parent;
            _originalSiblingIndex = _listRect.GetSiblingIndex();
            _originalAnchorMin = _listRect.anchorMin;
            _originalAnchorMax = _listRect.anchorMax;
            _originalPivot = _listRect.pivot;
            _originalAnchoredPosition = _listRect.anchoredPosition;
            _originalSizeDelta = _listRect.sizeDelta;
            _originalPanelSize = _dropdown != null ? _dropdown.panelSize : 0f;

            _listRect.SetParent(overlay, false);
            _listRect.SetAsLastSibling();
            _isPortalized = true;

            EnsureOverlayCanvas(_listRect.gameObject, OverlaySortingOrder + 1);
        }

        private void Restore()
        {
            if (!_isPortalized || _listRect == null || _originalParent == null)
                return;

            _listRect.SetParent(_originalParent, false);
            _listRect.SetSiblingIndex(Mathf.Clamp(_originalSiblingIndex, 0, _originalParent.childCount - 1));
            _listRect.anchorMin = _originalAnchorMin;
            _listRect.anchorMax = _originalAnchorMax;
            _listRect.pivot = _originalPivot;
            _listRect.anchoredPosition = _originalAnchoredPosition;
            _listRect.sizeDelta = _originalSizeDelta;
            if (_dropdown != null && _originalPanelSize > 0f)
            {
                _dropdown.panelSize = _originalPanelSize;
                _listRect.sizeDelta = new Vector2(_listRect.sizeDelta.x, _originalPanelSize);
            }
            _isPortalized = false;
        }

        private RectTransform ResolveOverlay()
        {
            var canvas = _dropdown != null
                ? _dropdown.GetComponentInParent<Canvas>(true)
                : GetComponentInParent<Canvas>(true);
            if (canvas == null)
                return null;

            const string overlayName = "[NH Dropdown Overlay]";
            var existing = canvas.transform.Find(overlayName);
            if (existing != null)
            {
                existing.SetAsLastSibling();
                return existing as RectTransform;
            }

            var overlayGo = new GameObject(overlayName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            overlayGo.layer = canvas.gameObject.layer;
            var overlay = overlayGo.GetComponent<RectTransform>();
            overlay.SetParent(canvas.transform, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.pivot = new Vector2(0.5f, 0.5f);

            EnsureOverlayCanvas(overlayGo, OverlaySortingOrder);
            overlay.SetAsLastSibling();
            return overlay;
        }

        private void PositionList()
        {
            if (_dropdown == null || _listRect == null)
                return;

            var source = _dropdown.transform as RectTransform;
            var overlay = _listRect.parent as RectTransform;
            if (source == null || overlay == null)
                return;

            var anchor = ResolveAnchorRect(source);
            source.GetWorldCorners(RootCorners);
            anchor.GetWorldCorners(Corners);

            Vector3 rootTopLeft = overlay.InverseTransformPoint(RootCorners[1]);
            Vector3 rootTopRight = overlay.InverseTransformPoint(RootCorners[2]);
            Vector3 rootBottomLeft = overlay.InverseTransformPoint(RootCorners[0]);
            Vector3 rootBottomRight = overlay.InverseTransformPoint(RootCorners[3]);
            Vector3 bottomLeft = overlay.InverseTransformPoint(Corners[0]);
            Vector3 topLeft = overlay.InverseTransformPoint(Corners[1]);
            Vector3 topRight = overlay.InverseTransformPoint(Corners[2]);

            float rootWidth = Mathf.Abs(rootTopRight.x - rootTopLeft.x);
            float anchorWidth = Mathf.Abs(topRight.x - topLeft.x);
            float width = Mathf.Max(1f, rootWidth, anchorWidth);
            float centerX = (rootBottomLeft.x + rootBottomRight.x) * 0.5f;
            float gap = 2f;
            float desiredHeight = Mathf.Max(1f, _dropdown.panelSize);
            float belowSpace = bottomLeft.y - overlay.rect.yMin;
            float aboveSpace = overlay.rect.yMax - topLeft.y;
            bool openUp = belowSpace < desiredHeight + gap && aboveSpace > belowSpace;
            float availableHeight = Mathf.Max(1f, (openUp ? aboveSpace : belowSpace) - gap);
            desiredHeight = Mathf.Min(desiredHeight, availableHeight);
            _dropdown.panelSize = desiredHeight;

            float halfWidth = width * 0.5f;
            if (overlay.rect.width > width)
                centerX = Mathf.Clamp(centerX, overlay.rect.xMin + halfWidth, overlay.rect.xMax - halfWidth);

            _listRect.anchorMin = new Vector2(0.5f, 0.5f);
            _listRect.anchorMax = new Vector2(0.5f, 0.5f);
            _listRect.pivot = openUp ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
            _listRect.anchoredPosition = openUp
                ? new Vector2(centerX, topLeft.y + gap)
                : new Vector2(centerX, bottomLeft.y - gap);
            _listRect.sizeDelta = new Vector2(width, _listRect.sizeDelta.y);
            _listRect.SetAsLastSibling();
        }

        private RectTransform ResolveAnchorRect(RectTransform fallback)
        {
            if (_dropdown?.selectedText == null)
                return fallback;

            RectTransform current = _dropdown.selectedText.rectTransform;
            RectTransform best = current;
            while (current != null && current != fallback)
            {
                if (current.rect.width > 20f && current.rect.height > 12f && current.rect.height < 96f)
                    best = current;

                current = current.parent as RectTransform;
            }

            return best != null ? best : fallback;
        }

        private static void EnsureOverlayCanvas(GameObject target, int sortingOrder)
        {
            if (target == null)
                return;

            var canvas = target.GetComponent<Canvas>();
            if (canvas == null)
                canvas = target.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            if (target.GetComponent<GraphicRaycaster>() == null)
                target.AddComponent<GraphicRaycaster>();
        }
    }
}
