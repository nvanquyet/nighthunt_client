using UnityEngine;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Safe tween wrapper.
    /// Currently sets values instantly; can be expanded to use DOTween animations later.
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

