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
    public sealed class BeaconDefinition : PhysicalItemDefinition
    {
        public override ItemType Type => ItemType.Deployable;

        [Header("Beacon Specifics")]
        [Tooltip("NetworkObject prefab spawned on the server when the player confirms placement.")]
        public GameObject NetworkBeaconPrefab;

        [Tooltip("Max health for the spawned beacon.")]
        public int BeaconHP = 100;

        [Tooltip("Preview mesh shown on client during placement mode.")]
        public GameObject PlacementPreviewPrefab;

        [Tooltip("Layer mask used for ground ray-cast during placement preview.")]
        public LayerMask PlacementLayerMask = ~0;

        [Tooltip("Max slope angle (degrees) allowed for placement.")]
        [Range(0f, 60f)]
        public float MaxPlacementSlope = 30f;

        [Tooltip("Yaw offset applied after facing the aim cursor. Use 180 when the prefab model's forward axis points backward.")]
        public float PlacementYawOffsetDegrees = 180f;

        [Header("Deployment Timing")]
        [Tooltip("Seconds after release before the beacon is actually placed. The player can cancel during this time.")]
        [Min(0f)] public float DeployDuration = 1.25f;

        [SerializeField] private bool _canCancelDeploy = true;

        public override bool CanCancelUsage { get => _canCancelDeploy; set => _canCancelDeploy = value; }
    }
}
