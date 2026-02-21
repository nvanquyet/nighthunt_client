using UnityEngine;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Wrapper tween an toàn.
    /// Hiện tại chỉ set giá trị tức thời, sau này có DOTween có thể mở rộng tại đây.
    /// </summary>
    public static class UITweenUtil
    {
        public static void ScaleInstant(Transform t, float scale)
        {
            if (t == null) return;
            t.localScale = Vector3.one * scale;
        }

        public static void FadeCanvasGroupInstant(CanvasGroup cg, float alpha)
        {
            if (cg == null) return;
            cg.alpha = alpha;
        }
    }
}

