using System.Collections;
using System.Collections.Generic;
using NightHunt.Core;
using NightHunt.Networking.Player;
using UnityEngine;

namespace NightHunt.GameplaySystems.ItemUse
{
    /// <summary>
    /// Client-only visual reveal for RADAR_SCANNER.
    /// The server chooses target player ObjectIds and TargetRpc sends them only to the item owner.
    /// This component renders temporary Minimap-layer markers at those players' live positions.
    /// </summary>
    public sealed class RadarSweepRevealClient : MonoBehaviour
    {
        [SerializeField] private float _markerHeight = 6f;
        [SerializeField] private float _markerScale = 1.2f;
        [SerializeField] private float _refreshInterval = 0.1f;
        [SerializeField] private Color _markerColor = new Color(0f, 0.9f, 1f, 0.95f);

        private readonly Dictionary<int, RevealMarker> _markers = new();
        private readonly List<int> _removeBuffer = new(16);
        private Coroutine _routine;
        private Material _markerMaterial;

        public void ShowTargets(int[] playerObjectIds, float duration)
        {
            if (playerObjectIds == null || playerObjectIds.Length == 0)
                return;

            float expiresAt = Time.time + Mathf.Max(0.1f, duration);
            for (int i = 0; i < playerObjectIds.Length; i++)
            {
                int objectId = playerObjectIds[i];
                if (objectId <= 0)
                    continue;

                if (!_markers.TryGetValue(objectId, out var marker))
                {
                    marker = CreateMarker(objectId);
                    _markers.Add(objectId, marker);
                }

                marker.ExpiresAt = Mathf.Max(marker.ExpiresAt, expiresAt);
                marker.Target = TryFindPlayer(objectId, out var player) ? player : null;
            }

            if (_routine == null && _markers.Count > 0)
                _routine = StartCoroutine(RevealRoutine());
        }

        private IEnumerator RevealRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.02f, _refreshInterval));
            while (_markers.Count > 0)
            {
                RefreshMarkers();
                yield return wait;
            }

            _routine = null;
        }

        private void RefreshMarkers()
        {
            _removeBuffer.Clear();
            float now = Time.time;

            foreach (var kvp in _markers)
            {
                int objectId = kvp.Key;
                var marker = kvp.Value;

                if (now >= marker.ExpiresAt)
                {
                    _removeBuffer.Add(objectId);
                    continue;
                }

                if (marker.Target == null || !marker.Target.IsAlive)
                    marker.Target = TryFindPlayer(objectId, out var player) ? player : null;

                if (marker.Target == null || !marker.Target.IsAlive)
                {
                    marker.Root.SetActive(false);
                    continue;
                }

                marker.Root.transform.position = marker.Target.transform.position + Vector3.up * _markerHeight;
                marker.Root.SetActive(true);
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
                RemoveMarker(_removeBuffer[i]);
        }

        private RevealMarker CreateMarker(int objectId)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = $"RadarRevealMarker_{objectId}";
            root.transform.localScale = Vector3.one * Mathf.Max(0.1f, _markerScale);
            root.SetActive(false);

            if (NightHuntLayers.IdMinimap >= 0)
                root.layer = NightHuntLayers.IdMinimap;

            if (root.TryGetComponent<Collider>(out var collider))
                Destroy(collider);

            if (root.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = GetMarkerMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return new RevealMarker { Root = root };
        }

        private Material GetMarkerMaterial()
        {
            if (_markerMaterial != null)
                return _markerMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Standard");

            _markerMaterial = new Material(shader);
            if (_markerMaterial.HasProperty("_BaseColor"))
                _markerMaterial.SetColor("_BaseColor", _markerColor);
            if (_markerMaterial.HasProperty("_Color"))
                _markerMaterial.SetColor("_Color", _markerColor);

            return _markerMaterial;
        }

        private static bool TryFindPlayer(int objectId, out NetworkPlayer player)
        {
            player = null;
            var registry = PlayerPublicRegistry.Instance;
            if (registry == null)
                return false;

            var players = registry.GetAllPlayers();
            for (int i = 0; i < players.Length; i++)
            {
                var candidate = players[i];
                if (candidate != null && (int)candidate.ObjectId == objectId)
                {
                    player = candidate;
                    return true;
                }
            }

            return false;
        }

        private void RemoveMarker(int objectId)
        {
            if (!_markers.TryGetValue(objectId, out var marker))
                return;

            if (marker.Root != null)
                Destroy(marker.Root);

            _markers.Remove(objectId);
        }

        private void ClearMarkers()
        {
            _removeBuffer.Clear();
            foreach (var kvp in _markers)
                _removeBuffer.Add(kvp.Key);

            for (int i = 0; i < _removeBuffer.Count; i++)
                RemoveMarker(_removeBuffer[i]);
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            ClearMarkers();
        }

        private void OnDestroy()
        {
            ClearMarkers();
            if (_markerMaterial != null)
                Destroy(_markerMaterial);
        }

        private sealed class RevealMarker
        {
            public GameObject Root;
            public NetworkPlayer Target;
            public float ExpiresAt;
        }
    }
}
