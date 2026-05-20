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

            Canvas _canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 999;

            if (gameObject.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
    }
}
