using UnityEngine;
using UnityEngine.EventSystems;

namespace Michsky.UI.Shift
{
    public class VirtualCursorAnimate : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Resources")]
        public VirtualCursor virtualCursor;

        void Start()
        {
            if (virtualCursor == null)
            {
                try
                {
#if UNITY_2023_2_OR_NEWER
                    var cursors = Object.FindObjectsByType<VirtualCursor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    virtualCursor = (cursors != null && cursors.Length > 0) ? cursors[0] : null;
#else
                    var arr = GameObject.FindObjectsOfType(typeof(VirtualCursor)) as VirtualCursor[];
                    virtualCursor = (arr != null && arr.Length > 0) ? arr[0] : null;
#endif
                }

                catch { this.enabled = false; }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (virtualCursor != null)
                virtualCursor.AnimateCursorIn();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (virtualCursor != null)
                virtualCursor.AnimateCursorOut();
        }
    }
}