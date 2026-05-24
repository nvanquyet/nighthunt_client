using System.Collections.Generic;
using UnityEngine;

namespace Michsky.UI.Shift
{
    public class MainPanelButtonParent : MonoBehaviour
    {
        private List<Animator> mainButtons = new List<Animator>();

        void Awake()
        {
            foreach (Transform child in transform)
            {
                var animator = child.GetComponent<Animator>();
                if (animator != null)
                    mainButtons.Add(animator);
            }

            for (int i = 0; i < mainButtons.Count; ++i)
            {
                var animator = mainButtons[i];
                if (animator != null &&
                    animator.runtimeAnimatorController != null &&
                    animator.isActiveAndEnabled &&
                    animator.gameObject.activeInHierarchy)
                {
                    animator.Play("Normal to Dissolve");
                }
            }

            // Ensure this object has a Canvas component so overrideSorting can be set.
            // Wrap in try/catch because this script may appear on a non-root GO in older
            // prefabs where Canvas.overrideSorting throws MissingComponentException during Awake.
            try
            {
                Canvas _canvas = gameObject.GetComponent<Canvas>();
                if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = 999;

                if (gameObject.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                    gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[MainPanelButtonParent] Could not configure Canvas on '{gameObject.name}': {ex.Message}");
            }
        }
    }
}
