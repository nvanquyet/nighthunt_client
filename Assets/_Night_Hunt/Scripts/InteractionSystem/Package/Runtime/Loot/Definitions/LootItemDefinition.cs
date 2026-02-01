using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;

namespace NightHunt.InteractionSystem.Loot.Definitions
{
    /// <summary>
    /// Defines how a loot item should appear and behave in the world.
    /// This is what the spawner picks (weighted/random), then injects into NetworkLootItem at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "LootItemDefinition", menuName = "NightHunt/InteractionSystem/Loot/LootItemDefinition")]
    public class LootItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string definitionId = "loot_def_id";

        [Header("Item")]
        [SerializeField] private ItemDataBase itemData;

        [Header("World Presentation")]
        [SerializeField] private GameObject worldPrefab;
        // [SerializeField] private Vector3 worldPrefabLocalPosition = Vector3.zero;
        // [SerializeField] private Vector3 worldPrefabLocalEuler = Vector3.zero;
        // [SerializeField] private Vector3 worldPrefabLocalScale = Vector3.one;

        [Header("Pickup")]
        [SerializeField] private float pickupRange = 3f;
        [SerializeField] private int defaultMinQuantity = 1;
        [SerializeField] private int defaultMaxQuantity = 1;

        [Header("Idle Visual (applied to root if no custom prefab animation)")]
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float floatSpeed = 1f;
        [SerializeField] private float floatAmount = 0.5f;

        public string DefinitionId => definitionId;
        public ItemDataBase ItemData => itemData;
        public GameObject WorldPrefab => worldPrefab;
        // public Vector3 WorldPrefabLocalPosition => worldPrefabLocalPosition;
        // public Vector3 WorldPrefabLocalEuler => worldPrefabLocalEuler;
        // public Vector3 WorldPrefabLocalScale => worldPrefabLocalScale;

        public Vector3 WorldPrefabLocalPosition => Vector3.zero;
        public Vector3 WorldPrefabLocalEuler => Vector3.zero;
        public Vector3 WorldPrefabLocalScale => Vector3.one;
        public float PickupRange => pickupRange;
        public int DefaultMinQuantity => defaultMinQuantity;
        public int DefaultMaxQuantity => defaultMaxQuantity;
        public float RotationSpeed => rotationSpeed;
        public float FloatSpeed => floatSpeed;
        public float FloatAmount => floatAmount;
    }
}

