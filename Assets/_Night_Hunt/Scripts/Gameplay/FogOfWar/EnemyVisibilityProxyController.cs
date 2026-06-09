using System;
using System.Collections.Generic;
using NightHunt.Networking.Player;
using UnityEngine;

namespace NightHunt.Gameplay.FogOfWar
{
    [Serializable]
    public struct EnemyVisibilitySnapshot
    {
        public int ObjectId;
        public string DisplayName;
        public int TeamId;
        public int CharacterModelIndex;
        public Vector3 Position;
        public float Yaw;
        public float ReceivedAt;
        public bool Visible;
    }

    public static class EnemyVisibilitySnapshotRegistry
    {
        private static EnemyVisibilityProxyController _controller;

        public static void ApplySnapshot(
            int objectId,
            string displayName,
            int teamId,
            int characterModelIndex,
            Vector3 position,
            float yaw,
            bool visible)
        {
            EnsureController().ApplySnapshot(new EnemyVisibilitySnapshot
            {
                ObjectId = objectId,
                DisplayName = displayName,
                TeamId = teamId,
                CharacterModelIndex = characterModelIndex,
                Position = position,
                Yaw = yaw,
                ReceivedAt = Time.time,
                Visible = visible
            });
        }

        private static EnemyVisibilityProxyController EnsureController()
        {
            if (_controller != null)
                return _controller;

            var existing = UnityEngine.Object.FindFirstObjectByType<EnemyVisibilityProxyController>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                _controller = existing;
                return _controller;
            }

            var go = new GameObject("EnemyVisibilityProxyController");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _controller = go.AddComponent<EnemyVisibilityProxyController>();
            return _controller;
        }
    }

    /// <summary>
    /// Client-only renderer for server-approved enemy visibility snapshots.
    /// This is intentionally independent from NetworkPlayer so enemy world actors
    /// can later be observer-culled without losing visible enemy rendering.
    /// </summary>
    public sealed class EnemyVisibilityProxyController : MonoBehaviour
    {
        private sealed class ProxyState
        {
            public GameObject Root;
            public Transform Transform;
            public Vector3 TargetPosition;
            public float TargetYaw;
            public float LastSeenAt;
        }

        [SerializeField] private GameObject _proxyPrefab;
        [SerializeField] private float _staleSeconds = 0.35f;
        [SerializeField] private float _positionLerp = 18f;
        [SerializeField] private float _rotationLerp = 18f;

        private readonly Dictionary<int, ProxyState> _proxies = new();
        private Material _fallbackMaterial;

        public void ApplySnapshot(EnemyVisibilitySnapshot snapshot)
        {
            if (snapshot.ObjectId <= 0)
                return;

            if (!snapshot.Visible)
            {
                HideProxy(snapshot.ObjectId);
                return;
            }

            // While the real NetworkPlayer is observed, do not double-render a proxy.
            if (PlayerPublicRegistry.Instance != null
                && PlayerPublicRegistry.Instance.HasNetworkPlayer(snapshot.ObjectId))
            {
                HideProxy(snapshot.ObjectId);
                return;
            }

            ProxyState state = GetOrCreateProxy(snapshot);
            state.TargetPosition = snapshot.Position;
            state.TargetYaw = snapshot.Yaw;
            state.LastSeenAt = Time.time;
            state.Root.SetActive(true);
        }

        private void Update()
        {
            if (_proxies.Count == 0)
                return;

            float now = Time.time;
            float posT = 1f - Mathf.Exp(-_positionLerp * Time.deltaTime);
            float rotT = 1f - Mathf.Exp(-_rotationLerp * Time.deltaTime);

            foreach (ProxyState state in _proxies.Values)
            {
                if (state.Root == null || !state.Root.activeSelf)
                    continue;

                if (now - state.LastSeenAt > _staleSeconds)
                {
                    state.Root.SetActive(false);
                    continue;
                }

                state.Transform.position = Vector3.Lerp(state.Transform.position, state.TargetPosition, posT);
                Quaternion targetRot = Quaternion.Euler(0f, state.TargetYaw, 0f);
                state.Transform.rotation = Quaternion.Slerp(state.Transform.rotation, targetRot, rotT);
            }
        }

        private ProxyState GetOrCreateProxy(EnemyVisibilitySnapshot snapshot)
        {
            if (_proxies.TryGetValue(snapshot.ObjectId, out ProxyState state)
                && state.Root != null)
            {
                return state;
            }

            GameObject root = _proxyPrefab != null
                ? Instantiate(_proxyPrefab)
                : CreateFallbackProxy();

            root.name = $"EnemyVisibleProxy_{snapshot.ObjectId}_{snapshot.DisplayName}";
            root.transform.position = snapshot.Position;
            root.transform.rotation = Quaternion.Euler(0f, snapshot.Yaw, 0f);

            state = new ProxyState
            {
                Root = root,
                Transform = root.transform,
                TargetPosition = snapshot.Position,
                TargetYaw = snapshot.Yaw,
                LastSeenAt = Time.time
            };
            _proxies[snapshot.ObjectId] = state;
            return state;
        }

        private GameObject CreateFallbackProxy()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                _fallbackMaterial ??= CreateFallbackMaterial();
                renderer.sharedMaterial = _fallbackMaterial;
            }

            return go;
        }

        private static Material CreateFallbackMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.color = new Color(1f, 0.18f, 0.12f, 0.95f);
            return material;
        }

        private void HideProxy(int objectId)
        {
            if (_proxies.TryGetValue(objectId, out ProxyState state) && state.Root != null)
                state.Root.SetActive(false);
        }
    }
}
