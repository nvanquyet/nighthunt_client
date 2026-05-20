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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

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
            var primitiveType = ResolveFallbackPrimitive(definition, purpose);
            var visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = "[FallbackVisual]";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = ResolveFallbackScale(definition, purpose);

            var collider = visual.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            TintFallback(visual, definition);
            return root;
        }

        private static PrimitiveType ResolveFallbackPrimitive(ItemDefinition definition, ItemVisualPurpose purpose)
        {
            if (definition == null)
                return purpose == ItemVisualPurpose.Ground ? PrimitiveType.Cube : PrimitiveType.Capsule;

            return definition.Type switch
            {
                ItemType.Consumable => PrimitiveType.Capsule,
                ItemType.Throwable => PrimitiveType.Sphere,
                ItemType.Deployable => PrimitiveType.Cylinder,
                ItemType.Attachment => PrimitiveType.Cube,
                ItemType.Equipment => PrimitiveType.Cube,
                _ => purpose == ItemVisualPurpose.Ground ? PrimitiveType.Cube : PrimitiveType.Capsule,
            };
        }

        private static Vector3 ResolveFallbackScale(ItemDefinition definition, ItemVisualPurpose purpose)
        {
            ItemType type = definition != null ? definition.Type : ItemType.Misc;

            if (purpose == ItemVisualPurpose.Held)
            {
                return type switch
                {
                    ItemType.Throwable => new Vector3(0.16f, 0.16f, 0.16f),
                    ItemType.Deployable => new Vector3(0.18f, 0.08f, 0.18f),
                    ItemType.Consumable => new Vector3(0.12f, 0.28f, 0.12f),
                    _ => new Vector3(0.12f, 0.28f, 0.12f),
                };
            }

            return type switch
            {
                ItemType.Weapon => new Vector3(0.18f, 0.10f, 0.48f),
                ItemType.Throwable => new Vector3(0.22f, 0.22f, 0.22f),
                ItemType.Deployable => new Vector3(0.32f, 0.10f, 0.32f),
                ItemType.Equipment => new Vector3(0.34f, 0.16f, 0.28f),
                ItemType.Attachment => new Vector3(0.22f, 0.08f, 0.16f),
                _ => new Vector3(0.35f, 0.12f, 0.35f),
            };
        }

        private static void TintFallback(GameObject visual, ItemDefinition definition)
        {
            var renderer = visual.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Color color = GetFallbackColor(definition);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        private static Color GetFallbackColor(ItemDefinition definition)
        {
            if (definition == null)
                return Color.gray;

            return definition.Type switch
            {
                ItemType.Equipment => new Color(0.18f, 0.42f, 0.48f),
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
