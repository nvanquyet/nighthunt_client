using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FOW
{
    public class HiderDisableRenderers : HiderBehavior
    {
        [SerializeField] private Renderer[] ObjectsToHide;

        protected override void OnHide()
        {
            if (ObjectsToHide == null)
                return;

            foreach (Renderer renderer in ObjectsToHide)
            {
                if (renderer != null)
                    renderer.enabled = false;
            }
        }

        protected override void OnReveal()
        {
            if (ObjectsToHide == null)
                return;

            foreach (Renderer renderer in ObjectsToHide)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }
        }

        public void ModifyHiddenRenderers(Renderer[] newObjectsToHide)
        {
            OnReveal();
            ObjectsToHide = newObjectsToHide ?? System.Array.Empty<Renderer>();
            if (!enabled)
                return;

            if (!IsEnabled)
                OnHide();
            else
                OnReveal();
        }
    }
}
