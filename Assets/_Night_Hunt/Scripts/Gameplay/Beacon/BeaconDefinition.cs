using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Gameplay.Beacon
{
    /// <summary>
    /// ScriptableObject definition for a deployable beacon item.
    /// Create via: Assets → Create → NightHunt → Items → Beacon Definition
    /// </summary>
    [CreateAssetMenu(
        menuName = "NightHunt/Items/Beacon Definition",
        fileName = "BeaconDefinition")]
    public sealed class BeaconDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Deployable;

        [Header("Beacon Specifics")]
        [Tooltip("Prefab spawned on the server when the player confirms placement.")]
        public GameObject BeaconPrefab;

        [Tooltip("Preview mesh shown on client during placement mode.")]
        public GameObject PlacementPreviewPrefab;

        [Tooltip("Placement confirmation input action name (default: Interact).")]
        public string PlaceAction = "Interact";

        [Tooltip("Cancel placement input action name (default: AltFire).")]
        public string CancelAction = "AltFire";

        [Tooltip("Layer mask used for ground ray-cast during placement preview.")]
        public LayerMask PlacementLayerMask = ~0;

        [Tooltip("Max slope angle (degrees) allowed for placement.")]
        [Range(0f, 60f)]
        public float MaxPlacementSlope = 30f;
    }
}
