using UnityEngine;

namespace NightHunt.GameplaySystems.UI.Minimap
{
    /// <summary>
    /// Static top-down tactical map camera. Assign a dedicated RenderTexture here
    /// and the same texture to MinimapUI._fullMapRenderTexture.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FullMapCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _mapCamera;
        [SerializeField] private RenderTexture _renderTexture;
        [SerializeField] private Transform _boundsRoot;

        [Header("Manual Bounds Fallback")]
        [SerializeField] private Vector3 _manualCenter = Vector3.zero;
        [SerializeField] private Vector2 _manualSize = new Vector2(250f, 250f);

        [Header("Camera")]
        [SerializeField, Min(1f)] private float _height = 180f;
        [SerializeField, Min(0f)] private float _padding = 12f;
        [SerializeField] private bool _fitOnAwake = true;

        private void Awake()
        {
            if (_mapCamera == null)
                _mapCamera = GetComponentInChildren<Camera>();

            ConfigureCamera();

            if (_fitOnAwake)
                RecalculateBounds();
        }

        private void OnValidate()
        {
            if (_manualSize.x < 1f) _manualSize.x = 1f;
            if (_manualSize.y < 1f) _manualSize.y = 1f;
        }

        [ContextMenu("NightHunt/Recalculate Full Map Bounds")]
        public void RecalculateBounds()
        {
            if (TryGetRendererBounds(out var bounds))
            {
                ApplyBounds(bounds.center, new Vector2(bounds.size.x, bounds.size.z));
                return;
            }

            ApplyBounds(_manualCenter, _manualSize);
        }

        public void SetRenderTexture(RenderTexture texture)
        {
            _renderTexture = texture;
            ConfigureCamera();
        }

        public void SetManualBounds(Vector3 center, Vector2 size)
        {
            _manualCenter = center;
            _manualSize = size;
            ApplyBounds(center, size);
        }

        public void ApplyBounds(Bounds bounds)
        {
            ApplyBounds(bounds.center, new Vector2(bounds.size.x, bounds.size.z));
        }

        private void ConfigureCamera()
        {
            if (_mapCamera == null)
                return;

            _mapCamera.orthographic = true;
            _mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (_renderTexture != null)
                _mapCamera.targetTexture = _renderTexture;
        }

        private void ApplyBounds(Vector3 center, Vector2 size)
        {
            if (_mapCamera == null)
                return;

            float aspect = ResolveAspect();
            float halfHeight = Mathf.Max(1f, size.y * 0.5f);
            float halfWidth = Mathf.Max(1f, size.x * 0.5f / Mathf.Max(0.01f, aspect));

            _mapCamera.orthographic = true;
            _mapCamera.orthographicSize = Mathf.Max(halfHeight, halfWidth) + _padding;
            _mapCamera.transform.position = new Vector3(center.x, center.y + _height, center.z);
            _mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private bool TryGetRendererBounds(out Bounds bounds)
        {
            bounds = default;
            if (_boundsRoot == null)
                return false;

            var renderers = _boundsRoot.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private float ResolveAspect()
        {
            if (_renderTexture != null && _renderTexture.height > 0)
                return (float)_renderTexture.width / _renderTexture.height;

            return _mapCamera != null && _mapCamera.aspect > 0f
                ? _mapCamera.aspect
                : 1f;
        }
    }
}
