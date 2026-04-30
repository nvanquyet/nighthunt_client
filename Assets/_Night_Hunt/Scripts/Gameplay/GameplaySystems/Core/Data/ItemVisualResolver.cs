using FishNet.Object;
using UnityEngine;

namespace NightHunt.GameplaySystems.Core.Data
{
    public enum ItemVisualPurpose
    {
        Ground = 0,
        Held = 1,
    }

    /// <summary>
    /// Single resolver for item visuals. It only reads PhysicalItemDefinition.VisualPrefab.
    /// Runtime/networked prefabs stay on concrete runtime definitions.
    /// </summary>
    public static class ItemVisualResolver
    {
        public static GameObject ResolveVisualPrefab(ItemDefinition definition)
        {
            return definition is PhysicalItemDefinition physical
                ? physical.VisualPrefab
                : null;
        }

        public static bool IsNetworkedVisual(GameObject prefab)
        {
            if (prefab == null)
                return false;

            return prefab.GetComponent<NetworkObject>() != null
                   || prefab.GetComponentInChildren<NetworkObject>(true) != null;
        }

        public static GameObject CreateRuntimeFallback(ItemDefinition definition, ItemVisualPurpose purpose)
        {
            string itemName = definition != null && !string.IsNullOrEmpty(definition.ItemID)
                ? definition.ItemID
                : "UnknownItem";

            var root = new GameObject($"Fallback_{purpose}_{itemName}");
            var primitiveType = purpose == ItemVisualPurpose.Ground ? PrimitiveType.Cube : PrimitiveType.Capsule;
            var visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = "[FallbackVisual]";
            visual.transform.SetParent(root.transform, false);

            if (purpose == ItemVisualPurpose.Ground)
                visual.transform.localScale = new Vector3(0.35f, 0.12f, 0.35f);
            else
                visual.transform.localScale = new Vector3(0.12f, 0.28f, 0.12f);

            Object.Destroy(visual.GetComponent<Collider>());
            TintFallback(visual, definition);
            return root;
        }

        private static void TintFallback(GameObject visual, ItemDefinition definition)
        {
            var renderer = visual.GetComponent<Renderer>();
            if (renderer == null)
                return;

            var material = new Material(Shader.Find("Standard"));
            material.color = GetFallbackColor(definition);
            renderer.sharedMaterial = material;
        }

        private static Color GetFallbackColor(ItemDefinition definition)
        {
            if (definition == null)
                return Color.gray;

            return definition.Type switch
            {
                ItemType.Weapon => new Color(0.24f, 0.24f, 0.26f),
                ItemType.Consumable => new Color(0.1f, 0.55f, 0.25f),
                ItemType.Throwable => new Color(0.55f, 0.34f, 0.1f),
                ItemType.Deployable => new Color(0.1f, 0.42f, 0.7f),
                ItemType.Attachment => new Color(0.45f, 0.45f, 0.55f),
                _ => Color.gray,
            };
        }
    }
}
