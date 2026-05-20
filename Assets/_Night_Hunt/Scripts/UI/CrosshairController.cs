using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    public class CrosshairController : MonoBehaviour
    {
        [Header("Crosshair Variants")]
        [SerializeField] private GameObject[] crosshairPrefabs;
        [SerializeField] private Transform crosshairRoot;

        private GameObject _currentActive;

        public void SetCrosshairType(int index)
        {
            if (crosshairPrefabs == null || index < 0 || index >= crosshairPrefabs.Length)
            {
                // Fallback to default (0) if invalid
                if (crosshairPrefabs != null && crosshairPrefabs.Length > 0) index = 0;
                else return;
            }

            if (_currentActive != null)
                _currentActive.SetActive(false);

            // In a real implementation, we might instantiate or just toggle children
            if (index < crosshairRoot.childCount)
            {
                _currentActive = crosshairRoot.GetChild(index).gameObject;
                _currentActive.SetActive(true);
            }
        }

        private void Awake()
        {
            if (crosshairRoot == null) crosshairRoot = transform;
        }
    }
}