using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Shared RectTransform positioning helpers for context menus.
    /// Converts an anchor RectTransform to the menu parent space and clamps the
    /// result so the menu stays visible inside its parent.
    /// </summary>
    public static class UIContextMenuPositioner
    {
        public static void PlaceNearPivot(RectTransform panel, RectTransform anchor, Vector2 offset = default)
        {
            if (panel == null || anchor == null)
                return;

            var parent = panel.parent as RectTransform;
            if (parent == null)
                return;

            PreparePanelForPlacement(panel);

            var camera = ResolveCamera(panel);
            var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, anchor.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out var local))
                return;

            local += offset;
            ClampToParent(panel, parent, ref local);
            panel.anchoredPosition = local;
        }

        public static void PlaceNearTopLeft(RectTransform panel, RectTransform anchor, Vector2 offset = default)
        {
            if (panel == null || anchor == null)
                return;

            var parent = panel.parent as RectTransform;
            if (parent == null)
                return;

            PreparePanelForPlacement(panel);

            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);

            var camera = ResolveCamera(panel);
            // corners[1] is Top-Left in world space
            var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out var local))
                return;

            // Adjust 'local' so it represents where the panel's PIVOT should be 
            // for the panel's top-left corner to be at 'local'.
            // Top-left of panel relative to pivot is (-pivot.x * size.x, (1-pivot.y) * size.y)
            // So we subtract that from 'local'.
            Vector2 size = ResolvePanelSize(panel);
            Vector2 pivot = panel.pivot;
            
            local.x += pivot.x * size.x;
            local.y -= (1f - pivot.y) * size.y;

            local += offset;
            ClampToParent(panel, parent, ref local);
            panel.anchoredPosition = local;
        }

        public static void PlaceAtMouse(RectTransform panel, Vector2 offset = default)
        {
            if (panel == null)
                return;

            var parent = panel.parent as RectTransform;
            if (parent == null)
                return;

            PreparePanelForPlacement(panel);

            var camera = ResolveCamera(panel);
            var screenPoint = (Vector2)Input.mousePosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out var local))
                return;

            local += offset;
            ClampToParent(panel, parent, ref local);
            panel.anchoredPosition = local;
        }

        public static void PrepareFullscreenBackdrop(RectTransform backdrop)
        {
            if (backdrop == null)
                return;

            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = Vector2.zero;
            backdrop.offsetMax = Vector2.zero;
        }

        private static void ClampToParent(RectTransform panel, RectTransform parent, ref Vector2 position)
        {
            var parentRect = parent.rect;
            var size = ResolvePanelSize(panel);

            float minX = parentRect.xMin + size.x * panel.pivot.x;
            float maxX = parentRect.xMax - size.x * (1f - panel.pivot.x);
            float minY = parentRect.yMin + size.y * panel.pivot.y;
            float maxY = parentRect.yMax - size.y * (1f - panel.pivot.y);

            if (minX <= maxX)
                position.x = Mathf.Clamp(position.x, minX, maxX);
            else
                position.x = parentRect.center.x;

            if (minY <= maxY)
                position.y = Mathf.Clamp(position.y, minY, maxY);
            else
                position.y = parentRect.center.y;
        }

        private static void PreparePanelForPlacement(RectTransform panel)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        private static Vector2 ResolvePanelSize(RectTransform panel)
        {
            var size = panel.rect.size;

            if (size.x <= 0f)
                size.x = LayoutUtility.GetPreferredWidth(panel);
            if (size.y <= 0f)
                size.y = LayoutUtility.GetPreferredHeight(panel);

            if (size.x <= 0f)
                size.x = panel.sizeDelta.x;
            if (size.y <= 0f)
                size.y = panel.sizeDelta.y;

            size.x = Mathf.Max(1f, size.x);
            size.y = Mathf.Max(1f, size.y);
            return size;
        }

        private static Camera ResolveCamera(Component component)
        {
            var canvas = component != null ? component.GetComponentInParent<Canvas>() : null;
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;
            return canvas.worldCamera;
        }
    }
}
